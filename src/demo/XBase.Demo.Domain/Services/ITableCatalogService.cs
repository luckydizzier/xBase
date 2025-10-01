using System.Threading;
using System.Threading.Tasks;
using XBase.Demo.Domain.Catalog;

namespace XBase.Demo.Domain.Services;

/// <summary>
/// Provides catalog discovery and table metadata loading capabilities.
/// </summary>
public interface ITableCatalogService
{
  /// <summary>
  /// Scans the provided root directory for xBase table artifacts.
  /// </summary>
  /// <param name="rootPath">The directory containing DBF/DBT/NTX assets.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns>A populated <see cref="CatalogModel"/> describing the catalog.</returns>
  Task<CatalogModel> LoadCatalogAsync(string rootPath, CancellationToken cancellationToken = default);
}
