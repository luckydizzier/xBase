using System;
using XBase.Demo.Domain.Catalog;

namespace XBase.Demo.App.ViewModels;

/// <summary>
/// Represents an index entry for the selected table.
/// </summary>
public sealed class IndexListItemViewModel
{
  public IndexListItemViewModel(IndexModel model)
  {
    Model = model ?? throw new ArgumentNullException(nameof(model));
    Name = model.Name;
    Expression = model.Expression;
    Order = model.Order;
  }

  public IndexModel Model { get; }

  public string Name { get; }

  public string Expression { get; }

  public int Order { get; }
}
