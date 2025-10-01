using XBase.Abstractions;

namespace XBase.Core.Table;

public sealed class FieldDescriptor : IFieldDescriptor
{
  public FieldDescriptor(string name, string type, int length, int decimalCount, bool isNullable)
  {
    Name = name;
    Type = type;
    Length = length;
    DecimalCount = decimalCount;
    IsNullable = isNullable;
  }

  public string Name { get; }

  public string Type { get; }

  public int Length { get; }

  public int DecimalCount { get; }

  public bool IsNullable { get; }
}
