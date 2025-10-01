using System;
using System.IO;

namespace XBase.Demo.Domain.Services.Models;

/// <summary>
/// Describes the inputs required to create an index in the demo catalog.
/// </summary>
/// <param name="TablePath">Absolute path to the table (DBF) file.</param>
/// <param name="IndexName">Logical index name including extension.</param>
/// <param name="Expression">Index key expression.</param>
public sealed record IndexCreateRequest(string TablePath, string IndexName, string Expression)
{
  public static IndexCreateRequest Create(string tablePath, string indexName, string expression)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(tablePath);
    ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
    ArgumentException.ThrowIfNullOrWhiteSpace(expression);
    return new IndexCreateRequest(tablePath, indexName, expression);
  }
}

/// <summary>
/// Describes the inputs required to drop an index from the demo catalog.
/// </summary>
/// <param name="TablePath">Absolute path to the table (DBF) file.</param>
/// <param name="IndexName">Logical index name including extension.</param>
public sealed record IndexDropRequest(string TablePath, string IndexName)
{
  public static IndexDropRequest Create(string tablePath, string indexName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(tablePath);
    ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
    return new IndexDropRequest(tablePath, indexName);
  }
}

/// <summary>
/// Describes the inputs required to rebuild an index alongside the existing artifact.
/// </summary>
/// <param name="TablePath">Absolute path to the table (DBF) file.</param>
/// <param name="IndexName">Logical index name including extension.</param>
/// <param name="TemporaryIndexName">Optional override for the temporary index artifact name.</param>
public sealed record IndexRebuildRequest(string TablePath, string IndexName, string? TemporaryIndexName = null)
{
  public static IndexRebuildRequest Create(string tablePath, string indexName, string? temporaryIndexName = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(tablePath);
    ArgumentException.ThrowIfNullOrWhiteSpace(indexName);
    if (!string.IsNullOrWhiteSpace(temporaryIndexName) && Path.GetFileName(temporaryIndexName) != temporaryIndexName)
    {
      throw new ArgumentException("Temporary index name must not contain directory segments.", nameof(temporaryIndexName));
    }

    return new IndexRebuildRequest(tablePath, indexName, temporaryIndexName);
  }
}

/// <summary>
/// Captures performance characteristics for a completed index operation.
/// </summary>
/// <param name="Duration">Total execution time.</param>
/// <param name="BytesProcessed">Number of bytes processed during the operation.</param>
public sealed record IndexPerformanceSnapshot(TimeSpan Duration, long BytesProcessed)
{
  public double BytesPerSecond => Duration > TimeSpan.Zero
      ? BytesProcessed / Duration.TotalSeconds
      : BytesProcessed;
}

/// <summary>
/// Represents progress emitted during an index rebuild operation.
/// </summary>
/// <param name="Stage">Logical stage of execution.</param>
/// <param name="PercentComplete">Percentage complete represented as 0.0 - 1.0.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="BytesProcessed">Number of bytes processed so far.</param>
/// <param name="TotalBytes">Total bytes expected for the operation, if known.</param>
/// <param name="Performance">Performance metrics available for completed stages.</param>
/// <param name="IsCompleted">Indicates whether the rebuild has completed successfully.</param>
public sealed record IndexRebuildProgress(
    string Stage,
    double PercentComplete,
    string Message,
    long BytesProcessed = 0,
    long? TotalBytes = null,
    IndexPerformanceSnapshot? Performance = null,
    bool IsCompleted = false)
{
  public static IndexRebuildProgress Starting(string indexPath, long? totalBytes)
      => new("Preparing", 0d, $"Preparing rebuild for {Path.GetFileName(indexPath)}", 0, totalBytes);

  public static IndexRebuildProgress Copying(long bytesProcessed, long? totalBytes)
  {
    var percent = totalBytes is > 0
        ? Math.Clamp(bytesProcessed / (double)totalBytes.Value, 0d, 1d)
        : 0.5d;
    return new IndexRebuildProgress("Copying", percent, "Cloning index side-by-side", bytesProcessed, totalBytes);
  }

  public static IndexRebuildProgress Swapping(long? totalBytes)
      => new("Swapping", 0.95d, "Swapping rebuilt index into place", totalBytes ?? 0, totalBytes);

  public static IndexRebuildProgress Completed(string indexPath, IndexPerformanceSnapshot performance)
      => new("Completed", 1d, $"Index '{Path.GetFileName(indexPath)}' rebuilt successfully.", performance.BytesProcessed, performance.BytesProcessed, performance, true);
}

/// <summary>
/// Represents the result of an index lifecycle operation.
/// </summary>
/// <param name="Succeeded">Indicates whether the operation completed successfully.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="Exception">Optional exception surfaced by the operation.</param>
public sealed record IndexOperationResult(bool Succeeded, string Message, Exception? Exception = null)
{
  public static IndexOperationResult Success(string message)
      => new(true, message, null);

  public static IndexOperationResult Failure(string message, Exception? exception = null)
      => new(false, message, exception);
};
