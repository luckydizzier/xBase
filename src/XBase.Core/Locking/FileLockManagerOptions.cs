using System;
using XBase.Abstractions;

namespace XBase.Core.Locking;

public sealed class FileLockManagerOptions
{
  public LockingMode Mode { get; init; } = LockingMode.File;

  public string? LockDirectory { get; init; }

  public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(50);

  public TimeSpan AcquisitionTimeout { get; init; } = TimeSpan.FromSeconds(10);

  public long RecordLockSpan { get; init; } = 1;
}
