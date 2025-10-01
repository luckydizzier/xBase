using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Table;

namespace XBase.Data.Providers;

public sealed class SqlTableResolver : ITableResolver
{
  private static readonly Encoding Ascii = Encoding.ASCII;

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
    IReadOnlyList<TableColumn> columns = BuildColumns(descriptor, statement.Columns);
    bool includeDeleted = options.DeletedRecordVisibility == DeletedRecordVisibility.Show;

    var cursorOptions = new CursorOptions(includeDeleted, Limit: null, Offset: null);
    var result = new TableResolveResult(descriptor, columns, cursorOptions);
    return ValueTask.FromResult<TableResolveResult?>(result);
  }

  private static IReadOnlyList<TableColumn> BuildColumns(
    DbfTableDescriptor descriptor,
    IReadOnlyList<string>? requestedColumns)
  {
    Dictionary<string, FieldLayout> layout = BuildLayout(descriptor);
    Encoding encoding = DbfEncodingRegistry.Resolve(descriptor.LanguageDriverId);

    if (requestedColumns is null || requestedColumns.Count == 0)
    {
      return descriptor.FieldSchemas
        .Select(schema => CreateColumn(layout[schema.Name], encoding))
        .ToArray();
    }

    List<TableColumn> columns = new(requestedColumns.Count);
    foreach (string columnName in requestedColumns)
    {
      if (!layout.TryGetValue(columnName, out FieldLayout layoutEntry))
      {
        throw new InvalidOperationException(
          $"Column '{columnName}' was not found in table '{descriptor.Name}'.");
      }

      columns.Add(CreateColumn(layoutEntry, encoding));
    }

    return columns;
  }

  private static TableColumn CreateColumn(FieldLayout layout, Encoding encoding)
  {
    Type clrType = ResolveClrType(layout.Schema);

    return new TableColumn(
      layout.Schema.Name,
      clrType,
      record =>
      {
        ReadOnlySpan<byte> buffer = record.IsSingleSegment
          ? record.FirstSpan
          : record.ToArray();

        int fieldOffset = layout.Offset;
        int fieldLength = layout.Schema.Length;
        if (buffer.Length < fieldOffset + fieldLength)
        {
          return null;
        }

        ReadOnlySpan<byte> slice = buffer.Slice(fieldOffset, fieldLength);
        return ExtractValue(slice, layout.Schema, encoding, clrType);
      });
  }

  private static object? ExtractValue(
    ReadOnlySpan<byte> slice,
    DbfFieldSchema schema,
    Encoding encoding,
    Type clrType)
  {
    char type = char.ToUpperInvariant(schema.Type);

    switch (type)
    {
      case 'C':
      case 'V':
      case 'W':
      case 'G':
      case 'P':
        return ReadCharacter(slice, encoding, schema.IsNullable);
      case 'L':
        return ReadLogical(slice, schema.IsNullable);
      case 'D':
        return ReadDate(slice, schema.IsNullable);
      case 'I':
        return ReadIntegerBinary(slice, schema.IsNullable);
      case 'B':
      case 'O':
        return ReadDoubleBinary(slice, schema.IsNullable);
      case 'Y':
      case 'N':
      case 'F':
        return ReadNumeric(slice, schema, clrType);
      default:
        string fallback = encoding.GetString(slice).TrimEnd(' ', '\0');
        if (fallback.Length == 0 && schema.IsNullable)
        {
          return null;
        }

        return fallback;
    }
  }

  private static object? ReadNumeric(ReadOnlySpan<byte> slice, DbfFieldSchema schema, Type clrType)
  {
    string text = Ascii.GetString(slice).Trim();
    if (text.Length == 0)
    {
      return schema.IsNullable ? null : GetDefaultNumeric(clrType);
    }

    if (clrType == typeof(int))
    {
      if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int @int))
      {
        return @int;
      }
    }
    else if (clrType == typeof(long))
    {
      if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long @long))
      {
        return @long;
      }
    }
    else if (clrType == typeof(decimal))
    {
      if (decimal.TryParse(
        text,
        NumberStyles.Float | NumberStyles.AllowLeadingSign,
        CultureInfo.InvariantCulture,
        out decimal @decimal))
      {
        return @decimal;
      }
    }
    else if (clrType == typeof(double))
    {
      if (double.TryParse(
        text,
        NumberStyles.Float | NumberStyles.AllowLeadingSign,
        CultureInfo.InvariantCulture,
        out double @double))
      {
        return @double;
      }
    }

    return schema.IsNullable ? null : GetDefaultNumeric(clrType);
  }

  private static object GetDefaultNumeric(Type clrType)
  {
    if (clrType == typeof(int))
    {
      return 0;
    }

    if (clrType == typeof(long))
    {
      return 0L;
    }

    if (clrType == typeof(decimal))
    {
      return 0m;
    }

    if (clrType == typeof(double))
    {
      return 0d;
    }

    return 0;
  }

  private static object? ReadIntegerBinary(ReadOnlySpan<byte> slice, bool isNullable)
  {
    if (slice.Length < 4)
    {
      return isNullable ? null : 0;
    }

    return BinaryPrimitives.ReadInt32LittleEndian(slice);
  }

  private static object? ReadDoubleBinary(ReadOnlySpan<byte> slice, bool isNullable)
  {
    if (slice.Length < 8)
    {
      return isNullable ? null : 0d;
    }

    long bits = BinaryPrimitives.ReadInt64LittleEndian(slice);
    return BitConverter.Int64BitsToDouble(bits);
  }

  private static object? ReadDate(ReadOnlySpan<byte> slice, bool isNullable)
  {
    string text = Ascii.GetString(slice).Trim();
    if (text.Length == 0)
    {
      return isNullable ? null : DateTime.MinValue;
    }

    if (text.Length == 8 &&
      int.TryParse(text[..4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int year) &&
      int.TryParse(text.Substring(4, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int month) &&
      int.TryParse(text.Substring(6, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
    {
      try
      {
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
      }
      catch (ArgumentOutOfRangeException)
      {
        return isNullable ? null : DateTime.MinValue;
      }
    }

    return isNullable ? null : DateTime.MinValue;
  }

  private static object? ReadLogical(ReadOnlySpan<byte> slice, bool isNullable)
  {
    if (slice.IsEmpty)
    {
      return isNullable ? null : false;
    }

    char indicator = char.ToUpperInvariant((char)slice[0]);
    return indicator switch
    {
      'T' or 'Y' or '1' => true,
      'F' or 'N' or '0' => false,
      _ => isNullable ? null : false
    };
  }

  private static object? ReadCharacter(ReadOnlySpan<byte> slice, Encoding encoding, bool isNullable)
  {
    string text = encoding.GetString(slice).TrimEnd(' ', '\0');
    if (text.Length == 0 && isNullable)
    {
      return null;
    }

    return text;
  }

  private static Dictionary<string, FieldLayout> BuildLayout(DbfTableDescriptor descriptor)
  {
    var layout = new Dictionary<string, FieldLayout>(StringComparer.OrdinalIgnoreCase);
    int offset = 1; // skip deletion flag

    foreach (DbfFieldSchema schema in descriptor.FieldSchemas)
    {
      layout[schema.Name] = new FieldLayout(schema, offset);
      offset += schema.Length;
    }

    return layout;
  }

  private static Type ResolveClrType(DbfFieldSchema schema)
  {
    char type = char.ToUpperInvariant(schema.Type);

    return type switch
    {
      'C' or 'V' or 'W' or 'G' or 'P' => typeof(string),
      'L' => typeof(bool),
      'D' => typeof(DateTime),
      'I' => typeof(int),
      'B' or 'O' => typeof(double),
      'F' => typeof(double),
      'Y' => typeof(decimal),
      'N' when schema.DecimalCount == 0 && schema.Length <= 9 => typeof(int),
      'N' when schema.DecimalCount == 0 && schema.Length <= 18 => typeof(long),
      'N' => typeof(decimal),
      _ => typeof(string)
    };
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

  private readonly record struct FieldLayout(DbfFieldSchema Schema, int Offset);

  private sealed record SelectStatement(string TableName, IReadOnlyList<string>? Columns);
}
