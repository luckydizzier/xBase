using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Table;

namespace XBase.Data.Providers;

public sealed class SqlTableResolver : ITableResolver
{

  private readonly Func<XBaseConnectionOptions> _optionsAccessor;
  private readonly DbfTableLoader _tableLoader;

  public SqlTableResolver(Func<XBaseConnectionOptions> optionsAccessor, DbfTableLoader? tableLoader = null)
  {
    _optionsAccessor = optionsAccessor ?? throw new ArgumentNullException(nameof(optionsAccessor));
    _tableLoader = tableLoader ?? new DbfTableLoader();
  }

  public ValueTask<TableResolveResult?> ResolveAsync(string commandText, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (!TryParse(commandText, out SelectStatement? statement))
    {
      return ValueTask.FromResult<TableResolveResult?>(null);
    }

    XBaseConnectionOptions options = _optionsAccessor();
    if (string.IsNullOrWhiteSpace(options.RootPath))
    {
      throw new InvalidOperationException("Connection string must provide a 'path' for query execution.");
    }

    string tablePath = ResolveTablePath(options.RootPath, statement.TableName);
    DbfTableDescriptor descriptor = _tableLoader.LoadDbf(tablePath);
    IReadOnlyList<TableColumn> columns = DbfColumnFactory.CreateColumns(descriptor, statement.Columns);
    bool includeDeleted = options.DeletedRecordVisibility == DeletedRecordVisibility.Show;

    var cursorOptions = new CursorOptions(includeDeleted, Limit: null, Offset: null);
    var result = new TableResolveResult(descriptor, columns, cursorOptions);
    return ValueTask.FromResult<TableResolveResult?>(result);
  }

  private static string ResolveTablePath(string rootPath, string tableName)
  {
    string baseDirectory = Path.GetFullPath(rootPath);
    if (!Directory.Exists(baseDirectory))
    {
      throw new DirectoryNotFoundException($"Directory '{baseDirectory}' was not found.");
    }

    string normalized = TrimIdentifier(tableName);
    if (string.IsNullOrWhiteSpace(normalized))
    {
      throw new InvalidOperationException("Table name must be specified.");
    }

    string candidate = Path.Combine(baseDirectory, normalized);
    if (!Path.HasExtension(candidate))
    {
      candidate = Path.Combine(baseDirectory, normalized + ".dbf");
    }

    if (File.Exists(candidate))
    {
      return candidate;
    }

    string lookupName = Path.GetFileNameWithoutExtension(normalized);
    foreach (string file in Directory.EnumerateFiles(baseDirectory, "*.dbf", SearchOption.TopDirectoryOnly))
    {
      string name = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
      if (string.Equals(name, lookupName, StringComparison.OrdinalIgnoreCase))
      {
        return file;
      }
    }

    throw new FileNotFoundException($"Table '{tableName}' was not found in '{baseDirectory}'.");
  }

  private static bool TryParse(string commandText, out SelectStatement statement)
  {
    statement = default!;
    if (string.IsNullOrWhiteSpace(commandText))
    {
      return false;
    }

    string trimmed = commandText.Trim().TrimEnd(';');
    if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    int fromIndex = IndexOfKeyword(trimmed, "FROM");
    if (fromIndex < 0)
    {
      return false;
    }

    string columnsSegment = trimmed.Substring("SELECT".Length, fromIndex - "SELECT".Length).Trim();
    if (columnsSegment.Length == 0)
    {
      columnsSegment = "*";
    }

    string afterFrom = trimmed[(fromIndex + 4)..].Trim();
    if (afterFrom.Length == 0)
    {
      return false;
    }

    int whereIndex = IndexOfKeyword(afterFrom, "WHERE");
    string tableSegment = whereIndex >= 0 ? afterFrom[..whereIndex].Trim() : afterFrom;
    if (tableSegment.Length == 0)
    {
      return false;
    }

    char[]? separators = null;
    string[] tableTokens = tableSegment.Split(separators, StringSplitOptions.RemoveEmptyEntries);
    if (tableTokens.Length == 0)
    {
      return false;
    }

    string tableName = TrimIdentifier(tableTokens[0]);
    IReadOnlyList<string>? columns = ParseColumns(columnsSegment);

    statement = new SelectStatement(tableName, columns);
    return true;
  }

  private static IReadOnlyList<string>? ParseColumns(string segment)
  {
    if (segment == "*")
    {
      return null;
    }

    string[] parts = segment.Split(',', StringSplitOptions.RemoveEmptyEntries);
    List<string> columns = new(parts.Length);
    foreach (string part in parts)
    {
      string normalized = NormalizeColumnName(part);
      if (normalized.Length > 0)
      {
        columns.Add(normalized);
      }
    }

    return columns;
  }

  private static string NormalizeColumnName(string text)
  {
    string trimmed = text.Trim();
    int asIndex = IndexOfKeyword(trimmed, "AS");
    if (asIndex >= 0)
    {
      trimmed = trimmed[..asIndex].TrimEnd();
    }

    int spaceIndex = IndexOfWhitespace(trimmed);
    if (spaceIndex >= 0)
    {
      trimmed = trimmed[..spaceIndex];
    }

    trimmed = TrimIdentifier(trimmed);
    int dotIndex = trimmed.LastIndexOf('.');
    if (dotIndex >= 0)
    {
      trimmed = trimmed[(dotIndex + 1)..];
    }

    return TrimIdentifier(trimmed);
  }

  private static string TrimIdentifier(string text)
  {
    string trimmed = text.Trim();
    if (trimmed.Length == 0)
    {
      return trimmed;
    }

    if ((trimmed[0] == '[' && trimmed[^1] == ']') ||
      (trimmed[0] == '"' && trimmed[^1] == '"') ||
      (trimmed[0] == '\'' && trimmed[^1] == '\'') ||
      (trimmed[0] == '`' && trimmed[^1] == '`'))
    {
      trimmed = trimmed[1..^1];
    }

    return trimmed;
  }

  private static int IndexOfWhitespace(string text)
  {
    for (int i = 0; i < text.Length; i++)
    {
      if (char.IsWhiteSpace(text[i]))
      {
        return i;
      }
    }

    return -1;
  }

  private static int IndexOfKeyword(string text, string keyword)
  {
    int index = 0;
    while (index <= text.Length - keyword.Length)
    {
      int found = text.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase);
      if (found < 0)
      {
        return -1;
      }

      bool startBoundary = found == 0 || !char.IsLetterOrDigit(text[found - 1]);
      int endIndex = found + keyword.Length;
      bool endBoundary = endIndex >= text.Length || !char.IsLetterOrDigit(text[endIndex]);
      if (startBoundary && endBoundary)
      {
        return found;
      }

      index = endIndex;
    }

    return -1;
  }

  private sealed record SelectStatement(string TableName, IReadOnlyList<string>? Columns);
}
