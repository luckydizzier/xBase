using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Transactions;

public sealed class WalJournal : IJournal, IAsyncDisposable
{
  private const int EntryHeaderLength = 8;
  private static readonly byte[] FileHeader = CreateFileHeader();
  private static long _globalTransactionSeed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

  private readonly WalJournalOptions _options;
  private readonly SemaphoreSlim _gate = new(1, 1);
  private readonly string _journalPath;
  private readonly Func<long> _transactionIdProvider;
  private FileStream? _stream;
  private WalJournalState _state = WalJournalState.Idle;
  private long _currentTransactionId;
  private bool _disposed;

  public WalJournal(WalJournalOptions options)
  {
    _options = options ?? throw new ArgumentNullException(nameof(options));
    _journalPath = options.ResolveJournalPath();
    Directory.CreateDirectory(options.DirectoryPath);
    _transactionIdProvider = options.TransactionIdProvider ?? DefaultTransactionIdProvider;
  }

  public long? ActiveTransactionId => _state == WalJournalState.Active ? _currentTransactionId : null;

  public async ValueTask BeginAsync(CancellationToken cancellationToken = default)
  {
    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      ThrowIfDisposed();
      FileStream stream = await GetWritableStreamAsync(cancellationToken).ConfigureAwait(false);

      if (stream.Length > FileHeader.Length)
      {
        throw new InvalidOperationException("Journal contains pending entries. Run recovery before starting a transaction.");
      }

      if (_state != WalJournalState.Idle)
      {
        throw new InvalidOperationException("A transaction is already active.");
      }

      _currentTransactionId = _transactionIdProvider();
      JournalEntry beginEntry = JournalEntry.Begin(_currentTransactionId, DateTimeOffset.UtcNow);
      await AppendInternalAsync(stream, beginEntry, cancellationToken).ConfigureAwait(false);
      _state = WalJournalState.Active;
    }
    finally
    {
      _gate.Release();
    }
  }

  public async ValueTask AppendAsync(JournalEntry entry, CancellationToken cancellationToken = default)
  {
    if (entry is null)
    {
      throw new ArgumentNullException(nameof(entry));
    }

    if (entry.EntryType != JournalEntryType.Mutation)
    {
      throw new ArgumentException("Only mutation entries can be appended through AppendAsync.", nameof(entry));
    }

    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      ThrowIfDisposed();
      if (_state != WalJournalState.Active)
      {
        throw new InvalidOperationException("No active transaction.");
      }

      if (entry.TransactionId != _currentTransactionId)
      {
        throw new InvalidOperationException("Entry transaction id does not match the active transaction.");
      }

      FileStream stream = await GetWritableStreamAsync(cancellationToken).ConfigureAwait(false);
      await AppendInternalAsync(stream, entry, cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      _gate.Release();
    }
  }

  public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
  {
    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      ThrowIfDisposed();
      if (_state != WalJournalState.Active)
      {
        throw new InvalidOperationException("No active transaction to commit.");
      }

      FileStream stream = await GetWritableStreamAsync(cancellationToken).ConfigureAwait(false);
      JournalEntry commitEntry = JournalEntry.Commit(_currentTransactionId, DateTimeOffset.UtcNow);
      await AppendInternalAsync(stream, commitEntry, cancellationToken).ConfigureAwait(false);
      await FinalizeTransactionAsync(stream, cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      _gate.Release();
    }
  }

  public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
  {
    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      ThrowIfDisposed();
      if (_state != WalJournalState.Active)
      {
        throw new InvalidOperationException("No active transaction to roll back.");
      }

      FileStream stream = await GetWritableStreamAsync(cancellationToken).ConfigureAwait(false);
      JournalEntry rollbackEntry = JournalEntry.Rollback(_currentTransactionId, DateTimeOffset.UtcNow);
      await AppendInternalAsync(stream, rollbackEntry, cancellationToken).ConfigureAwait(false);
      await FinalizeTransactionAsync(stream, cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      _gate.Release();
    }
  }

  public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
  {
    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      ThrowIfDisposed();
      if (_state == WalJournalState.Active)
      {
        throw new InvalidOperationException("Cannot reset journal while a transaction is active.");
      }

      await ResetJournalFileAsync(cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      _gate.Release();
    }
  }

  public async ValueTask DisposeAsync()
  {
    if (_disposed)
    {
      return;
    }

    await _gate.WaitAsync().ConfigureAwait(false);
    try
    {
      if (_disposed)
      {
        return;
      }

      _disposed = true;
      FileStream? stream = _stream;
      _stream = null;
      stream?.Dispose();
    }
    finally
    {
      _gate.Release();
      _gate.Dispose();
    }
  }

  public static async ValueTask<WalRecoveryResult> RecoverAsync(
    WalJournalOptions options,
    CancellationToken cancellationToken = default)
  {
    if (options is null)
    {
      throw new ArgumentNullException(nameof(options));
    }

    string journalPath = options.ResolveJournalPath();
    if (!File.Exists(journalPath))
    {
      return WalRecoveryResult.Empty;
    }

    using FileStream stream = new(
      journalPath,
      FileMode.Open,
      FileAccess.Read,
      FileShare.ReadWrite,
      bufferSize: 4096,
      FileOptions.Asynchronous);

    if (stream.Length < FileHeader.Length)
    {
      return WalRecoveryResult.Empty;
    }

    await ValidateHeaderAsync(stream, cancellationToken).ConfigureAwait(false);

    var transactions = new Dictionary<long, TransactionBuilder>();
    bool truncated = false;
    bool checksumFailure = false;

    while (true)
    {
      WalJournalReadResult result = await WalJournalCodec.TryReadEntryAsync(stream, cancellationToken).ConfigureAwait(false);
      if (result.Status == WalJournalReadStatus.EndOfFile)
      {
        break;
      }

      if (result.Status == WalJournalReadStatus.Truncated)
      {
        truncated = true;
        break;
      }

      if (result.Status == WalJournalReadStatus.ChecksumMismatch)
      {
        checksumFailure = true;
        break;
      }

      JournalEntry entry = result.Entry!;
      if (!transactions.TryGetValue(entry.TransactionId, out TransactionBuilder? builder))
      {
        if (entry.EntryType != JournalEntryType.Begin)
        {
          continue;
        }

        builder = new TransactionBuilder(entry.TransactionId, entry.Timestamp);
        transactions[entry.TransactionId] = builder;
        continue;
      }

      switch (entry.EntryType)
      {
        case JournalEntryType.Begin:
          builder = new TransactionBuilder(entry.TransactionId, entry.Timestamp);
          transactions[entry.TransactionId] = builder;
          break;
        case JournalEntryType.Mutation:
          builder.Mutations.Add(entry.Mutation!);
          break;
        case JournalEntryType.Commit:
          builder.MarkCommitted(entry.Timestamp);
          break;
        case JournalEntryType.Rollback:
          builder.MarkRolledBack(entry.Timestamp);
          break;
      }
    }

    var committed = new List<WalCommittedTransaction>();
    var incomplete = new List<WalInFlightTransaction>();

    foreach (TransactionBuilder builder in transactions.Values)
    {
      if (builder.IsCommitted)
      {
        committed.Add(builder.BuildCommitted());
      }
      else if (builder.HasBegun)
      {
        incomplete.Add(builder.BuildIncomplete());
      }
    }

    return committed.Count == 0 && incomplete.Count == 0 && !checksumFailure && !truncated
      ? WalRecoveryResult.Empty
      : new WalRecoveryResult(committed, incomplete, checksumFailure, truncated);
  }

  private async ValueTask<FileStream> GetWritableStreamAsync(CancellationToken cancellationToken)
  {
    if (_stream is not null)
    {
      return _stream;
    }

    var stream = new FileStream(
      _journalPath,
      FileMode.OpenOrCreate,
      FileAccess.ReadWrite,
      FileShare.Read,
      bufferSize: 4096,
      FileOptions.Asynchronous | FileOptions.WriteThrough);

    if (stream.Length == 0)
    {
      await WriteHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
    }
    else
    {
      await ValidateHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
      stream.Seek(FileHeader.Length, SeekOrigin.Begin);
    }

    _stream = stream;
    return stream;
  }

  private async ValueTask AppendInternalAsync(FileStream stream, JournalEntry entry, CancellationToken cancellationToken)
  {
    ArrayBufferWriter<byte> payloadWriter = new();
    WalJournalCodec.WritePayload(payloadWriter, entry);
    ReadOnlyMemory<byte> payload = payloadWriter.WrittenMemory;
    int payloadLength = payload.Length;
    uint checksum = Crc32.HashToUInt32(payload.Span);

    byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(EntryHeaderLength);
    try
    {
      BinaryPrimitives.WriteInt32LittleEndian(headerBuffer.AsSpan(0, 4), payloadLength);
      BinaryPrimitives.WriteUInt32LittleEndian(headerBuffer.AsSpan(4, 4), checksum);

      await stream.WriteAsync(headerBuffer.AsMemory(0, EntryHeaderLength), cancellationToken).ConfigureAwait(false);
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(headerBuffer);
    }

    await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);

    if (_options.FlushOnWrite)
    {
      await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
      if (_options.FlushToDisk)
      {
        stream.Flush(flushToDisk: true);
      }
    }
  }

  private async ValueTask FinalizeTransactionAsync(FileStream stream, CancellationToken cancellationToken)
  {
    if (_options.AutoResetOnCommit)
    {
      await ResetJournalFileAsync(cancellationToken).ConfigureAwait(false);
    }
    else
    {
      await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
      if (_options.FlushToDisk)
      {
        stream.Flush(flushToDisk: true);
      }

      stream.Dispose();
      _stream = null;
      _state = WalJournalState.Idle;
      _currentTransactionId = 0;
    }
  }

  private async ValueTask ResetJournalFileAsync(CancellationToken cancellationToken)
  {
    FileStream? existing = _stream;
    _stream = null;
    existing?.Dispose();

    using FileStream resetStream = new(
      _journalPath,
      FileMode.Create,
      FileAccess.Write,
      FileShare.Read,
      bufferSize: 4096,
      FileOptions.Asynchronous | FileOptions.WriteThrough);

    await WriteHeaderAsync(resetStream, cancellationToken).ConfigureAwait(false);
    _state = WalJournalState.Idle;
    _currentTransactionId = 0;
  }

  private static async ValueTask WriteHeaderAsync(FileStream stream, CancellationToken cancellationToken)
  {
    stream.Seek(0, SeekOrigin.Begin);
    await stream.WriteAsync(FileHeader, cancellationToken).ConfigureAwait(false);
    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    stream.Flush(flushToDisk: true);
    stream.Seek(0, SeekOrigin.End);
  }

  private static async ValueTask ValidateHeaderAsync(FileStream stream, CancellationToken cancellationToken)
  {
    byte[] buffer = ArrayPool<byte>.Shared.Rent(FileHeader.Length);
    try
    {
      stream.Seek(0, SeekOrigin.Begin);
      await stream.ReadExactlyAsync(buffer.AsMemory(0, FileHeader.Length), cancellationToken).ConfigureAwait(false);
      if (!FileHeader.AsSpan().SequenceEqual(buffer.AsSpan(0, FileHeader.Length)))
      {
        throw new InvalidDataException("Journal header is invalid or corrupted.");
      }

      stream.Seek(FileHeader.Length, SeekOrigin.Begin);
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(buffer);
    }
  }

  private static byte[] CreateFileHeader()
  {
    byte[] header = new byte[16];
    ReadOnlySpan<byte> magic = "XBASEJNL"u8;
    magic.CopyTo(header);
    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), 1);
    return header;
  }

  private static long DefaultTransactionIdProvider()
  {
    return Interlocked.Increment(ref _globalTransactionSeed);
  }

  private void ThrowIfDisposed()
  {
    if (_disposed)
    {
      throw new ObjectDisposedException(nameof(WalJournal));
    }
  }

  private enum WalJournalState
  {
    Idle,
    Active
  }

  private sealed class TransactionBuilder
  {
    private DateTimeOffset? _commitTimestamp;

    public TransactionBuilder(long transactionId, DateTimeOffset beganAt)
    {
      TransactionId = transactionId;
      BeganAt = beganAt;
    }

    public long TransactionId { get; }

    public DateTimeOffset BeganAt { get; }

    public List<JournalMutation> Mutations { get; } = new();

    public bool HasBegun => BeganAt != DateTimeOffset.MinValue;

    public bool IsCommitted => _commitTimestamp.HasValue;

    public bool IsRolledBack { get; private set; }

    public void MarkCommitted(DateTimeOffset commitTimestamp)
    {
      _commitTimestamp = commitTimestamp;
    }

    public void MarkRolledBack(DateTimeOffset rollbackTimestamp)
    {
      IsRolledBack = true;
      _commitTimestamp = null;
    }

    public WalCommittedTransaction BuildCommitted()
    {
      if (!_commitTimestamp.HasValue)
      {
        throw new InvalidOperationException("Transaction did not record a commit timestamp.");
      }

      return new WalCommittedTransaction(TransactionId, BeganAt, _commitTimestamp.Value, Mutations.AsReadOnly());
    }

    public WalInFlightTransaction BuildIncomplete()
    {
      return new WalInFlightTransaction(TransactionId, BeganAt, HasRollbackMarker: IsRolledBack, Mutations.AsReadOnly());
    }
  }

  private enum WalJournalReadStatus
  {
    Entry,
    EndOfFile,
    Truncated,
    ChecksumMismatch
  }

  private readonly struct WalJournalReadResult
  {
    public WalJournalReadResult(WalJournalReadStatus status, JournalEntry? entry)
    {
      Status = status;
      Entry = entry;
    }

    public WalJournalReadStatus Status { get; }

    public JournalEntry? Entry { get; }
  }

  private static class WalJournalCodec
  {
    public static void WritePayload(IBufferWriter<byte> writer, JournalEntry entry)
    {
      WriteByte(writer, (byte)entry.EntryType);
      WriteInt64(writer, entry.TransactionId);
      WriteInt64(writer, entry.Timestamp.UtcTicks);

      if (entry.EntryType == JournalEntryType.Mutation)
      {
        JournalMutation mutation = entry.Mutation!;
        byte[] tableNameBytes = Encoding.UTF8.GetBytes(mutation.TableName);
        WriteInt32(writer, tableNameBytes.Length);
        WriteBytes(writer, tableNameBytes);
        WriteInt32(writer, mutation.RecordNumber);
        WriteByte(writer, (byte)mutation.Kind);

        WriteBinaryPayload(writer, mutation.BeforeImage);
        WriteBinaryPayload(writer, mutation.AfterImage);
      }
    }

    public static async ValueTask<WalJournalReadResult> TryReadEntryAsync(Stream stream, CancellationToken cancellationToken)
    {
      byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(EntryHeaderLength);
      try
      {
        int read = await stream.ReadAsync(headerBuffer.AsMemory(0, EntryHeaderLength), cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
          return new WalJournalReadResult(WalJournalReadStatus.EndOfFile, null);
        }

        if (read < EntryHeaderLength)
        {
          return new WalJournalReadResult(WalJournalReadStatus.Truncated, null);
        }

        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(headerBuffer.AsSpan(0, 4));
        if (payloadLength <= 0)
        {
          return new WalJournalReadResult(WalJournalReadStatus.Truncated, null);
        }

        uint expectedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(headerBuffer.AsSpan(4, 4));
        byte[] payload = ArrayPool<byte>.Shared.Rent(payloadLength);
        try
        {
          try
          {
            await stream.ReadExactlyAsync(payload.AsMemory(0, payloadLength), cancellationToken).ConfigureAwait(false);
          }
          catch (EndOfStreamException)
          {
            return new WalJournalReadResult(WalJournalReadStatus.Truncated, null);
          }

          Memory<byte> payloadMemory = payload.AsMemory(0, payloadLength);
          uint actualChecksum = Crc32.HashToUInt32(payloadMemory.Span);
          if (actualChecksum != expectedChecksum)
          {
            return new WalJournalReadResult(WalJournalReadStatus.ChecksumMismatch, null);
          }

          JournalEntry entry = ReadPayload(payloadMemory.Span);
          return new WalJournalReadResult(WalJournalReadStatus.Entry, entry);
        }
        finally
        {
          ArrayPool<byte>.Shared.Return(payload);
        }
      }
      finally
      {
        ArrayPool<byte>.Shared.Return(headerBuffer);
      }
    }

    private static JournalEntry ReadPayload(ReadOnlySpan<byte> payload)
    {
      if (payload.Length < 1 + sizeof(long) + sizeof(long))
      {
        throw new InvalidDataException("Journal entry payload is too small.");
      }

      int offset = 0;
      JournalEntryType entryType = (JournalEntryType)payload[offset];
      offset += 1;

      long transactionId = BinaryPrimitives.ReadInt64LittleEndian(payload.Slice(offset, sizeof(long)));
      offset += sizeof(long);

      long ticks = BinaryPrimitives.ReadInt64LittleEndian(payload.Slice(offset, sizeof(long)));
      offset += sizeof(long);
      var timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);

      if (entryType != JournalEntryType.Mutation)
      {
        return entryType switch
        {
          JournalEntryType.Begin => JournalEntry.Begin(transactionId, timestamp),
          JournalEntryType.Commit => JournalEntry.Commit(transactionId, timestamp),
          JournalEntryType.Rollback => JournalEntry.Rollback(transactionId, timestamp),
          _ => throw new InvalidDataException($"Unsupported journal entry type: {entryType}")
        };
      }

      int tableNameLength = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
      offset += sizeof(int);
      if (tableNameLength < 0 || offset + tableNameLength > payload.Length)
      {
        throw new InvalidDataException("Invalid table name length in journal entry.");
      }

      string tableName = Encoding.UTF8.GetString(payload.Slice(offset, tableNameLength));
      offset += tableNameLength;

      int recordNumber = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
      offset += sizeof(int);

      JournalMutationKind mutationKind = (JournalMutationKind)payload[offset];
      offset += sizeof(byte);

      ReadOnlyMemory<byte> before = ReadBinaryPayload(payload, ref offset);
      ReadOnlyMemory<byte> after = ReadBinaryPayload(payload, ref offset);

      JournalMutation mutation = new(tableName, recordNumber, mutationKind, before, after);
      return JournalEntry.ForMutation(transactionId, timestamp, mutation);
    }

    private static ReadOnlyMemory<byte> ReadBinaryPayload(ReadOnlySpan<byte> payload, ref int offset)
    {
      int length = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, sizeof(int)));
      offset += sizeof(int);
      if (length < 0 || offset + length > payload.Length)
      {
        throw new InvalidDataException("Invalid binary payload length in journal entry.");
      }

      ReadOnlyMemory<byte> data = payload.Slice(offset, length).ToArray();
      offset += length;
      return data;
    }

    private static void WriteBinaryPayload(IBufferWriter<byte> writer, ReadOnlyMemory<byte> data)
    {
      WriteInt32(writer, data.Length);
      WriteBytes(writer, data.Span);
    }

    private static void WriteInt32(IBufferWriter<byte> writer, int value)
    {
      Span<byte> span = writer.GetSpan(sizeof(int));
      BinaryPrimitives.WriteInt32LittleEndian(span, value);
      writer.Advance(sizeof(int));
    }

    private static void WriteInt64(IBufferWriter<byte> writer, long value)
    {
      Span<byte> span = writer.GetSpan(sizeof(long));
      BinaryPrimitives.WriteInt64LittleEndian(span, value);
      writer.Advance(sizeof(long));
    }

    private static void WriteByte(IBufferWriter<byte> writer, byte value)
    {
      Span<byte> span = writer.GetSpan(sizeof(byte));
      span[0] = value;
      writer.Advance(sizeof(byte));
    }

    private static void WriteBytes(IBufferWriter<byte> writer, ReadOnlySpan<byte> value)
    {
      Span<byte> span = writer.GetSpan(value.Length);
      value.CopyTo(span);
      writer.Advance(value.Length);
    }
  }
}
