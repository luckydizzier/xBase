using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace XBase.Data.Providers;

public sealed class XBaseDataReader : DbDataReader
{
  private readonly IReadOnlyList<ReadOnlySequence<byte>> _records;
  private int _position = -1;

  public XBaseDataReader(IReadOnlyList<ReadOnlySequence<byte>> records)
  {
    _records = records;
  }

  public override int FieldCount => 0;

  public override bool HasRows => _records.Count > 0;

  public override bool IsClosed => false;

  public override int RecordsAffected => 0;

  public override int Depth => 0;

  public override bool Read()
  {
    _position++;
    return _position < _records.Count;
  }

  public override Task<bool> ReadAsync(CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(Read());
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

  public override object GetValue(int ordinal)
  {
    throw new IndexOutOfRangeException();
  }

  public override bool IsDBNull(int ordinal)
  {
    return true;
  }

  public override int GetOrdinal(string name)
  {
    throw new IndexOutOfRangeException();
  }

  public override string GetName(int ordinal)
  {
    throw new IndexOutOfRangeException();
  }

  public override string GetDataTypeName(int ordinal)
  {
    throw new IndexOutOfRangeException();
  }

  public override Type GetFieldType(int ordinal)
  {
    throw new IndexOutOfRangeException();
  }

  public override object this[int ordinal] => GetValue(ordinal);

  public override object this[string name] => GetValue(GetOrdinal(name));

  public override int GetValues(object[] values)
  {
    return 0;
  }

  public override bool GetBoolean(int ordinal) => false;

  public override byte GetByte(int ordinal) => 0;

  public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;

  public override char GetChar(int ordinal) => '\0';

  public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;

  public override Guid GetGuid(int ordinal) => Guid.Empty;

  public override short GetInt16(int ordinal) => 0;

  public override int GetInt32(int ordinal) => 0;

  public override long GetInt64(int ordinal) => 0;

  public override float GetFloat(int ordinal) => 0f;

  public override double GetDouble(int ordinal) => 0d;

  public override string GetString(int ordinal) => string.Empty;

  public override decimal GetDecimal(int ordinal) => 0m;

  public override DateTime GetDateTime(int ordinal) => DateTime.MinValue;

  public override IEnumerator<object> GetEnumerator()
  {
    yield break;
  }

  public override void Close()
  {
  }
}
