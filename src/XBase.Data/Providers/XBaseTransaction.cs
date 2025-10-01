using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Data.Providers;

public sealed class XBaseTransaction : DbTransaction
{
  private readonly XBaseConnection _connection;
  private readonly IJournal _journal;
  private bool _disposed;

  public XBaseTransaction(XBaseConnection connection, IJournal journal)
  {
    _connection = connection;
    _journal = journal;
  }

  public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

  protected override DbConnection DbConnection => _connection;

  public override void Commit()
  {
    _journal.CommitAsync().GetAwaiter().GetResult();
  }

  public override void Rollback()
  {
    _journal.RollbackAsync().GetAwaiter().GetResult();
  }

  public override Task CommitAsync(CancellationToken cancellationToken = default)
  {
    return _journal.CommitAsync(cancellationToken).AsTask();
  }

  public override Task RollbackAsync(CancellationToken cancellationToken = default)
  {
    return _journal.RollbackAsync(cancellationToken).AsTask();
  }

  protected override void Dispose(bool disposing)
  {
    if (!_disposed)
    {
      _disposed = true;
      base.Dispose(disposing);
    }
  }
}
