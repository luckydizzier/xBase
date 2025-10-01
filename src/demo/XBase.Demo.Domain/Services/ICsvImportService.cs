using System.Threading;
using System.Threading.Tasks;
using XBase.Demo.Domain.Seed;

namespace XBase.Demo.Domain.Services;

/// <summary>
/// Provides CSV import capabilities for demo scenarios.
/// </summary>
public interface ICsvImportService
{
  Task<CsvImportResult> ImportAsync(CsvImportRequest request, CancellationToken cancellationToken = default);
}
