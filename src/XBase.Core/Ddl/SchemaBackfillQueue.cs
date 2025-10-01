using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Ddl;

public sealed class SchemaBackfillQueue
{
  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
  };

  private readonly string _filePath;

  public SchemaBackfillQueue(string filePath)
  {
    _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
  }

  public string FilePath => _filePath;

  public ValueTask<IReadOnlyList<SchemaBackfillTask>> ReadAsync(CancellationToken cancellationToken = default)
  {
    IReadOnlyList<SchemaBackfillTask> tasks = ReadInternal();
    return ValueTask.FromResult(tasks);
  }

  public ValueTask EnqueueAsync(IEnumerable<SchemaBackfillTask> tasks, CancellationToken cancellationToken = default)
  {
    if (tasks is null)
    {
      throw new ArgumentNullException(nameof(tasks));
    }

    List<SchemaBackfillTask> buffer = new(ReadInternal());
    buffer.AddRange(tasks.Select(CloneTask));
    WriteInternal(buffer);
    return ValueTask.CompletedTask;
  }

  public ValueTask RemoveUpToAsync(SchemaVersion version, CancellationToken cancellationToken = default)
  {
    List<SchemaBackfillTask> buffer = new(ReadInternal());
    if (buffer.Count == 0)
    {
      return ValueTask.CompletedTask;
    }

    buffer.RemoveAll(task => task.Version <= version);
    WriteInternal(buffer);
    return ValueTask.CompletedTask;
  }

  private IReadOnlyList<SchemaBackfillTask> ReadInternal()
  {
    if (!File.Exists(_filePath))
    {
      return Array.Empty<SchemaBackfillTask>();
    }

    using FileStream stream = new(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    List<SchemaBackfillTask>? tasks = JsonSerializer.Deserialize<List<SchemaBackfillTask>>(stream, SerializerOptions);
    return tasks ?? new List<SchemaBackfillTask>();
  }

  private void WriteInternal(List<SchemaBackfillTask> tasks)
  {
    string? directory = Path.GetDirectoryName(_filePath);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    using FileStream stream = new(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
    JsonSerializer.Serialize(stream, tasks, SerializerOptions);
    stream.Flush(true);
  }

  private static SchemaBackfillTask CloneTask(SchemaBackfillTask task)
  {
    Dictionary<string, string> properties = new(StringComparer.OrdinalIgnoreCase);
    if (task.Properties is not null)
    {
      foreach (KeyValuePair<string, string> pair in task.Properties)
      {
        properties[pair.Key] = pair.Value;
      }
    }

    return new SchemaBackfillTask(task.Version, task.Kind, task.TableName, properties);
  }
}
