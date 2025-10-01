using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Cursors;

public sealed class EmptyCursor : ICursor
{
  private static readonly ReadOnlySequence<byte> EmptySequence = ReadOnlySequence<byte>.Empty;

  public ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.FromResult(false);
  }

  public ReadOnlySequence<byte> Current => EmptySequence;

  public ValueTask DisposeAsync()
  {
    return ValueTask.CompletedTask;
  }
}
