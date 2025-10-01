using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace XBase.Tools.Tests;

public sealed class PipelineCommandTests
{
  [Fact]
  public async Task Verify_Command_ReturnsZero()
  {
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    string projectPath = Path.Combine(repoRoot, "src", "XBase.Tools", "XBase.Tools.csproj");
    Assert.True(File.Exists(projectPath));

    using var process = new Process
    {
      StartInfo = new ProcessStartInfo
      {
        FileName = "dotnet",
        ArgumentList = { "run", "--project", projectPath, "--", "verify" },
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        WorkingDirectory = repoRoot
      }
    };

    process.Start();
    await process.WaitForExitAsync();

    string output = await process.StandardOutput.ReadToEndAsync();
    Assert.Equal(0, process.ExitCode);
    Assert.Contains("Verified", output);
  }
}
