using XBase.Abstractions;

namespace XBase.Core.Table;

public sealed class IndexDescriptor : IIndexDescriptor
{
  public IndexDescriptor(string name, string expression, bool isDescending)
  {
    Name = name;
    Expression = expression;
    IsDescending = isDescending;
  }

  public string Name { get; }

  public string Expression { get; }

  public bool IsDescending { get; }
}
