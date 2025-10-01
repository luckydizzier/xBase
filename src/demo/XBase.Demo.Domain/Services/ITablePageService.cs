using System.Threading;
using System.Threading.Tasks;
using XBase.Demo.Domain.Catalog;

namespace XBase.Demo.Domain.Services;

/// <summary>
/// Provides paginated access to table data for browsing scenarios.
/// </summary>
public interface ITablePageService
{
  Task<TablePage> LoadPageAsync(TableModel table, TablePageRequest request, CancellationToken cancellationToken = default);
}
