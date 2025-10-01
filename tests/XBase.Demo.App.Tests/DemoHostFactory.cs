using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XBase.Demo.App.DependencyInjection;

namespace XBase.Demo.App.Tests;

internal static class DemoHostFactory
{
  public static IHost CreateHost()
  {
    var builder = Host.CreateApplicationBuilder();
    builder.Services.AddDemoApp();
    return builder.Build();
  }
}
