using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XBase.Demo.Domain.Services;
using XBase.Demo.Infrastructure.Catalog;
using XBase.Demo.Infrastructure.Indexes;
using XBase.Demo.Infrastructure.Schema;

namespace XBase.Demo.Infrastructure;

/// <summary>
/// Dependency injection helpers for the demo infrastructure layer.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Registers services necessary for the Phase A demo scenarios.
  /// </summary>
  public static IServiceCollection AddXBaseDemoInfrastructure(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddLogging(builder => builder.AddDebug());
    services.AddSingleton<ITableCatalogService, FileSystemTableCatalogService>();
    services.AddSingleton<ITablePageService, NullTablePageService>();
    services.AddSingleton<ISchemaDdlService, TemplateSchemaDdlService>();
    services.AddSingleton<IIndexManagementService, FileSystemIndexManagementService>();

    return services;
  }
}
