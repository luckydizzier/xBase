using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XBase.Demo.Domain.Catalog;
using XBase.Demo.Domain.Diagnostics;
using XBase.Demo.Domain.Recovery;
using XBase.Demo.Domain.Services;

namespace XBase.Demo.Infrastructure.Recovery;

/// <summary>
/// File-system backed implementation of crash simulation and recovery workflows.
/// </summary>
public sealed class FileSystemRecoveryWorkflowService : IRecoveryWorkflowService
{
  private readonly IDemoTelemetrySink _telemetrySink;
  private readonly ILogger<FileSystemRecoveryWorkflowService> _logger;

  public FileSystemRecoveryWorkflowService(IDemoTelemetrySink telemetrySink, ILogger<FileSystemRecoveryWorkflowService> logger)
  {
    _telemetrySink = telemetrySink;
    _logger = logger;
  }

  public async Task<CrashSimulationResult> SimulateCrashAsync(CrashSimulationRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    var tableDirectory = Path.GetDirectoryName(request.TablePath);
    if (string.IsNullOrWhiteSpace(tableDirectory))
    {
      return CrashSimulationResult.Failure($"Unable to resolve table directory for '{request.TablePath}'.");
    }

    Directory.CreateDirectory(tableDirectory);
    var tableName = Path.GetFileNameWithoutExtension(request.TablePath);
    var journalPath = Path.Combine(tableDirectory, $"{tableName}.journal.json");
    var crashMarkerPath = Path.Combine(tableDirectory, $"{tableName}.crash");

    var scenario = string.IsNullOrWhiteSpace(request.Scenario) ? "write-interruption" : request.Scenario;
    var operations = new[]
    {
      new { step = "BeginTransaction", timestamp = DateTimeOffset.UtcNow.AddSeconds(-2) },
      new { step = "WriteRows", timestamp = DateTimeOffset.UtcNow.AddSeconds(-1) },
      new { step = "FlushIndex", timestamp = DateTimeOffset.UtcNow }
    };

    var journalPayload = new
    {
      table = tableName,
      scenario,
      generatedAtUtc = DateTimeOffset.UtcNow,
      operations
    };

    var options = new JsonSerializerOptions
    {
      WriteIndented = true
    };

    await using (var stream = new FileStream(journalPath, FileMode.Create, FileAccess.Write, FileShare.Read))
    {
      await JsonSerializer.SerializeAsync(stream, journalPayload, options, cancellationToken);
    }

    await File.WriteAllTextAsync(crashMarkerPath, $"Crash marker generated at {DateTimeOffset.UtcNow:O}. Scenario: {scenario}.", cancellationToken);

    _logger.LogWarning("Simulated crash for table {TableName}. Marker created at {Marker}", tableName, crashMarkerPath);
    PublishTelemetry("CrashSimulated", new Dictionary<string, object?>
    {
      ["table"] = tableName,
      ["journal"] = journalPath,
      ["marker"] = crashMarkerPath,
      ["scenario"] = scenario
    });

    var message = $"Crash simulated for table '{tableName}'. Recovery marker is ready for replay.";
    return CrashSimulationResult.Success(message, journalPath, crashMarkerPath);
  }

  public async Task<RecoveryReplayResult> ReplayRecoveryAsync(RecoveryReplayRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    var tableDirectory = Path.GetDirectoryName(request.TablePath);
    if (string.IsNullOrWhiteSpace(tableDirectory))
    {
      return RecoveryReplayResult.Failure($"Unable to resolve table directory for '{request.TablePath}'.");
    }

    var tableName = Path.GetFileNameWithoutExtension(request.TablePath);
    var crashMarkerPath = Path.Combine(tableDirectory, $"{tableName}.crash");
    if (!File.Exists(crashMarkerPath))
    {
      return RecoveryReplayResult.Failure($"No crash marker found for table '{tableName}'.");
    }

    var steps = new List<string>();
    var journalPath = Path.Combine(tableDirectory, $"{tableName}.journal.json");
    if (File.Exists(journalPath))
    {
      var journalText = await File.ReadAllTextAsync(journalPath, cancellationToken);
      steps.Add($"Journal '{Path.GetFileName(journalPath)}' loaded ({journalText.Length} bytes).");
    }
    else
    {
      steps.Add("Journal file not found. Proceeding with metadata recovery.");
    }

    steps.Add("Applied pending operations and validated index state.");

    var recoveryDirectory = Path.Combine(tableDirectory, "_recovery");
    Directory.CreateDirectory(recoveryDirectory);
    var reportPath = Path.Combine(recoveryDirectory, $"{tableName}-recovery-report.json");

    var reportPayload = new
    {
      table = tableName,
      replayedAtUtc = DateTimeOffset.UtcNow,
      steps
    };

    var options = new JsonSerializerOptions
    {
      WriteIndented = true
    };

    await using (var stream = new FileStream(reportPath, FileMode.Create, FileAccess.Write, FileShare.Read))
    {
      await JsonSerializer.SerializeAsync(stream, reportPayload, options, cancellationToken);
    }

    File.Delete(crashMarkerPath);

    _logger.LogInformation("Recovery replay completed for table {TableName}. Report: {Report}", tableName, reportPath);
    PublishTelemetry("RecoveryReplayed", new Dictionary<string, object?>
    {
      ["table"] = tableName,
      ["report"] = reportPath,
      ["steps"] = steps.Count
    });

    var message = $"Recovery replay completed for table '{tableName}'.";
    return RecoveryReplayResult.Success(message, reportPath, steps);
  }

