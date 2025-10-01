using System;
using System.IO;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using XBase.Demo.App.ViewModels;
using XBase.Demo.Domain.Catalog;
using XBase.Demo.Domain.Services;

namespace XBase.Demo.App.Tests;

public sealed class IndexManagerViewModelTests
{
  [Fact]
  public async Task CreateAndDropIndexCommand_ManagesPlaceholderFiles()
  {
    using var catalog = new TempCatalog();
    catalog.AddTable("PRODUCTS", addPlaceholderIndex: false);

    using var host = DemoHostFactory.CreateHost();
    await host.StartAsync();

    try
    {
      var indexManager = host.Services.GetRequiredService<IndexManagerViewModel>();
      var catalogService = host.Services.GetRequiredService<ITableCatalogService>();
      var catalogModel = await catalogService.LoadCatalogAsync(catalog.Path);
      var tableModel = Assert.Single(catalogModel.Tables);
      var tableViewModel = new TableListItemViewModel(tableModel);

      indexManager.SetTargetTable(tableViewModel);
      indexManager.IndexName = "PRODUCTS.NTX";
      indexManager.IndexExpression = "UPPER(NAME)";

      var createResult = await indexManager.CreateIndexCommand.Execute().ToTask();

      Assert.True(createResult.Succeeded, createResult.Message);
      Assert.True(File.Exists(Path.Combine(catalog.Path, "PRODUCTS.NTX")));
      Assert.Null(indexManager.ErrorMessage);
      Assert.NotNull(indexManager.StatusMessage);

      var indexViewModel = new IndexListItemViewModel(new IndexModel("PRODUCTS.NTX", "NTX"));
      var dropResult = await indexManager.DropIndexCommand.Execute(indexViewModel).ToTask();

      Assert.True(dropResult.Succeeded, dropResult.Message);
      Assert.False(File.Exists(Path.Combine(catalog.Path, "PRODUCTS.NTX")));
    }
    finally
    {
      await host.StopAsync();
    }
  }

  [Fact]
  public async Task DropIndexCommand_ForMissingIndex_SurfacesError()
  {
    using var catalog = new TempCatalog();
    catalog.AddTable("CUSTOMERS", addPlaceholderIndex: false);

    using var host = DemoHostFactory.CreateHost();
    await host.StartAsync();

    try
    {
      var indexManager = host.Services.GetRequiredService<IndexManagerViewModel>();
      var catalogService = host.Services.GetRequiredService<ITableCatalogService>();
      var catalogModel = await catalogService.LoadCatalogAsync(catalog.Path);
      var tableModel = Assert.Single(catalogModel.Tables);
      var tableViewModel = new TableListItemViewModel(tableModel);

      indexManager.SetTargetTable(tableViewModel);

      var indexViewModel = new IndexListItemViewModel(new IndexModel("CUSTOMERS.NTX", "NTX"));
      var dropResult = await indexManager.DropIndexCommand.Execute(indexViewModel).ToTask();

      Assert.False(dropResult.Succeeded);
      Assert.NotNull(indexManager.ErrorMessage);
      Assert.Contains("not found", indexManager.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
      await host.StopAsync();
    }
  }
}
