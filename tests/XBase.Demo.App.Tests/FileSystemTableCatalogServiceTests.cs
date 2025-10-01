using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using XBase.Demo.Infrastructure.Catalog;

namespace XBase.Demo.App.Tests;

public sealed class FileSystemTableCatalogServiceTests
{
  [Fact]
  public async Task LoadCatalogAsync_FindsUppercaseDbfExtensions()
  {
    using var catalog = new TempCatalog();
    var tablePath = Path.Combine(catalog.Path, "CUSTOMERS.DBF");
    await File.WriteAllBytesAsync(tablePath, Array.Empty<byte>());

    var service = new FileSystemTableCatalogService(NullLogger<FileSystemTableCatalogService>.Instance);
    var model = await service.LoadCatalogAsync(catalog.Path);

    Assert.Single(model.Tables);
    Assert.Equal("CUSTOMERS", model.Tables[0].Name);
    Assert.Equal(tablePath, model.Tables[0].Path);
  }
}
