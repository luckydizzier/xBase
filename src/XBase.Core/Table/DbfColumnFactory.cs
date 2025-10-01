using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using XBase.Abstractions;

namespace XBase.Core.Table;

public readonly record struct DbfFieldLayout(DbfFieldSchema Schema, int Offset);

public static class DbfColumnFactory
{
  private static readonly Encoding Ascii = Encoding.ASCII;

  public static IReadOnlyList<TableColumn> CreateColumns(DbfTableDescriptor descriptor) =>
    CreateColumns(descriptor, requestedColumns: null, encodingOverride: null);

  public static IReadOnlyList<TableColumn> CreateColumns(
    DbfTableDescriptor descriptor,
    IReadOnlyList<string>? requestedColumns,
    Encoding? encodingOverride = null)
  {
    if (descriptor is null)
    {
      throw new ArgumentNullException(nameof(descriptor));
    }

    IReadOnlyDictionary<string, DbfFieldLayout> layout = CreateLayoutLookup(descriptor);
    Encoding encoding = encodingOverride ?? DbfEncodingRegistry.Resolve(descriptor.LanguageDriverId);

    IEnumerable<DbfFieldLayout> selection;
    if (requestedColumns is null || requestedColumns.Count == 0)
    {
      selection = descriptor.FieldSchemas.Select(schema => layout[schema.Name]);
    }
    else
    {
      var buffer = new List<DbfFieldLayout>(requestedColumns.Count);
      foreach (string column in requestedColumns)
      {
        if (!layout.TryGetValue(column, out DbfFieldLayout fieldLayout))
        {
          throw new InvalidOperationException(
            $"Column '{column}' was not found in table '{descriptor.Name}'.");
        }

        buffer.Add(fieldLayout);
      }

      selection = buffer;
    }

    return selection.Select(entry => CreateColumn(entry, encoding)).ToArray();
  }

  public static IReadOnlyDictionary<string, DbfFieldLayout> CreateLayoutLookup(DbfTableDescriptor descriptor)
  {
    if (descriptor is null)
    {
      throw new ArgumentNullException(nameof(descriptor));
    }

    var layout = new Dictionary<string, DbfFieldLayout>(StringComparer.OrdinalIgnoreCase);
    int offset = 1;

    foreach (DbfFieldSchema schema in descriptor.FieldSchemas)
    {
      layout[schema.Name] = new DbfFieldLayout(schema, offset);
      offset += schema.Length;
    }

    return layout;
  }

  private static TableColumn CreateColumn(DbfFieldLayout layout, Encoding encoding)
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
}
