using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Data.Providers;

public sealed class NoOpTableResolver : ITableResolver
{
  public ValueTask<TableResolveResult?> ResolveAsync(string commandText, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.FromResult<TableResolveResult?>(null);
  }
}
