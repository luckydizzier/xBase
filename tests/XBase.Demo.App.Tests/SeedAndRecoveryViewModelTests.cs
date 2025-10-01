using System;
using System.IO;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using XBase.Demo.App.ViewModels;
using XBase.Demo.Domain.Catalog;
using XBase.Demo.Domain.Diagnostics;

namespace XBase.Demo.App.Tests;

public sealed class SeedAndRecoveryViewModelTests
{
  [Fact]
  public async Task ImportCsvCommand_DetectsEncodingAndGeneratesReport()
  {
    using var catalog = new TempCatalog();
    catalog.AddTable("PRODUCTS");

    var csvPath = Path.Combine(catalog.Path, "products.csv");
    await File.WriteAllLinesAsync(csvPath, new[]
    {
      "Id,Name",
      "1,Widget",
      "2,Gadget"
    }, Encoding.UTF8);

    using var host = DemoHostFactory.CreateHost();
    await host.StartAsync();

    try
    {
      var viewModel = host.Services.GetRequiredService<SeedAndRecoveryViewModel>();
      var tableModel = new TableModel("PRODUCTS", Path.Combine(catalog.Path, "PRODUCTS.dbf"), Array.Empty<IndexModel>());
      var tableVm = new TableListItemViewModel(tableModel);
      viewModel.SetCatalogContext(catalog.Path, new[] { tableVm });
      viewModel.SetTargetTable(tableVm);

      var result = await viewModel.ImportCsvCommand.Execute(csvPath).ToTask();

      Assert.True(result.Succeeded);
      Assert.Equal(2, viewModel.LastImportedRowCount);
      Assert.Equal("utf-8", viewModel.LastDetectedEncoding);
      Assert.False(string.IsNullOrWhiteSpace(viewModel.LastImportReportPath));
      Assert.True(File.Exists(viewModel.LastImportReportPath!));
      Assert.Contains("Imported 2 rows", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);

      var telemetry = host.Services.GetRequiredService<IDemoTelemetrySink>();
      Assert.Contains(telemetry.GetSnapshot(), evt => evt.Name == "CsvImportCompleted");
    }
    finally
    {
      await host.StopAsync();
    }
  }

  [Fact]
  public async Task SimulateCrashAndReplay_WorkflowCreatesArtifacts()
  {
    using var catalog = new TempCatalog();
    catalog.AddTable("ORDERS");

    using var host = DemoHostFactory.CreateHost();
    await host.StartAsync();

    try
    {
      var viewModel = host.Services.GetRequiredService<SeedAndRecoveryViewModel>();
      var tableModel = new TableModel("ORDERS", Path.Combine(catalog.Path, "ORDERS.dbf"), Array.Empty<IndexModel>());
      var tableVm = new TableListItemViewModel(tableModel);
      viewModel.SetCatalogContext(catalog.Path, new[] { tableVm });
      viewModel.SetTargetTable(tableVm);

      var crashResult = await viewModel.SimulateCrashCommand.Execute(Unit.Default).ToTask();
      Assert.True(crashResult.Succeeded);
      Assert.True(viewModel.HasPendingCrash);
      Assert.False(string.IsNullOrWhiteSpace(viewModel.CrashMarkerPath));
      Assert.True(File.Exists(viewModel.CrashMarkerPath!));

      var replayResult = await viewModel.ReplayRecoveryCommand.Execute(Unit.Default).ToTask();
      Assert.True(replayResult.Succeeded);
      Assert.False(viewModel.HasPendingCrash);
      Assert.False(File.Exists(Path.Combine(catalog.Path, "ORDERS.crash")));
      Assert.False(string.IsNullOrWhiteSpace(viewModel.RecoveryReportPath));
      Assert.True(File.Exists(viewModel.RecoveryReportPath!));
    }
    finally
    {
      await host.StopAsync();
    }
  }

  [Fact]
  public async Task ExportSupportPackageCommand_ExportsDiagnosticBundle()
  {
    using var catalog = new TempCatalog();
    catalog.AddTable("CUSTOMERS");

    using var host = DemoHostFactory.CreateHost();
    await host.StartAsync();

    try
    {
      var viewModel = host.Services.GetRequiredService<SeedAndRecoveryViewModel>();
      var tableModel = new TableModel("CUSTOMERS", Path.Combine(catalog.Path, "CUSTOMERS.dbf"), Array.Empty<IndexModel>());
      var tableVm = new TableListItemViewModel(tableModel);
      viewModel.SetCatalogContext(catalog.Path, new[] { tableVm });
      viewModel.SetTargetTable(tableVm);

      var packageResult = await viewModel.ExportSupportPackageCommand.Execute(Unit.Default).ToTask();

      Assert.True(packageResult.Succeeded);
      Assert.False(string.IsNullOrWhiteSpace(viewModel.SupportPackagePath));
      Assert.True(File.Exists(viewModel.SupportPackagePath!));
      Assert.Contains("Support package", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
      await host.StopAsync();
    }
  }
}
