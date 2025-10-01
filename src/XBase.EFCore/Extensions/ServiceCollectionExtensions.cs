using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
      .TryAdd<IQuerySqlGeneratorFactory, XBaseQuerySqlGeneratorFactory>()
      .TryAdd<IModificationCommandBatchFactory, NoOpModificationCommandBatchFactory>();

    services.TryAddScoped<IRelationalConnection>(provider =>
    {
      var options = provider.GetRequiredService<IDbContextOptions>();
      var extension = options.FindExtension<XBaseOptionsExtension>();
      var dependencies = provider.GetRequiredService<RelationalConnectionDependencies>();
      XBaseConnection? connection = provider.GetService<XBaseConnection>();
      return new XBaseRelationalConnection(dependencies, connection, extension?.ConnectionString);
    });

    return services;
  }
}
