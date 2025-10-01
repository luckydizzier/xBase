using System;
using System.Collections.Generic;
using XBase.Demo.Domain.Catalog;

namespace XBase.Demo.Domain.Recovery;

/// <summary>
/// Request descriptor for simulating a crash event against a table.
/// </summary>
/// <param name="TablePath">Absolute path to the target table.</param>
/// <param name="Scenario">Optional scenario identifier.</param>
public sealed record CrashSimulationRequest(string TablePath, string? Scenario = null)
{
  public static CrashSimulationRequest Create(string tablePath, string? scenario = null)
      => new(tablePath, scenario);
}

/// <summary>
/// Describes the result of a crash simulation.
/// </summary>
/// <param name="Succeeded">Indicates whether the simulation succeeded.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="JournalPath">Location of the generated journal artifact.</param>
/// <param name="CrashMarkerPath">Location of the crash marker created for recovery.</param>
public sealed record CrashSimulationResult(bool Succeeded, string Message, string? JournalPath, string? CrashMarkerPath)
{
  public static CrashSimulationResult Success(string message, string journalPath, string crashMarkerPath)
      => new(true, message, journalPath, crashMarkerPath);

  public static CrashSimulationResult Failure(string message)
      => new(false, message, null, null);
}

/// <summary>
/// Request descriptor for replaying a recovery workflow.
/// </summary>
/// <param name="TablePath">Absolute path to the target table.</param>
public sealed record RecoveryReplayRequest(string TablePath)
{
  public static RecoveryReplayRequest Create(string tablePath)
      => new(tablePath);
}

/// <summary>
/// Represents the outcome of a recovery replay.
/// </summary>
/// <param name="Succeeded">Indicates whether the replay completed successfully.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="ReportPath">Path to the generated recovery report, when available.</param>
/// <param name="Steps">Collection of steps executed during replay.</param>
public sealed record RecoveryReplayResult(bool Succeeded, string Message, string? ReportPath, IReadOnlyList<string> Steps)
{
  public static RecoveryReplayResult Success(string message, string reportPath, IReadOnlyList<string>? steps = null)
      => new(true, message, reportPath, steps ?? Array.Empty<string>());

  public static RecoveryReplayResult Failure(string message)
      => new(false, message, null, Array.Empty<string>());
}

/// <summary>
/// Request descriptor for generating a support package containing diagnostics.
/// </summary>
/// <param name="CatalogRoot">Catalog root path.</param>
/// <param name="Tables">Tables to include in the package.</param>
/// <param name="OutputDirectory">Optional output directory override.</param>
public sealed record SupportPackageRequest(string CatalogRoot, IReadOnlyList<TableModel> Tables, string? OutputDirectory = null)
{
  public static SupportPackageRequest Create(string catalogRoot, IReadOnlyList<TableModel> tables, string? outputDirectory = null)
      => new(catalogRoot, tables, outputDirectory);
}

/// <summary>
/// Represents the result of exporting a support package.
/// </summary>
/// <param name="Succeeded">Indicates whether the package was created.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="PackagePath">Absolute path to the package artifact.</param>
/// <param name="Metadata">Supplemental metadata describing the export.</param>
public sealed record SupportPackageResult(bool Succeeded, string Message, string? PackagePath, IReadOnlyDictionary<string, object?> Metadata)
{
  public static SupportPackageResult Success(string message, string packagePath, IReadOnlyDictionary<string, object?>? metadata = null)
      => new(true, message, packagePath, metadata ?? new Dictionary<string, object?>());

  public static SupportPackageResult Failure(string message)
      => new(false, message, null, new Dictionary<string, object?>());
}
