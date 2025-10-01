using System.Data;
using XBase.Data.Providers;

namespace XBase.Data.Tests;

public sealed class XBaseConnectionTests
{
  [Fact]
  public void Open_SetsStateToOpen()
  {
    using var connection = new XBaseConnection();

    connection.Open();

    Assert.Equal(ConnectionState.Open, connection.State);
  }
}
