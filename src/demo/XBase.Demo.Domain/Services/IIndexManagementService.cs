using System;
using System.Threading;
using System.Threading.Tasks;
using XBase.Demo.Domain.Services.Models;

namespace XBase.Demo.Domain.Services;

/// <summary>
/// Provides index lifecycle management for demo scenarios.
/// </summary>
public interface IIndexManagementService
{
  Task<IndexOperationResult> CreateIndexAsync(IndexCreateRequest request, CancellationToken cancellationToken = default);

  Task<IndexOperationResult> DropIndexAsync(IndexDropRequest request, CancellationToken cancellationToken = default);

  IObservable<IndexRebuildProgress> RebuildIndex(IndexRebuildRequest request);
}
