using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Cursors;

internal sealed class DbfCursor : ICursor
{
  private readonly FileStream _stream;
  private readonly ushort _recordLength;
  private readonly bool _includeDeleted;
  private readonly Memory<byte> _buffer;

  private bool _disposed;

  public DbfCursor(string path, ushort headerLength, ushort recordLength, bool includeDeleted)
  {
    if (recordLength == 0)
    {
      throw new ArgumentOutOfRangeException(nameof(recordLength));
    }

    _recordLength = recordLength;
    _includeDeleted = includeDeleted;
    _buffer = new byte[recordLength];
    _stream = new FileStream(
      path,
      FileMode.Open,
      FileAccess.Read,
      FileShare.ReadWrite,
      bufferSize: Math.Max(recordLength, (ushort)4096),
      useAsync: true);
    _stream.Seek(headerLength, SeekOrigin.Begin);
  }

  public ReadOnlySequence<byte> Current { get; private set; }

  public async ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    ThrowIfDisposed();

    while (true)
    {
      int read = await _stream
        .ReadAsync(_buffer[.._recordLength], cancellationToken)
        .ConfigureAwait(false);
      if (read < _recordLength)
      {
        Current = default;
        return false;
      }

      ReadOnlyMemory<byte> record = _buffer[.._recordLength];
      byte status = record.Span[0];
      if (!_includeDeleted && (status == (byte)'*' || status == 0x2A))
      {
        continue;
      }

      Current = new ReadOnlySequence<byte>(record);
      return true;
    }
  }

  public async ValueTask DisposeAsync()
  {
    if (_disposed)
    {
      return;
    }

    _disposed = true;
    await _stream.DisposeAsync().ConfigureAwait(false);
    Current = default;
  }

  private void ThrowIfDisposed()
  {
    if (_disposed)
    {
      throw new ObjectDisposedException(nameof(DbfCursor));
    }
  }
}
