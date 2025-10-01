using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Data.Providers;

public sealed class NoOpSchemaMutator : ISchemaMutator
{
  public ValueTask<SchemaVersion> ExecuteAsync(
    SchemaOperation operation,
    string? author = null,
    CancellationToken cancellationToken = default)
  {
    if (operation is null)
    {
      throw new ArgumentNullException(nameof(operation));
    }

    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.FromResult(SchemaVersion.Start);
  }

  public ValueTask<IReadOnlyList<SchemaLogEntry>> ReadHistoryAsync(
    string tableName,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    IReadOnlyList<SchemaLogEntry> empty = Array.Empty<SchemaLogEntry>();
    return ValueTask.FromResult(empty);
  }

  public ValueTask<IReadOnlyList<SchemaBackfillTask>> ReadBackfillQueueAsync(
    string tableName,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    IReadOnlyList<SchemaBackfillTask> empty = Array.Empty<SchemaBackfillTask>();
    return ValueTask.FromResult(empty);
  }
}
