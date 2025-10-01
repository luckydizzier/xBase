using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Data.Providers;

public sealed class XBaseDataReader : DbDataReader
{
  private readonly ICursor? _cursor;
  private readonly IReadOnlyList<TableColumn> _columns;
  private readonly Dictionary<string, int> _ordinals;
  private readonly DbConnection? _connectionToClose;
  private readonly bool _ownsCursor;

  private bool _isClosed;
  private bool _hasRowsInitialized;
  private bool _hasRows;
  private bool _hasPendingRecord;
  private ReadOnlySequence<byte> _pendingRecord;
  private ReadOnlySequence<byte> _currentRecord;
  private bool _hasCurrent;

  private XBaseDataReader(IReadOnlyList<TableColumn> columns, ICursor? cursor, bool ownsCursor, DbConnection? connectionToClose)
  {
    _columns = columns ?? throw new ArgumentNullException(nameof(columns));
    _cursor = cursor;
    _ownsCursor = ownsCursor;
    _connectionToClose = connectionToClose;
    _ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < _columns.Count; i++)
    {
      _ordinals[_columns[i].Name] = i;
    }
  }

  public XBaseDataReader(ICursor cursor, IReadOnlyList<TableColumn> columns, DbConnection? connectionToClose = null)
    : this(columns, cursor ?? throw new ArgumentNullException(nameof(cursor)), ownsCursor: true, connectionToClose)
  {
  }

  public static XBaseDataReader CreateEmpty()
  {
    return new XBaseDataReader(Array.Empty<TableColumn>(), cursor: null, ownsCursor: false, connectionToClose: null)
    {
      _hasRowsInitialized = true,
      _hasRows = false
    };
  }

  public override int FieldCount => _columns.Count;

  public override bool HasRows
  {
    get
    {
      EnsureNotClosed();
      if (_cursor is null)
      {
        return false;
      }

      EnsureInitialized();
      return _hasRows;
    }
  }

  public override bool IsClosed => _isClosed;

  public override int RecordsAffected => 0;

  public override int Depth => 0;

  public override object this[int ordinal] => GetValue(ordinal);

  public override object this[string name] => GetValue(GetOrdinal(name));

  public override bool Read()
  {
    return ReadInternalAsync(CancellationToken.None).GetAwaiter().GetResult();
  }

  public override Task<bool> ReadAsync(CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ReadInternalAsync(cancellationToken).AsTask();
  }

  public override bool NextResult()
  {
    return false;
  }

  public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(false);
  }

  public override string GetName(int ordinal)
  {
    TableColumn column = GetColumn(ordinal);
    return column.Name;
  }

  public override string GetDataTypeName(int ordinal)
  {
    TableColumn column = GetColumn(ordinal);
    return column.ClrType.Name;
  }

  public override Type GetFieldType(int ordinal)
  {
    TableColumn column = GetColumn(ordinal);
    return column.ClrType;
  }

  public override int GetOrdinal(string name)
  {
    if (!_ordinals.TryGetValue(name, out int ordinal))
    {
      throw new IndexOutOfRangeException($"Column '{name}' was not found.");
    }

    return ordinal;
  }

  public override object GetValue(int ordinal)
  {
    object? value = GetRawValue(ordinal);
    return value ?? DBNull.Value;
  }

  public override int GetValues(object[] values)
  {
    if (values is null)
    {
      throw new ArgumentNullException(nameof(values));
    }

    int count = Math.Min(values.Length, FieldCount);
    for (int i = 0; i < count; i++)
    {
      values[i] = GetValue(i);
    }

    return count;
  }

  public override bool IsDBNull(int ordinal)
  {
    return GetRawValue(ordinal) is null;
  }

  public override bool GetBoolean(int ordinal)
  {
    return Convert.ToBoolean(GetNonNullValue(ordinal), CultureInfo.InvariantCulture);
  }

  public override byte GetByte(int ordinal)
  {
    return Convert.ToByte(GetNonNullValue(ordinal), CultureInfo.InvariantCulture);
  }

  public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
  {
    byte[] data = GetBuffer(ordinal);
    return CopyBuffer(data, dataOffset, buffer, bufferOffset, length);
  }

  public override char GetChar(int ordinal)
  {
    return Convert.ToChar(GetNonNullValue(ordinal), CultureInfo.InvariantCulture);
  }

  public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
  {
    char[] data = GetCharBuffer(ordinal);
    if (buffer is null)
    {
      return data.Length;
    }

    int available = Math.Max(0, data.Length - (int)dataOffset);
    int count = Math.Min(length, available);
    if (count > 0)
    {
      Array.Copy(data, dataOffset, buffer, bufferOffset, count);
    }

    return count;
  }

  public override Guid GetGuid(int ordinal)
  {
    object value = GetNonNullValue(ordinal);
    return value switch
    {
      Guid guid => guid,
      string text => Guid.Parse(text),
      byte[] bytes => new Guid(bytes),
      ReadOnlyMemory<byte> memory => new Guid(memory.ToArray()),
      _ => new Guid(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
    };
  }

  public override short GetInt16(int ordinal)
  {
    return Convert.ToInt16(GetNonNullValue(ordinal), CultureInfo.InvariantCulture);
  }

  public override int GetInt32(int ordinal)
  {
    return Convert.ToInt32(GetNonNullValue(ordinal), CultureInfo.InvariantCulture);
  }

  public override long GetInt64(int ordinal)
  {
    return Convert.ToInt64(GetNonNullValue(ordinal), CultureInfo.InvariantCulture);
  }

  public override float GetFloat(int ordinal)
  {
    return Convert.ToSingle(GetNonNullValue(ordinal), CultureInfo.InvariantCulture);
  }

  public override double GetDouble(int ordinal)
  {
    return Convert.ToDouble(GetNonNullValue(ordinal), CultureInfo.InvariantCulture);
  }

  public override string GetString(int ordinal)
  {
    object? value = GetRawValue(ordinal);
    return value switch
    {
      null => throw new InvalidOperationException("Value is null."),
      string text => text,
      ReadOnlyMemory<char> memory => new string(memory.Span),
      ReadOnlyMemory<byte> bytes => System.Text.Encoding.UTF8.GetString(bytes.Span),
      byte[] buffer => System.Text.Encoding.UTF8.GetString(buffer),
      _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };
  }

  public override decimal GetDecimal(int ordinal)
  {
    return Convert.ToDecimal(GetNonNullValue(ordinal), CultureInfo.InvariantCulture);
  }

  public override DateTime GetDateTime(int ordinal)
  {
    return Convert.ToDateTime(GetNonNullValue(ordinal), CultureInfo.InvariantCulture);
  }

  public override IEnumerator<object> GetEnumerator()
  {
    for (int i = 0; i < FieldCount; i++)
    {
      yield return GetValue(i);
    }
  }

  public override void Close()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected override void Dispose(bool disposing)
  {
    if (_isClosed)
    {
      return;
    }

    if (disposing)
    {
      if (_ownsCursor && _cursor is not null)
      {
        _cursor.DisposeAsync().AsTask().GetAwaiter().GetResult();
      }

      _connectionToClose?.Close();
    }

    _isClosed = true;
  }

  private void EnsureNotClosed()
  {
    if (_isClosed)
    {
      throw new InvalidOperationException("The data reader is closed.");
    }
  }

  private void EnsureInitialized()
  {
    if (_hasRowsInitialized || _cursor is null)
    {
      _hasRowsInitialized = true;
      return;
    }

    InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
  }

  private ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
  {
    if (_hasRowsInitialized || _cursor is null)
    {
      _hasRowsInitialized = true;
      return ValueTask.CompletedTask;
    }

    return InitializeAsync(cancellationToken);
  }

  private async ValueTask InitializeAsync(CancellationToken cancellationToken)
  {
    bool has = await _cursor!.ReadAsync(cancellationToken).ConfigureAwait(false);
    _hasRowsInitialized = true;
    if (has)
    {
      _hasRows = true;
      _pendingRecord = _cursor.Current;
      _hasPendingRecord = true;
    }
    else
    {
      _hasRows = false;
    }
  }

  private async ValueTask<bool> ReadInternalAsync(CancellationToken cancellationToken)
  {
    EnsureNotClosed();
    await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

    if (_cursor is null)
    {
      _hasCurrent = false;
      return false;
    }

    if (_hasPendingRecord)
    {
      _currentRecord = _pendingRecord;
      _hasPendingRecord = false;
      _hasCurrent = true;
      return true;
    }

    bool has = await _cursor.ReadAsync(cancellationToken).ConfigureAwait(false);
    if (!has)
    {
      _hasCurrent = false;
      return false;
    }

    _currentRecord = _cursor.Current;
    _hasCurrent = true;
    return true;
  }

  private TableColumn GetColumn(int ordinal)
  {
    if ((uint)ordinal >= (uint)FieldCount)
    {
      throw new IndexOutOfRangeException();
    }

    return _columns[ordinal];
  }

  private object? GetRawValue(int ordinal)
  {
    EnsureNotClosed();
    if (!_hasCurrent)
    {
      throw new InvalidOperationException("Read must be called before accessing data.");
    }

    TableColumn column = GetColumn(ordinal);
    return column.ValueAccessor(_currentRecord);
  }

  private object GetNonNullValue(int ordinal)
  {
    object? value = GetRawValue(ordinal);
    if (value is null)
    {
      throw new InvalidOperationException("Value is null.");
    }

    return value;
  }

  private byte[] GetBuffer(int ordinal)
  {
    object value = GetNonNullValue(ordinal);
    return value switch
    {
      byte[] bytes => bytes,
      ReadOnlyMemory<byte> memory => memory.ToArray(),
      ReadOnlySequence<byte> sequence => sequence.ToArray(),
      _ => throw new InvalidCastException($"Column '{GetName(ordinal)}' does not contain binary data.")
    };
  }

  private char[] GetCharBuffer(int ordinal)
  {
    object value = GetNonNullValue(ordinal);
    return value switch
    {
      string text => text.ToCharArray(),
      char[] chars => chars,
      ReadOnlyMemory<char> memory => memory.ToArray(),
      _ => GetString(ordinal).ToCharArray()
    };
  }

  private static long CopyBuffer(byte[] data, long dataOffset, byte[]? buffer, int bufferOffset, int length)
  {
    if (dataOffset < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(dataOffset));
    }

    if (buffer is null)
    {
      return data.Length;
    }

    int available = Math.Max(0, data.Length - (int)dataOffset);
    int count = Math.Min(length, available);
    if (count > 0)
    {
      Array.Copy(data, dataOffset, buffer, bufferOffset, count);
    }

    return count;
  }
}
