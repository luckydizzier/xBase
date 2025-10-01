using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Ddl;

public sealed class SchemaMutator : ISchemaMutator
{
  private readonly string _rootDirectory;
  private readonly Func<DateTimeOffset> _clock;

  public SchemaMutator(string rootDirectory, Func<DateTimeOffset>? clock = null)
  {
    if (string.IsNullOrWhiteSpace(rootDirectory))
    {
      throw new ArgumentException("Root directory must be provided.", nameof(rootDirectory));
    }

    _rootDirectory = Path.GetFullPath(rootDirectory);
    Directory.CreateDirectory(_rootDirectory);
    _clock = clock ?? (() => DateTimeOffset.UtcNow);
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
    var queue = new SchemaBackfillQueue(GetBackfillPath(tableName));
    IReadOnlyList<SchemaBackfillTask> tasks = await queue.ReadAsync(cancellationToken).ConfigureAwait(false);
    if (tasks.Count == 0)
    {
      return 0;
    }

    SchemaVersion latest = tasks[^1].Version;
    await queue.RemoveUpToAsync(latest, cancellationToken).ConfigureAwait(false);
    return tasks.Count;
  }

  private string GetLogPath(string tableName)
  {
    return Path.Combine(_rootDirectory, tableName + ".ddl");
  }

  private string GetBackfillPath(string tableName)
  {
    return Path.Combine(_rootDirectory, tableName + ".ddlq");
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
}
