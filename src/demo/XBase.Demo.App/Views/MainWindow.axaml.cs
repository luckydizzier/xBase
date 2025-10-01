using System;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using XBase.Demo.App.ViewModels;

namespace XBase.Demo.App.Views;

public partial class MainWindow : ReactiveWindow<ShellViewModel>
{
  public MainWindow()
      : this(ResolveViewModel())
  {
  }

  public MainWindow(ShellViewModel viewModel)
  {
    InitializeComponent();
    DataContext = viewModel;
  }

  private static ShellViewModel ResolveViewModel()
  {
    var services = App.Services ?? throw new InvalidOperationException("Service provider is not available for MainWindow.");
    return services.GetRequiredService<ShellViewModel>();
  }

  private void InitializeComponent()
  {
    AvaloniaXamlLoader.Load(this);
  }
}
