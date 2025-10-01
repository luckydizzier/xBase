using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Locking;

public sealed class FileLockManager : ILockManager
{
  private readonly FileLockManagerOptions _options;
  private readonly bool _infiniteTimeout;
  private readonly ConcurrentDictionary<string, SemaphoreSlim> _recordGuards = new(StringComparer.OrdinalIgnoreCase);
  public FileLockManager(FileLockManagerOptions? options = null)
  {
    _options = options ?? new FileLockManagerOptions();

    if (_options.RecordLockSpan <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(options), "RecordLockSpan must be positive.");
    }

    if (_options.RetryDelay <= TimeSpan.Zero)
    {
      throw new ArgumentOutOfRangeException(nameof(options), "RetryDelay must be positive.");
    }

    if (_options.AcquisitionTimeout < TimeSpan.Zero && _options.AcquisitionTimeout != Timeout.InfiniteTimeSpan)
    {
      throw new ArgumentOutOfRangeException(nameof(options), "AcquisitionTimeout must be non-negative or infinite.");
    }

    _infiniteTimeout = _options.AcquisitionTimeout == Timeout.InfiniteTimeSpan;

    if (!string.IsNullOrEmpty(_options.LockDirectory))
    {
      Directory.CreateDirectory(_options.LockDirectory!);
    }
  }

  public async ValueTask<ILockHandle> AcquireAsync(LockKey key, LockType type, CancellationToken cancellationToken = default)
  {
    if (_options.Mode == LockingMode.None)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return new NullLockHandle(key, type);
    }

    if (key.Kind == LockKind.Record && _options.Mode != LockingMode.Record)
    {
      throw new InvalidOperationException("Record-level locking is not enabled.");
    }

    if (key.Kind == LockKind.Record && type == LockType.Shared)
    {
      throw new NotSupportedException("Record locks support exclusive mode only.");
    }

    bool supportsRecordLocks = OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    if (key.Kind == LockKind.Record && !supportsRecordLocks)
    {
      throw new PlatformNotSupportedException("Record locking requires OS support for FileStream.Lock/Unlock.");
    }

    SemaphoreSlim? recordGuard = null;
    bool guardHeld = false;

    if (key.Kind == LockKind.Record)
    {
      string guardKey = GetRecordGuardKey(key);
      recordGuard = _recordGuards.GetOrAdd(guardKey, static _ => new SemaphoreSlim(1, 1));

      if (_infiniteTimeout)
      {
        await recordGuard.WaitAsync(cancellationToken).ConfigureAwait(false);
        guardHeld = true;
      }
      else
      {
        bool acquiredGuard = await recordGuard
          .WaitAsync(_options.AcquisitionTimeout, cancellationToken)
          .ConfigureAwait(false);

        if (!acquiredGuard)
        {
          throw new TimeoutException($"Failed to acquire record guard for '{key.ResourcePath}' (record {key.RecordNumber}).");
        }

        guardHeld = true;
      }
    }

    string lockPath = GetLockFilePath(key);
    string? directoryName = Path.GetDirectoryName(lockPath);
    if (!string.IsNullOrEmpty(directoryName))
    {
      Directory.CreateDirectory(directoryName);
    }

    DateTimeOffset start = DateTimeOffset.UtcNow;
    Exception? lastError = null;

    try
    {
      while (true)
      {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
          FileStream stream = OpenStream(lockPath, key, type);
          long recordOffset = 0;
          bool hasRecordLock = false;

          if (key.Kind == LockKind.Record)
          {
            recordOffset = checked(key.RecordNumber * _options.RecordLockSpan);
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
              stream.Lock(recordOffset, _options.RecordLockSpan);
              hasRecordLock = true;
            }
          }

          return new FileLockHandle(
            key,
            type,
            stream,
            hasRecordLock,
            recordOffset,
            _options.RecordLockSpan,
            recordGuard);
        }
        catch (IOException ex)
        {
          lastError = ex;
        }
        catch (UnauthorizedAccessException ex)
        {
          lastError = ex;
        }

        if (!_infiniteTimeout && DateTimeOffset.UtcNow - start >= _options.AcquisitionTimeout)
        {
          throw new TimeoutException($"Failed to acquire {type} lock for '{key.ResourcePath}'.", lastError);
        }

        await Task.Delay(_options.RetryDelay, cancellationToken).ConfigureAwait(false);
      }
    }
    catch
    {
      if (guardHeld && recordGuard is not null)
      {
        recordGuard.Release();
      }

      throw;
    }
  }

  private static FileStream OpenStream(string lockPath, LockKey key, LockType type)
  {
    FileShare share = FileShare.None;
    FileAccess access = FileAccess.ReadWrite;

    if (key.Kind == LockKind.Record)
    {
      share = FileShare.ReadWrite;
    }
    else if (type == LockType.Shared)
    {
      share = FileShare.Read;
      access = FileAccess.Read;
    }

    return new FileStream(lockPath, FileMode.OpenOrCreate, access, share, bufferSize: 1, FileOptions.Asynchronous);
  }

  private string GetLockFilePath(LockKey key)
  {
    string directory = _options.LockDirectory ?? Path.GetDirectoryName(key.ResourcePath) ?? Directory.GetCurrentDirectory();
    string fileName = Path.GetFileName(key.ResourcePath);

    if (string.IsNullOrEmpty(fileName))
    {
      fileName = "resource";
    }

    return Path.Combine(directory, $"{fileName}.lck");
  }

  private sealed class FileLockHandle : ILockHandle
  {
    private readonly FileStream _stream;
    private readonly bool _hasRecordLock;
    private readonly long _offset;
    private readonly long _span;
    private readonly SemaphoreSlim? _recordGuard;
    private bool _disposed;

    public FileLockHandle(
      LockKey key,
      LockType type,
      FileStream stream,
      bool hasRecordLock,
      long offset,
      long span,
      SemaphoreSlim? recordGuard)
    {
      Key = key;
      Type = type;
      _stream = stream;
      _hasRecordLock = hasRecordLock;
      _offset = offset;
      _span = span;
      _recordGuard = recordGuard;
    }

    public LockKey Key { get; }

    public LockType Type { get; }

    public ValueTask DisposeAsync()
    {
      Dispose();
      return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
      if (_disposed)
      {
        return;
      }

      if (_hasRecordLock && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
      {
        try
        {
          _stream.Unlock(_offset, _span);
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
      }

      _stream.Dispose();
      _recordGuard?.Release();
      _disposed = true;
    }
  }

  private static string GetRecordGuardKey(LockKey key)
  {
    return $"{key.ResourcePath.ToUpperInvariant()}#{key.RecordNumber}";
  }
}
