using System.IO;
using Microsoft.Extensions.Logging;
using XBase.Diagnostics.Logging;

namespace XBase.Diagnostics.Tests;

public sealed class XBaseLoggerTests
{
  [Fact]
  public void Log_WritesMessageForInformationLevel()
  {
    var logger = new XBaseLogger("Test");
    using var writer = new StringWriter();
    TextWriter original = Console.Out;
    try
    {
      Console.SetOut(writer);

      logger.LogInformation("Hello {User}", "World");
    }
    finally
    {
      Console.SetOut(original);
    }

    string output = writer.ToString();
    Assert.Contains("Hello World", output);
  }
}
