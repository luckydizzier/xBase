using System;
using System.Data.Common;

namespace XBase.Data.Providers;

public sealed class XBaseConnectionStringBuilder : DbConnectionStringBuilder
{
  public XBaseConnectionStringBuilder()
  {
  }

  public XBaseConnectionStringBuilder(string connectionString)
    : this()
  {
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
      ConnectionString = Normalize(connectionString);
    }
  }

  public string? Path => GetString("path");

  public bool? ReadOnly => GetBoolean("readonly");

  public string? Locking => GetString("locking");

  public string? Deleted => GetString("deleted");

  public string? Journal => GetString("journal");

  public string? JournalDirectory => GetString("journal.directory");

  public string? JournalFileName => GetString("journal.file");

  public bool? GetBoolean(string keyword)
  {
    if (!TryGetValue(keyword, out object? raw))
    {
      return null;
    }

    if (raw is bool boolValue)
    {
      return boolValue;
    }

    if (raw is int intValue)
    {
      return intValue != 0;
    }

    if (raw is string text)
    {
      if (string.IsNullOrWhiteSpace(text))
      {
        return null;
      }

      return ParseBoolean(text.Trim());
    }

    throw new InvalidOperationException($"Value for '{keyword}' could not be interpreted as boolean.");
  }

  public string? GetString(string keyword)
  {
    if (!TryGetValue(keyword, out object? value) || value is null)
    {
      return null;
    }

    if (value is string text)
    {
      return text;
    }

    return Convert.ToString(value);
  }

  public static string Normalize(string connectionString)
  {
    if (string.IsNullOrWhiteSpace(connectionString))
    {
      return string.Empty;
    }

    if (connectionString.StartsWith("xbase://", StringComparison.OrdinalIgnoreCase))
    {
      return connectionString.Substring("xbase://".Length);
    }

    return connectionString;
  }

  private static bool ParseBoolean(string value)
  {
    return value.ToLowerInvariant() switch
    {
      "1" => true,
      "0" => false,
      "true" => true,
      "false" => false,
      "yes" => true,
      "no" => false,
      "on" => true,
      "off" => false,
      _ => throw new InvalidOperationException($"Value '{value}' is not recognized as boolean.")
    };
  }
}
