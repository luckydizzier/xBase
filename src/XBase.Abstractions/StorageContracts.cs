using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XBase.Abstractions;

public readonly record struct SchemaVersion(ulong Value)
{
  public static SchemaVersion Start { get; } = new(0);

  public bool IsStart => Value == 0;

  public SchemaVersion Next() => new(Value + 1);

  public override string ToString() => Value.ToString();

  public static bool operator >(SchemaVersion left, SchemaVersion right) => left.Value > right.Value;

  public static bool operator <(SchemaVersion left, SchemaVersion right) => left.Value < right.Value;

  public static bool operator >=(SchemaVersion left, SchemaVersion right) => left.Value >= right.Value;

  public static bool operator <=(SchemaVersion left, SchemaVersion right) => left.Value <= right.Value;
}

public enum SchemaOperationKind
{
  CreateTable,
  AlterTableAddColumn,
  AlterTableDropColumn,
  AlterTableRenameColumn,
  AlterTableModifyColumn,
  DropTable,
  CreateIndex,
  DropIndex,
  Checkpoint,
  Pack
}

public sealed record SchemaOperation
{
  public SchemaOperation(
    SchemaOperationKind kind,
    string tableName,
    string? objectName,
    IReadOnlyDictionary<string, string>? properties = null)
  {
    if (string.IsNullOrWhiteSpace(tableName))
    {
      throw new ArgumentException("Table name must be provided.", nameof(tableName));
    }

    Kind = kind;
    TableName = tableName;
    ObjectName = objectName;
    Properties = properties ?? new Dictionary<string, string>();
  }

  public SchemaOperationKind Kind { get; }

  public string TableName { get; }

  public string? ObjectName { get; }

  public IReadOnlyDictionary<string, string> Properties { get; }
}

public sealed record SchemaLogEntry
{
  public SchemaLogEntry(
    SchemaVersion version,
    DateTimeOffset timestamp,
    string author,
    SchemaOperationKind kind,
    IReadOnlyDictionary<string, string> properties,
    string checksum)
  {
    Version = version;
    Timestamp = timestamp;
    Author = author;
    Kind = kind;
    Properties = properties ?? new Dictionary<string, string>();
    Checksum = checksum;
  }

  public SchemaVersion Version { get; }

  public DateTimeOffset Timestamp { get; }

  public string Author { get; }

  public SchemaOperationKind Kind { get; }

  public IReadOnlyDictionary<string, string> Properties { get; }

  public string Checksum { get; }
}

public sealed record SchemaBackfillTask
{
  public SchemaBackfillTask(
    SchemaVersion version,
    SchemaOperationKind kind,
    string tableName,
    IReadOnlyDictionary<string, string> properties)
  {
    Version = version;
    Kind = kind;
    TableName = tableName;
    Properties = properties ?? new Dictionary<string, string>();
  }

  public SchemaVersion Version { get; }

  public SchemaOperationKind Kind { get; }

  public string TableName { get; }

  public IReadOnlyDictionary<string, string> Properties { get; }
}

public interface ITableDescriptor
{
  string Name { get; }
  string? MemoFileName { get; }
  IReadOnlyList<IFieldDescriptor> Fields { get; }
  IReadOnlyList<IIndexDescriptor> Indexes { get; }
  SchemaVersion SchemaVersion { get; }
}

public interface IFieldDescriptor
{
  string Name { get; }
  string Type { get; }
  int Length { get; }
  int DecimalCount { get; }
  bool IsNullable { get; }
}

public interface IIndexDescriptor
{
  string Name { get; }
  string Expression { get; }
  bool IsDescending { get; }
}

public interface ICursor : IAsyncDisposable
{
  ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default);
  ReadOnlySequence<byte> Current { get; }
}

public interface ICursorFactory
{
  ValueTask<ICursor> CreateSequentialAsync(ITableDescriptor table, CursorOptions options, CancellationToken cancellationToken = default);
  ValueTask<ICursor> CreateIndexedAsync(ITableDescriptor table, IIndexDescriptor index, CursorOptions options, CancellationToken cancellationToken = default);
}

public interface IJournal
{
  ValueTask BeginAsync(CancellationToken cancellationToken = default);
  ValueTask CommitAsync(CancellationToken cancellationToken = default);
  ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}

public interface ITableMutator
{
  ValueTask InsertAsync(ReadOnlySequence<byte> record, CancellationToken cancellationToken = default);
  ValueTask UpdateAsync(int recordNumber, ReadOnlySequence<byte> record, CancellationToken cancellationToken = default);
  ValueTask DeleteAsync(int recordNumber, CancellationToken cancellationToken = default);
}

public interface ISchemaMutator
{
  ValueTask<SchemaVersion> ExecuteAsync(
    SchemaOperation operation,
    string? author = null,
    CancellationToken cancellationToken = default);

  ValueTask<IReadOnlyList<SchemaLogEntry>> ReadHistoryAsync(
    string tableName,
    CancellationToken cancellationToken = default);

  ValueTask<IReadOnlyList<SchemaBackfillTask>> ReadBackfillQueueAsync(
    string tableName,
    CancellationToken cancellationToken = default);
}

public readonly record struct CursorOptions(bool IncludeDeleted, int? Limit, int? Offset);
