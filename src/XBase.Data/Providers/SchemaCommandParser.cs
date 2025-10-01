using System;
using System.Collections.Generic;
using XBase.Abstractions;

namespace XBase.Data.Providers;

internal static class SchemaCommandParser
{
  private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

  public static bool TryParse(string commandText, out SchemaOperation operation)
  {
    operation = null!;
    if (string.IsNullOrWhiteSpace(commandText))
    {
      return false;
    }

    string normalized = commandText.Trim();
    if (normalized.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
    {
      return TryParseCreateTable(normalized, out operation);
    }

    if (normalized.StartsWith("ALTER TABLE", StringComparison.OrdinalIgnoreCase))
    {
      return TryParseAlterTable(normalized, out operation);
    }

    if (normalized.StartsWith("DROP TABLE", StringComparison.OrdinalIgnoreCase))
    {
      return TryParseDropTable(normalized, out operation);
    }

    if (normalized.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase))
    {
      return TryParseCreateIndex(normalized, out operation);
    }

    if (normalized.StartsWith("DROP INDEX", StringComparison.OrdinalIgnoreCase))
    {
      return TryParseDropIndex(normalized, out operation);
    }

    return false;
  }

  private static bool TryParseCreateTable(string commandText, out SchemaOperation operation)
  {
    operation = null!;
    string remainder = commandText["CREATE TABLE".Length..].Trim();
    if (string.IsNullOrWhiteSpace(remainder))
    {
      return false;
    }

    string tableName;
    string definition = string.Empty;
    int firstSpace = IndexOfWhitespace(remainder);
    if (firstSpace < 0)
    {
      tableName = TrimIdentifier(remainder);
    }
    else
    {
      tableName = TrimIdentifier(remainder[..firstSpace]);
      definition = remainder[firstSpace..].Trim();
    }

    Dictionary<string, string> properties = CreateProperties();
    if (!string.IsNullOrWhiteSpace(definition))
    {
      properties["definition"] = definition;
    }

    operation = new SchemaOperation(
      SchemaOperationKind.CreateTable,
      tableName,
      objectName: null,
      properties);
    return true;
  }

  private static bool TryParseAlterTable(string commandText, out SchemaOperation operation)
  {
    operation = null!;
    string remainder = commandText["ALTER TABLE".Length..].Trim();
    if (string.IsNullOrWhiteSpace(remainder))
    {
      return false;
    }

    string tableName;
    int firstSpace = IndexOfWhitespace(remainder);
    if (firstSpace < 0)
    {
      return false;
    }

    tableName = TrimIdentifier(remainder[..firstSpace]);
    string actionPart = remainder[firstSpace..].Trim();
    if (actionPart.Length == 0)
    {
      return false;
    }

    string[] tokens = SplitTokens(actionPart);
    if (tokens.Length == 0)
    {
      return false;
    }

    string verb = tokens[0];
    if (Comparer.Equals(verb, "ADD"))
    {
      return TryParseAddColumn(tableName, tokens, actionPart, out operation);
    }

    if (Comparer.Equals(verb, "DROP"))
    {
      return TryParseDropColumn(tableName, tokens, out operation);
    }

    if (Comparer.Equals(verb, "RENAME"))
    {
      return TryParseRenameColumn(tableName, tokens, out operation);
    }

    if (Comparer.Equals(verb, "MODIFY") || Comparer.Equals(verb, "ALTER"))
    {
      return TryParseModifyColumn(tableName, tokens, actionPart, out operation);
    }

    return false;
  }

  private static bool TryParseAddColumn(
    string tableName,
    string[] tokens,
    string actionPart,
    out SchemaOperation operation)
  {
    operation = null!;
    int index = 1;
    if (tokens.Length <= index)
    {
      return false;
    }

    bool hasColumnKeyword = Comparer.Equals(tokens[index], "COLUMN");
    if (hasColumnKeyword)
    {
      index++;
    }

    if (tokens.Length <= index)
    {
      return false;
    }

    string columnName = TrimIdentifier(tokens[index]);
    int columnPosition = actionPart.IndexOf(tokens[index], StringComparison.OrdinalIgnoreCase);
    string definition = columnPosition >= 0
      ? actionPart[(columnPosition + tokens[index].Length)..].Trim()
      : string.Empty;

    Dictionary<string, string> properties = CreateProperties();
    properties["column"] = columnName;
    if (!string.IsNullOrWhiteSpace(definition))
    {
      properties["definition"] = definition;
    }

    operation = new SchemaOperation(
      SchemaOperationKind.AlterTableAddColumn,
      tableName,
      columnName,
      properties);
    return true;
  }

  private static bool TryParseDropColumn(string tableName, string[] tokens, out SchemaOperation operation)
  {
    operation = null!;
    int index = 1;
    if (tokens.Length <= index)
    {
      return false;
    }

    bool hasColumnKeyword = Comparer.Equals(tokens[index], "COLUMN");
    if (hasColumnKeyword)
    {
      index++;
    }

    if (tokens.Length <= index)
    {
      return false;
    }

    string columnName = TrimIdentifier(tokens[index]);
    Dictionary<string, string> properties = CreateProperties();
    properties["column"] = columnName;

    operation = new SchemaOperation(
      SchemaOperationKind.AlterTableDropColumn,
      tableName,
      columnName,
      properties);
    return true;
  }

