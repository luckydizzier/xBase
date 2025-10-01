using System;
using System.Collections.Generic;
using System.Linq;
using XBase.Demo.Domain.Catalog;

namespace XBase.Demo.App.ViewModels;

/// <summary>
/// Represents a single table entry displayed in the catalog browser.
/// </summary>
public sealed class TableListItemViewModel
{
  public TableListItemViewModel(TableModel model)
  {
    Model = model ?? throw new ArgumentNullException(nameof(model));
    Name = model.Name;
    Path = model.Path;
    Indexes = model.Indexes
        .OrderBy(index => index.Order)
        .ThenBy(index => index.Name, StringComparer.OrdinalIgnoreCase)
        .Select(index => new IndexListItemViewModel(index))
        .ToArray();
  }

  public TableModel Model { get; }

  public string Name { get; }

  public string Path { get; }

  public IReadOnlyList<IndexListItemViewModel> Indexes { get; }
}
