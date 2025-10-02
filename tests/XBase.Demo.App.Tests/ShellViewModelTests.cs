using System;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using XBase.Demo.App.ViewModels;

namespace XBase.Demo.App.Tests;

public sealed class ShellViewModelTests
{
  [Fact]
  public async Task OpenCatalogCommand_LoadsTablesAndTelemetry()
  {
    using var catalog = new TempCatalog();
    catalog.AddTable("CUSTOMERS");

    using var host = DemoHostFactory.CreateHost();
    await host.StartAsync();

    try
    {
      var viewModel = host.Services.GetRequiredService<ShellViewModel>();

      await viewModel.OpenCatalogCommand.Execute(catalog.Path).ToTask();

      Assert.Equal(catalog.Path, viewModel.CatalogRoot);
      var table = Assert.Single(viewModel.Tables);
      Assert.Equal("CUSTOMERS", table.Name);
      Assert.NotNull(viewModel.SelectedTable);
      Assert.Equal(1, table.Indexes.Count);

      Assert.Contains(viewModel.TelemetryEvents, evt => evt.Name == "CatalogLoaded");
      Assert.Contains(viewModel.TelemetryEvents, evt => evt.Name == "TablePreviewLoaded");

      Assert.Equal(3, viewModel.TablePage.TotalCount);
      Assert.Equal(25, viewModel.TablePage.PageSize);
      Assert.Equal(0, viewModel.TablePage.PageNumber);
      Assert.Contains("Page 1", viewModel.TablePage.Summary, StringComparison.Ordinal);

      Assert.Equal(catalog.Path, viewModel.SeedAndRecovery.CatalogRoot);
      Assert.Equal("CUSTOMERS", viewModel.SeedAndRecovery.TargetTableName);
      Assert.False(viewModel.SeedAndRecovery.HasPendingCrash);
    }
    finally
    {
      await host.StopAsync();
    }
  }

}
