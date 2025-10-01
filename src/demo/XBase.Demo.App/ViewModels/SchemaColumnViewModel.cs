using System;
using XBase.Demo.Domain.Schema;

namespace XBase.Demo.App.ViewModels;

/// <summary>
/// Represents a column definition used by the schema designer.
/// </summary>
public sealed class SchemaColumnViewModel
{
  public SchemaColumnViewModel(string name, string dataType, bool allowNulls = true, int? length = null, int? scale = null)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      throw new ArgumentException("Column name is required.", nameof(name));
    }

    if (string.IsNullOrWhiteSpace(dataType))
    {
      throw new ArgumentException("Column data type is required.", nameof(dataType));
    }

    Name = name;
    DataType = dataType;
    AllowNulls = allowNulls;
    Length = length;
    Scale = scale;
  }

  public string Name { get; }

  public string DataType { get; }

  public bool AllowNulls { get; }

  public int? Length { get; }

  public int? Scale { get; }

  public TableColumnDefinition ToDefinition()
      => new(Name, DataType, AllowNulls, Length, Scale);
}
