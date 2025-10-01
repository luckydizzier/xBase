using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Locking;

internal sealed class NullLockHandle : ILockHandle
{
  public NullLockHandle(LockKey key, LockType type)
  {
    Key = key;
    Type = type;
  }

  public LockKey Key { get; }

  public LockType Type { get; }

  public ValueTask DisposeAsync()
  {
    return ValueTask.CompletedTask;
  }

  public void Dispose()
  {
  }
}
