using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Locking;

public sealed class NoOpLockManager : ILockManager
{
  public ValueTask<ILockHandle> AcquireAsync(LockKey key, LockType type, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.FromResult<ILockHandle>(new NullLockHandle(key, type));
  }
}
