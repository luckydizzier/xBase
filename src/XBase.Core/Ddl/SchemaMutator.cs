using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Table;

namespace XBase.Core.Ddl;

public sealed class SchemaMutator : ISchemaMutator
{
  private readonly string _rootDirectory;
  private readonly Func<DateTimeOffset> _clock;
  private readonly DbfTableLoader _tableLoader;

  public SchemaMutator(string rootDirectory, Func<DateTimeOffset>? clock = null, DbfTableLoader? tableLoader = null)
  {
    if (string.IsNullOrWhiteSpace(rootDirectory))
    {
      throw new ArgumentException("Root directory must be provided.", nameof(rootDirectory));
    }

    _rootDirectory = Path.GetFullPath(rootDirectory);
    Directory.CreateDirectory(_rootDirectory);
    _clock = clock ?? (() => DateTimeOffset.UtcNow);
    _tableLoader = tableLoader ?? new DbfTableLoader();
  }

  public async ValueTask<SchemaVersion> ExecuteAsync(
    SchemaOperation operation,
    string? author = null,
    CancellationToken cancellationToken = default)
  {
    if (operation is null)
    {
      throw new ArgumentNullException(nameof(operation));
    }

    string logPath = GetLogPath(operation.TableName);
    var log = new SchemaLog(logPath);
    IReadOnlyDictionary<string, string> properties = CloneProperties(operation);
    SchemaLogEntry entry = new(
      SchemaVersion.Start,
      _clock(),
      string.IsNullOrWhiteSpace(author) ? "unknown" : author!,
      operation.Kind,
      properties,
      string.Empty);

    SchemaVersion version = await log.AppendAsync(entry, cancellationToken).ConfigureAwait(false);

    if (RequiresBackfill(operation.Kind))
    {
      var queue = new SchemaBackfillQueue(GetBackfillPath(operation.TableName));
      SchemaBackfillTask task = new(
        version,
        operation.Kind,
        operation.TableName,
        CloneProperties(operation));
      await queue.EnqueueAsync(new[] { task }, cancellationToken).ConfigureAwait(false);
    }

    if (operation.Kind == SchemaOperationKind.DropTable)
    {
      await ClearBackfillAsync(operation.TableName, cancellationToken).ConfigureAwait(false);
    }

    return version;
  }

  public ValueTask<IReadOnlyList<SchemaLogEntry>> ReadHistoryAsync(
    string tableName,
    CancellationToken cancellationToken = default)
  {
    var log = new SchemaLog(GetLogPath(tableName));
    IReadOnlyList<SchemaLogEntry> entries = log.ReadEntries();
    return ValueTask.FromResult(entries);
  }

  public ValueTask<IReadOnlyList<SchemaBackfillTask>> ReadBackfillQueueAsync(
    string tableName,
    CancellationToken cancellationToken = default)
  {
    var queue = new SchemaBackfillQueue(GetBackfillPath(tableName));
    return queue.ReadAsync(cancellationToken);
  }

  public async ValueTask<SchemaVersion> CreateCheckpointAsync(
    string tableName,
    CancellationToken cancellationToken = default)
  {
    var log = new SchemaLog(GetLogPath(tableName));
    SchemaVersion current = log.GetCurrentVersion();
    await log.CreateCheckpointAsync(cancellationToken).ConfigureAwait(false);
    await log.CompactAsync(current, cancellationToken).ConfigureAwait(false);
    var queue = new SchemaBackfillQueue(GetBackfillPath(tableName));
    await queue.RemoveUpToAsync(current, cancellationToken).ConfigureAwait(false);
    return current;
  }

  public async ValueTask<int> PackAsync(string tableName, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(tableName))
    {
      throw new ArgumentException("Table name must be provided.", nameof(tableName));
    }

    string tablePath = GetTablePath(tableName);
    if (!File.Exists(tablePath))
    {
      throw new FileNotFoundException($"Table '{tableName}' was not found for compaction.", tablePath);
    }

    DbfTableDescriptor descriptor = _tableLoader.LoadDbf(tablePath);
    string tempPath = tablePath + ".pack";
    string? directory = Path.GetDirectoryName(tempPath);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    byte[] header = new byte[descriptor.HeaderLength];
    uint survivors = 0;

