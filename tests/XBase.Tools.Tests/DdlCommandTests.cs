using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Ddl;
using XBase.Core.Table;
using XBase.TestSupport;

namespace XBase.Tools.Tests;

public sealed class DdlCommandTests
{
  [Fact]
  public async Task ApplyCommand_WritesSchemaLog()
  {
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    string buildConfiguration = GetBuildConfiguration();
    string toolAssembly = Path.Combine(repoRoot, "src", "XBase.Tools", "bin", buildConfiguration, "net8.0", "XBase.Tools.dll");
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
    string buildConfiguration = GetBuildConfiguration();
    string toolAssembly = Path.Combine(repoRoot, "src", "XBase.Tools", "bin", buildConfiguration, "net8.0", "XBase.Tools.dll");
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
    string buildConfiguration = GetBuildConfiguration();
    string toolAssembly = Path.Combine(repoRoot, "src", "XBase.Tools", "bin", buildConfiguration, "net8.0", "XBase.Tools.dll");
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

  [Fact]
  public async Task PackCommand_RewritesDbfAndClearsQueue()
  {
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    string buildConfiguration = GetBuildConfiguration();
    string toolAssembly = Path.Combine(repoRoot, "src", "XBase.Tools", "bin", buildConfiguration, "net8.0", "XBase.Tools.dll");
    Assert.True(File.Exists(toolAssembly));
    string workspacePath = Path.Combine(Path.GetTempPath(), $"xbase-ddl-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workspacePath);
    try
    {
      string tableName = "customers";
      string dbfPath = DbfTestBuilder.CreateTable(
        workspacePath,
        tableName,
        (false, "A001"),
        (true, "A002"),
        (false, "A003"));
      string indexPath = DbfTestBuilder.CreateIndex(workspacePath, tableName + ".ntx", "legacy");
      var mutator = new SchemaMutator(workspacePath);
      var addColumn = new SchemaOperation(
        SchemaOperationKind.AlterTableAddColumn,
        tableName,
        "balance",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
          ["column"] = "balance",
          ["definition"] = "N(10,2)"
        });
      await mutator.ExecuteAsync(addColumn, "cli").ConfigureAwait(false);

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
            workspacePath,
            tableName
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
      Assert.Contains("Pack completed", output);

      var loader = new DbfTableLoader();
      DbfTableDescriptor descriptor = loader.LoadDbf(dbfPath);
      Assert.Equal<uint>(2u, descriptor.RecordCount);

      string manifest = File.ReadAllText(indexPath);
      Assert.Contains("xBase Index Manifest", manifest);
      Assert.Contains("RecordCount: 2", manifest);

      IReadOnlyList<SchemaBackfillTask> queue = await mutator.ReadBackfillQueueAsync(tableName).ConfigureAwait(false);
      Assert.Empty(queue);
    }
    finally
    {
      if (Directory.Exists(workspacePath))
      {
        Directory.Delete(workspacePath, recursive: true);
      }
    }
  }

  [Fact]
  public async Task ReindexCommand_RebuildsIndexes()
  {
    string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    string buildConfiguration = GetBuildConfiguration();
    string toolAssembly = Path.Combine(repoRoot, "src", "XBase.Tools", "bin", buildConfiguration, "net8.0", "XBase.Tools.dll");
    Assert.True(File.Exists(toolAssembly));
    string workspacePath = Path.Combine(Path.GetTempPath(), $"xbase-ddl-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workspacePath);
    try
    {
      string tableName = "orders";
      DbfTestBuilder.CreateTable(
        workspacePath,
        tableName,
        (false, "B001"),
        (false, "B002"));
      string indexPath = DbfTestBuilder.CreateIndex(workspacePath, tableName + ".ntx", "stale");

      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "dotnet",
          ArgumentList =
          {
            toolAssembly,
            "ddl",
            "reindex",
            workspacePath,
            tableName
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
      Assert.Contains("Reindex completed", output);

      string manifest = File.ReadAllText(indexPath);
      Assert.Contains("xBase Index Manifest", manifest);
      Assert.Contains("RecordCount: 2", manifest);
    }
    finally
    {
      if (Directory.Exists(workspacePath))
      {
        Directory.Delete(workspacePath, recursive: true);
      }
    }
  }

  private static string GetBuildConfiguration()
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
