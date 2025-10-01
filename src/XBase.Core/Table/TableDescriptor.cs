using System.Collections.Generic;
using XBase.Abstractions;

namespace XBase.Core.Table;

public sealed class TableDescriptor : ITableDescriptor
{
  public TableDescriptor(
    string name,
    string? memoFileName,
    IReadOnlyList<IFieldDescriptor> fields,
    IReadOnlyList<IIndexDescriptor> indexes,
    SchemaVersion schemaVersion)
  {
    Name = name;
    MemoFileName = memoFileName;
    Fields = fields;
    Indexes = indexes;
    SchemaVersion = schemaVersion;
  }

  public string Name { get; }

  public string? MemoFileName { get; }

  public IReadOnlyList<IFieldDescriptor> Fields { get; }

  public IReadOnlyList<IIndexDescriptor> Indexes { get; }

  public SchemaVersion SchemaVersion { get; }
}
