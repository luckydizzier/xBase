using System;
using System.Collections.Generic;
using XBase.Demo.Domain.Catalog;
using XBase.Demo.Domain.Services;

namespace XBase.Demo.Infrastructure.Catalog;

/// <summary>
/// Temporary placeholder that returns empty pages until the data engine is wired up.
/// </summary>
public sealed class NullTablePageService : ITablePageService
{
  public Task<TablePage> LoadPageAsync(TableModel table, TablePageRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(table);
    ArgumentNullException.ThrowIfNull(request);

    var emptyRows = Array.Empty<IDictionary<string, object?>>();
    return Task.FromResult(new TablePage(emptyRows, 0, request.PageNumber, request.PageSize));
  }
}
