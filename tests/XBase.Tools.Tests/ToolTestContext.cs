using System;
using System.IO;

namespace XBase.Tools.Tests;

internal static class ToolTestContext
{
  public static string RepoRoot { get; } =
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

  public static string BuildConfiguration
  {
    get
    {
      var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
      string? configuration = baseDirectory.Parent?.Name;
      if (string.IsNullOrEmpty(configuration))
      {
        throw new InvalidOperationException("Unable to determine build configuration for test execution.");
      }

      return configuration;
    }
  }

  public static string GetToolAssemblyPath()
  {
    string toolAssembly = Path.Combine(
      RepoRoot,
      "src",
      "XBase.Tools",
      "bin",
      BuildConfiguration,
      "net8.0",
      "XBase.Tools.dll");

    return toolAssembly;
  }
}