    await using (FileStream source = new(tablePath, FileMode.Open, FileAccess.Read, FileShare.Read))
    await using (FileStream destination = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
    {
      await source.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
      await destination.WriteAsync(header, cancellationToken).ConfigureAwait(false);

      byte[] recordBuffer = new byte[descriptor.RecordLength];
      while (true)
      {
        cancellationToken.ThrowIfCancellationRequested();
        int marker = source.ReadByte();
        if (marker < 0)
        {
          break;
        }

        if (marker == 0x1A)
        {
          break;
        }

        if (recordBuffer.Length == 0)
        {
          throw new InvalidDataException($"Record length for '{tableName}' is zero; cannot compact.");
        }

        recordBuffer[0] = (byte)marker;
        await source
          .ReadExactlyAsync(recordBuffer.AsMemory(1, recordBuffer.Length - 1), cancellationToken)
          .ConfigureAwait(false);

        if (recordBuffer[0] != 0x2A && recordBuffer[0] != (byte)'*')
        {
          await destination.WriteAsync(recordBuffer, cancellationToken).ConfigureAwait(false);
          survivors++;
        }
      }

      await destination.WriteAsync(new byte[] { 0x1A }, cancellationToken).ConfigureAwait(false);
      UpdateHeaderMetadata(header, survivors);
      destination.Position = 0;
      await destination.WriteAsync(header, cancellationToken).ConfigureAwait(false);
      await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
      destination.Flush(true);
    }

    SwapFiles(tempPath, tablePath);
    await RewriteIndexesAsync(tablePath, descriptor, survivors, cancellationToken).ConfigureAwait(false);

    var queue = new SchemaBackfillQueue(GetBackfillPath(tableName));
    IReadOnlyList<SchemaBackfillTask> tasks = await queue.ReadAsync(cancellationToken).ConfigureAwait(false);
    if (tasks.Count > 0)
    {
      await queue.RemoveUpToAsync(tasks[^1].Version, cancellationToken).ConfigureAwait(false);
    }

    return tasks.Count;
  }

  public async ValueTask<int> ReindexAsync(string tableName, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(tableName))
    {
      throw new ArgumentException("Table name must be provided.", nameof(tableName));
    }

    string tablePath = GetTablePath(tableName);
    if (!File.Exists(tablePath))
    {
      throw new FileNotFoundException($"Table '{tableName}' was not found for reindexing.", tablePath);
    }