  private static bool TryParseRenameColumn(string tableName, string[] tokens, out SchemaOperation operation)
  {
    operation = null!;
    int index = 1;
    if (tokens.Length <= index)
    {
      return false;
    }

    bool hasColumnKeyword = Comparer.Equals(tokens[index], "COLUMN");
    if (hasColumnKeyword)
    {
      index++;
    }

    if (tokens.Length <= index + 2)
    {
      return false;
    }

    string fromName = TrimIdentifier(tokens[index]);
    int toIndex = Array.FindIndex(tokens, index + 1, token => Comparer.Equals(token, "TO"));
    if (toIndex < 0 || toIndex + 1 >= tokens.Length)
    {
      return false;
    }

    string toName = TrimIdentifier(tokens[toIndex + 1]);
    Dictionary<string, string> properties = CreateProperties();
    properties["from"] = fromName;
    properties["to"] = toName;

    operation = new SchemaOperation(
      SchemaOperationKind.AlterTableRenameColumn,
      tableName,
      toName,
      properties);
    return true;
  }

  private static bool TryParseModifyColumn(
    string tableName,
    string[] tokens,
    string actionPart,
    out SchemaOperation operation)
  {
    operation = null!;
    int index = 1;
    if (tokens.Length <= index)
    {
      return false;
    }

    if (Comparer.Equals(tokens[index], "COLUMN"))
    {
      index++;
    }

    if (tokens.Length <= index)
    {
      return false;
    }

    string columnName = TrimIdentifier(tokens[index]);
    int columnPosition = actionPart.IndexOf(tokens[index], StringComparison.OrdinalIgnoreCase);
    string definition = columnPosition >= 0
      ? actionPart[(columnPosition + tokens[index].Length)..].Trim()
      : string.Empty;

    Dictionary<string, string> properties = CreateProperties();
    properties["column"] = columnName;
    if (!string.IsNullOrWhiteSpace(definition))
    {
      properties["definition"] = definition;
    }

    operation = new SchemaOperation(
      SchemaOperationKind.AlterTableModifyColumn,
      tableName,
      columnName,
      properties);
    return true;
  }

  private static bool TryParseDropTable(string commandText, out SchemaOperation operation)
  {
    operation = null!;
    string remainder = commandText["DROP TABLE".Length..].Trim();
    if (string.IsNullOrWhiteSpace(remainder))
    {
      return false;
    }

    string tableName = TrimIdentifier(remainder);
    operation = new SchemaOperation(
      SchemaOperationKind.DropTable,
      tableName,
      objectName: null,
      CreateProperties());
    return true;
  }

  private static bool TryParseCreateIndex(string commandText, out SchemaOperation operation)
  {
    operation = null!;
    string remainder = commandText["CREATE INDEX".Length..].Trim();
    if (string.IsNullOrWhiteSpace(remainder))
    {
      return false;
    }

    string[] tokens = SplitTokens(remainder);
    if (tokens.Length < 3)
    {
      return false;
    }

    string indexName = TrimIdentifier(tokens[0]);
    int onIndex = Array.FindIndex(tokens, token => Comparer.Equals(token, "ON"));
    if (onIndex < 0 || onIndex + 1 >= tokens.Length)
    {
      return false;
    }

    string tableName = TrimIdentifier(tokens[onIndex + 1]);
    string expression = ExtractParenthetical(remainder);

    Dictionary<string, string> properties = CreateProperties();
    properties["index"] = indexName;
    if (!string.IsNullOrWhiteSpace(expression))
    {
      properties["expression"] = expression;
    }

    operation = new SchemaOperation(
      SchemaOperationKind.CreateIndex,
      tableName,
      indexName,
      properties);
    return true;
  }

  private static bool TryParseDropIndex(string commandText, out SchemaOperation operation)
  {
    operation = null!;
    string remainder = commandText["DROP INDEX".Length..].Trim();
    if (string.IsNullOrWhiteSpace(remainder))
    {
      return false;
    }

    string[] tokens = SplitTokens(remainder);
    if (tokens.Length == 0)
    {
      return false;
    }

    string indexName = TrimIdentifier(tokens[0]);
    int onIndex = Array.FindIndex(tokens, token => Comparer.Equals(token, "ON"));
    if (onIndex < 0 || onIndex + 1 >= tokens.Length)
    {
      return false;
    }

    string tableName = TrimIdentifier(tokens[onIndex + 1]);
    Dictionary<string, string> properties = CreateProperties();
    properties["index"] = indexName;

    operation = new SchemaOperation(
      SchemaOperationKind.DropIndex,
      tableName,
      indexName,
      properties);
    return true;
  }

  private static int IndexOfWhitespace(string value)
  {
    for (int i = 0; i < value.Length; i++)
    {
      if (char.IsWhiteSpace(value[i]))
      {
        return i;
      }
    }

    return -1;
  }

  private static string TrimIdentifier(string identifier)
  {
    if (string.IsNullOrEmpty(identifier))
    {
      return identifier;
    }

    string trimmed = identifier.Trim();
    if (trimmed.Length >= 2)
    {
      if ((trimmed[0] == '"' && trimmed[^1] == '"') ||
          (trimmed[0] == '\'' && trimmed[^1] == '\'') ||
          (trimmed[0] == '[' && trimmed[^1] == ']') ||
          (trimmed[0] == '`' && trimmed[^1] == '`'))
      {
        return trimmed[1..^1];
      }
    }

    return trimmed;
  }

  private static string[] SplitTokens(string value)
  {
    return value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
  }

  private static string ExtractParenthetical(string value)
  {
    int openIndex = value.IndexOf('(');
    if (openIndex < 0)
    {
      return string.Empty;
    }

    int closeIndex = value.LastIndexOf(')');
    if (closeIndex <= openIndex)
    {
      return string.Empty;
    }

    return value[(openIndex + 1)..closeIndex].Trim();
  }

  private static Dictionary<string, string> CreateProperties()
  {
    return new Dictionary<string, string>(Comparer);
  }
}
