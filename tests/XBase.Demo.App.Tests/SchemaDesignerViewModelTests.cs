using System;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using XBase.Demo.App.ViewModels;
using XBase.Demo.Domain.Schema;

namespace XBase.Demo.App.Tests;

public sealed class SchemaDesignerViewModelTests
{
  [Fact]
  public async Task GenerateCreatePreviewCommand_WithColumns_ProducesScript()
  {
    using var host = DemoHostFactory.CreateHost();
    await host.StartAsync();

    try
    {
      var viewModel = host.Services.GetRequiredService<SchemaDesignerViewModel>();
      viewModel.TableName = "PRODUCTS";
      viewModel.SetColumns(
          new SchemaColumnViewModel("ID", "NUMERIC", allowNulls: false, length: 8, scale: 0),
          new SchemaColumnViewModel("NAME", "CHAR", length: 50));

      var preview = await viewModel.GenerateCreatePreviewCommand.Execute().ToTask();

      Assert.Equal("CreateTable", preview.Operation);
      Assert.Contains("CREATE TABLE PRODUCTS", viewModel.PreviewUpScript, StringComparison.OrdinalIgnoreCase);
      Assert.Contains("DROP TABLE PRODUCTS", viewModel.PreviewDownScript, StringComparison.OrdinalIgnoreCase);
      Assert.Null(viewModel.ErrorMessage);
    }
    finally
    {
      await host.StopAsync();
    }
  }

  [Fact]
  public async Task GenerateAlterPreviewCommand_WithAddChange_ProducesScript()
  {
    using var host = DemoHostFactory.CreateHost();
    await host.StartAsync();

    try
    {
      var viewModel = host.Services.GetRequiredService<SchemaDesignerViewModel>();
      viewModel.TableName = "CUSTOMERS";
      viewModel.SetAlterations(
          new SchemaColumnChangeViewModel(
              ColumnChangeOperation.Add,
              "EMAIL",
              new SchemaColumnViewModel("EMAIL", "CHAR", length: 60)));

      var preview = await viewModel.GenerateAlterPreviewCommand.Execute().ToTask();

      Assert.Equal("AlterTable", preview.Operation);
      Assert.Contains("ADD COLUMN EMAIL", viewModel.PreviewUpScript, StringComparison.OrdinalIgnoreCase);
      Assert.Contains("DROP COLUMN EMAIL", viewModel.PreviewDownScript, StringComparison.OrdinalIgnoreCase);
      Assert.Null(viewModel.ErrorMessage);
    }
    finally
    {
      await host.StopAsync();
    }
  }

  [Fact]
  public async Task GenerateDropPreviewCommand_WithTable_SetsPreview()
  {
    using var host = DemoHostFactory.CreateHost();
    await host.StartAsync();

    try
    {
      var viewModel = host.Services.GetRequiredService<SchemaDesignerViewModel>();
      viewModel.TableName = "ORDERS";

      var preview = await viewModel.GenerateDropPreviewCommand.Execute().ToTask();

      Assert.Equal("DropTable", preview.Operation);
      Assert.Contains("DROP TABLE ORDERS", viewModel.PreviewUpScript, StringComparison.OrdinalIgnoreCase);
      Assert.Contains("CREATE", viewModel.PreviewDownScript, StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
      await host.StopAsync();
    }
  }
}
