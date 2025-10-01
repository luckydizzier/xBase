using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Ddl;
using XBase.Core.Table;

var commands = new Dictionary<string, Func<Queue<string>, Task<int>>>(StringComparer.OrdinalIgnoreCase)
{
  ["verify"] = _ => VerifyAsync(),
  ["clean"] = _ => RunDotNetAsync("clean", "xBase.sln"),
  ["restore"] = _ => RunDotNetAsync("restore", "xBase.sln"),
  ["build"] = _ => RunDotNetAsync("build", "xBase.sln", "-c", "Release"),
  ["test"] = _ => RunDotNetAsync("test", "xBase.sln", "--configuration", "Release"),
  ["publish"] = _ => RunDotNetAsync("pack", "xBase.sln", "-c", "Release", "-o", "artifacts/packages"),
  ["dbfinfo"] = DbfInfoAsync,
  ["dbfreindex"] = DbfReindexAsync,
  ["ddl"] = DdlAsync
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
  Console.WriteLine("  dbfreindex  Rebuild indexes for a DBF file or table.");
  Console.WriteLine("  ddl      Manage online DDL operations (apply/checkpoint/pack/reindex).");
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
    DbfTableDescriptor table = loader.LoadDbf(target);
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

static async Task<int> DdlAsync(Queue<string> arguments)
{
  if (arguments.Count == 0)
  {
    Console.Error.WriteLine("ddl requires a subcommand (apply/checkpoint/pack/reindex).");
    return 1;
  }

  string subcommand = arguments.Dequeue();
  switch (subcommand.ToLowerInvariant())
  {
    case "apply":
      return await DdlApplyAsync(arguments).ConfigureAwait(false);
    case "checkpoint":
      return await DdlCheckpointAsync(arguments).ConfigureAwait(false);
    case "pack":
      return await DdlPackAsync(arguments).ConfigureAwait(false);
    case "reindex":
      return await DdlReindexAsync(arguments).ConfigureAwait(false);
    default:
      Console.Error.WriteLine($"Unknown ddl subcommand '{subcommand}'.");
      return 1;
  }
}

static async Task<int> DdlApplyAsync(Queue<string> arguments)
{
  if (arguments.Count < 3)
  {
    Console.Error.WriteLine("ddl apply requires <root> <table> <operation> [key=value] [--author <name>] [--dry-run].");
    return 1;
  }

  string root = arguments.Dequeue();
  string table = arguments.Dequeue();
  string operationToken = arguments.Dequeue();
  bool dryRun = false;
  string? author = null;
  Dictionary<string, string> properties = new(StringComparer.OrdinalIgnoreCase);

  while (arguments.Count > 0)
  {
    string token = arguments.Peek();
    if (token.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
    {
      dryRun = true;
      arguments.Dequeue();
      continue;
    }

    if (token.Equals("--author", StringComparison.OrdinalIgnoreCase))
    {
      arguments.Dequeue();
      if (arguments.Count == 0)
      {
        Console.Error.WriteLine("--author requires a value.");
        return 1;
      }

      author = arguments.Dequeue();
      continue;
    }

    if (token.Contains('='))
    {
      arguments.Dequeue();
      string[] pair = token.Split('=', 2);
      properties[pair[0]] = pair.Length > 1 ? pair[1] : string.Empty;
      continue;
    }

    Console.Error.WriteLine($"Unexpected argument '{token}'.");
    return 1;
  }

  if (!TryMapOperationKind(operationToken, out SchemaOperationKind kind))
  {
    Console.Error.WriteLine($"Unsupported operation '{operationToken}'.");
    return 1;
  }

  string? objectName = ResolveObjectName(kind, properties);
  var operation = new SchemaOperation(kind, table, objectName, properties);

  if (dryRun)
  {
    Console.WriteLine($"[dry-run] {kind} for {table} would be appended to {Path.Combine(root, table + ".ddl")}.");
    return 0;
  }

  var mutator = new SchemaMutator(root);
  SchemaVersion version = await mutator.ExecuteAsync(operation, author).ConfigureAwait(false);
  Console.WriteLine($"Applied {kind} for {table}; schema version is now {version.Value}.");
  return 0;
}

static async Task<int> DdlCheckpointAsync(Queue<string> arguments)
{
  if (arguments.Count < 2)
  {
    Console.Error.WriteLine("ddl checkpoint requires <root> <table> [--dry-run].");
    return 1;
  }

  string root = arguments.Dequeue();
  string table = arguments.Dequeue();
  bool dryRun = false;

  while (arguments.Count > 0)
  {
    string token = arguments.Peek();
    if (token.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
    {
      dryRun = true;
      arguments.Dequeue();
      continue;
    }

    Console.Error.WriteLine($"Unexpected argument '{token}'.");
    return 1;
  }

  var mutator = new SchemaMutator(root);
  if (dryRun)
  {
    IReadOnlyList<SchemaLogEntry> history = await mutator.ReadHistoryAsync(table).ConfigureAwait(false);
    SchemaVersion target = history.Count == 0 ? SchemaVersion.Start : history[^1].Version;
    Console.WriteLine($"[dry-run] checkpoint for {table} would seal version {target.Value}.");
    return 0;
  }

  SchemaVersion version = await mutator.CreateCheckpointAsync(table).ConfigureAwait(false);
  Console.WriteLine($"Checkpoint completed for {table} at version {version.Value}.");
  return 0;
}

static async Task<int> DdlPackAsync(Queue<string> arguments)
{
  if (arguments.Count < 2)
  {
    Console.Error.WriteLine("ddl pack requires <root> <table> [--dry-run].");
    return 1;
  }

  string root = arguments.Dequeue();
  string table = arguments.Dequeue();
  bool dryRun = false;

  while (arguments.Count > 0)
  {
    string token = arguments.Peek();
    if (token.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
    {
      dryRun = true;
      arguments.Dequeue();
      continue;
    }

    Console.Error.WriteLine($"Unexpected argument '{token}'.");
    return 1;
  }

  var mutator = new SchemaMutator(root);
  IReadOnlyList<SchemaBackfillTask> pending = await mutator.ReadBackfillQueueAsync(table).ConfigureAwait(false);
  if (dryRun)
  {
    Console.WriteLine($"[dry-run] pack for {table} would compact {pending.Count} pending backfill tasks.");
    return 0;
  }

  int removed = await mutator.PackAsync(table).ConfigureAwait(false);
  Console.WriteLine($"Pack completed for {table}; cleared {removed} backfill tasks.");
  return 0;
}

static async Task<int> DdlReindexAsync(Queue<string> arguments)
{
  if (arguments.Count < 2)
  {
    Console.Error.WriteLine("ddl reindex requires <root> <table> [--dry-run].");
    return 1;
  }

  string root = arguments.Dequeue();
  string table = arguments.Dequeue();
  bool dryRun = false;

  while (arguments.Count > 0)
  {
    string token = arguments.Peek();
    if (token.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
    {
      dryRun = true;
      arguments.Dequeue();
      continue;
    }

    Console.Error.WriteLine($"Unexpected argument '{token}'.");
    return 1;
  }

  var mutator = new SchemaMutator(root);
  if (dryRun)
  {
    string dbfPath = Path.Combine(root, table + ".dbf");
    int indexCount = 0;
    if (File.Exists(dbfPath))
    {
      var loader = new DbfTableLoader();
      DbfTableDescriptor descriptor = loader.LoadDbf(dbfPath);
      indexCount = descriptor.Indexes.Count;
    }

    Console.WriteLine($"[dry-run] reindex for {table} would rebuild {indexCount} index files.");
    return 0;
  }

  int rebuilt = await mutator.ReindexAsync(table).ConfigureAwait(false);
  Console.WriteLine($"Reindex completed for {table}; rebuilt {rebuilt} index files.");
  return 0;
}

static async Task<int> DbfReindexAsync(Queue<string> arguments)
{
  if (arguments.Count == 0)
  {
    Console.Error.WriteLine("dbfreindex requires <dbf-path> or <root> <table>.");
    return 1;
  }

  string target = arguments.Dequeue();
  string root;
  string table;

  if (File.Exists(target) && string.Equals(Path.GetExtension(target), ".dbf", StringComparison.OrdinalIgnoreCase))
  {
    string fullPath = Path.GetFullPath(target);
    root = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
    table = Path.GetFileNameWithoutExtension(fullPath) ?? Path.GetFileName(fullPath);
  }
  else
  {
    if (arguments.Count == 0)
    {
      Console.Error.WriteLine("dbfreindex requires <dbf-path> or <root> <table>.");
      return 1;
    }

    root = target;
    table = arguments.Dequeue();
  }

  var mutator = new SchemaMutator(root);
  int rebuilt = await mutator.ReindexAsync(table).ConfigureAwait(false);
  Console.WriteLine($"Reindex completed for {table}; rebuilt {rebuilt} index files.");
  return 0;
}

static bool TryMapOperationKind(string token, out SchemaOperationKind kind)
{
  switch (token.ToLowerInvariant())
  {
    case "create-table":
      kind = SchemaOperationKind.CreateTable;
      return true;
    case "alter-add-column":
      kind = SchemaOperationKind.AlterTableAddColumn;
      return true;
    case "alter-drop-column":
      kind = SchemaOperationKind.AlterTableDropColumn;
      return true;
    case "alter-rename-column":
      kind = SchemaOperationKind.AlterTableRenameColumn;
      return true;
    case "alter-modify-column":
      kind = SchemaOperationKind.AlterTableModifyColumn;
      return true;
    case "drop-table":
      kind = SchemaOperationKind.DropTable;
      return true;
    case "create-index":
      kind = SchemaOperationKind.CreateIndex;
      return true;
    case "drop-index":
      kind = SchemaOperationKind.DropIndex;
      return true;
    default:
      kind = SchemaOperationKind.CreateTable;
      return false;
  }
}

static string? ResolveObjectName(SchemaOperationKind kind, IReadOnlyDictionary<string, string> properties)
{
  if (properties.TryGetValue("object", out string? explicitName))
  {
    return explicitName;
  }

  return kind switch
  {
    SchemaOperationKind.CreateIndex or SchemaOperationKind.DropIndex when properties.TryGetValue("index", out string? index) => index,
    SchemaOperationKind.AlterTableAddColumn or SchemaOperationKind.AlterTableDropColumn or SchemaOperationKind.AlterTableModifyColumn when properties.TryGetValue("column", out string? column) => column,
    SchemaOperationKind.AlterTableRenameColumn when properties.TryGetValue("to", out string? to) => to,
    _ => null
  };
}
