using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XBase.Abstractions;

public interface ITableDescriptor
{
  string Name { get; }
  string? MemoFileName { get; }
  IReadOnlyList<IFieldDescriptor> Fields { get; }
  IReadOnlyList<IIndexDescriptor> Indexes { get; }
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

public readonly record struct CursorOptions(bool IncludeDeleted, int? Limit, int? Offset);
