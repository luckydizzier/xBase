using System;
using System.IO;

namespace XBase.Core.Transactions;

public sealed class WalJournalOptions
{
  public string DirectoryPath { get; init; } = string.Empty;

  public string JournalFileName { get; init; } = "xbase.trx";

  public bool FlushOnWrite { get; init; } = true;

  public bool FlushToDisk { get; init; } = true;

  public bool AutoResetOnCommit { get; init; } = true;

  public Func<long>? TransactionIdProvider { get; init; }

  internal string ResolveJournalPath()
  {
    if (string.IsNullOrWhiteSpace(DirectoryPath))
    {
      throw new InvalidOperationException("Journal directory must be provided.");
    }

    return Path.Combine(DirectoryPath, JournalFileName);
  }
}
