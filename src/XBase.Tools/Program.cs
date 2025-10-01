using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Ddl;
using XBase.Core.Cursors;
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
  ["dbfdump"] = DbfDumpAsync,
  ["dbfpack"] = DbfPackAsync,
  ["dbfreindex"] = DbfReindexAsync,
  ["dbfconvert"] = DbfConvertAsync,
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
  Console.WriteLine("  dbfdump  Export table rows to CSV or JSON lines.");
  Console.WriteLine("  dbfpack  Compact deleted records and rewrite the table.");
  Console.WriteLine("  dbfreindex  Rebuild indexes for a DBF file or table.");
  Console.WriteLine("  dbfconvert  Create a transcoded copy of a DBF file.");
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

static async Task<int> DbfDumpAsync(Queue<string> arguments)
{
  if (arguments.Count == 0)
  {
    Console.Error.WriteLine("dbfdump requires <dbf-path> or <root> <table>.");
    return 1;
  }

  string target = arguments.Dequeue();
  if (!TryResolveTableArguments(target, arguments, out _, out string table, out string dbfPath))
  {
    if (!File.Exists(target))
    {
      Console.Error.WriteLine("dbfdump requires <dbf-path> or <root> <table>.");
      return 1;
    }

    string fullPath = Path.GetFullPath(target);
    table = Path.GetFileNameWithoutExtension(fullPath) ?? Path.GetFileName(fullPath);
    dbfPath = fullPath;
  }

  string format = "csv";
  string? outputCandidate = null;
  bool includeDeleted = false;
  int? limit = null;

  while (arguments.Count > 0)
  {
    string token = arguments.Dequeue();
    switch (token)
    {
      case "--format":
        if (arguments.Count == 0)
        {
          Console.Error.WriteLine("--format requires a value of 'csv' or 'jsonl'.");
          return 1;
        }

        format = arguments.Dequeue();
        break;
      case "--output":
        if (arguments.Count == 0)
        {
          Console.Error.WriteLine("--output requires a file or directory path.");
          return 1;
        }

        outputCandidate = arguments.Dequeue();
        break;
      case "--include-deleted":
        includeDeleted = true;
        break;
      case "--limit":
        if (arguments.Count == 0 || !int.TryParse(arguments.Dequeue(), out int parsedLimit) || parsedLimit <= 0)
        {
          Console.Error.WriteLine("--limit requires a positive integer.");
          return 1;
        }

        limit = parsedLimit;
        break;
      default:
        Console.Error.WriteLine($"Unknown option '{token}'.");
        return 1;
    }
  }

  string normalizedFormat = format.ToLowerInvariant();
  if (normalizedFormat != "csv" && normalizedFormat != "json" && normalizedFormat != "jsonl")
  {
    Console.Error.WriteLine("--format supports 'csv' or 'jsonl'.");
    return 1;
  }

  string effectiveFormat = normalizedFormat.StartsWith("json", StringComparison.Ordinal) ? "jsonl" : "csv";
  string? resolvedOutputPath = ResolveOutputPath(outputCandidate, table, effectiveFormat);

  var loader = new DbfTableLoader();
  DbfTableDescriptor descriptor = loader.LoadDbf(dbfPath);
  IReadOnlyList<TableColumn> columns = DbfColumnFactory.CreateColumns(descriptor);
  var cursorFactory = new DbfCursorFactory();
  await using ICursor cursor = await cursorFactory
    .CreateSequentialAsync(descriptor, new CursorOptions(includeDeleted, limit, Offset: null))
    .ConfigureAwait(false);

  StreamWriter? fileWriter = null;
  TextWriter writer;

  if (resolvedOutputPath is null)
  {
    writer = Console.Out;
  }
  else
  {
    string directory = Path.GetDirectoryName(resolvedOutputPath) ?? string.Empty;
    if (directory.Length > 0)
    {
      Directory.CreateDirectory(directory);
    }

    fileWriter = new StreamWriter(new FileStream(resolvedOutputPath, FileMode.Create, FileAccess.Write, FileShare.None), new UTF8Encoding(false));
    writer = fileWriter;
  }

  int written = 0;

  try
  {
    if (effectiveFormat == "csv")
    {
      written = await WriteCsvAsync(cursor, columns, writer, limit).ConfigureAwait(false);
    }
    else
    {
      written = await WriteJsonLinesAsync(cursor, columns, writer, limit).ConfigureAwait(false);
    }

    await writer.FlushAsync().ConfigureAwait(false);
  }
  finally
  {
    if (fileWriter is not null)
    {
      await fileWriter.DisposeAsync().ConfigureAwait(false);
    }
  }

  if (resolvedOutputPath is null)
  {
    Console.Error.WriteLine($"Exported {written} records from {table}.");
  }
  else
  {
    Console.Error.WriteLine($"Exported {written} records from {table} to '{resolvedOutputPath}'.");
  }

  return 0;
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

static async Task<int> DbfPackAsync(Queue<string> arguments)
{
  if (arguments.Count == 0)
  {
    Console.Error.WriteLine("dbfpack requires <dbf-path> or <root> <table>.");
    return 1;
  }

  string target = arguments.Dequeue();
  if (!TryResolveTableArguments(target, arguments, out string root, out string table, out string dbfPath))
  {
    Console.Error.WriteLine("dbfpack requires <dbf-path> or <root> <table>.");
    return 1;
  }

  bool dryRun = false;

  while (arguments.Count > 0)
  {
    string token = arguments.Dequeue();
    if (token.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
    {
      dryRun = true;
      continue;
    }

    Console.Error.WriteLine($"Unknown option '{token}'.");
    return 1;
  }

  var loader = new DbfTableLoader();
  DbfTableDescriptor descriptor = loader.LoadDbf(dbfPath);
  (uint total, uint deleted) before = CountRecords(descriptor);

  if (dryRun)
  {
    Console.WriteLine($"[dry-run] pack for {table} would remove {before.deleted} deleted records from {before.total} rows.");
    return 0;
  }

  var mutator = new SchemaMutator(root);
  await mutator.PackAsync(table).ConfigureAwait(false);
  DbfTableDescriptor packed = loader.LoadDbf(dbfPath);
  (uint total, uint deleted) after = CountRecords(packed);
  uint removed = before.deleted > after.deleted ? before.deleted - after.deleted : before.deleted;
  Console.WriteLine($"Pack completed for {table}; removed {removed} deleted records. Current records: {after.total - after.deleted}.");
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
  if (!TryResolveTableArguments(target, arguments, out string root, out string table, out _))
  {
    Console.Error.WriteLine("dbfreindex requires <dbf-path> or <root> <table>.");
    return 1;
  }

  var mutator = new SchemaMutator(root);
  int rebuilt = await mutator.ReindexAsync(table).ConfigureAwait(false);
  Console.WriteLine($"Reindex completed for {table}; rebuilt {rebuilt} index files.");
  return 0;
}

static async Task<int> DbfConvertAsync(Queue<string> arguments)
{
  if (arguments.Count == 0)
  {
    Console.Error.WriteLine("dbfconvert requires <dbf-path> or <root> <table>.");
    return 1;
  }

  string target = arguments.Dequeue();
  if (!TryResolveTableArguments(target, arguments, out string root, out string table, out string dbfPath))
  {
    if (!File.Exists(target))
    {
      Console.Error.WriteLine("dbfconvert requires <dbf-path> or <root> <table>.");
      return 1;
    }

    string fullPath = Path.GetFullPath(target);
    root = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
    table = Path.GetFileNameWithoutExtension(fullPath) ?? Path.GetFileName(fullPath);
    dbfPath = fullPath;
  }

  string? outputCandidate = null;
  string? encodingName = null;
  int? codePageOverride = null;
  byte? ldidOverride = null;
  bool dropDeleted = false;
  bool overwrite = false;

  while (arguments.Count > 0)
  {
    string token = arguments.Dequeue();
    switch (token)
    {
      case "--output":
        if (arguments.Count == 0)
        {
          Console.Error.WriteLine("--output requires a file or directory path.");
          return 1;
        }

        outputCandidate = arguments.Dequeue();
        break;
      case "--encoding":
        if (arguments.Count == 0)
        {
          Console.Error.WriteLine("--encoding requires an encoding name (e.g., utf-8).");
          return 1;
        }

        encodingName = arguments.Dequeue();
        break;
      case "--codepage":
        if (arguments.Count == 0 || !int.TryParse(arguments.Dequeue(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedCodePage))
        {
          Console.Error.WriteLine("--codepage requires a numeric code page.");
          return 1;
        }

        codePageOverride = parsedCodePage;
        break;
      case "--target-ldid":
        if (arguments.Count == 0)
        {
          Console.Error.WriteLine("--target-ldid requires a byte value (decimal or 0x.. hex).");
          return 1;
        }

        string idValue = arguments.Dequeue();
        if (idValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
          if (!byte.TryParse(idValue[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte parsedLdid))
          {
            Console.Error.WriteLine("--target-ldid could not parse the hex value.");
            return 1;
          }

          ldidOverride = parsedLdid;
        }
        else if (byte.TryParse(idValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte parsedDecimal))
        {
          ldidOverride = parsedDecimal;
        }
        else
        {
          Console.Error.WriteLine("--target-ldid requires a byte value (decimal or hex).");
          return 1;
        }

        break;
      case "--drop-deleted":
        dropDeleted = true;
        break;
      case "--overwrite":
        overwrite = true;
        break;
      default:
        Console.Error.WriteLine($"Unknown option '{token}'.");
        return 1;
    }
  }

  string defaultOutputName = Path.Combine(root, table + ".utf8.dbf");
  string? resolvedOutput = outputCandidate is null
    ? defaultOutputName
    : ResolveOutputPath(outputCandidate, table, "dbf") ?? defaultOutputName;

  resolvedOutput = Path.GetFullPath(resolvedOutput);
  string? outputDirectory = Path.GetDirectoryName(resolvedOutput);
  if (!string.IsNullOrEmpty(outputDirectory))
  {
    Directory.CreateDirectory(outputDirectory);
  }

  if (File.Exists(resolvedOutput) && !overwrite)
  {
    Console.Error.WriteLine($"Output '{resolvedOutput}' already exists. Use --overwrite to replace it.");
    return 1;
  }

  Encoding targetEncoding;
  try
  {
    if (!string.IsNullOrWhiteSpace(encodingName))
    {
      targetEncoding = Encoding.GetEncoding(encodingName);
    }
    else if (codePageOverride.HasValue)
    {
      targetEncoding = Encoding.GetEncoding(codePageOverride.Value);
    }
    else
    {
      targetEncoding = Encoding.UTF8;
    }
  }
  catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
  {
    Console.Error.WriteLine($"Unable to resolve encoding: {ex.Message}");
    return 1;
  }

  int targetCodePage = codePageOverride ?? targetEncoding.CodePage;

  var loader = new DbfTableLoader();
  DbfTableDescriptor descriptor = loader.LoadDbf(dbfPath);
  Encoding sourceEncoding = DbfEncodingRegistry.Resolve(descriptor.LanguageDriverId);
  byte targetLanguageDriverId;

  if (ldidOverride.HasValue)
  {
    targetLanguageDriverId = ldidOverride.Value;
  }
  else if (DbfEncodingRegistry.TryGetLanguageDriverId(targetCodePage, out byte resolvedLdid))
  {
    targetLanguageDriverId = resolvedLdid;
  }
  else
  {
    targetLanguageDriverId = descriptor.LanguageDriverId;
    Console.Error.WriteLine($"Warning: no LDID mapping for code page {targetCodePage}; preserving 0x{targetLanguageDriverId:X2}.");
  }

  IReadOnlyDictionary<string, DbfFieldLayout> layoutLookup = DbfColumnFactory.CreateLayoutLookup(descriptor);
  DbfFieldLayout[] layouts = descriptor.FieldSchemas
    .Select(schema => layoutLookup[schema.Name])
    .ToArray();

  await using FileStream source = new(dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
  await using FileStream destination = new(resolvedOutput, FileMode.Create, FileAccess.Write, FileShare.None);

  byte[] header = new byte[descriptor.HeaderLength];
  await source.ReadExactlyAsync(header, default).ConfigureAwait(false);

  DateTime now = DateTime.UtcNow;
  header[1] = (byte)Math.Clamp(now.Year - 1900, 0, 255);
  header[2] = (byte)now.Month;
  header[3] = (byte)now.Day;
  header[29] = targetLanguageDriverId;
  await destination.WriteAsync(header, default).ConfigureAwait(false);

  byte[] recordBuffer = new byte[descriptor.RecordLength];
  byte[] outputRecord = new byte[descriptor.RecordLength];
  uint survivors = 0;
  uint droppedDeleted = 0;
  int truncatedFields = 0;

  for (uint i = 0; i < descriptor.RecordCount; i++)
  {
    int read = await source.ReadAsync(recordBuffer, 0, recordBuffer.Length).ConfigureAwait(false);
    if (read < recordBuffer.Length)
    {
      break;
    }

    bool isDeleted = IsDeleted(recordBuffer[0]);
    if (dropDeleted && isDeleted)
    {
      droppedDeleted++;
      continue;
    }

    Array.Copy(recordBuffer, outputRecord, recordBuffer.Length);
    outputRecord[0] = dropDeleted ? (byte)' ' : recordBuffer[0];

    foreach (DbfFieldLayout layout in layouts)
    {
      if (!IsCharacterField(layout.Schema.Type))
      {
        continue;
      }

      if (TranscodeField(
        recordBuffer,
        layout.Offset,
        layout.Schema.Length,
        outputRecord,
        layout.Offset,
        sourceEncoding,
        targetEncoding))
      {
        truncatedFields++;
      }
    }

    await destination.WriteAsync(outputRecord, 0, outputRecord.Length).ConfigureAwait(false);
    survivors++;
  }

  await destination.WriteAsync(new byte[] { 0x1A }, default).ConfigureAwait(false);

  BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), survivors);
  destination.Position = 0;
  await destination.WriteAsync(header, default).ConfigureAwait(false);
  await destination.FlushAsync().ConfigureAwait(false);
  destination.Flush(true);

  Console.WriteLine(
    $"Converted {table} to '{resolvedOutput}' with code page {targetEncoding.CodePage} (LDID 0x{targetLanguageDriverId:X2}); " +
    $"records: {survivors} (dropped {droppedDeleted}), truncated fields: {truncatedFields}.");

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

static string? ResolveOutputPath(string? candidate, string table, string format)
{
  if (string.IsNullOrWhiteSpace(candidate))
  {
    return null;
  }

  string fullPath = Path.GetFullPath(candidate);
  bool treatAsDirectory = Directory.Exists(fullPath);

  if (!treatAsDirectory)
  {
    if (Path.EndsInDirectorySeparator(candidate.AsSpan()))
    {
      treatAsDirectory = true;
    }
    else
    {
      string fileName = Path.GetFileName(fullPath);
      if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(Path.GetExtension(fullPath)))
      {
        treatAsDirectory = true;
      }
    }
  }

  if (treatAsDirectory)
  {
    string extension = format.ToLowerInvariant() switch
    {
      "csv" => ".csv",
      "json" or "jsonl" => ".jsonl",
      _ => ".dbf"
    };

    return Path.Combine(fullPath, table + extension);
  }

  return fullPath;
}

static bool TryResolveTableArguments(
  string target,
  Queue<string> arguments,
  out string root,
  out string table,
  out string dbfPath)
{
  root = string.Empty;
  table = string.Empty;
  dbfPath = string.Empty;

  if (File.Exists(target) && string.Equals(Path.GetExtension(target), ".dbf", StringComparison.OrdinalIgnoreCase))
  {
    string fullPath = Path.GetFullPath(target);
    root = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
    table = Path.GetFileNameWithoutExtension(fullPath) ?? Path.GetFileName(fullPath);
    dbfPath = fullPath;
    return true;
  }

  string rootCandidate = Path.GetFullPath(target);
  if (!Directory.Exists(rootCandidate))
  {
    return false;
  }

  if (arguments.Count == 0)
  {
    return false;
  }

  string potentialTable = arguments.Peek();
  if (potentialTable.StartsWith("--", StringComparison.Ordinal))
  {
    return false;
  }

  arguments.Dequeue();
  string tableCandidate = potentialTable;
  string candidatePath = Path.Combine(rootCandidate, tableCandidate);
  if (!Path.HasExtension(candidatePath))
  {
    candidatePath += ".dbf";
  }

  if (File.Exists(candidatePath))
  {
    root = rootCandidate;
    table = Path.GetFileNameWithoutExtension(candidatePath) ?? tableCandidate;
    dbfPath = Path.GetFullPath(candidatePath);
    return true;
  }

  string lookupName = Path.GetFileNameWithoutExtension(tableCandidate);
  string? discovered = Directory
    .EnumerateFiles(rootCandidate, "*.dbf", SearchOption.TopDirectoryOnly)
    .FirstOrDefault(file => string.Equals(Path.GetFileNameWithoutExtension(file), lookupName, StringComparison.OrdinalIgnoreCase));

  if (discovered is null)
  {
    return false;
  }

  root = rootCandidate;
  table = Path.GetFileNameWithoutExtension(discovered) ?? lookupName;
  dbfPath = Path.GetFullPath(discovered);
  return true;
}

static (uint total, uint deleted) CountRecords(DbfTableDescriptor descriptor)
{
  if (descriptor.RecordLength == 0)
  {
    return (0, 0);
  }

  string? path = descriptor.FilePath;
  if (string.IsNullOrEmpty(path) || !File.Exists(path))
  {
    throw new FileNotFoundException($"Table '{descriptor.Name}' does not have a readable DBF path.", path);
  }

  using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
  stream.Seek(descriptor.HeaderLength, SeekOrigin.Begin);
  byte[] buffer = new byte[descriptor.RecordLength];
  uint total = 0;
  uint deleted = 0;

  while (total < descriptor.RecordCount)
  {
    int read = stream.Read(buffer, 0, buffer.Length);
    if (read < buffer.Length)
    {
      break;
    }

    total++;
    if (IsDeleted(buffer[0]))
    {
      deleted++;
    }
  }

  return (total, deleted);
}

static async Task<int> WriteCsvAsync(
  ICursor cursor,
  IReadOnlyList<TableColumn> columns,
  TextWriter writer,
  int? limit)
{
  await writer.WriteLineAsync(string.Join(",", columns.Select(column => EscapeCsv(column.Name)))).ConfigureAwait(false);
  int count = 0;

  while (await cursor.ReadAsync().ConfigureAwait(false))
  {
    string[] values = new string[columns.Count];
    for (int i = 0; i < columns.Count; i++)
    {
      object? value = columns[i].ValueAccessor(cursor.Current);
      values[i] = FormatCsvValue(value);
    }

    await writer.WriteLineAsync(string.Join(",", values)).ConfigureAwait(false);
    count++;

    if (limit.HasValue && count >= limit.Value)
    {
      break;
    }
  }

  return count;
}

static async Task<int> WriteJsonLinesAsync(
  ICursor cursor,
  IReadOnlyList<TableColumn> columns,
  TextWriter writer,
  int? limit)
{
  var options = new JsonSerializerOptions { WriteIndented = false };
  int count = 0;

  while (await cursor.ReadAsync().ConfigureAwait(false))
  {
    var payload = new Dictionary<string, object?>(columns.Count, StringComparer.OrdinalIgnoreCase);
    foreach (TableColumn column in columns)
    {
      payload[column.Name] = column.ValueAccessor(cursor.Current);
    }

    string json = JsonSerializer.Serialize(payload, options);
    await writer.WriteLineAsync(json).ConfigureAwait(false);
    count++;

    if (limit.HasValue && count >= limit.Value)
    {
      break;
    }
  }

  return count;
}

static string FormatCsvValue(object? value)
{
  if (value is null || value is DBNull)
  {
    return string.Empty;
  }

  return value switch
  {
    string str => EscapeCsv(str),
    DateTime dateTime => EscapeCsv(dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
    bool boolean => EscapeCsv(boolean ? "true" : "false"),
    IFormattable formattable => EscapeCsv(formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty),
    _ => EscapeCsv(value.ToString() ?? string.Empty)
  };
}

static string EscapeCsv(string value)
{
  if (string.IsNullOrEmpty(value))
  {
    return string.Empty;
  }

  if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
  {
    return "\"" + value.Replace("\"", "\"\"") + "\"";
  }

  return value;
}

static bool IsDeleted(byte marker)
{
  return marker == 0x2A || marker == (byte)'*';
}

static bool IsCharacterField(char type)
{
  char normalized = char.ToUpperInvariant(type);
  return normalized is 'C' or 'V' or 'W' or 'G' or 'P';
}

static bool TranscodeField(
  byte[] source,
  int sourceOffset,
  int length,
  byte[] destination,
  int destinationOffset,
  Encoding sourceEncoding,
  Encoding targetEncoding)
{
  string text = sourceEncoding.GetString(source, sourceOffset, length);

  for (int i = 0; i < length; i++)
  {
    destination[destinationOffset + i] = (byte)' ';
  }

  if (text.Length == 0)
  {
    return false;
  }

  string current = text;
  byte[] encoded = targetEncoding.GetBytes(current);
  bool truncated = false;

  while (encoded.Length > length && current.Length > 0)
  {
    truncated = true;
    current = current[..^1];
    encoded = targetEncoding.GetBytes(current);
  }

  int copyLength = Math.Min(encoded.Length, length);
  Array.Copy(encoded, 0, destination, destinationOffset, copyLength);
  return truncated || encoded.Length > length;
}