    DbfTableDescriptor descriptor = _tableLoader.LoadDbf(tablePath);
    return await RewriteIndexesAsync(tablePath, descriptor, descriptor.RecordCount, cancellationToken).ConfigureAwait(false);
  }

  private string GetLogPath(string tableName)
  {
    return Path.Combine(_rootDirectory, tableName + ".ddl");
  }

  private string GetBackfillPath(string tableName)
  {
    return Path.Combine(_rootDirectory, tableName + ".ddlq");
  }

  private string GetTablePath(string tableName)
  {
    return Path.Combine(_rootDirectory, tableName + ".dbf");
  }

  private static bool RequiresBackfill(SchemaOperationKind kind)
  {
    return kind is SchemaOperationKind.AlterTableAddColumn
      or SchemaOperationKind.AlterTableDropColumn
      or SchemaOperationKind.AlterTableModifyColumn;
  }

  private static IReadOnlyDictionary<string, string> CloneProperties(SchemaOperation operation)
  {
    var clone = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (operation.Properties is not null)
    {
      foreach (KeyValuePair<string, string> pair in operation.Properties)
      {
        clone[pair.Key] = pair.Value;
      }
    }

    if (!clone.ContainsKey("table"))
    {
      clone["table"] = operation.TableName;
    }

    if (!string.IsNullOrWhiteSpace(operation.ObjectName) && !clone.ContainsKey("object"))
    {
      clone["object"] = operation.ObjectName!;
    }

    return clone;
  }

  private async ValueTask ClearBackfillAsync(string tableName, CancellationToken cancellationToken)
  {
    var queue = new SchemaBackfillQueue(GetBackfillPath(tableName));
    IReadOnlyList<SchemaBackfillTask> tasks = await queue.ReadAsync(cancellationToken).ConfigureAwait(false);
    if (tasks.Count == 0)
    {
      return;
    }

    await queue.RemoveUpToAsync(tasks[^1].Version, cancellationToken).ConfigureAwait(false);
  }

  private static void UpdateHeaderMetadata(Span<byte> header, uint recordCount)
  {
    DateTime utcNow = DateTime.UtcNow;
    int year = Math.Clamp(utcNow.Year - 1900, 0, 255);
    header[1] = (byte)year;
    header[2] = (byte)utcNow.Month;
    header[3] = (byte)utcNow.Day;
    BinaryPrimitives.WriteUInt32LittleEndian(header[4..8], recordCount);
  }

  private async ValueTask<int> RewriteIndexesAsync(
    string tablePath,
    DbfTableDescriptor descriptor,
    uint recordCount,
    CancellationToken cancellationToken)
  {
    if (descriptor.Sidecars.IndexFileNames.Count == 0)
    {
      return 0;
    }

    string? directory = Path.GetDirectoryName(tablePath);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    int rebuilt = 0;
    foreach (string indexFile in descriptor.Sidecars.IndexFileNames)
    {
      string indexPath = Path.Combine(directory ?? _rootDirectory, indexFile);
      string tempPath = indexPath + ".rebuild";
      string manifest = BuildIndexManifest(descriptor, recordCount, indexFile);
      string? indexDirectory = Path.GetDirectoryName(indexPath);
      if (!string.IsNullOrEmpty(indexDirectory))
      {
        Directory.CreateDirectory(indexDirectory);
      }

      await File.WriteAllTextAsync(tempPath, manifest, cancellationToken).ConfigureAwait(false);
      SwapFiles(tempPath, indexPath);
      rebuilt++;
    }

    return rebuilt;
  }

  private static string BuildIndexManifest(
    DbfTableDescriptor descriptor,
    uint recordCount,
    string fileName)
  {
    IIndexDescriptor? indexDescriptor = descriptor.Indexes
      .OfType<IndexDescriptor>()
      .FirstOrDefault(index => !string.IsNullOrEmpty(index.FileName) &&
        string.Equals(index.FileName, fileName, StringComparison.OrdinalIgnoreCase));

    string expression = indexDescriptor?.Expression ?? string.Empty;
    var builder = new StringBuilder();
    builder.AppendLine("xBase Index Manifest");
    builder.AppendLine($"Table: {descriptor.Name}");
    builder.AppendLine($"IndexFile: {fileName}");
    if (indexDescriptor is not null)
    {
      builder.AppendLine($"Index: {indexDescriptor.Name}");
    }

    if (!string.IsNullOrWhiteSpace(expression))
    {
      builder.AppendLine($"Expression: {expression}");
    }

    builder.AppendLine($"RecordCount: {recordCount}");
    builder.AppendLine($"Fields: {string.Join(',', descriptor.Fields.Select(field => field.Name))}");
    return builder.ToString();
  }

  private static void SwapFiles(string sourcePath, string destinationPath)
  {
    string backupPath = destinationPath + ".bak";
    try
    {
      try
      {
        File.Replace(sourcePath, destinationPath, backupPath, ignoreMetadataErrors: true);
      }
      catch (PlatformNotSupportedException)
      {
        ReplaceByMove(sourcePath, destinationPath, backupPath);
      }
      catch (IOException) when (!OperatingSystem.IsWindows())
      {
        ReplaceByMove(sourcePath, destinationPath, backupPath);
      }
    }
    finally
    {
      if (File.Exists(sourcePath))
      {
        File.Delete(sourcePath);
      }

      if (File.Exists(backupPath))
      {
        File.Delete(backupPath);
      }
    }
  }

  private static void ReplaceByMove(string sourcePath, string destinationPath, string backupPath)
  {
    if (File.Exists(destinationPath))
    {
      File.Copy(destinationPath, backupPath, overwrite: true);
      File.Delete(destinationPath);
    }

    File.Move(sourcePath, destinationPath, overwrite: true);
  }
}
