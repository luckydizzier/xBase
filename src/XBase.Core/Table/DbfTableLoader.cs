using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace XBase.Core.Table;

public sealed class DbfTableLoader
{
  private static readonly string[] MemoExtensions = [".dbt", ".fpt"];
  private static readonly string[] IndexExtensions = [".ndx", ".ntx", ".mdx"];

  public DbfTableDescriptor Load(string filePath)
  {
    if (filePath is null)
    {
      throw new ArgumentNullException(nameof(filePath));
    }

    if (!File.Exists(filePath))
    {
      throw new FileNotFoundException("DBF file was not found.", filePath);
    }

    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    Span<byte> header = stackalloc byte[32];
    stream.ReadExactly(header);

    byte version = header[0];
    DateOnly lastUpdated = CreateLastUpdatedDate(header[1], header[2], header[3]);
    uint recordCount = BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);
    ushort headerLength = BinaryPrimitives.ReadUInt16LittleEndian(header[8..10]);
    ushort recordLength = BinaryPrimitives.ReadUInt16LittleEndian(header[10..12]);
    byte languageDriverId = header[29];

    if (headerLength < 33)
    {
      throw new InvalidDataException($"Header length {headerLength} is too small to contain field descriptors.");
    }

    int remainingHeaderLength = headerLength - header.Length;
    byte[] headerTail = new byte[remainingHeaderLength];
    stream.ReadExactly(headerTail);

    List<DbfFieldSchema> fields = new();
    int offset = 0;
    while (offset < headerTail.Length)
    {
      byte marker = headerTail[offset];
      if (marker == 0x0D)
      {
        offset++;
        break;
      }

      if (offset + 32 > headerTail.Length)
      {
        throw new InvalidDataException("Unexpected end of field descriptor array.");
      }

      fields.Add(ReadFieldDescriptor(headerTail.AsSpan(offset, 32)));
      offset += 32;
    }

    string tableName = Path.GetFileNameWithoutExtension(filePath) ?? Path.GetFileName(filePath);
    string directory = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
    var sidecars = DetectSidecars(directory, tableName, version);

    return new DbfTableDescriptor(
      tableName,
      version,
      lastUpdated,
      recordCount,
      headerLength,
      recordLength,
      languageDriverId,
      fields,
      sidecars);
  }

  private static DbfFieldSchema ReadFieldDescriptor(ReadOnlySpan<byte> descriptor)
  {
    string rawName = Encoding.ASCII.GetString(descriptor[..11]);
    string name = rawName.TrimEnd('\0', ' ');
    char type = (char)descriptor[11];
    byte length = descriptor[16];
    byte decimalCount = descriptor[17];
    bool isNullable = (descriptor[18] & 0x02) != 0;
    return new DbfFieldSchema(name, type, length, decimalCount, isNullable);
  }

  private static DateOnly CreateLastUpdatedDate(byte year, byte month, byte day)
  {
    int resolvedYear = 1900 + year;
    int resolvedMonth = Math.Clamp(month, (byte)1, (byte)12);
    int daysInMonth = DateTime.DaysInMonth(resolvedYear, resolvedMonth);
    int resolvedDay = Math.Clamp(day, (byte)1, (byte)daysInMonth);
    return new DateOnly(resolvedYear, resolvedMonth, resolvedDay);
  }

  private static DbfSidecarManifest DetectSidecars(string directory, string tableName, byte version)
  {
    var lookup = Directory
      .EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
      .Select(Path.GetFileName)
      .Where(name => name is not null)
      .ToDictionary(name => name!, StringComparer.OrdinalIgnoreCase);

    string? memoFileName = null;
    foreach (string extension in MemoExtensions)
    {
      string candidate = tableName + extension;
      if (lookup.TryGetValue(candidate, out string? actualName) && actualName is not null)
      {
        memoFileName = actualName;
        break;
      }
    }

    if (memoFileName is null && (version & 0x80) != 0)
    {
      string candidate = tableName + ".dbt";
      if (lookup.TryGetValue(candidate, out string? actualName) && actualName is not null)
      {
        memoFileName = actualName;
      }
    }

    List<string> indexFileNames = new();
    foreach (string extension in IndexExtensions)
    {
      string candidate = tableName + extension;
      if (lookup.TryGetValue(candidate, out string? actualName) && actualName is not null)
      {
        if (!indexFileNames.Contains(actualName, StringComparer.OrdinalIgnoreCase))
        {
          indexFileNames.Add(actualName);
        }
      }
    }

    return new DbfSidecarManifest(memoFileName, indexFileNames.ToArray());
  }
}
