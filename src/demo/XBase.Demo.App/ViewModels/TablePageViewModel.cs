using System;
using System.Collections.Generic;
using ReactiveUI;
using XBase.Demo.Domain.Catalog;

namespace XBase.Demo.App.ViewModels;

/// <summary>
/// Represents the active table page surfaced in the browser preview.
/// </summary>
public sealed class TablePageViewModel : ReactiveObject
{
  private IReadOnlyList<IDictionary<string, object?>> _rows = Array.Empty<IDictionary<string, object?>>();
  private long _totalCount;
  private int _pageNumber;
  private int _pageSize = 25;

  public IReadOnlyList<IDictionary<string, object?>> Rows
  {
    get => _rows;
    private set => this.RaiseAndSetIfChanged(ref _rows, value);
  }

  public long TotalCount
  {
    get => _totalCount;
    private set => this.RaiseAndSetIfChanged(ref _totalCount, value);
  }

  public int PageNumber
  {
    get => _pageNumber;
    private set => this.RaiseAndSetIfChanged(ref _pageNumber, value);
  }

  public int PageSize
  {
    get => _pageSize;
    private set => this.RaiseAndSetIfChanged(ref _pageSize, value);
  }

  public string Summary
  {
    get
    {
      var pageIndex = PageNumber + 1;
      var totalPages = PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
      return $"Page {pageIndex} of {totalPages} · Showing {Rows.Count} rows (Total {TotalCount}).";
    }
  }

  public void Apply(TablePage page)
  {
    ArgumentNullException.ThrowIfNull(page);

    Rows = page.Rows;
    TotalCount = page.TotalCount;
    PageNumber = page.PageNumber;
    PageSize = page.PageSize;

    this.RaisePropertyChanged(nameof(Summary));
  }
}
