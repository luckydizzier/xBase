using Microsoft.EntityFrameworkCore;
using XBase.EFCore.Extensions;

namespace XBase.EFCore.Tests;

public sealed class UseXBaseTests
{
  [Fact]
  public void UseXBase_StoresConnectionString()
  {
    var optionsBuilder = new DbContextOptionsBuilder<SampleContext>();

    optionsBuilder.UseXBase("path=./data");

    var extension = optionsBuilder.Options.FindExtension<XBaseOptionsExtension>();

    Assert.NotNull(extension);
    Assert.Equal("path=./data", extension!.ConnectionString);
  }

  private sealed class SampleContext : DbContext
  {
  }
}
