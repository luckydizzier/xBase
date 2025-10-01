using XBase.Abstractions;

namespace XBase.Abstractions.Tests;

public sealed class CursorOptionsTests
{
  [Fact]
  public void Constructor_AssignsProperties()
  {
    var options = new CursorOptions(true, 10, 5);

    Assert.True(options.IncludeDeleted);
    Assert.Equal(10, options.Limit);
    Assert.Equal(5, options.Offset);
  }
}
