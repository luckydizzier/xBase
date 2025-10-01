using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XBase.TestSupport;

public static class DbfTestBuilder
{
  public static string CreateTable(string directory, string tableName, params (bool deleted, string code)[] records)
  {
    if (string.IsNullOrWhiteSpace(directory))
    {
      throw new ArgumentException("Directory must be provided.", nameof(directory));
    }

    if (string.IsNullOrWhiteSpace(tableName))
    {
      throw new ArgumentException("Table name must be provided.", nameof(tableName));
    }

    Directory.CreateDirectory(directory);
    string path = Path.Combine(directory, tableName + ".dbf");
    WriteDbf(path, records);
    return path;
  }

  public static string CreateIndex(string directory, string fileName, string contents)
  {
    if (string.IsNullOrWhiteSpace(directory))
    {
      throw new ArgumentException("Directory must be provided.", nameof(directory));
    }

    if (string.IsNullOrWhiteSpace(fileName))
    {
      throw new ArgumentException("File name must be provided.", nameof(fileName));
    }

    Directory.CreateDirectory(directory);
    string path = Path.Combine(directory, fileName);
    File.WriteAllText(path, contents ?? string.Empty, Encoding.UTF8);
    return path;
  }

  private static void WriteDbf(string path, IReadOnlyList<(bool deleted, string code)> records)
  {
    byte version = 0x03;
    DateTime now = DateTime.UtcNow;
    ushort recordLength = 5; // delete flag + 4 byte CODE field
    ushort headerLength = 32 + 32 + 1;
    uint recordCount = records is null ? 0u : (uint)records.Count;

    byte[] header = new byte[headerLength];
    header[0] = version;
    header[1] = (byte)Math.Clamp(now.Year - 1900, 0, 255);
    header[2] = (byte)now.Month;
    header[3] = (byte)now.Day;
    BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), recordCount);
    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(8, 2), headerLength);
    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(10, 2), recordLength);
    header[29] = 0x00;

    Span<byte> descriptor = header.AsSpan(32, 32);
    Encoding ascii = Encoding.ASCII;
    ascii.GetBytes("CODE".PadRight(11, '\0'), descriptor[..11]);
    descriptor[11] = (byte)'C';
    descriptor[16] = 4;
    descriptor[17] = 0;
    descriptor[18] = 0;
    header[64] = 0x0D;

    string? directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
    stream.Write(header);

    if (records is not null)
    {
      foreach ((bool deleted, string code) in records)
      {
        byte[] buffer = new byte[recordLength];
        buffer[0] = deleted ? (byte)'*' : (byte)' ';
        string normalized = (code ?? string.Empty).PadRight(4);
        ascii.GetBytes(normalized.AsSpan(0, 4), buffer.AsSpan(1));
        stream.Write(buffer);
      }
    }

    stream.WriteByte(0x1A);
    stream.Flush(true);
  }
}
