using System;
using System.Collections.Generic;

namespace XBase.Demo.Domain.Seed;

/// <summary>
/// Represents the parameters necessary to import CSV rows into a target table.
/// </summary>
/// <param name="TablePath">Absolute path to the target DBF table.</param>
/// <param name="CsvPath">Absolute path to the CSV source file.</param>
/// <param name="EncodingName">Optional encoding override to apply.</param>
/// <param name="TruncateBeforeLoad">Flag indicating whether existing seed artifacts should be replaced.</param>
public sealed record CsvImportRequest(string TablePath, string CsvPath, string? EncodingName = null, bool TruncateBeforeLoad = false)
{
  public static CsvImportRequest Create(string tablePath, string csvPath, string? encodingName = null, bool truncateBeforeLoad = false)
      => new(tablePath, csvPath, encodingName, truncateBeforeLoad);
}

/// <summary>
/// Represents the outcome of a CSV import operation.
/// </summary>
/// <param name="Succeeded">Indicates whether the operation completed successfully.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="EncodingName">Encoding detected or applied during import.</param>
/// <param name="RowsImported">Number of rows persisted from the CSV.</param>
/// <param name="ReportPath">Path to the generated import report, when available.</param>
/// <param name="Diagnostics">Optional diagnostics emitted by the pipeline.</param>
/// <param name="WasCancelled">Indicates whether the import was cancelled prior to execution.</param>
public sealed record CsvImportResult(
    bool Succeeded,
    string Message,
    string? EncodingName,
    int RowsImported,
    string? ReportPath,
    IReadOnlyList<string> Diagnostics,
    bool WasCancelled = false)
{
  public static CsvImportResult Success(string message, string encodingName, int rowsImported, string reportPath, IReadOnlyList<string>? diagnostics = null)
      => new(true, message, encodingName, rowsImported, reportPath, diagnostics ?? Array.Empty<string>());

  public static CsvImportResult Failure(string message, string? encodingName = null, IReadOnlyList<string>? diagnostics = null)
      => new(false, message, encodingName, 0, null, diagnostics ?? Array.Empty<string>());

  public static CsvImportResult Cancelled(string message)
      => new(false, message, null, 0, null, Array.Empty<string>(), true);
}
