using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XBase.Abstractions;

namespace XBase.Core.Table;

public sealed record DbfFieldSchema(string Name, char Type, byte Length, byte DecimalCount, bool IsNullable)
{
  public IFieldDescriptor ToDescriptor() => new FieldDescriptor(Name, Type.ToString(), Length, DecimalCount, IsNullable);
}

public sealed record DbfSidecarManifest
{
  public static readonly DbfSidecarManifest Empty = new(null, Array.Empty<string>());

  public DbfSidecarManifest(string? memoFileName, IReadOnlyList<string> indexFileNames)
  {
    MemoFileName = memoFileName;
    IndexFileNames = indexFileNames ?? Array.Empty<string>();
  }

  public string? MemoFileName { get; }

  public IReadOnlyList<string> IndexFileNames { get; }
}

public sealed class DbfTableDescriptor : ITableDescriptor
{
  public DbfTableDescriptor(
    string name,
    byte version,
    DateOnly lastUpdated,
    uint recordCount,
    ushort headerLength,
    ushort recordLength,
    byte languageDriverId,
    IReadOnlyList<DbfFieldSchema> fieldSchemas,
    DbfSidecarManifest sidecars)
  {
    Name = name;
    Version = version;
    LastUpdated = lastUpdated;
    RecordCount = recordCount;
    HeaderLength = headerLength;
    RecordLength = recordLength;
    LanguageDriverId = languageDriverId;
    FieldSchemas = fieldSchemas.ToArray();
    Sidecars = sidecars ?? DbfSidecarManifest.Empty;
    Fields = FieldSchemas.Select(schema => schema.ToDescriptor()).ToArray();
    Indexes = Sidecars.IndexFileNames
      .Select(CreateIndexDescriptor)
      .Cast<IIndexDescriptor>()
      .ToArray();
  }

  public string Name { get; }

  public string? MemoFileName => Sidecars.MemoFileName;

  public IReadOnlyList<IFieldDescriptor> Fields { get; }

  public IReadOnlyList<IIndexDescriptor> Indexes { get; }

  public byte Version { get; }

  public DateOnly LastUpdated { get; }

  public uint RecordCount { get; }

  public ushort HeaderLength { get; }

  public ushort RecordLength { get; }

  public byte LanguageDriverId { get; }

  public IReadOnlyList<DbfFieldSchema> FieldSchemas { get; }

  public DbfSidecarManifest Sidecars { get; }

  private static IndexDescriptor CreateIndexDescriptor(string fileName)
  {
    string name = Path.GetFileNameWithoutExtension(fileName) ?? fileName;
    return new IndexDescriptor(name, string.Empty, false, fileName);
  }
}
