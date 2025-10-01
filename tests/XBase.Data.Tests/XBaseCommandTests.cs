using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Cursors;
using XBase.Core.Table;
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

  [Fact]
  public void ExecuteReader_WithResolver_ReturnsRows()
  {
    var records = new List<ReadOnlySequence<byte>>
    {
      CreateRecord(1, "Alice"),
      CreateRecord(2, "Bob")
    };

    var cursorFactory = new StubCursorFactory(records);
    var resolver = new StubTableResolver();
    resolver.Register(
      "SELECT * FROM Customers",
      new TableResolveResult(
        new TableDescriptor("Customers", null, Array.Empty<IFieldDescriptor>(), Array.Empty<IIndexDescriptor>(), SchemaVersion.Start),
        new[]
        {
          new TableColumn("Id", typeof(int), sequence =>
          {
            byte[] data = sequence.ToArray();
            return BitConverter.ToInt32(data, 0);
          }),
          new TableColumn("Name", typeof(string), sequence =>
          {
            byte[] data = sequence.ToArray();
            return Encoding.UTF8.GetString(data, 4, data.Length - 4).TrimEnd('\0');
          })
        },
        new CursorOptions(false, null, null)));

    using var connection = new XBaseConnection(cursorFactory, new NoOpJournal(), new NoOpSchemaMutator(), resolver);
    connection.Open();

    using DbCommand command = connection.CreateCommand();
    command.CommandText = "SELECT * FROM Customers";

    using DbDataReader reader = command.ExecuteReader();

    Assert.Equal(2, reader.FieldCount);
    Assert.True(reader.HasRows);
    Assert.Equal("Id", reader.GetName(0));
    Assert.Equal(typeof(int), reader.GetFieldType(0));
    Assert.True(reader.Read());
    Assert.Equal(1, reader.GetInt32(0));
    Assert.Equal("Alice", reader.GetString(1));
    Assert.True(reader.Read());
    Assert.Equal(2, reader.GetInt32(0));
    Assert.Equal("Bob", reader.GetString(1));
    Assert.False(reader.Read());
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

    public ValueTask<int> PackAsync(string tableName, CancellationToken cancellationToken = default)
    {
      return ValueTask.FromResult(0);
    }

    public ValueTask<int> ReindexAsync(string tableName, CancellationToken cancellationToken = default)
    {
      return ValueTask.FromResult(0);
    }
  }

  private static ReadOnlySequence<byte> CreateRecord(int id, string name)
  {
    byte[] buffer = new byte[4 + 16];
    BitConverter.GetBytes(id).CopyTo(buffer, 0);
    byte[] nameBytes = Encoding.UTF8.GetBytes(name);
    Array.Copy(nameBytes, 0, buffer, 4, Math.Min(nameBytes.Length, 16));
    return new ReadOnlySequence<byte>(buffer);
  }

  private sealed class StubCursorFactory : ICursorFactory
  {
    private readonly IReadOnlyList<ReadOnlySequence<byte>> _records;

    public StubCursorFactory(IReadOnlyList<ReadOnlySequence<byte>> records)
    {
      _records = records;
    }

    public ValueTask<ICursor> CreateSequentialAsync(ITableDescriptor table, CursorOptions options, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return ValueTask.FromResult<ICursor>(new StubCursor(_records));
    }

    public ValueTask<ICursor> CreateIndexedAsync(ITableDescriptor table, IIndexDescriptor index, CursorOptions options, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return ValueTask.FromResult<ICursor>(new StubCursor(_records));
    }
  }

  private sealed class StubCursor : ICursor
  {
    private readonly IReadOnlyList<ReadOnlySequence<byte>> _records;
    private int _position = -1;

    public StubCursor(IReadOnlyList<ReadOnlySequence<byte>> records)
    {
      _records = records;
    }

    public ReadOnlySequence<byte> Current { get; private set; }

    public ValueTask DisposeAsync()
    {
      return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _position++;
      if (_position >= _records.Count)
      {
        return ValueTask.FromResult(false);
      }

      Current = _records[_position];
      return ValueTask.FromResult(true);
    }
  }

  private sealed class StubTableResolver : ITableResolver
  {
    private readonly Dictionary<string, TableResolveResult> _results = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string commandText, TableResolveResult result)
    {
      _results[commandText] = result;
    }

    public ValueTask<TableResolveResult?> ResolveAsync(string commandText, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (_results.TryGetValue(commandText, out TableResolveResult? result) && result is not null)
      {
        return ValueTask.FromResult<TableResolveResult?>(result);
      }

      return ValueTask.FromResult<TableResolveResult?>(default);
    }
  }
}
