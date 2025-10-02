using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XBase.Core.Cursors;
using XBase.Core.Table;
using XBase.Demo.Domain.Services;
using XBase.Demo.Infrastructure.Catalog;
using XBase.Demo.Infrastructure.Indexes;
using XBase.Demo.Infrastructure.Recovery;
using XBase.Demo.Infrastructure.Schema;
using XBase.Demo.Infrastructure.Seed;
using XBase.Expressions.Evaluation;

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
    services.AddSingleton<DbfTableLoader>();
    services.AddSingleton<DbfCursorFactory>();
    services.AddSingleton<ExpressionEvaluator>();
    services.AddSingleton<ITableCatalogService, FileSystemTableCatalogService>();
    services.AddSingleton<ITablePageService, DbfTablePageService>();
    services.AddSingleton<ISchemaDdlService, TemplateSchemaDdlService>();
    services.AddSingleton<IIndexManagementService, FileSystemIndexManagementService>();
    services.AddSingleton<ICsvImportService, FileSystemCsvImportService>();
    services.AddSingleton<IRecoveryWorkflowService, FileSystemRecoveryWorkflowService>();

    return services;
  }
}
