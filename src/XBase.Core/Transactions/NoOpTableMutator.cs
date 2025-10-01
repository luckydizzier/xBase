using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Transactions;

public sealed class NoOpTableMutator : ITableMutator
{
  public ValueTask InsertAsync(ReadOnlySequence<byte> record, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.CompletedTask;
  }

  public ValueTask UpdateAsync(int recordNumber, ReadOnlySequence<byte> record, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.CompletedTask;
  }

  public ValueTask DeleteAsync(int recordNumber, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.CompletedTask;
  }
}
