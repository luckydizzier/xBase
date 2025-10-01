using System;
using System.IO;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Transactions;

namespace XBase.Core.Tests.Transactions;

public sealed class WalJournalTests
{
  [Fact]
  public async Task RecoverAsync_WithIncompleteTransaction_ReturnsUndoPlan()
  {
    using var workspace = new TemporaryWorkspace();
    var options = new WalJournalOptions
    {
      DirectoryPath = workspace.DirectoryPath,
      TransactionIdProvider = () => 42L,
      FlushOnWrite = true,
      FlushToDisk = false
    };

    await using (var journal = new WalJournal(options))
    {
      await journal.BeginAsync();
      long transactionId = journal.ActiveTransactionId!.Value;

      var mutation = JournalMutation.Update(
        "Customers",
        recordNumber: 7,
        beforeImage: new byte[] { 0x01, 0x02 },
        afterImage: new byte[] { 0x03, 0x04 });

      JournalEntry entry = JournalEntry.ForMutation(transactionId, DateTimeOffset.UtcNow, mutation);
      await journal.AppendAsync(entry);
    }

    string journalPath = Path.Combine(workspace.DirectoryPath, "xbase.trx");
    Assert.True(new FileInfo(journalPath).Length > 16);

    WalRecoveryResult recovery = await WalJournal.RecoverAsync(options);

    WalInFlightTransaction inFlight = Assert.Single(recovery.IncompleteTransactions);
    Assert.Equal(42L, inFlight.TransactionId);
    JournalMutation recoveredMutation = Assert.Single(inFlight.Mutations);
    Assert.Equal(JournalMutationKind.Update, recoveredMutation.Kind);
    Assert.Equal(new byte[] { 0x01, 0x02 }, recoveredMutation.BeforeImage.ToArray());
    Assert.Equal(new byte[] { 0x03, 0x04 }, recoveredMutation.AfterImage.ToArray());
  }

  [Fact]
  public async Task RecoverAsync_WithCommitMarker_ReturnsRedoPlan()
  {
    using var workspace = new TemporaryWorkspace();
    var options = new WalJournalOptions
    {
      DirectoryPath = workspace.DirectoryPath,
      TransactionIdProvider = () => 100L,
      AutoResetOnCommit = false,
      FlushOnWrite = true,
      FlushToDisk = false
    };

    await using (var journal = new WalJournal(options))
    {
      await journal.BeginAsync();
      long transactionId = journal.ActiveTransactionId!.Value;
      var mutation = JournalMutation.Insert("Orders", 5, new byte[] { 0x10, 0x11, 0x12 });
      JournalEntry entry = JournalEntry.ForMutation(transactionId, DateTimeOffset.UtcNow, mutation);
      await journal.AppendAsync(entry);
      await journal.CommitAsync();
    }

    string journalPath = Path.Combine(workspace.DirectoryPath, "xbase.trx");
    Assert.True(new FileInfo(journalPath).Length > 16);

    WalRecoveryResult recovery = await WalJournal.RecoverAsync(options);

    WalCommittedTransaction committed = Assert.Single(recovery.CommittedTransactions);
    Assert.Equal(100L, committed.TransactionId);
    Assert.Single(committed.Mutations);
    Assert.Equal("Orders", committed.Mutations[0].TableName);
    Assert.True(committed.Mutations[0].AfterImage.Span.SequenceEqual(new byte[] { 0x10, 0x11, 0x12 }));
  }

  [Fact]
  public async Task BeginAsync_WhenPendingEntriesExist_Throws()
  {
    using var workspace = new TemporaryWorkspace();
    var options = new WalJournalOptions
    {
      DirectoryPath = workspace.DirectoryPath,
      TransactionIdProvider = () => 7L,
      FlushOnWrite = true,
      FlushToDisk = false
    };

    await using (var journal = new WalJournal(options))
    {
      await journal.BeginAsync();
      long transactionId = journal.ActiveTransactionId!.Value;
      var mutation = JournalMutation.Delete("Products", 3, new byte[] { 0xAA });
      JournalEntry entry = JournalEntry.ForMutation(transactionId, DateTimeOffset.UtcNow, mutation);
      await journal.AppendAsync(entry);
    }

    string journalPath = Path.Combine(workspace.DirectoryPath, "xbase.trx");
    Assert.True(new FileInfo(journalPath).Length > 16);

    await using var second = new WalJournal(options);
    await Assert.ThrowsAsync<InvalidOperationException>(async () => await second.BeginAsync());
  }

  [Fact]
  public async Task RecoverAsync_WithTruncatedTail_SignalsFlag()
  {
    using var workspace = new TemporaryWorkspace();
    string journalPath = Path.Combine(workspace.DirectoryPath, "xbase.trx");
    var options = new WalJournalOptions
    {
      DirectoryPath = workspace.DirectoryPath,
      TransactionIdProvider = () => 200L,
      FlushOnWrite = true,
      FlushToDisk = false
    };

    await using (var journal = new WalJournal(options))
    {
      await journal.BeginAsync();
      long transactionId = journal.ActiveTransactionId!.Value;
      var mutation = JournalMutation.Insert("Ledger", 1, new byte[] { 0x01, 0x02, 0x03 });
      JournalEntry entry = JournalEntry.ForMutation(transactionId, DateTimeOffset.UtcNow, mutation);
      await journal.AppendAsync(entry);
    }

    using (FileStream stream = new(journalPath, FileMode.Open, FileAccess.ReadWrite))
    {
      long truncatedLength = Math.Max(0, stream.Length - 1);
      stream.SetLength(truncatedLength);
    }

    WalRecoveryResult recovery = await WalJournal.RecoverAsync(options);

    Assert.True(recovery.HasTruncatedTail);
    Assert.NotEmpty(recovery.IncompleteTransactions);
  }
}
