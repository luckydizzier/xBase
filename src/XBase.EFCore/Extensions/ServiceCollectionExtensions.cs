using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XBase.Abstractions;
using XBase.Core.Cursors;
using XBase.Core.Transactions;
using XBase.Data.Providers;
using XBase.EFCore.Internal;

namespace XBase.EFCore.Extensions;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddEntityFrameworkXBase(this IServiceCollection services)
  {
    var builder = new EntityFrameworkRelationalServicesBuilder(services);
    builder.TryAddCoreServices();
    builder
      .TryAdd<IDatabaseProvider, XBaseDatabaseProvider>()
      .TryAdd<LoggingDefinitions, XBaseLoggingDefinitions>()
      .TryAdd<ISqlGenerationHelper, RelationalSqlGenerationHelper>()
      .TryAdd<IRelationalTypeMappingSource, XBaseTypeMappingSource>()
      .TryAdd<IModificationCommandBatchFactory, NoOpModificationCommandBatchFactory>();

    services.Replace(ServiceDescriptor.Singleton<IQuerySqlGeneratorFactory, XBaseQuerySqlGeneratorFactory>());

    services.TryAddSingleton<ICursorFactory, DbfCursorFactory>();
    services.TryAddSingleton<IJournal, NoOpJournal>();
    services.TryAddSingleton<ISchemaMutator, NoOpSchemaMutator>();
    services.TryAddScoped<ITableResolver>(provider =>
    {
      var options = provider.GetService<IDbContextOptions>();
      var extension = options?.FindExtension<XBaseOptionsExtension>();
      string? connectionString = extension?.ConnectionString;
      XBaseConnectionOptions cached = string.IsNullOrWhiteSpace(connectionString)
        ? XBaseConnectionOptions.Default
        : XBaseConnectionOptions.Parse(connectionString);

      return new SqlTableResolver(() =>
      {
        XBaseConnection? connection = provider.GetService<XBaseConnection>();
        return connection is not null ? connection.Options : cached;
      });
    });

    services.TryAddScoped<IRelationalConnection>(provider =>
    {
      var options = provider.GetRequiredService<IDbContextOptions>();
      var extension = options.FindExtension<XBaseOptionsExtension>();
      var dependencies = provider.GetRequiredService<RelationalConnectionDependencies>();
      XBaseConnection? connection = provider.GetService<XBaseConnection>();
      var cursorFactory = provider.GetRequiredService<ICursorFactory>();
      var journal = provider.GetRequiredService<IJournal>();
      var schemaMutator = provider.GetRequiredService<ISchemaMutator>();
      var tableResolver = provider.GetRequiredService<ITableResolver>();
      return new XBaseRelationalConnection(
        dependencies,
        connection,
        extension?.ConnectionString,
        cursorFactory,
        journal,
        schemaMutator,
        tableResolver);
    });

    return services;
  }
}
