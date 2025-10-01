using System.Collections.Generic;

namespace XBase.Demo.Domain.Catalog;

/// <summary>
/// Represents a single page of table data for the browser grid.
/// </summary>
/// <param name="Rows">The row set projected for the current page.</param>
/// <param name="TotalCount">Total row count matching the query.</param>
/// <param name="PageNumber">Zero-based page number.</param>
/// <param name="PageSize">Maximum number of rows per page.</param>
public sealed record TablePage(IReadOnlyList<IDictionary<string, object?>> Rows, long TotalCount, int PageNumber, int PageSize);

/// <summary>
/// Request descriptor for loading a page of table data.
/// </summary>
/// <param name="PageNumber">Zero-based page number.</param>
/// <param name="PageSize">Maximum number of rows per page.</param>
/// <param name="SortExpression">Optional sort expression to apply.</param>
/// <param name="IncludeDeleted">Flag indicating whether deleted rows should be included.</param>
public sealed record TablePageRequest(int PageNumber, int PageSize, string? SortExpression = null, bool IncludeDeleted = false);
