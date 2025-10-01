using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Table;

var commands = new Dictionary<string, Func<Queue<string>, Task<int>>>(StringComparer.OrdinalIgnoreCase)
{
  ["verify"] = _ => VerifyAsync(),
  ["clean"] = _ => RunDotNetAsync("clean", "xBase.sln"),
  ["restore"] = _ => RunDotNetAsync("restore", "xBase.sln"),
  ["build"] = _ => RunDotNetAsync("build", "xBase.sln", "-c", "Release"),
  ["test"] = _ => RunDotNetAsync("test", "xBase.sln", "--configuration", "Release"),
  ["publish"] = _ => RunDotNetAsync("pack", "xBase.sln", "-c", "Release", "-o", "artifacts/packages"),
  ["dbfinfo"] = DbfInfoAsync
};

if (args.Length == 0)
{
  PrintUsage();
  return;
}

var argumentQueue = new Queue<string>(args);

while (argumentQueue.Count > 0)
{
  string command = argumentQueue.Dequeue();

  if (!commands.TryGetValue(command, out var handler))
  {
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintUsage();
    Environment.ExitCode = 1;
    return;
  }

  int exitCode = await handler(argumentQueue);
  if (exitCode != 0)
  {
    Environment.ExitCode = exitCode;
    return;
  }
}

static void PrintUsage()
{
  Console.WriteLine("xBase.Tools pipeline commands:");
  Console.WriteLine("  verify   Validate solution assets are present.");
  Console.WriteLine("  clean    Run 'dotnet clean xBase.sln'.");
  Console.WriteLine("  restore  Run 'dotnet restore xBase.sln'.");
  Console.WriteLine("  build    Run 'dotnet build xBase.sln -c Release'.");
  Console.WriteLine("  test     Run 'dotnet test xBase.sln --configuration Release'.");
  Console.WriteLine("  publish  Run 'dotnet pack xBase.sln -c Release -o artifacts/packages'.");
  Console.WriteLine("  dbfinfo  Display header metadata for a DBF file or directory.");
}

static Task<int> RunDotNetAsync(string verb, params string[] arguments)
{
  return RunProcessAsync("dotnet", new[] { verb }.Concat(arguments).ToArray());
}

static Task<int> VerifyAsync()
{
  string solutionPath = Path.Combine(Environment.CurrentDirectory, "xBase.sln");
  if (!File.Exists(solutionPath))
  {
    Console.Error.WriteLine($"Solution '{solutionPath}' is missing.");
    return Task.FromResult(1);
  }

  Console.WriteLine($"Verified solution at '{solutionPath}'.");
  return Task.FromResult(0);
}

static Task<int> DbfInfoAsync(Queue<string> arguments)
{
  if (arguments.Count == 0)
  {
    Console.Error.WriteLine("dbfinfo requires a path to a .dbf file or directory.");
    return Task.FromResult(1);
  }

  string target = arguments.Dequeue();
  var loader = new DbfTableLoader();

  if (Directory.Exists(target))
  {
    var catalog = new TableCatalog(loader);
    IReadOnlyList<ITableDescriptor> tables = catalog.EnumerateTables(target);
    var fileLookup = Directory
      .EnumerateFiles(target, "*.dbf", SearchOption.TopDirectoryOnly)
      .ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, path => path, StringComparer.OrdinalIgnoreCase);

    foreach (ITableDescriptor table in tables)
    {
      fileLookup.TryGetValue(table.Name, out string? actualPath);
      PrintTable(table, actualPath ?? Path.Combine(target, table.Name + ".dbf"));
    }

    return Task.FromResult(0);
  }

  if (File.Exists(target))
  {
    DbfTableDescriptor table = loader.Load(target);
    PrintTable(table, target);
    return Task.FromResult(0);
  }

  Console.Error.WriteLine($"Path '{target}' was not found.");
  return Task.FromResult(1);
}

static void PrintTable(ITableDescriptor descriptor, string sourcePath)
{
  Console.WriteLine($"{descriptor.Name} ({sourcePath})");
  if (descriptor is DbfTableDescriptor dbf)
  {
    Console.WriteLine($"  Version: 0x{dbf.Version:X2}  Records: {dbf.RecordCount}  RecordLength: {dbf.RecordLength}");
    Console.WriteLine($"  HeaderLength: {dbf.HeaderLength}  LDID: 0x{dbf.LanguageDriverId:X2}");
  }
  if (descriptor.MemoFileName is not null)
  {
    Console.WriteLine($"  Memo: {descriptor.MemoFileName}");
  }

  if (descriptor.Indexes.Count > 0)
  {
    Console.WriteLine("  Indexes:");
    foreach (IIndexDescriptor index in descriptor.Indexes)
    {
      string label = index.Name;
      if (index is IndexDescriptor sidecar && !string.IsNullOrEmpty(sidecar.FileName))
      {
        label = sidecar.FileName;
      }

      if (!string.IsNullOrWhiteSpace(index.Expression))
      {
        label += $" ({index.Expression})";
      }

      Console.WriteLine($"    - {label}");
    }
  }

  Console.WriteLine("  Fields:");
  foreach (IFieldDescriptor field in descriptor.Fields)
  {
    Console.WriteLine($"    - {field.Name} ({field.Type}) len={field.Length} dec={field.DecimalCount}");
  }
}

static async Task<int> RunProcessAsync(string fileName, string[] arguments)
{
  using var process = new Process
  {
    StartInfo = new ProcessStartInfo
    {
      FileName = fileName,
      ArgumentList = { },
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false
    }
  };

  foreach (string argument in arguments)
  {
    process.StartInfo.ArgumentList.Add(argument);
  }

  process.OutputDataReceived += (_, e) =>
  {
    if (e.Data is not null)
    {
      Console.WriteLine(e.Data);
    }
  };

  process.ErrorDataReceived += (_, e) =>
  {
    if (e.Data is not null)
    {
      Console.Error.WriteLine(e.Data);
    }
  };

  process.Start();
  process.BeginOutputReadLine();
  process.BeginErrorReadLine();
  await process.WaitForExitAsync();
  return process.ExitCode;
}
