using System;
using System.IO;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using XBase.Demo.App.DependencyInjection;
using XBase.Demo.App.ViewModels;

namespace XBase.Demo.App.Tests;

public sealed class ShellViewModelTests
{
  [Fact]
  public async Task OpenCatalogCommand_LoadsTablesAndTelemetry()
  {
    using var catalog = new TempCatalog();
    catalog.AddTable("CUSTOMERS");

    using var host = CreateHost();
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

      Assert.Equal(0, viewModel.TablePage.TotalCount);
      Assert.Equal(25, viewModel.TablePage.PageSize);
      Assert.Equal(0, viewModel.TablePage.PageNumber);
      Assert.Contains("Page 1", viewModel.TablePage.Summary, StringComparison.Ordinal);
    }
    finally
    {
      await host.StopAsync();
    }
  }

  private static IHost CreateHost()
  {
    var builder = Host.CreateApplicationBuilder();
    builder.Services.AddDemoApp();
    return builder.Build();
  }

  private sealed class TempCatalog : IDisposable
  {
    private readonly DirectoryInfo _directory;

    public TempCatalog()
    {
      _directory = Directory.CreateTempSubdirectory("xbase-demo-");
    }

    public string Path => _directory.FullName;

    public void AddTable(string tableName)
    {
      ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

      var tablePath = System.IO.Path.Combine(Path, tableName + ".dbf");
      File.WriteAllBytes(tablePath, Array.Empty<byte>());

      var indexPath = System.IO.Path.Combine(Path, tableName + ".ntx");
      File.WriteAllBytes(indexPath, Array.Empty<byte>());
    }

    public void Dispose()
    {
      try
      {
        _directory.Delete(true);
      }
      catch
      {
        // best effort cleanup
      }
    }
  }
}
