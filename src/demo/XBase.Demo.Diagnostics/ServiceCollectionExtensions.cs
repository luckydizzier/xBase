using System;
using Microsoft.Extensions.DependencyInjection;
using XBase.Demo.Domain.Diagnostics;

namespace XBase.Demo.Diagnostics;

/// <summary>
/// Registers diagnostics sinks for the demo experience.
/// </summary>
public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddXBaseDemoDiagnostics(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddSingleton<InMemoryTelemetrySink>();
    services.AddSingleton<IDemoTelemetrySink>(sp => sp.GetRequiredService<InMemoryTelemetrySink>());

    return services;
  }
}
