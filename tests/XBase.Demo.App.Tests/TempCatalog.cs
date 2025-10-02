using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace XBase.Demo.App.Tests;

internal sealed class TempCatalog : IDisposable
{
  private readonly DirectoryInfo _directory;
  private static readonly SampleRecord[] SampleRecords =
  {
    new("Alpha", false),
    new("Omega", false),
    new("Discarded", true),
    new("Beta", false)
  };

  public TempCatalog()
  {
    _directory = Directory.CreateTempSubdirectory("xbase-demo-");
  }

  public string Path => _directory.FullName;

  public void AddTable(string tableName, bool addPlaceholderIndex = true)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

    var tablePath = System.IO.Path.Combine(Path, tableName + ".dbf");
    WriteSampleDbf(tablePath);

    if (addPlaceholderIndex)
    {
      AddIndex(tableName, tableName + ".ntx");
    }
  }

  public void AddIndex(string tableName, string indexName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
    ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

    var indexPath = System.IO.Path.Combine(Path, indexName);
    File.WriteAllBytes(indexPath, Array.Empty<byte>());
  }

  public void Dispose()
  {
    try
    {
      _directory.Delete(true);
    }
    catch
    {
      // best effort cleanup
    }
  }

  private static void WriteSampleDbf(string path)
  {
    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
    using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

    Span<byte> header = stackalloc byte[32];
    header.Clear();
    header[0] = 0x03;
    var today = DateTime.UtcNow;
    header[1] = (byte)Math.Clamp(today.Year - 1900, 0, 255);
    header[2] = (byte)Math.Clamp(today.Month, 1, 12);
    header[3] = (byte)Math.Clamp(today.Day, 1, 31);
    BinaryPrimitives.WriteUInt32LittleEndian(header[4..8], (uint)SampleRecords.Length);
    BinaryPrimitives.WriteUInt16LittleEndian(header[8..10], 65);
    BinaryPrimitives.WriteUInt16LittleEndian(header[10..12], 11);
    stream.Write(header);

    Span<byte> fieldDescriptor = stackalloc byte[32];
    fieldDescriptor.Clear();
    Encoding.ASCII.GetBytes("NAME").CopyTo(fieldDescriptor);
    fieldDescriptor[11] = (byte)'C';
    fieldDescriptor[16] = 10;
    stream.Write(fieldDescriptor);

    stream.WriteByte(0x0D);

    foreach (var record in SampleRecords)
    {
      stream.WriteByte(record.Deleted ? (byte)'*' : (byte)' ');
      var buffer = new byte[10];
      var nameBytes = Encoding.ASCII.GetBytes(record.Name);
      var length = Math.Min(nameBytes.Length, buffer.Length);
      Array.Copy(nameBytes, buffer, length);
      for (var index = length; index < buffer.Length; index++)
      {
        buffer[index] = 0x20;
      }

      stream.Write(buffer);
    }

    stream.WriteByte(0x1A);
  }

  private readonly record struct SampleRecord(string Name, bool Deleted);
}
