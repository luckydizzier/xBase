using System.Collections.Generic;

namespace XBase.Demo.Domain.Schema;

/// <summary>
/// Describes a table schema used when generating create/alter previews.
/// </summary>
/// <param name="TableName">Logical table name.</param>
/// <param name="Columns">Ordered column definitions.</param>
public sealed record TableSchemaDefinition(string TableName, IReadOnlyList<TableColumnDefinition> Columns);

/// <summary>
/// Describes a single column participating in schema operations.
/// </summary>
/// <param name="Name">Column name.</param>
/// <param name="DataType">Provider-specific data type token.</param>
/// <param name="AllowNulls">Indicates whether NULL values are permitted.</param>
/// <param name="Length">Optional data type length.</param>
/// <param name="Scale">Optional data type scale.</param>
public sealed record TableColumnDefinition(string Name, string DataType, bool AllowNulls = true, int? Length = null, int? Scale = null);

/// <summary>
/// Enumerates supported column change operations for ALTER TABLE previews.
/// </summary>
public enum ColumnChangeOperation
{
  Add,
  Alter,
  Drop,
  Rename
}

/// <summary>
/// Describes a column-level change used when building ALTER TABLE previews.
/// </summary>
/// <param name="Operation">Type of change to apply.</param>
/// <param name="ColumnName">Name of the target column.</param>
/// <param name="ColumnDefinition">Optional column definition used for add/alter/drop rollback scripts.</param>
/// <param name="NewColumnName">Optional new column name when renaming.</param>
public sealed record ColumnChangeDefinition(
    ColumnChangeOperation Operation,
    string ColumnName,
    TableColumnDefinition? ColumnDefinition = null,
    string? NewColumnName = null);

/// <summary>
/// Represents a collection of column changes for an ALTER TABLE preview.
/// </summary>
/// <param name="TableName">Target table name.</param>
/// <param name="ColumnChanges">Ordered column change operations.</param>
public sealed record TableAlterationDefinition(string TableName, IReadOnlyList<ColumnChangeDefinition> ColumnChanges);
