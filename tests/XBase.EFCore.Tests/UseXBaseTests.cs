using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using XBase.Abstractions;
using XBase.Core.Table;
using XBase.Core.Transactions;
using XBase.Data.Providers;
using XBase.EFCore.Extensions;
using XBase.EFCore.Internal;

namespace XBase.EFCore.Tests;

public sealed class UseXBaseTests
{
  [Fact]
  public void UseXBase_StoresConnectionString()
  {
    var optionsBuilder = new DbContextOptionsBuilder<SampleContext>();

    optionsBuilder.UseXBase("path=./data");

    var extension = optionsBuilder.Options.FindExtension<XBaseOptionsExtension>();

    Assert.NotNull(extension);
    Assert.Equal("path=./data", extension!.ConnectionString);
  }

  [Fact]
  public void UseXBase_AllowsQueryExecution()
  {
    var descriptor = new TableDescriptor(
      "Customers",
      null,
      new IFieldDescriptor[]
      {
        new FieldDescriptor("Id", "N", 4, 0, false),
        new FieldDescriptor("Name", "C", 10, 0, true)
      },
      Array.Empty<IIndexDescriptor>(),
      SchemaVersion.Start);

    var resolver = new InMemoryTableResolver(descriptor);
    var cursorFactory = new FakeCursorFactory(new()
    {
      ["Customers"] = new[]
      {
        CreateRecord(1, "Alice"),
        CreateRecord(2, "Bob")
      }
    });

    var connection = new XBaseConnection(cursorFactory, new NoOpJournal(), new NoOpSchemaMutator(), resolver);

    var services = new ServiceCollection();
    services.AddEntityFrameworkXBase();
    services.AddScoped(_ => connection);

    ServiceProvider provider = services.BuildServiceProvider();

    var optionsBuilder = new DbContextOptionsBuilder<SampleContext>();
    optionsBuilder.UseXBase("Data Source=memory");
    optionsBuilder.UseInternalServiceProvider(provider);

    using var context = new SampleContext(optionsBuilder.Options);
    var relationalConnection = context.GetService<IRelationalConnection>();
    relationalConnection.Open();

    try
    {
      using var command = relationalConnection.DbConnection.CreateCommand();
      command.CommandText = "SELECT Name FROM Customers WHERE Id = 2";

      using var reader = command.ExecuteReader();
      var names = new List<string?>();
      while (reader.Read())
      {
        names.Add(reader.GetString(0));
      }

      Assert.Equal(new[] { "Bob" }, names);
    }
    finally
    {
      relationalConnection.Close();
    }
  }

  private static ReadOnlySequence<byte> CreateRecord(int id, string name)
  {
    byte[] buffer = new byte[1 + 4 + 10];
    buffer[0] = 0x20;
    Encoding ascii = Encoding.ASCII;
    ascii.GetBytes(id.ToString(CultureInfo.InvariantCulture).PadLeft(4, ' '), buffer.AsSpan(1, 4));
    ascii.GetBytes(name.PadRight(10), buffer.AsSpan(5, 10));
    return new ReadOnlySequence<byte>(buffer);
  }

  private sealed class SampleContext : DbContext
  {
    public SampleContext(DbContextOptions options)
      : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      var entity = modelBuilder.Entity<Customer>();
      entity.ToTable("Customers");
      entity.HasKey(customer => customer.Id);
      entity.Property(customer => customer.Name).HasColumnName("Name");
    }
  }

  private sealed class Customer
  {
    public int Id { get; set; }

    public string? Name { get; set; }
  }

  private sealed class InMemoryTableResolver : ITableResolver
  {
    private readonly Dictionary<string, ITableDescriptor> _tables;

    public InMemoryTableResolver(params ITableDescriptor[] tables)
    {
      _tables = tables.ToDictionary(table => table.Name, table => table, StringComparer.OrdinalIgnoreCase);
    }

    public ValueTask<ITableDescriptor?> ResolveAsync(string tableName, CancellationToken cancellationToken = default)
    {
      _tables.TryGetValue(tableName, out ITableDescriptor? descriptor);
      return ValueTask.FromResult<ITableDescriptor?>(descriptor);
    }
  }

  private sealed class FakeCursorFactory : ICursorFactory
  {
    private readonly Dictionary<string, IReadOnlyList<ReadOnlySequence<byte>>> _records;

    public FakeCursorFactory(Dictionary<string, IReadOnlyList<ReadOnlySequence<byte>>> records)
    {
      _records = records;
    }

    public ValueTask<ICursor> CreateSequentialAsync(ITableDescriptor table, CursorOptions options, CancellationToken cancellationToken = default)
    {
      IReadOnlyList<ReadOnlySequence<byte>> records = _records.TryGetValue(table.Name, out var value)
        ? value
        : Array.Empty<ReadOnlySequence<byte>>();

      return ValueTask.FromResult<ICursor>(new FakeCursor(records));
    }

    public ValueTask<ICursor> CreateIndexedAsync(ITableDescriptor table, IIndexDescriptor index, CursorOptions options, CancellationToken cancellationToken = default)
    {
      return CreateSequentialAsync(table, options, cancellationToken);
    }
  }

  private sealed class FakeCursor : ICursor
  {
    private readonly IReadOnlyList<ReadOnlySequence<byte>> _records;
    private int _position = -1;

    public FakeCursor(IReadOnlyList<ReadOnlySequence<byte>> records)
    {
      _records = records;
    }

    public ReadOnlySequence<byte> Current => _records[_position];

    public ValueTask DisposeAsync()
    {
      return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      _position++;
      return ValueTask.FromResult(_position < _records.Count);
    }
  }

  private sealed class NoOpSchemaMutator : ISchemaMutator
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
