using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Cursors;
using XBase.Core.Table;
using XBase.Demo.Domain.Catalog;
using XBase.Demo.Domain.Services;

namespace XBase.Demo.Infrastructure.Catalog;

/// <summary>
/// Provides paged DBF row materialization for the demo browser experience.
/// </summary>
public sealed class DbfTablePageService : ITablePageService
{
  private readonly DbfTableLoader _tableLoader;
  private readonly DbfCursorFactory _cursorFactory;

  public DbfTablePageService(DbfTableLoader tableLoader, DbfCursorFactory cursorFactory)
  {
    _tableLoader = tableLoader ?? throw new ArgumentNullException(nameof(tableLoader));
    _cursorFactory = cursorFactory ?? throw new ArgumentNullException(nameof(cursorFactory));
  }

  public async Task<TablePage> LoadPageAsync(
    TableModel table,
    TablePageRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(table);
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    if (string.IsNullOrWhiteSpace(table.Path))
    {
      throw new ArgumentException("Table path must be provided.", nameof(table));
    }

    if (request.PageSize <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(request), "Page size must be greater than zero.");
    }

    if (request.PageNumber < 0)
    {
      throw new ArgumentOutOfRangeException(nameof(request), "Page number cannot be negative.");
    }

    DbfTableDescriptor descriptor;
    try
    {
      descriptor = _tableLoader.LoadDbf(table.Path);
    }
    catch (Exception ex) when (IsRecoverableFormatException(ex))
    {
      return CreateEmptyPage(request);
    }

    var columns = DbfColumnFactory.CreateColumns(descriptor);
    var options = new CursorOptions(request.IncludeDeleted, Limit: null, Offset: null);

    var rows = new List<IDictionary<string, object?>>();

    try
    {
      await using var cursor = await _cursorFactory
        .CreateSequentialAsync(descriptor, options, cancellationToken)
        .ConfigureAwait(false);

      while (await cursor.ReadAsync(cancellationToken).ConfigureAwait(false))
      {
        cancellationToken.ThrowIfCancellationRequested();

        var record = cursor.Current;
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in columns)
        {
          row[column.Name] = column.ValueAccessor(record);
        }

        rows.Add(row);
      }
    }
    catch (Exception ex) when (IsRecoverableFormatException(ex))
    {
      return CreateEmptyPage(request);
    }

    rows = ApplySort(rows, request.SortExpression);

    var safePageNumber = request.PageNumber;
    var safePageSize = request.PageSize;
    var totalCount = rows.Count;
    var startIndex = (int)Math.Min((long)safePageNumber * safePageSize, int.MaxValue);

    if (totalCount > 0 && startIndex >= totalCount)
    {
      safePageNumber = (totalCount - 1) / safePageSize;
      startIndex = safePageNumber * safePageSize;
    }

    var pagedRows = rows
      .Skip(startIndex)
      .Take(safePageSize)
      .Select(static row => (IDictionary<string, object?>)row)
      .ToArray();

    return new TablePage(pagedRows, totalCount, safePageNumber, safePageSize);
  }

  private static List<IDictionary<string, object?>> ApplySort(
    List<IDictionary<string, object?>> rows,
    string? sortExpression)
  {
    if (rows.Count == 0 || string.IsNullOrWhiteSpace(sortExpression))
    {
      return rows;
    }

    var (column, descending) = ParseSortExpression(sortExpression);
    if (string.IsNullOrWhiteSpace(column))
    {
      return rows;
    }

    return descending
      ? rows.OrderByDescending(row => ResolveValue(row, column), TableValueComparer.Instance).ToList()
      : rows.OrderBy(row => ResolveValue(row, column), TableValueComparer.Instance).ToList();
  }

  private static object? ResolveValue(IDictionary<string, object?> row, string column)
  {
    return row.TryGetValue(column, out var value)
      ? value
      : null;
  }

  private static (string Column, bool Descending) ParseSortExpression(string expression)
  {
    var parts = expression
      .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (parts.Length == 0)
    {
      return (string.Empty, false);
    }

    var column = parts[0];
    var descending = parts.Length > 1 &&
      parts[1].Equals("DESC", StringComparison.OrdinalIgnoreCase);

    return (column, descending);
  }

  private static TablePage CreateEmptyPage(TablePageRequest request)
  {
    var safePageNumber = Math.Max(0, request.PageNumber);
    return new TablePage(Array.Empty<IDictionary<string, object?>>(), 0, safePageNumber, request.PageSize);
  }

  private static bool IsRecoverableFormatException(Exception exception)
  {
    return exception is EndOfStreamException
        or InvalidDataException
        or FormatException;
  }

  private sealed class TableValueComparer : IComparer<object?>
  {
    public static TableValueComparer Instance { get; } = new();

    public int Compare(object? x, object? y)
    {
      if (ReferenceEquals(x, y))
      {
        return 0;
      }

      if (x is null)
      {
        return -1;
      }

      if (y is null)
      {
        return 1;
      }

      if (x is IComparable comparableX)
      {
        try
        {
          return comparableX.CompareTo(y);
        }
        catch (ArgumentException)
        {
          if (x is IConvertible && y is IConvertible)
          {
            try
            {
              var converted = Convert.ChangeType(y, x.GetType(), CultureInfo.InvariantCulture);
              return comparableX.CompareTo(converted);
            }
            catch (Exception)
            {
              // Ignore and fall back to string comparison
            }
          }
        }
      }

      var left = Convert.ToString(x, CultureInfo.InvariantCulture) ?? string.Empty;
      var right = Convert.ToString(y, CultureInfo.InvariantCulture) ?? string.Empty;
      return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
  }
}
