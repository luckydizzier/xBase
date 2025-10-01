using System;
using System.Buffers;
using System.Collections.Generic;
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
    var records = new List<ReadOnlySequence<byte>>
    {
      CreateRecord(2, "Bob")
    };

    var cursorFactory = new FakeCursorFactory(records);
    var resolver = new FakeTableResolver();
    resolver.Register(
      "SELECT Name FROM Customers WHERE Id = 2",
      new TableResolveResult(
        new TableDescriptor("Customers", null, Array.Empty<IFieldDescriptor>(), Array.Empty<IIndexDescriptor>(), SchemaVersion.Start),
        new[]
        {
          new TableColumn("Name", typeof(string), sequence =>
          {
            byte[] data = sequence.ToArray();
            return Encoding.UTF8.GetString(data, 4, data.Length - 4).TrimEnd('\0');
          })
        },
        new CursorOptions(false, null, null)));

    var connection = new XBaseConnection(cursorFactory, new NoOpJournal(), new NoOpSchemaMutator(), resolver);

    var services = new ServiceCollection();
    services.AddEntityFrameworkXBase();
    services.AddScoped(_ => connection);
    services.AddScoped<ITableResolver>(_ => resolver);

    ServiceProvider provider = services.BuildServiceProvider();

    var optionsBuilder = new DbContextOptionsBuilder<SampleContext>();
    optionsBuilder.UseXBase("xbase://path=memory");
    optionsBuilder.UseInternalServiceProvider(provider);

    using var context = new SampleContext(optionsBuilder.Options);
    var relationalConnection = context.GetService<IRelationalConnection>();
    relationalConnection.Open();

    try
    {
      using var command = relationalConnection.DbConnection.CreateCommand();
      command.CommandText = "SELECT Name FROM Customers WHERE Id = 2";

      using var reader = command.ExecuteReader();
      Assert.NotNull(reader);
      Assert.True(reader.HasRows);
      Assert.True(reader.Read());
      Assert.Equal("Name", reader.GetName(0));
      Assert.Equal("Bob", reader.GetString(0));
      Assert.False(reader.Read());
    }
    finally
    {
      relationalConnection.Close();
    }
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

  private static ReadOnlySequence<byte> CreateRecord(int id, string name)
  {
    byte[] buffer = new byte[4 + 16];
    BitConverter.GetBytes(id).CopyTo(buffer, 0);
    byte[] nameBytes = Encoding.UTF8.GetBytes(name);
    Array.Copy(nameBytes, 0, buffer, 4, Math.Min(nameBytes.Length, 16));
    return new ReadOnlySequence<byte>(buffer);
  }

  private sealed class FakeCursorFactory : ICursorFactory
  {
    private readonly IReadOnlyList<ReadOnlySequence<byte>> _records;

    public FakeCursorFactory(IReadOnlyList<ReadOnlySequence<byte>> records)
    {
      _records = records;
    }

    public ValueTask<ICursor> CreateSequentialAsync(ITableDescriptor table, CursorOptions options, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return ValueTask.FromResult<ICursor>(new FakeCursor(_records));
    }

    public ValueTask<ICursor> CreateIndexedAsync(ITableDescriptor table, IIndexDescriptor index, CursorOptions options, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return ValueTask.FromResult<ICursor>(new FakeCursor(_records));
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

  private sealed class FakeTableResolver : ITableResolver
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
