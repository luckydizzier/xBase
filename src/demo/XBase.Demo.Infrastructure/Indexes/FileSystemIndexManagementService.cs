using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XBase.Demo.Domain.Services;
using XBase.Demo.Domain.Services.Models;

namespace XBase.Demo.Infrastructure.Indexes;

/// <summary>
/// Minimal index lifecycle service that creates placeholder index files on disk.
/// </summary>
public sealed class FileSystemIndexManagementService : IIndexManagementService
{
  private readonly ILogger<FileSystemIndexManagementService> _logger;

  public FileSystemIndexManagementService(ILogger<FileSystemIndexManagementService> logger)
  {
    _logger = logger;
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
      await using var stream = new FileStream(indexPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
      var content = Encoding.UTF8.GetBytes($"// Placeholder index for expression: {request.Expression}{Environment.NewLine}");
      await stream.WriteAsync(content, 0, content.Length, cancellationToken);
      await stream.FlushAsync(cancellationToken);

      _logger.LogInformation("Created index placeholder {Index} for table {Table}", indexPath, request.TablePath);
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
      _logger.LogInformation("Dropped index placeholder {Index} for table {Table}", indexPath, request.TablePath);
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

          _logger.LogInformation("Rebuilt index placeholder {Index} for table {Table}", originalIndexPath, request.TablePath);
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
}
