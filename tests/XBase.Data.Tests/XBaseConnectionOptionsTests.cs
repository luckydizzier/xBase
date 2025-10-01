using System;
using XBase.Data.Providers;

namespace XBase.Data.Tests;

public sealed class XBaseConnectionOptionsTests
{
  [Fact]
  public void Parse_WithWalSettings_BuildsOptions()
  {
    const string connectionString = "xbase://path=/var/db;readonly=false;journal=wal;journal.directory=/var/db/journal;journal.file=custom.trx;journal.flushOnWrite=false;journal.flushToDisk=false;journal.autoResetOnCommit=false";

    XBaseConnectionOptions options = XBaseConnectionOptions.Parse(connectionString);

    Assert.Equal("/var/db", options.RootPath);
    Assert.False(options.IsReadOnly);
    Assert.Equal(XBaseJournalMode.WriteAheadLog, options.Journal.Mode);
    Assert.Equal("/var/db/journal", options.Journal.DirectoryPath);
    Assert.Equal("custom.trx", options.Journal.FileName);
    Assert.False(options.Journal.FlushOnWrite);
    Assert.False(options.Journal.FlushToDisk);
    Assert.False(options.Journal.AutoResetOnCommit);
  }

  [Fact]
  public void Parse_ReadOnlyForcesNoJournal()
  {
    const string connectionString = "xbase://path=/var/db;readonly=true;journal=on";

    XBaseConnectionOptions options = XBaseConnectionOptions.Parse(connectionString);

    Assert.True(options.IsReadOnly);
    Assert.Equal(XBaseJournalMode.Disabled, options.Journal.Mode);
  }

  [Fact]
  public void Parse_WithoutPath_Throws()
  {
    Assert.Throws<InvalidOperationException>(() => XBaseConnectionOptions.Parse("journal=on"));
  }

  [Fact]
  public void JournalOptions_CreateWalOptions_UsesRootPath()
  {
    var options = new XBaseJournalOptions(
      XBaseJournalMode.WriteAheadLog,
      directoryPath: string.Empty,
      fileName: "example.trx",
      flushOnWrite: true,
      flushToDisk: true,
      autoResetOnCommit: true);

    var walOptions = options.CreateWalOptions("/var/db");

    Assert.Equal("/var/db", walOptions.DirectoryPath);
    Assert.Equal("example.trx", walOptions.JournalFileName);
    Assert.True(walOptions.FlushOnWrite);
    Assert.True(walOptions.FlushToDisk);
    Assert.True(walOptions.AutoResetOnCommit);
  }

  [Fact]
  public void JournalOptions_CreateWalOptions_WithoutDirectory_Throws()
  {
    var options = new XBaseJournalOptions(
      XBaseJournalMode.WriteAheadLog,
      directoryPath: string.Empty,
      fileName: "example.trx",
      flushOnWrite: true,
      flushToDisk: true,
      autoResetOnCommit: true);

    Assert.Throws<InvalidOperationException>(() => options.CreateWalOptions(string.Empty));
  }
}
