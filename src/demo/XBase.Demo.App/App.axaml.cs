using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using XBase.Demo.App.Views;

namespace XBase.Demo.App;

public partial class App : Application
{
  public static IServiceProvider? Services { get; set; }

  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      var services = Services ?? throw new InvalidOperationException("Service provider is not initialized.");
      desktop.MainWindow = services.GetRequiredService<MainWindow>();
    }

    base.OnFrameworkInitializationCompleted();
  }
}
