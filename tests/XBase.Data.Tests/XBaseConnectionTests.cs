using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Cursors;
using XBase.Data.Providers;

namespace XBase.Data.Tests;

public sealed class XBaseConnectionTests
{
  [Fact]
  public void Open_SetsStateToOpen()
  {
    using var connection = new XBaseConnection();

    connection.Open();

    Assert.Equal(ConnectionState.Open, connection.State);
  }

  [Fact]
  public void BeginTransaction_StartsJournal()
  {
    var journal = new FakeJournal();
    using var connection = new XBaseConnection(new NoOpCursorFactory(), journal, new FakeSchemaMutator());

    using var transaction = connection.BeginTransaction();

    Assert.Equal(1, journal.BeginCallCount);
    Assert.Equal(CancellationToken.None, journal.LastBeginCancellationToken);
  }

  [Fact]
  public async Task BeginTransactionAsync_StartsJournal()
  {
    var journal = new FakeJournal();
    using var connection = new XBaseConnection(new NoOpCursorFactory(), journal, new FakeSchemaMutator());

    await using var transaction = await connection.BeginTransactionAsync();

    Assert.Equal(1, journal.BeginCallCount);
    Assert.Equal(CancellationToken.None, journal.LastBeginCancellationToken);
  }

  [Fact]
  public void DisposeWithoutCommit_RollsBackJournal()
  {
    var journal = new FakeJournal();
    using var connection = new XBaseConnection(new NoOpCursorFactory(), journal, new FakeSchemaMutator());

    var transaction = connection.BeginTransaction();
    transaction.Dispose();

    Assert.Equal(1, journal.RollbackCallCount);
  }

  private sealed class FakeJournal : IJournal
  {
    public int BeginCallCount { get; private set; }

    public CancellationToken LastBeginCancellationToken { get; private set; } = CancellationToken.None;

    public int RollbackCallCount { get; private set; }

    public ValueTask BeginAsync(CancellationToken cancellationToken = default)
    {
      BeginCallCount++;
      LastBeginCancellationToken = cancellationToken;
      return ValueTask.CompletedTask;
    }

    public ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
      return ValueTask.CompletedTask;
    }

    public ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
      RollbackCallCount++;
      return ValueTask.CompletedTask;
    }
  }

  private sealed class FakeSchemaMutator : ISchemaMutator
  {
    public ValueTask<SchemaVersion> ExecuteAsync(
      SchemaOperation operation,
      string? author = null,
      CancellationToken cancellationToken = default)
    {
      return ValueTask.FromResult(SchemaVersion.Start);
    }

    public ValueTask<IReadOnlyList<SchemaLogEntry>> ReadHistoryAsync(
      string tableName,
      CancellationToken cancellationToken = default)
    {
      return ValueTask.FromResult<IReadOnlyList<SchemaLogEntry>>(Array.Empty<SchemaLogEntry>());
    }

    public ValueTask<IReadOnlyList<SchemaBackfillTask>> ReadBackfillQueueAsync(
      string tableName,
      CancellationToken cancellationToken = default)
    {
      return ValueTask.FromResult<IReadOnlyList<SchemaBackfillTask>>(Array.Empty<SchemaBackfillTask>());
    }
  }
}
