using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Cursors;
using XBase.Data.Providers;
using XBase.Core.Transactions;

namespace XBase.Data.Tests;

public sealed class XBaseCommandTests
{
  [Fact]
  public void ExecuteNonQuery_WithCreateTable_InvokesSchemaMutator()
  {
    var schemaMutator = new RecordingSchemaMutator();
    using var connection = new XBaseConnection(new NoOpCursorFactory(), new NoOpJournal(), schemaMutator);
    connection.Open();

    using DbCommand command = connection.CreateCommand();
    command.CommandText = "CREATE TABLE Customers (Id INT)";

    int affected = command.ExecuteNonQuery();

    Assert.Equal(0, affected);
    Assert.Single(schemaMutator.Operations);
    Assert.Equal(SchemaOperationKind.CreateTable, schemaMutator.Operations[0].Kind);
    Assert.Equal<ulong>(1, schemaMutator.Versions[^1].Value);
  }

  [Fact]
  public void ExecuteScalar_ReturnsSchemaVersion()
  {
    var schemaMutator = new RecordingSchemaMutator();
    using var connection = new XBaseConnection(new NoOpCursorFactory(), new NoOpJournal(), schemaMutator);
    connection.Open();

    using DbCommand command = connection.CreateCommand();
    command.CommandText = "DROP TABLE Customers";

    object? result = command.ExecuteScalar();

    Assert.IsType<SchemaVersion>(result);
    Assert.Single(schemaMutator.Operations);
    Assert.Equal(SchemaOperationKind.DropTable, schemaMutator.Operations[0].Kind);
  }

  private sealed class RecordingSchemaMutator : ISchemaMutator
  {
    private SchemaVersion _current = SchemaVersion.Start;

    public List<SchemaOperation> Operations { get; } = new();

    public List<SchemaVersion> Versions { get; } = new();

    public ValueTask<SchemaVersion> ExecuteAsync(
      SchemaOperation operation,
      string? author = null,
      CancellationToken cancellationToken = default)
    {
      Operations.Add(operation);
      _current = _current.Next();
      Versions.Add(_current);
      return ValueTask.FromResult(_current);
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
