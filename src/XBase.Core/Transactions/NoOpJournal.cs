using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Transactions;

public sealed class NoOpJournal : IJournal
{
  public ValueTask BeginAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.CompletedTask;
  }

  public ValueTask CommitAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.CompletedTask;
  }

  public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.CompletedTask;
  }
}
