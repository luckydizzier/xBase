using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XBase.Demo.App.DependencyInjection;

namespace XBase.Demo.App;

public static class Program
{
  [STAThread]
  public static int Main(string[] args)
  {
    using var host = CreateHost(args);
    host.Start();

    App.Services = host.Services;

    try
    {
      return BuildAvaloniaApp()
          .AfterSetup(_ => App.Services = host.Services)
          .StartWithClassicDesktopLifetime(args);
    }
    finally
    {
      host.StopAsync().GetAwaiter().GetResult();
    }
  }

  private static IHost CreateHost(string[] args)
  {
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddDemoApp();
    return builder.Build();
  }

  private static AppBuilder BuildAvaloniaApp()
      => AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .LogToTrace()
          .UseReactiveUI();
}
