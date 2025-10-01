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

    using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    string tableName = Path.GetFileNameWithoutExtension(filePath) ?? Path.GetFileName(filePath);
    string? directory = Path.GetDirectoryName(filePath);
    return Load(stream, tableName, directory);
  }

  public DbfTableDescriptor Load(Stream stream, string tableName, string? directoryPath = null)
  {
    if (stream is null)
    {
      throw new ArgumentNullException(nameof(stream));
    }

    if (string.IsNullOrWhiteSpace(tableName))
    {
      throw new ArgumentException("Table name must be provided for stream-based loading.", nameof(tableName));
    }

    if (!stream.CanRead)
    {
      throw new ArgumentException("Stream must be readable.", nameof(stream));
    }

    if (stream.CanSeek)
    {
      stream.Seek(0, SeekOrigin.Begin);
    }

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

    int remainingHeaderLength = Math.Max(0, headerLength - header.Length);
    byte[] headerTail = new byte[remainingHeaderLength];
    if (remainingHeaderLength > 0)
    {
      stream.ReadExactly(headerTail);
    }

    IReadOnlyList<DbfFieldSchema> fields = ParseFieldDescriptors(headerTail);

    DbfSidecarManifest sidecars = directoryPath is not null && Directory.Exists(directoryPath)
      ? DetectSidecars(directoryPath, tableName, version)
      : DbfSidecarManifest.Empty;

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

  private static IReadOnlyList<DbfFieldSchema> ParseFieldDescriptors(ReadOnlySpan<byte> buffer)
  {
    List<DbfFieldSchema> fields = new();
    int offset = 0;
    while (offset < buffer.Length)
    {
      byte marker = buffer[offset];
      if (marker == 0x0D)
      {
        break;
      }

      if (marker == 0x00)
      {
        offset++;
        continue;
      }

      if (offset + 32 > buffer.Length)
      {
        throw new InvalidDataException("Unexpected end of field descriptor array.");
      }

      fields.Add(ReadFieldDescriptor(buffer[offset..(offset + 32)]));
      offset += 32;
    }

    return fields;
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

  private static DbfSidecarManifest DetectSidecars(string directoryPath, string tableName, byte version)
  {
    var lookup = Directory
      .EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
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
