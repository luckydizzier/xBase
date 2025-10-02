using System;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XBase.Abstractions;
using XBase.Core.Cursors;
using XBase.Core.Table;
using XBase.Demo.Domain.Services;
using XBase.Demo.Domain.Services.Models;
using XBase.Expressions.Evaluation;

namespace XBase.Demo.Infrastructure.Indexes;

public sealed class FileSystemIndexManagementService : IIndexManagementService
{
  private const int IndexFormatVersion = 1;
  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true
  };

  private readonly ILogger<FileSystemIndexManagementService> _logger;
  private readonly DbfTableLoader _tableLoader;
  private readonly DbfCursorFactory _cursorFactory;
  private readonly ExpressionEvaluator _expressionEvaluator;

  public FileSystemIndexManagementService(
    ILogger<FileSystemIndexManagementService> logger,
    DbfTableLoader tableLoader,
    DbfCursorFactory cursorFactory,
    ExpressionEvaluator expressionEvaluator)
  {
    _logger = logger;
    _tableLoader = tableLoader ?? throw new ArgumentNullException(nameof(tableLoader));
    _cursorFactory = cursorFactory ?? throw new ArgumentNullException(nameof(cursorFactory));
    _expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
  }

  public async Task<IndexOperationResult> CreateIndexAsync(IndexCreateRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    try
    {
      var tableDirectory = ResolveTableDirectory(request.TablePath);
      var indexPath = Path.Combine(tableDirectory, request.IndexName);
      if (File.Exists(indexPath))
      {
        return IndexOperationResult.Failure($"Index '{request.IndexName}' already exists.");
      }

      Directory.CreateDirectory(tableDirectory);

      var descriptor = _tableLoader.LoadDbf(request.TablePath);
      var columns = DbfColumnFactory.CreateColumns(descriptor);
      var knownColumns = columns.ToDictionary(column => column.Name, column => column, StringComparer.OrdinalIgnoreCase);
      var evaluator = IndexKeyExpressionCompiler.Compile(request.Expression, knownColumns.Keys, _expressionEvaluator);

      var entries = new List<SimpleIndexEntry>();
      var totalRecords = 0;
      var activeRecords = 0;
      var deletedRecords = 0;

      await using (var cursor = await _cursorFactory
        .CreateSequentialAsync(descriptor, new CursorOptions(true, null, null), cancellationToken)
        .ConfigureAwait(false))
      {
        while (await cursor.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();

          totalRecords++;
          var record = cursor.Current;
          var row = MaterializeRow(record, columns);
          var isDeleted = IsDeleted(record);
          if (isDeleted)
          {
            deletedRecords++;
          }
          else
          {
            activeRecords++;
          }

          var key = evaluator(row) ?? string.Empty;
          entries.Add(new SimpleIndexEntry(totalRecords, key, isDeleted));
        }
      }

      entries.Sort(SimpleIndexEntryComparer.Instance);
      var signature = ComputeSignature(entries);

      var artifact = new SimpleIndexArtifact(
        IndexFormatVersion,
        descriptor.Name,
        request.TablePath,
        request.Expression,
        DateTimeOffset.UtcNow,
        new SimpleIndexStatistics(totalRecords, activeRecords, deletedRecords),
        signature,
        entries);

      await using (var stream = new FileStream(indexPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
      {
        await JsonSerializer.SerializeAsync(stream, artifact, SerializerOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
      }

      _logger.LogInformation("Created index artifact {Index} for table {Table}", indexPath, request.TablePath);
      return IndexOperationResult.Success($"Index '{request.IndexName}' created successfully.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to create index {Index} for table {Table}", request.IndexName, request.TablePath);
      return IndexOperationResult.Failure($"Failed to create index '{request.IndexName}'. {ex.Message}", ex);
    }
  }

  public Task<IndexOperationResult> DropIndexAsync(IndexDropRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    try
    {
      var tableDirectory = ResolveTableDirectory(request.TablePath);
      var indexPath = Path.Combine(tableDirectory, request.IndexName);
      if (!File.Exists(indexPath))
      {
        return Task.FromResult(IndexOperationResult.Failure($"Index '{request.IndexName}' was not found."));
      }

      File.Delete(indexPath);
      _logger.LogInformation("Dropped index artifact {Index} for table {Table}", indexPath, request.TablePath);
      return Task.FromResult(IndexOperationResult.Success($"Index '{request.IndexName}' dropped successfully."));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to drop index {Index} for table {Table}", request.IndexName, request.TablePath);
      return Task.FromResult(IndexOperationResult.Failure($"Failed to drop index '{request.IndexName}'. {ex.Message}", ex));
    }
  }

  public IObservable<IndexRebuildProgress> RebuildIndex(IndexRebuildRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    return Observable.Create<IndexRebuildProgress>(observer =>
    {
      var cancellation = new CancellationTokenSource();

      _ = Task.Run(async () =>
      {
        string? temporaryIndexPath = null;
        string? backupPath = null;
        try
        {
          var tableDirectory = ResolveTableDirectory(request.TablePath);
          var originalIndexPath = Path.Combine(tableDirectory, request.IndexName);
          if (!File.Exists(originalIndexPath))
          {
            throw new FileNotFoundException($"Index '{request.IndexName}' was not found.", originalIndexPath);
          }

          var temporaryName = string.IsNullOrWhiteSpace(request.TemporaryIndexName)
              ? $"{Path.GetFileNameWithoutExtension(request.IndexName)}.rebuilt{Path.GetExtension(request.IndexName)}"
              : request.TemporaryIndexName;
          temporaryIndexPath = Path.Combine(tableDirectory, temporaryName);
          backupPath = Path.Combine(tableDirectory, $"{request.IndexName}.bak");

          if (File.Exists(temporaryIndexPath))
          {
            File.Delete(temporaryIndexPath);
          }

          if (File.Exists(backupPath))
          {
            File.Delete(backupPath);
          }

          var sourceInfo = new FileInfo(originalIndexPath);
          var totalBytes = sourceInfo.Exists ? sourceInfo.Length : 0;

          observer.OnNext(IndexRebuildProgress.Starting(originalIndexPath, totalBytes));

          long bytesCopied = 0;
          var stopwatch = Stopwatch.StartNew();
          await using (var readStream = new FileStream(originalIndexPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
          await using (var writeStream = new FileStream(temporaryIndexPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
          {
            var buffer = new byte[81920];
            while (true)
            {
              cancellation.Token.ThrowIfCancellationRequested();
              var bytesRead = await readStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellation.Token);
              if (bytesRead == 0)
              {
                break;
              }

              await writeStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellation.Token);
              bytesCopied += bytesRead;
              observer.OnNext(IndexRebuildProgress.Copying(bytesCopied, totalBytes));
            }

            await writeStream.FlushAsync(cancellation.Token);
          }

          observer.OnNext(IndexRebuildProgress.Swapping(totalBytes));

          File.Move(originalIndexPath, backupPath, overwrite: true);
          File.Move(temporaryIndexPath, originalIndexPath, overwrite: true);
          File.Delete(backupPath);

          var finalInfo = new FileInfo(originalIndexPath);
          stopwatch.Stop();
          var performance = new IndexPerformanceSnapshot(stopwatch.Elapsed, finalInfo.Exists ? finalInfo.Length : bytesCopied);
          observer.OnNext(IndexRebuildProgress.Completed(originalIndexPath, performance));
          observer.OnCompleted();

          _logger.LogInformation("Rebuilt index artifact {Index} for table {Table}", originalIndexPath, request.TablePath);
        }
        catch (OperationCanceledException)
        {
          observer.OnCompleted();
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Failed to rebuild index {Index} for table {Table}", request.IndexName, request.TablePath);
          observer.OnError(ex);
        }
        finally
        {
          if (!string.IsNullOrWhiteSpace(temporaryIndexPath) && File.Exists(temporaryIndexPath))
          {
            try
            {
              File.Delete(temporaryIndexPath);
            }
            catch (Exception cleanupEx)
            {
              _logger.LogWarning(cleanupEx, "Failed to cleanup temporary index artifact {Index}", temporaryIndexPath);
            }
          }

          if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath))
          {
            try
            {
              File.Delete(backupPath);
            }
            catch (Exception cleanupEx)
            {
              _logger.LogWarning(cleanupEx, "Failed to cleanup backup index artifact {Index}", backupPath);
            }
          }
        }
      }, cancellation.Token);

      return Disposable.Create(() => cancellation.Cancel());
    });
  }

  private static string ResolveTableDirectory(string tablePath)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(tablePath);
    var tableDirectory = Path.GetDirectoryName(tablePath);
    if (string.IsNullOrWhiteSpace(tableDirectory))
    {
      throw new DirectoryNotFoundException($"Unable to resolve directory for table path '{tablePath}'.");
    }

    return tableDirectory;
  }

  private static bool IsDeleted(ReadOnlySequence<byte> record)
  {
    if (record.IsEmpty)
    {
      return false;
    }

    var firstSegment = record.FirstSpan;
    var indicator = firstSegment.Length > 0 ? firstSegment[0] : record.ToArray()[0];
    return indicator is (byte)'*' or 0x2A;
  }

  private static IReadOnlyDictionary<string, object?> MaterializeRow(
    ReadOnlySequence<byte> record,
    IReadOnlyList<TableColumn> columns)
  {
    var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    foreach (var column in columns)
    {
      values[column.Name] = column.ValueAccessor(record);
    }

    return values;
  }

  private static string ComputeSignature(IReadOnlyList<SimpleIndexEntry> entries)
  {
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    var fieldSeparator = new byte[] { 0x1F };
    var recordSeparator = new byte[] { 0x1E };
    var numberBuffer = new byte[sizeof(int)];

    foreach (var entry in entries)
    {
      var keyBytes = Encoding.UTF8.GetBytes(entry.Key ?? string.Empty);
      hash.AppendData(keyBytes);

      hash.AppendData(fieldSeparator);

      BinaryPrimitives.WriteInt32LittleEndian(numberBuffer, entry.RecordNumber);
      hash.AppendData(numberBuffer);

      hash.AppendData(recordSeparator);
    }

    return Convert.ToHexString(hash.GetHashAndReset());
  }

  private sealed record SimpleIndexArtifact(
    int FormatVersion,
    string Table,
    string Source,
    string Expression,
    DateTimeOffset GeneratedAtUtc,
    SimpleIndexStatistics Statistics,
    string Signature,
    IReadOnlyList<SimpleIndexEntry> Entries);

  private sealed record SimpleIndexStatistics(int TotalRecords, int ActiveRecords, int DeletedRecords);

  private sealed record SimpleIndexEntry(int RecordNumber, string Key, bool Deleted);

  private sealed class SimpleIndexEntryComparer : IComparer<SimpleIndexEntry>
  {
    public static SimpleIndexEntryComparer Instance { get; } = new();

    public int Compare(SimpleIndexEntry? x, SimpleIndexEntry? y)
    {
      if (ReferenceEquals(x, y))
      {
        return 0;
      }

      if (x is null)
      {
        return -1;
      }

      if (y is null)
      {
        return 1;
      }

      var keyComparison = string.Compare(x.Key, y.Key, StringComparison.OrdinalIgnoreCase);
      if (keyComparison != 0)
      {
        return keyComparison;
      }

      return x.RecordNumber.CompareTo(y.RecordNumber);
    }
  }
}
