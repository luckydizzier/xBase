using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Cursors;

public sealed class NoOpCursorFactory : ICursorFactory
{
  public ValueTask<ICursor> CreateSequentialAsync(ITableDescriptor table, CursorOptions options, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.FromResult<ICursor>(new EmptyCursor());
  }

  public ValueTask<ICursor> CreateIndexedAsync(ITableDescriptor table, IIndexDescriptor index, CursorOptions options, CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.FromResult<ICursor>(new EmptyCursor());
  }
}
