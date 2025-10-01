using System;
using System.IO;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Locking;
using Xunit;

namespace XBase.Core.Tests.Locking;

public sealed class FileLockManagerTests
{
  [Fact]
  public async Task AcquireShared_AllowsConcurrentReaders()
  {
    using var workspace = new TemporaryWorkspace();
    var options = new FileLockManagerOptions
    {
      Mode = LockingMode.File,
      LockDirectory = workspace.Combine("locks")
    };
    var manager = new FileLockManager(options);
    string tablePath = workspace.Combine("table.dbf");
    File.WriteAllBytes(tablePath, Array.Empty<byte>());
    LockKey key = LockKey.ForFile(tablePath);

    await using ILockHandle first = await manager.AcquireAsync(key, LockType.Shared);
    await using ILockHandle second = await manager.AcquireAsync(key, LockType.Shared);

    Assert.Equal(LockType.Shared, first.Type);
    Assert.Equal(LockType.Shared, second.Type);
  }

  [Fact]
  public async Task AcquireExclusive_WaitsForSharedToRelease()
  {
    using var workspace = new TemporaryWorkspace();
    var options = new FileLockManagerOptions
    {
      Mode = LockingMode.File,
      LockDirectory = workspace.Combine("locks"),
      RetryDelay = TimeSpan.FromMilliseconds(10),
      AcquisitionTimeout = TimeSpan.FromSeconds(2)
    };
    var manager = new FileLockManager(options);
    string tablePath = workspace.Combine("table.dbf");
    File.WriteAllBytes(tablePath, Array.Empty<byte>());
    LockKey key = LockKey.ForFile(tablePath);

    await using ILockHandle shared = await manager.AcquireAsync(key, LockType.Shared);

    ValueTask<ILockHandle> exclusiveTask = manager.AcquireAsync(key, LockType.Exclusive);
    await Task.Delay(100);

    Assert.False(exclusiveTask.IsCompleted);

    await shared.DisposeAsync();
    await using ILockHandle exclusive = await exclusiveTask;

    Assert.Equal(LockType.Exclusive, exclusive.Type);
  }

  [Fact]
  public async Task AcquireRecordExclusive_AllowsDifferentRecords()
  {
    if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
    {
      return;
    }

    using var workspace = new TemporaryWorkspace();
    var options = new FileLockManagerOptions
    {
      Mode = LockingMode.Record,
      LockDirectory = workspace.Combine("locks"),
      RetryDelay = TimeSpan.FromMilliseconds(10)
    };
    var manager = new FileLockManager(options);
    string tablePath = workspace.Combine("table.dbf");
    File.WriteAllBytes(tablePath, Array.Empty<byte>());

    LockKey firstRecord = LockKey.ForRecord(tablePath, 1);
    LockKey secondRecord = LockKey.ForRecord(tablePath, 2);

    await using ILockHandle first = await manager.AcquireAsync(firstRecord, LockType.Exclusive);
    await using ILockHandle second = await manager.AcquireAsync(secondRecord, LockType.Exclusive);

    Assert.Equal(LockType.Exclusive, first.Type);
    Assert.Equal(LockType.Exclusive, second.Type);
  }

  [Fact]
  public async Task AcquireRecordExclusive_BlocksSameRecord()
  {
    if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
    {
      return;
    }

    using var workspace = new TemporaryWorkspace();
    var options = new FileLockManagerOptions
    {
      Mode = LockingMode.Record,
      LockDirectory = workspace.Combine("locks"),
      RetryDelay = TimeSpan.FromMilliseconds(10),
      AcquisitionTimeout = TimeSpan.FromSeconds(2)
    };
    var manager = new FileLockManager(options);
    string tablePath = workspace.Combine("table.dbf");
    File.WriteAllBytes(tablePath, Array.Empty<byte>());
    LockKey key = LockKey.ForRecord(tablePath, 5);

    await using ILockHandle first = await manager.AcquireAsync(key, LockType.Exclusive);

    ValueTask<ILockHandle> secondTask = manager.AcquireAsync(key, LockType.Exclusive);
    await Task.Delay(100);
    Assert.False(secondTask.IsCompleted);

    await first.DisposeAsync();
    await using ILockHandle second = await secondTask;

    Assert.Equal(LockType.Exclusive, second.Type);
  }

  [Fact]
  public async Task AcquireRecord_WhenDisabled_Throws()
  {
    using var workspace = new TemporaryWorkspace();
    var options = new FileLockManagerOptions
    {
      Mode = LockingMode.File,
      LockDirectory = workspace.Combine("locks")
    };
    var manager = new FileLockManager(options);
    string tablePath = workspace.Combine("table.dbf");
    File.WriteAllBytes(tablePath, Array.Empty<byte>());
    LockKey key = LockKey.ForRecord(tablePath, 1);

    await Assert.ThrowsAsync<InvalidOperationException>(() => manager.AcquireAsync(key, LockType.Exclusive).AsTask());
  }
}
