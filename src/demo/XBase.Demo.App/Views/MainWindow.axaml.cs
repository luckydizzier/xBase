using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using System.Reactive.Disposables;
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

    this.WhenActivated(disposables =>
    {
      viewModel.SelectCatalogFolderInteraction.RegisterHandler(async interaction =>
          {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is not IStorageProvider storageProvider)
            {
              interaction.SetOutput(null);
              return;
            }

            var results = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
              AllowMultiple = false
            });

            var selected = results?.FirstOrDefault();
            interaction.SetOutput(selected?.Path.LocalPath);
          })
          .DisposeWith(disposables);

      viewModel.SeedAndRecovery.SelectCsvFileInteraction.RegisterHandler(async interaction =>
          {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is not IStorageProvider storageProvider)
            {
              interaction.SetOutput(null);
              return;
            }

            var options = new FilePickerOpenOptions
            {
              AllowMultiple = false,
              FileTypeFilter = new[]
              {
                new FilePickerFileType("CSV files")
                {
                  Patterns = new[] { "*.csv" }
                },
                FilePickerFileTypes.All
              }
            };

            var results = await storageProvider.OpenFilePickerAsync(options);
            var selected = results?.FirstOrDefault();
            interaction.SetOutput(selected?.Path.LocalPath);
          })
          .DisposeWith(disposables);
    });
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
