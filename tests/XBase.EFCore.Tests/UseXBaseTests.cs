using System.Buffers;
using System.Collections.Generic;
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
    var cursorFactory = new FakeCursorFactory();
    var connection = new XBaseConnection(cursorFactory, new NoOpJournal(), new NoOpSchemaMutator());

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
      Assert.NotNull(reader);
      Assert.False(reader.HasRows);
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

  private sealed class FakeCursorFactory : ICursorFactory
  {
    public ValueTask<ICursor> CreateSequentialAsync(ITableDescriptor table, CursorOptions options, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return ValueTask.FromResult<ICursor>(new FakeCursor());
    }

    public ValueTask<ICursor> CreateIndexedAsync(ITableDescriptor table, IIndexDescriptor index, CursorOptions options, CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return ValueTask.FromResult<ICursor>(new FakeCursor());
    }
  }

  private sealed class FakeCursor : ICursor
  {
    public ReadOnlySequence<byte> Current => ReadOnlySequence<byte>.Empty;

    public ValueTask DisposeAsync()
    {
      return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ReadAsync(CancellationToken cancellationToken = default)
    {
      cancellationToken.ThrowIfCancellationRequested();
      return ValueTask.FromResult(false);
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
