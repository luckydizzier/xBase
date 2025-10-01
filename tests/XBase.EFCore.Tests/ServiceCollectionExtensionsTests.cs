using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using XBase.EFCore.Extensions;

namespace XBase.EFCore.Tests;

public sealed class ServiceCollectionExtensionsTests
{
  [Fact]
  public void AddEntityFrameworkXBase_RegistersQuerySqlGeneratorFactory()
  {
    var services = new ServiceCollection();

    services.AddEntityFrameworkXBase();

    using ServiceProvider provider = services.BuildServiceProvider();
    var factory = provider.GetRequiredService<IQuerySqlGeneratorFactory>();

    Assert.Equal("XBaseQuerySqlGeneratorFactory", factory.GetType().Name);
    Assert.IsType<QuerySqlGenerator>(factory.Create());
  }
}
