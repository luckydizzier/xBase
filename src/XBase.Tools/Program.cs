using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

var commands = new Dictionary<string, Func<Task<int>>>(StringComparer.OrdinalIgnoreCase)
{
  ["verify"] = VerifyAsync,
  ["clean"] = () => RunDotNetAsync("clean", "xBase.sln"),
  ["restore"] = () => RunDotNetAsync("restore", "xBase.sln"),
  ["build"] = () => RunDotNetAsync("build", "xBase.sln", "-c", "Release"),
  ["test"] = () => RunDotNetAsync("test", "xBase.sln", "--configuration", "Release"),
  ["publish"] = () => RunDotNetAsync("pack", "xBase.sln", "-c", "Release", "-o", "artifacts/packages")
};

if (args.Length == 0)
{
  PrintUsage();
  return;
}

foreach (string argument in args)
{
  if (!commands.TryGetValue(argument, out var handler))
  {
    Console.Error.WriteLine($"Unknown command '{argument}'.");
    PrintUsage();
    Environment.ExitCode = 1;
    return;
  }

  int exitCode = await handler();
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
