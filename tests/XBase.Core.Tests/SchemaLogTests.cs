using System;
using System.Collections.Generic;
using System.IO;
using XBase.Abstractions;
using XBase.Core.Ddl;

namespace XBase.Core.Tests;

public sealed class SchemaLogTests
{
  [Fact]
  public async Task AppendAsync_IncrementsVersionAndPersists()
  {
    using var workspace = new TemporaryWorkspace();
    string logPath = workspace.Combine("orders.ddl");
    var log = new SchemaLog(logPath);

    var baseEntry = new SchemaLogEntry(
      SchemaVersion.Start,
      DateTimeOffset.UtcNow,
      "tester",
      SchemaOperationKind.CreateTable,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["table"] = "orders"
      },
      string.Empty);

    SchemaVersion first = await log.AppendAsync(baseEntry);
    SchemaVersion second = await log.AppendAsync(baseEntry);

    Assert.Equal<ulong>(1, first.Value);
    Assert.Equal<ulong>(2, second.Value);

    IReadOnlyList<SchemaLogEntry> entries = log.ReadEntries();
    Assert.Equal(2, entries.Count);
    Assert.Equal(second, entries[^1].Version);
  }

  [Fact]
  public async Task ReadEntries_IgnoresTruncatedTail()
  {
    using var workspace = new TemporaryWorkspace();
    string logPath = workspace.Combine("customers.ddl");
    var log = new SchemaLog(logPath);

    var baseEntry = new SchemaLogEntry(
      SchemaVersion.Start,
      DateTimeOffset.UtcNow,
      "tester",
      SchemaOperationKind.AlterTableAddColumn,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["table"] = "customers",
        ["column"] = "balance"
      },
      string.Empty);

    await log.AppendAsync(baseEntry);
    await log.AppendAsync(baseEntry);

    using (FileStream stream = new(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
    {
      stream.SetLength(stream.Length - 10);
    }

    IReadOnlyList<SchemaLogEntry> entries = log.ReadEntries();
    Assert.Single(entries);
    Assert.Equal<ulong>(1, entries[0].Version.Value);
  }

  [Fact]
  public async Task PackAsync_WithPendingTasks_RemovesEntries()
  {
    using var workspace = new TemporaryWorkspace();
    var mutator = new SchemaMutator(workspace.DirectoryPath);

    var addColumn = new SchemaOperation(
      SchemaOperationKind.AlterTableAddColumn,
      "customers",
      "balance",
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["column"] = "balance",
        ["definition"] = "N(10,2)"
      });

    await mutator.ExecuteAsync(addColumn, "tester").ConfigureAwait(false);

    IReadOnlyList<SchemaBackfillTask> before = await mutator.ReadBackfillQueueAsync("customers").ConfigureAwait(false);
    Assert.Single(before);

    int removed = await mutator.PackAsync("customers").ConfigureAwait(false);
    Assert.Equal(1, removed);

    IReadOnlyList<SchemaBackfillTask> after = await mutator.ReadBackfillQueueAsync("customers").ConfigureAwait(false);
    Assert.Empty(after);
  }

  [Fact]
  public async Task ExecuteAsync_AddColumnEnqueuesBackfillAndCheckpointClears()
  {
    using var workspace = new TemporaryWorkspace();
    var mutator = new SchemaMutator(workspace.DirectoryPath);

    var addColumn = new SchemaOperation(
      SchemaOperationKind.AlterTableAddColumn,
      "customers",
      "balance",
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["column"] = "balance",
        ["definition"] = "N(10,2)"
      });

    SchemaVersion version = await mutator.ExecuteAsync(addColumn, "tester").ConfigureAwait(false);
    Assert.Equal<ulong>(1, version.Value);

    IReadOnlyList<SchemaBackfillTask> queue = await mutator.ReadBackfillQueueAsync("customers").ConfigureAwait(false);
    Assert.Single(queue);
    Assert.Equal(version, queue[0].Version);

    var freshMutator = new SchemaMutator(workspace.DirectoryPath);
    IReadOnlyList<SchemaBackfillTask> persisted = await freshMutator.ReadBackfillQueueAsync("customers").ConfigureAwait(false);
    Assert.Single(persisted);

    SchemaVersion checkpoint = await freshMutator.CreateCheckpointAsync("customers").ConfigureAwait(false);
    Assert.Equal(version, checkpoint);

    int removed = await freshMutator.PackAsync("customers").ConfigureAwait(false);
    Assert.Equal(0, removed);

    var dropTable = new SchemaOperation(
      SchemaOperationKind.DropTable,
      "customers",
      null,
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    await mutator.ExecuteAsync(dropTable, "tester").ConfigureAwait(false);

    IReadOnlyList<SchemaBackfillTask> finalQueue = await mutator.ReadBackfillQueueAsync("customers").ConfigureAwait(false);
    Assert.Empty(finalQueue);
  }
}
