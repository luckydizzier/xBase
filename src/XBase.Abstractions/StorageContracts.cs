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

public enum JournalEntryType
{
  Begin = 1,
  Mutation = 2,
  Commit = 3,
  Rollback = 4
}

public enum JournalMutationKind
{
  Insert = 1,
  Update = 2,
  Delete = 3
}

public sealed class JournalMutation
{
  private readonly byte[] _beforeImage;
  private readonly byte[] _afterImage;

  public JournalMutation(
    string tableName,
    int recordNumber,
    JournalMutationKind kind,
    ReadOnlyMemory<byte> beforeImage,
    ReadOnlyMemory<byte> afterImage)
  {
    if (string.IsNullOrWhiteSpace(tableName))
    {
      throw new ArgumentException("Value cannot be null or whitespace.", nameof(tableName));
    }

    TableName = tableName;
    RecordNumber = recordNumber;
    Kind = kind;
    _beforeImage = beforeImage.ToArray();
    _afterImage = afterImage.ToArray();
  }

  public string TableName { get; }

  public int RecordNumber { get; }

  public JournalMutationKind Kind { get; }

  public ReadOnlyMemory<byte> BeforeImage => _beforeImage;

  public ReadOnlyMemory<byte> AfterImage => _afterImage;

  public static JournalMutation Insert(
    string tableName,
    int recordNumber,
    ReadOnlyMemory<byte> record)
  {
    return new JournalMutation(tableName, recordNumber, JournalMutationKind.Insert, ReadOnlyMemory<byte>.Empty, record);
  }

  public static JournalMutation Update(
    string tableName,
    int recordNumber,
    ReadOnlyMemory<byte> beforeImage,
    ReadOnlyMemory<byte> afterImage)
  {
    return new JournalMutation(tableName, recordNumber, JournalMutationKind.Update, beforeImage, afterImage);
  }

  public static JournalMutation Delete(
    string tableName,
    int recordNumber,
    ReadOnlyMemory<byte> beforeImage)
  {
    return new JournalMutation(tableName, recordNumber, JournalMutationKind.Delete, beforeImage, ReadOnlyMemory<byte>.Empty);
  }
}

public sealed class JournalEntry
{
  private JournalEntry(
    long transactionId,
    JournalEntryType entryType,
    DateTimeOffset timestamp,
    JournalMutation? mutation)
  {
    TransactionId = transactionId;
    EntryType = entryType;
    Timestamp = timestamp.ToUniversalTime();
    Mutation = mutation;
  }

  public long TransactionId { get; }

  public JournalEntryType EntryType { get; }

  public DateTimeOffset Timestamp { get; }

  public JournalMutation? Mutation { get; }

  public static JournalEntry Begin(long transactionId, DateTimeOffset timestamp)
  {
    return new JournalEntry(transactionId, JournalEntryType.Begin, timestamp, mutation: null);
  }

  public static JournalEntry Commit(long transactionId, DateTimeOffset timestamp)
  {
    return new JournalEntry(transactionId, JournalEntryType.Commit, timestamp, mutation: null);
  }

  public static JournalEntry Rollback(long transactionId, DateTimeOffset timestamp)
  {
    return new JournalEntry(transactionId, JournalEntryType.Rollback, timestamp, mutation: null);
  }

  public static JournalEntry ForMutation(long transactionId, DateTimeOffset timestamp, JournalMutation mutation)
  {
    if (mutation is null)
    {
      throw new ArgumentNullException(nameof(mutation));
    }

    return new JournalEntry(transactionId, JournalEntryType.Mutation, timestamp, mutation);
  }
}

public interface IJournal
{
  ValueTask BeginAsync(CancellationToken cancellationToken = default);
  ValueTask AppendAsync(JournalEntry entry, CancellationToken cancellationToken = default);
  ValueTask CommitAsync(CancellationToken cancellationToken = default);
  ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}

public enum LockKind
{
  File,
  Record
}

public enum LockType
{
  Shared,
  Exclusive
}

public enum LockingMode
{
  None,
  File,
  Record
}

public readonly record struct LockKey
{
  public LockKey(string resourcePath, LockKind kind, long recordNumber = -1)
  {
    if (string.IsNullOrWhiteSpace(resourcePath))
    {
      throw new ArgumentException("Resource path must be provided.", nameof(resourcePath));
    }

    if (kind == LockKind.Record && recordNumber < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(recordNumber), "Record number must be non-negative for record locks.");
    }

    ResourcePath = resourcePath;
    Kind = kind;
    RecordNumber = recordNumber;
  }

  public string ResourcePath { get; }

  public LockKind Kind { get; }

  public long RecordNumber { get; }

  public static LockKey ForFile(string resourcePath)
  {
    return new LockKey(resourcePath, LockKind.File);
  }

  public static LockKey ForRecord(string resourcePath, long recordNumber)
  {
    return new LockKey(resourcePath, LockKind.Record, recordNumber);
  }
}

public interface ILockHandle : IAsyncDisposable, IDisposable
{
  LockKey Key { get; }
  LockType Type { get; }
}

public interface ILockManager
{
  ValueTask<ILockHandle> AcquireAsync(LockKey key, LockType type, CancellationToken cancellationToken = default);
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
