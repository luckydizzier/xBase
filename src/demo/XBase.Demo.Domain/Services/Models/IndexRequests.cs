using System;

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
