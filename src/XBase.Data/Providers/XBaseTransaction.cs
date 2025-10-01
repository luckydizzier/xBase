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
  private bool _completed;

  public XBaseTransaction(XBaseConnection connection, IJournal journal, bool journalStarted = false)
  {
    _connection = connection;
    _journal = journal;
    if (!journalStarted)
    {
      _journal.BeginAsync().GetAwaiter().GetResult();
    }
  }

  public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

  protected override DbConnection DbConnection => _connection;

  public override void Commit()
  {
    _journal.CommitAsync().GetAwaiter().GetResult();
    _completed = true;
  }

  public override void Rollback()
  {
    _journal.RollbackAsync().GetAwaiter().GetResult();
    _completed = true;
  }

  public override Task CommitAsync(CancellationToken cancellationToken = default)
  {
    return CommitInternalAsync(cancellationToken);
  }

  public override Task RollbackAsync(CancellationToken cancellationToken = default)
  {
    return RollbackInternalAsync(cancellationToken);
  }

  protected override void Dispose(bool disposing)
  {
    if (!_disposed)
    {
      if (disposing && !_completed)
      {
        _journal.RollbackAsync().GetAwaiter().GetResult();
        _completed = true;
      }
      _disposed = true;
      base.Dispose(disposing);
    }
  }

  private async Task CommitInternalAsync(CancellationToken cancellationToken)
  {
    await _journal.CommitAsync(cancellationToken).ConfigureAwait(false);
    _completed = true;
  }

  private async Task RollbackInternalAsync(CancellationToken cancellationToken)
  {
    await _journal.RollbackAsync(cancellationToken).ConfigureAwait(false);
    _completed = true;
  }
}
