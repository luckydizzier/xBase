using System;
using XBase.Core.Transactions;

namespace XBase.Data.Providers;

public enum XBaseJournalMode
{
  Disabled,
  WriteAheadLog
}

public sealed class XBaseJournalOptions
{
  public static XBaseJournalOptions Disabled { get; } = new XBaseJournalOptions(
    XBaseJournalMode.Disabled,
    directoryPath: string.Empty,
    fileName: WalJournalDefaults.FileName,
    flushOnWrite: false,
    flushToDisk: false,
    autoResetOnCommit: false);

  public static XBaseJournalOptions Default { get; } = new XBaseJournalOptions(
    XBaseJournalMode.WriteAheadLog,
    directoryPath: string.Empty,
    fileName: WalJournalDefaults.FileName,
    flushOnWrite: WalJournalDefaults.FlushOnWrite,
    flushToDisk: WalJournalDefaults.FlushToDisk,
    autoResetOnCommit: WalJournalDefaults.AutoResetOnCommit);

  public XBaseJournalOptions(
    XBaseJournalMode mode,
    string directoryPath,
    string fileName,
    bool flushOnWrite,
    bool flushToDisk,
    bool autoResetOnCommit)
  {
    Mode = mode;
    DirectoryPath = directoryPath ?? string.Empty;
    FileName = string.IsNullOrWhiteSpace(fileName) ? WalJournalDefaults.FileName : fileName;
    FlushOnWrite = flushOnWrite;
    FlushToDisk = flushToDisk;
    AutoResetOnCommit = autoResetOnCommit;
  }

  public XBaseJournalMode Mode { get; }

  public string DirectoryPath { get; }

  public string FileName { get; }

  public bool FlushOnWrite { get; }

  public bool FlushToDisk { get; }

  public bool AutoResetOnCommit { get; }

  public WalJournalOptions CreateWalOptions(string rootPath)
  {
    if (Mode != XBaseJournalMode.WriteAheadLog)
    {
      throw new InvalidOperationException("Journal mode is disabled.");
    }

    string directory = string.IsNullOrWhiteSpace(DirectoryPath) ? rootPath : DirectoryPath;
    if (string.IsNullOrWhiteSpace(directory))
    {
      throw new InvalidOperationException("Journal directory must be resolved from either journal.directory or path.");
    }

    return new WalJournalOptions
    {
      DirectoryPath = directory,
      JournalFileName = FileName,
      FlushOnWrite = FlushOnWrite,
      FlushToDisk = FlushToDisk,
      AutoResetOnCommit = AutoResetOnCommit
    };
  }

  internal static XBaseJournalOptions FromBuilder(XBaseConnectionStringBuilder builder, string rootPath)
  {
    string? journalValue = builder.Journal;
    XBaseJournalMode mode = ParseJournalMode(journalValue);
    if (mode == XBaseJournalMode.Disabled)
    {
      return Disabled;
    }

    string directory = builder.JournalDirectory ?? rootPath;
    string fileName = builder.JournalFileName ?? WalJournalDefaults.FileName;
    bool flushOnWrite = builder.GetBoolean("journal.flushOnWrite") ?? WalJournalDefaults.FlushOnWrite;
    bool flushToDisk = builder.GetBoolean("journal.flushToDisk") ?? WalJournalDefaults.FlushToDisk;
    bool autoResetOnCommit = builder.GetBoolean("journal.autoResetOnCommit") ?? WalJournalDefaults.AutoResetOnCommit;

    return new XBaseJournalOptions(mode, directory, fileName, flushOnWrite, flushToDisk, autoResetOnCommit);
  }

  private static XBaseJournalMode ParseJournalMode(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return XBaseJournalMode.WriteAheadLog;
    }

    return value.Trim().ToLowerInvariant() switch
    {
      "on" => XBaseJournalMode.WriteAheadLog,
      "true" => XBaseJournalMode.WriteAheadLog,
      "wal" => XBaseJournalMode.WriteAheadLog,
      "writeaheadlog" => XBaseJournalMode.WriteAheadLog,
      "enabled" => XBaseJournalMode.WriteAheadLog,
      "off" => XBaseJournalMode.Disabled,
      "false" => XBaseJournalMode.Disabled,
      "disabled" => XBaseJournalMode.Disabled,
      "none" => XBaseJournalMode.Disabled,
      _ => throw new InvalidOperationException($"Unsupported journal mode '{value}'.")
    };
  }

  private static class WalJournalDefaults
  {
    public const string FileName = "xbase.trx";
    public const bool FlushOnWrite = true;
    public const bool FlushToDisk = true;
    public const bool AutoResetOnCommit = true;
  }
}
