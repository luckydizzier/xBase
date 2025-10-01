using System;
using XBase.Demo.Domain.Schema;

namespace XBase.Demo.App.ViewModels;

/// <summary>
/// Represents an alteration action for schema preview generation.
/// </summary>
public sealed class SchemaColumnChangeViewModel
{
  public SchemaColumnChangeViewModel(
      ColumnChangeOperation operation,
      string columnName,
      SchemaColumnViewModel? columnDefinition = null,
      string? newColumnName = null)
  {
    Operation = operation;
    ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
    ColumnDefinition = columnDefinition;
    NewColumnName = newColumnName;
  }

  public ColumnChangeOperation Operation { get; }

  public string ColumnName { get; }

  public SchemaColumnViewModel? ColumnDefinition { get; }

  public string? NewColumnName { get; }

  public ColumnChangeDefinition ToDefinition()
      => new(Operation, ColumnName, ColumnDefinition?.ToDefinition(), NewColumnName);
}
