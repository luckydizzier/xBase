using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XBase.Demo.Domain.Schema;
using XBase.Demo.Domain.Services;

namespace XBase.Demo.Infrastructure.Schema;

/// <summary>
/// Lightweight DDL generator that emits provider-neutral scripts suitable for previews.
/// </summary>
public sealed class TemplateSchemaDdlService : ISchemaDdlService
{
  private readonly ILogger<TemplateSchemaDdlService> _logger;

  public TemplateSchemaDdlService(ILogger<TemplateSchemaDdlService> logger)
  {
    _logger = logger;
  }

  public Task<DdlPreview> BuildCreateTablePreviewAsync(TableSchemaDefinition schema, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(schema);
    cancellationToken.ThrowIfCancellationRequested();

    EnsureValidIdentifier(schema.TableName, nameof(schema.TableName));
    if (schema.Columns.Count == 0)
    {
      throw new InvalidOperationException("At least one column is required to build a CREATE TABLE script.");
    }

    var builder = new StringBuilder();
    builder.AppendLine($"CREATE TABLE {schema.TableName} (");
    for (var i = 0; i < schema.Columns.Count; i++)
    {
      var column = schema.Columns[i];
      var line = FormatColumnDefinition(column);
      builder.Append("  ");
      builder.Append(line);
      if (i < schema.Columns.Count - 1)
      {
        builder.Append(',');
      }

      builder.AppendLine();
    }

    builder.AppendLine(");");

    var downStatements = new List<string>
    {
      $"DROP TABLE {schema.TableName};"
    };

    var preview = new DdlPreview("CreateTable", new[] { builder.ToString().TrimEnd() }, downStatements);
    _logger.LogInformation("Generated CREATE TABLE preview for {Table}", schema.TableName);
    return Task.FromResult(preview);
  }

  public Task<DdlPreview> BuildAlterTablePreviewAsync(TableAlterationDefinition alteration, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(alteration);
    cancellationToken.ThrowIfCancellationRequested();

    EnsureValidIdentifier(alteration.TableName, nameof(alteration.TableName));
    if (alteration.ColumnChanges.Count == 0)
    {
      throw new InvalidOperationException("At least one column change is required to build an ALTER TABLE script.");
    }

    var upStatements = new List<string>();
    var downStatements = new List<string>();

    foreach (var change in alteration.ColumnChanges)
    {
      cancellationToken.ThrowIfCancellationRequested();
      upStatements.Add(BuildAlterStatement(alteration.TableName, change));
      downStatements.Insert(0, BuildRollbackStatement(alteration.TableName, change));
    }

    var preview = new DdlPreview("AlterTable", upStatements, downStatements);
    _logger.LogInformation("Generated ALTER TABLE preview for {Table} with {ChangeCount} changes", alteration.TableName, alteration.ColumnChanges.Count);
    return Task.FromResult(preview);
  }

  public Task<DdlPreview> BuildDropTablePreviewAsync(string tableName, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
    cancellationToken.ThrowIfCancellationRequested();

    var statements = new[] { $"DROP TABLE {tableName};" };
    var rollback = new[] { $"-- Recreate table {tableName} using the captured CREATE script." };
    var preview = new DdlPreview("DropTable", statements, rollback);
    _logger.LogInformation("Generated DROP TABLE preview for {Table}", tableName);
    return Task.FromResult(preview);
  }

  private static string FormatColumnDefinition(TableColumnDefinition column)
  {
    ArgumentNullException.ThrowIfNull(column);
    EnsureValidIdentifier(column.Name, nameof(column.Name));
    ArgumentException.ThrowIfNullOrWhiteSpace(column.DataType);

    var typeToken = column.DataType.ToUpperInvariant();
    if (column.Length.HasValue)
    {
      typeToken += column.Scale.HasValue
          ? $"({column.Length.Value.ToString(CultureInfo.InvariantCulture)},{column.Scale.Value.ToString(CultureInfo.InvariantCulture)})"
          : $"({column.Length.Value.ToString(CultureInfo.InvariantCulture)})";
    }

    var nullability = column.AllowNulls ? "NULL" : "NOT NULL";
    return $"{column.Name} {typeToken} {nullability}";
  }

  private static string BuildAlterStatement(string tableName, ColumnChangeDefinition change)
  {
    return change.Operation switch
    {
      ColumnChangeOperation.Add => BuildAddColumnStatement(tableName, change),
      ColumnChangeOperation.Alter => BuildAlterColumnStatement(tableName, change),
      ColumnChangeOperation.Drop => BuildDropColumnStatement(tableName, change),
      ColumnChangeOperation.Rename => BuildRenameColumnStatement(tableName, change),
      _ => throw new ArgumentOutOfRangeException(nameof(change.Operation), change.Operation, "Unsupported column change operation.")
    };
  }

  private static string BuildRollbackStatement(string tableName, ColumnChangeDefinition change)
  {
    return change.Operation switch
    {
      ColumnChangeOperation.Add => $"ALTER TABLE {tableName} DROP COLUMN {change.ColumnName};",
      ColumnChangeOperation.Alter => $"-- Review manual rollback for column {change.ColumnName}.",
      ColumnChangeOperation.Drop => change.ColumnDefinition is not null
          ? $"ALTER TABLE {tableName} ADD COLUMN {FormatColumnDefinition(change.ColumnDefinition)};"
          : $"-- Unable to restore dropped column {change.ColumnName} without definition.",
      ColumnChangeOperation.Rename => change.NewColumnName is not null
          ? $"ALTER TABLE {tableName} RENAME COLUMN {change.NewColumnName} TO {change.ColumnName};"
          : $"-- Rename rollback unavailable for column {change.ColumnName}.",
      _ => throw new ArgumentOutOfRangeException(nameof(change.Operation), change.Operation, "Unsupported column change operation.")
    };
  }

  private static string BuildAddColumnStatement(string tableName, ColumnChangeDefinition change)
  {
    if (change.ColumnDefinition is null)
    {
      throw new InvalidOperationException($"Column definition is required when adding column '{change.ColumnName}'.");
    }

    return $"ALTER TABLE {tableName} ADD COLUMN {FormatColumnDefinition(change.ColumnDefinition)};";
  }

  private static string BuildAlterColumnStatement(string tableName, ColumnChangeDefinition change)
  {
    if (change.ColumnDefinition is null)
    {
      throw new InvalidOperationException($"Column definition is required when altering column '{change.ColumnName}'.");
    }

    return $"ALTER TABLE {tableName} ALTER COLUMN {FormatColumnDefinition(change.ColumnDefinition)};";
  }

  private static string BuildDropColumnStatement(string tableName, ColumnChangeDefinition change)
  {
    EnsureValidIdentifier(change.ColumnName, nameof(change.ColumnName));
    return $"ALTER TABLE {tableName} DROP COLUMN {change.ColumnName};";
  }

  private static string BuildRenameColumnStatement(string tableName, ColumnChangeDefinition change)
  {
    EnsureValidIdentifier(change.ColumnName, nameof(change.ColumnName));
    ArgumentException.ThrowIfNullOrWhiteSpace(change.NewColumnName);
    EnsureValidIdentifier(change.NewColumnName!, nameof(change.NewColumnName));
    return $"ALTER TABLE {tableName} RENAME COLUMN {change.ColumnName} TO {change.NewColumnName};";
  }

  private static void EnsureValidIdentifier(string identifier, string paramName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(identifier, paramName);
    if (identifier.Any(char.IsWhiteSpace))
    {
      throw new ArgumentException($"Identifier '{identifier}' cannot contain whitespace.", paramName);
    }
  }
}
