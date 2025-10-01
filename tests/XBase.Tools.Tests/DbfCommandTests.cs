using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using XBase.Core.Table;
using XBase.TestSupport;

namespace XBase.Tools.Tests;

public sealed class DbfCommandTests
{
  [Fact]
  public async Task Dbfdump_WritesCsvToDirectory()
  {
    string repoRoot = ToolTestContext.RepoRoot;
    string toolAssembly = ToolTestContext.GetToolAssemblyPath();
    Assert.True(File.Exists(toolAssembly));

    string workspace = Path.Combine(Path.GetTempPath(), $"xbase-dbfdump-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workspace);

    try
    {
      string tablePath = DbfTestBuilder.CreateTable(
        workspace,
        "customers",
        (false, "A001"),
        (true, "A002"),
        (false, "A003"));
      string outputDirectory = Path.Combine(workspace, "exports");

      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "dotnet",
          ArgumentList =
          {
            toolAssembly,
            "dbfdump",
            tablePath,
            "--output",
            outputDirectory,
            "--format",
            "csv",
            "--include-deleted"
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

      string error = await stderr;
      Assert.Equal(0, process.ExitCode);

      string csvPath = Path.Combine(outputDirectory, "customers.csv");
      Assert.True(File.Exists(csvPath), $"CSV export was not written to {csvPath}.");

      string[] lines = await File.ReadAllLinesAsync(csvPath);
      Assert.True(lines.Length >= 2, "CSV export did not contain the expected rows.");
      Assert.Equal("CODE", lines[0]);
      Assert.Contains("A001", lines[1]);
      Assert.Contains("Exported", error);
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
  public async Task DbfPack_RemovesDeletedRecords()
  {
    string repoRoot = ToolTestContext.RepoRoot;
    string toolAssembly = ToolTestContext.GetToolAssemblyPath();
    Assert.True(File.Exists(toolAssembly));

    string workspace = Path.Combine(Path.GetTempPath(), $"xbase-dbfpack-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workspace);

    try
    {
      string tablePath = DbfTestBuilder.CreateTable(
        workspace,
        "orders",
        (false, "A001"),
        (true, "A002"),
        (false, "A003"));

      var loader = new DbfTableLoader();
      DbfTableDescriptor before = loader.LoadDbf(tablePath);
      (uint totalBefore, uint deletedBefore) = CountRecords(
        tablePath,
        before.HeaderLength,
        before.RecordLength);
      Assert.Equal<uint>(3, totalBefore);
      Assert.Equal<uint>(1, deletedBefore);

      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "dotnet",
          ArgumentList = { toolAssembly, "dbfpack", tablePath },
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
      Assert.Equal(0, process.ExitCode);
      Assert.True(output.Contains("Pack completed", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("Pack completed", StringComparison.OrdinalIgnoreCase));

      DbfTableDescriptor after = loader.LoadDbf(tablePath);
      (uint totalAfter, uint deletedAfter) = CountRecords(
        tablePath,
        after.HeaderLength,
        after.RecordLength);
      Assert.Equal<uint>(2, totalAfter);
      Assert.Equal<uint>(0, deletedAfter);
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
  public async Task DbfReindex_SucceedsWithoutIndexes()
  {
    string repoRoot = ToolTestContext.RepoRoot;
    string toolAssembly = ToolTestContext.GetToolAssemblyPath();
    Assert.True(File.Exists(toolAssembly));

    string workspace = Path.Combine(Path.GetTempPath(), $"xbase-dbfreindex-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workspace);

    try
    {
      string tablePath = DbfTestBuilder.CreateTable(
        workspace,
        "inventory",
        (false, "A001"),
        (false, "A002"));

      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "dotnet",
          ArgumentList = { toolAssembly, "dbfreindex", tablePath },
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
      Assert.Equal(0, process.ExitCode);
      Assert.True(output.Contains("Reindex completed", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("Reindex completed", StringComparison.OrdinalIgnoreCase));
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
  public async Task DbfConvert_WritesToDirectoryWithDbfExtension()
  {
    string repoRoot = ToolTestContext.RepoRoot;
    string toolAssembly = ToolTestContext.GetToolAssemblyPath();
    Assert.True(File.Exists(toolAssembly));

    string workspace = Path.Combine(Path.GetTempPath(), $"xbase-dbfconvert-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workspace);

    try
    {
      string tablePath = DbfTestBuilder.CreateTable(
        workspace,
        "customers",
        (false, "A001"),
        (true, "A002"));
      string outputDirectory = Path.Combine(workspace, "converted");

      using var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "dotnet",
          ArgumentList =
          {
            toolAssembly,
            "dbfconvert",
            tablePath,
            "--output",
            outputDirectory,
            "--overwrite",
            "--drop-deleted"
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
      Assert.Equal(0, process.ExitCode);
      Assert.True(output.Contains("Converted", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("Converted", StringComparison.OrdinalIgnoreCase));

      string expectedPath = Path.Combine(outputDirectory, "customers.dbf");
      Assert.True(File.Exists(expectedPath), $"Converted DBF was not written to {expectedPath}.");

      var loader = new DbfTableLoader();
      DbfTableDescriptor converted = loader.LoadDbf(expectedPath);
      (uint total, uint deleted) = CountRecords(
        expectedPath,
        converted.HeaderLength,
        converted.RecordLength);
      Assert.Equal<uint>(0, deleted);
      Assert.Equal<uint>(1, total);
    }
    finally
    {
      if (Directory.Exists(workspace))
      {
        Directory.Delete(workspace, recursive: true);
      }
    }
  }

  private static (uint total, uint deleted) CountRecords(string path, ushort headerLength, ushort recordLength)
  {
    using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    stream.Seek(headerLength, SeekOrigin.Begin);

    if (recordLength == 0)
    {
      return (0, 0);
    }

    byte[] buffer = new byte[recordLength];
    uint total = 0;
    uint deleted = 0;

    while (true)
    {
      int read = stream.Read(buffer, 0, buffer.Length);
      if (read < buffer.Length)
      {
        break;
      }

      if (buffer[0] == 0x1A)
      {
        break;
      }

      total++;
      if (buffer[0] == 0x2A || buffer[0] == (byte)'*')
      {
        deleted++;
      }
    }

    return (total, deleted);
  }
}
