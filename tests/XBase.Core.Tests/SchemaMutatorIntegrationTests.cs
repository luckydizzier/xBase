using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Ddl;
using XBase.Core.Table;
using XBase.TestSupport;

namespace XBase.Core.Tests;

public sealed class SchemaMutatorIntegrationTests
{
  [Fact]
  public async Task PackAsync_WithDeletedRecords_RebuildsDataAndIndexes()
  {
    using var workspace = new TemporaryWorkspace();
    string tableName = "customers";
    string dbfPath = DbfTestBuilder.CreateTable(
      workspace.DirectoryPath,
      tableName,
      (false, "A001"),
      (true, "A002"),
      (false, "A003"));
    string indexPath = DbfTestBuilder.CreateIndex(workspace.DirectoryPath, tableName + ".ntx", "legacy-index");
    var mutator = new SchemaMutator(workspace.DirectoryPath);

    var addColumn = new SchemaOperation(
      SchemaOperationKind.AlterTableAddColumn,
      tableName,
      "balance",
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
      {
        ["column"] = "balance",
        ["definition"] = "N(10,2)"
      });
    await mutator.ExecuteAsync(addColumn, "tester").ConfigureAwait(false);

    int removed = await mutator.PackAsync(tableName).ConfigureAwait(false);
    Assert.Equal(1, removed);

    var loader = new DbfTableLoader();
    DbfTableDescriptor descriptor = loader.LoadDbf(dbfPath);
    Assert.Equal<uint>(2u, descriptor.RecordCount);

    using (FileStream stream = new(dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
      stream.Seek(descriptor.HeaderLength, SeekOrigin.Begin);
      byte[] record = new byte[descriptor.RecordLength];

      int read = stream.Read(record, 0, record.Length);
      Assert.Equal(record.Length, read);
      Assert.Equal((byte)' ', record[0]);
      Assert.Equal("A001", System.Text.Encoding.ASCII.GetString(record, 1, 4));

      read = stream.Read(record, 0, record.Length);
      Assert.Equal(record.Length, read);
      Assert.Equal((byte)' ', record[0]);
      Assert.Equal("A003", System.Text.Encoding.ASCII.GetString(record, 1, 4));

      int terminator = stream.ReadByte();
      Assert.Equal(0x1A, terminator);
    }

    string manifest = File.ReadAllText(indexPath);
    Assert.Contains("xBase Index Manifest", manifest);
    Assert.Contains("RecordCount: 2", manifest);
    Assert.Contains("Fields: CODE", manifest);

    IReadOnlyList<SchemaBackfillTask> queue = await mutator.ReadBackfillQueueAsync(tableName).ConfigureAwait(false);
    Assert.Empty(queue);
  }

  [Fact]
  public async Task ReindexAsync_WithStaleIndex_RewritesManifest()
  {
    using var workspace = new TemporaryWorkspace();
    string tableName = "orders";
    string dbfPath = DbfTestBuilder.CreateTable(
      workspace.DirectoryPath,
      tableName,
      (false, "B001"),
      (false, "B002"));
    string indexPath = DbfTestBuilder.CreateIndex(workspace.DirectoryPath, tableName + ".ntx", "stale");
    var mutator = new SchemaMutator(workspace.DirectoryPath);

    int rebuilt = await mutator.ReindexAsync(tableName).ConfigureAwait(false);
    Assert.Equal(1, rebuilt);

    string manifest = File.ReadAllText(indexPath);
    Assert.Contains("xBase Index Manifest", manifest);
    Assert.Contains("RecordCount: 2", manifest);
    Assert.Contains($"Table: {tableName}", manifest);

    IReadOnlyList<SchemaBackfillTask> queue = await mutator.ReadBackfillQueueAsync(tableName).ConfigureAwait(false);
    Assert.Empty(queue);

    var loader = new DbfTableLoader();
    DbfTableDescriptor descriptor = loader.LoadDbf(dbfPath);
    Assert.Equal<uint>(2u, descriptor.RecordCount);
  }
}
