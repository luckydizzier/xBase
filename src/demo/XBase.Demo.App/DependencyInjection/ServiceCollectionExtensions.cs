using System;
using Microsoft.Extensions.DependencyInjection;
using XBase.Demo.App.ViewModels;
using XBase.Demo.App.Views;
using XBase.Demo.Diagnostics;
using XBase.Demo.Infrastructure;

namespace XBase.Demo.App.DependencyInjection;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddDemoApp(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddXBaseDemoInfrastructure();
    services.AddXBaseDemoDiagnostics();

    services.AddSingleton<SchemaDesignerViewModel>();
    services.AddSingleton<IndexManagerViewModel>();
    services.AddSingleton<SeedAndRecoveryViewModel>();
    services.AddSingleton<ShellViewModel>();
    services.AddSingleton<MainWindow>();

    return services;
  }
}