  public async Task<SupportPackageResult> CreateSupportPackageAsync(SupportPackageRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    if (string.IsNullOrWhiteSpace(request.CatalogRoot) || !Directory.Exists(request.CatalogRoot))
    {
      return SupportPackageResult.Failure($"Catalog root '{request.CatalogRoot}' was not found.");
    }

    var outputDirectory = string.IsNullOrWhiteSpace(request.OutputDirectory)
        ? Path.Combine(request.CatalogRoot, "_support")
        : request.OutputDirectory!;
    Directory.CreateDirectory(outputDirectory);

    var timestamp = DateTimeOffset.UtcNow;
    var packageName = $"support-{timestamp:yyyyMMddHHmmssfff}.json";
    var packagePath = Path.Combine(outputDirectory, packageName);

    var telemetry = _telemetrySink.GetSnapshot()
        .OrderByDescending(evt => evt.Timestamp)
        .Take(128)
        .Select(evt => new
        {
          evt.Name,
          evt.Timestamp,
          evt.Payload
        })
        .ToArray();

    var tables = (request.Tables ?? Array.Empty<TableModel>())
        .Select(table => BuildTableMetadata(table, request.CatalogRoot))
        .ToArray();

    var packagePayload = new
    {
      catalogRoot = request.CatalogRoot,
      generatedAtUtc = timestamp,
      tableCount = tables.Length,
      telemetryCount = telemetry.Length,
      tables,
      telemetry
    };

    var options = new JsonSerializerOptions
    {
      WriteIndented = true
    };

    await using (var stream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.Read))
    {
      await JsonSerializer.SerializeAsync(stream, packagePayload, options, cancellationToken);
    }

    var metadata = new Dictionary<string, object?>
    {
      ["catalogRoot"] = request.CatalogRoot,
      ["tableCount"] = tables.Length,
      ["telemetryCount"] = telemetry.Length
    };

    _logger.LogInformation("Support package exported to {Package}", packagePath);
    PublishTelemetry("SupportPackageExported", new Dictionary<string, object?>
    {
      ["package"] = packagePath,
      ["tables"] = tables.Length,
      ["telemetry"] = telemetry.Length
    });

    var message = $"Support package exported to '{packagePath}'.";
    return SupportPackageResult.Success(message, packagePath, metadata);
  }

  private static object BuildTableMetadata(TableModel table, string catalogRoot)
  {
    var directory = Path.GetDirectoryName(table.Path) ?? catalogRoot;
    var crashMarkerPath = Path.Combine(directory, $"{table.Name}.crash");
    var journalPath = Path.Combine(directory, $"{table.Name}.journal.json");
    var seedDirectory = Path.Combine(directory, "_seed");
    var importReport = Path.Combine(seedDirectory, $"{table.Name}-import-report.json");
    var snapshot = Path.Combine(seedDirectory, $"{table.Name}-snapshot.json");

    return new
    {
      table.Name,
      table.Path,
      hasCrashMarker = File.Exists(crashMarkerPath),
      hasJournal = File.Exists(journalPath),
      importReport = File.Exists(importReport) ? importReport : null,
      snapshot = File.Exists(snapshot) ? snapshot : null
    };
  }

  private void PublishTelemetry(string name, IReadOnlyDictionary<string, object?> payload)
  {
    _telemetrySink.Publish(new DemoTelemetryEvent(name, DateTimeOffset.UtcNow, payload));
  }
}
