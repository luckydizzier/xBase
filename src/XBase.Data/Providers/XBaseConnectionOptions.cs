using System;
using XBase.Abstractions;

namespace XBase.Data.Providers;

public enum DeletedRecordVisibility
{
  Hide,
  Show
}

public sealed class XBaseConnectionOptions
{
  public static XBaseConnectionOptions Default { get; } = new XBaseConnectionOptions(
    rootPath: string.Empty,
    isReadOnly: false,
    lockingMode: LockingMode.File,
    deletedRecordVisibility: DeletedRecordVisibility.Hide,
    journal: XBaseJournalOptions.Default);

  private XBaseConnectionOptions(
    string rootPath,
    bool isReadOnly,
    LockingMode lockingMode,
    DeletedRecordVisibility deletedRecordVisibility,
    XBaseJournalOptions journal)
  {
    RootPath = rootPath ?? string.Empty;
    IsReadOnly = isReadOnly;
    LockingMode = lockingMode;
    DeletedRecordVisibility = deletedRecordVisibility;
    Journal = journal ?? XBaseJournalOptions.Default;
  }

  public string RootPath { get; }

  public bool IsReadOnly { get; }

  public LockingMode LockingMode { get; }

  public DeletedRecordVisibility DeletedRecordVisibility { get; }

  public XBaseJournalOptions Journal { get; }

  public static XBaseConnectionOptions Parse(string? connectionString)
  {
    if (string.IsNullOrWhiteSpace(connectionString))
    {
      return Default;
    }

    var builder = new XBaseConnectionStringBuilder(connectionString);

    string? path = builder.Path;
    if (string.IsNullOrWhiteSpace(path))
    {
      throw new InvalidOperationException("Connection string must include 'path'.");
    }

    bool isReadOnly = builder.ReadOnly ?? false;
    LockingMode lockingMode = ParseLockingMode(builder.Locking);
    DeletedRecordVisibility deletedVisibility = ParseDeletedVisibility(builder.Deleted);
    XBaseJournalOptions journal = XBaseJournalOptions.FromBuilder(builder, path);

    if (isReadOnly && journal.Mode != XBaseJournalMode.Disabled)
    {
      journal = XBaseJournalOptions.Disabled;
    }

    return new XBaseConnectionOptions(path, isReadOnly, lockingMode, deletedVisibility, journal);
  }

  private static LockingMode ParseLockingMode(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return LockingMode.File;
    }

    return value.Trim().ToLowerInvariant() switch
    {
      "none" => LockingMode.None,
      "file" => LockingMode.File,
      "record" => LockingMode.Record,
      _ => throw new InvalidOperationException($"Unsupported locking mode '{value}'.")
    };
  }

  private static DeletedRecordVisibility ParseDeletedVisibility(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return DeletedRecordVisibility.Hide;
    }

    return value.Trim().ToLowerInvariant() switch
    {
      "hide" => DeletedRecordVisibility.Hide,
      "show" => DeletedRecordVisibility.Show,
      _ => throw new InvalidOperationException($"Unsupported deleted record visibility '{value}'.")
    };
  }
}
