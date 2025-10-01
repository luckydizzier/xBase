using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Ddl;

namespace XBase.Tools.Tests;

public sealed class DdlCommandTests
{
  [Fact]
  public async Task ApplyCommand_WritesSchemaLog()
  {
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    string toolAssembly = Path.Combine(repoRoot, "src", "XBase.Tools", "bin", "Release", "net8.0", "XBase.Tools.dll");
    Assert.True(File.Exists(toolAssembly));
    string workspace = Path.Combine(Path.GetTempPath(), $"xbase-ddl-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workspace);

    try
    {
      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "dotnet",
          ArgumentList =
          {
            toolAssembly,
            "ddl",
            "apply",
            workspace,
            "customers",
            "create-table",
            "definition=name C(10)",
            "--author",
            "cli"
          },
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false,
          WorkingDirectory = repoRoot
        }
      };

      process.Start();
      Task<string> stdout = process.StandardOutput.ReadToEndAsync();
      Task<string> stderr = process.StandardError.ReadToEndAsync();
      await process.WaitForExitAsync();

      string output = await stdout;
      string error = await stderr;
      Assert.True(process.ExitCode == 0, $"Command failed: {error}");
      Assert.Contains("Applied", output);

      var log = new SchemaLog(Path.Combine(workspace, "customers.ddl"));
      IReadOnlyList<SchemaLogEntry> entries = log.ReadEntries();
      Assert.Single(entries);
      Assert.Equal(SchemaOperationKind.CreateTable, entries[0].Kind);
    }
    finally
    {
      if (Directory.Exists(workspace))
      {
        Directory.Delete(workspace, recursive: true);
      }
    }
  }

  [Fact]
  public async Task CheckpointDryRun_PrintsStatus()
  {
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    string toolAssembly = Path.Combine(repoRoot, "src", "XBase.Tools", "bin", "Release", "net8.0", "XBase.Tools.dll");
    Assert.True(File.Exists(toolAssembly));
    string workspace = Path.Combine(Path.GetTempPath(), $"xbase-ddl-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workspace);

    try
    {
      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "dotnet",
          ArgumentList =
          {
            toolAssembly,
            "ddl",
            "checkpoint",
            workspace,
            "customers",
            "--dry-run"
          },
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false,
          WorkingDirectory = repoRoot
        }
      };

      process.Start();
      Task<string> stdout = process.StandardOutput.ReadToEndAsync();
      Task<string> stderr = process.StandardError.ReadToEndAsync();
      await process.WaitForExitAsync();

      string output = await stdout;
      string error = await stderr;
      Assert.True(process.ExitCode == 0, $"Command failed: {error}");
      Assert.Contains("[dry-run] checkpoint", output);
    }
    finally
    {
      if (Directory.Exists(workspace))
      {
        Directory.Delete(workspace, recursive: true);
      }
    }
  }

  [Fact]
  public async Task PackDryRun_PrintsStatus()
  {
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    string toolAssembly = Path.Combine(repoRoot, "src", "XBase.Tools", "bin", "Release", "net8.0", "XBase.Tools.dll");
    Assert.True(File.Exists(toolAssembly));
    string workspace = Path.Combine(Path.GetTempPath(), $"xbase-ddl-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workspace);

    try
    {
      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "dotnet",
          ArgumentList =
          {
            toolAssembly,
            "ddl",
            "pack",
            workspace,
            "customers",
            "--dry-run"
          },
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false,
          WorkingDirectory = repoRoot
        }
      };

      process.Start();
      Task<string> stdout = process.StandardOutput.ReadToEndAsync();
      Task<string> stderr = process.StandardError.ReadToEndAsync();
      await process.WaitForExitAsync();

      string output = await stdout.ConfigureAwait(false);
      string error = await stderr.ConfigureAwait(false);
      Assert.True(process.ExitCode == 0, $"Command failed: {error}");
      Assert.Contains("[dry-run] pack", output);

      var mutator = new SchemaMutator(workspace);
      IReadOnlyList<SchemaBackfillTask> after = await mutator.ReadBackfillQueueAsync("customers").ConfigureAwait(false);
      Assert.Empty(after);
    }
    finally
    {
      if (Directory.Exists(workspace))
      {
        Directory.Delete(workspace, recursive: true);
      }
    }
  }
}
