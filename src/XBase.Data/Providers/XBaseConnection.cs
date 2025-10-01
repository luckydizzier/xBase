using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Cursors;
using XBase.Core.Transactions;

namespace XBase.Data.Providers;

public sealed class XBaseConnection : DbConnection
{
  private string _connectionString = string.Empty;
  private ConnectionState _state = ConnectionState.Closed;
  private readonly ICursorFactory _cursorFactory;
  private readonly IJournal _journal;
  private readonly ISchemaMutator _schemaMutator;

  public XBaseConnection()
    : this(new NoOpCursorFactory(), new NoOpJournal(), new NoOpSchemaMutator())
  {
  }

  public XBaseConnection(ICursorFactory cursorFactory, IJournal journal, ISchemaMutator schemaMutator)
  {
    _cursorFactory = cursorFactory;
    _journal = journal;
    _schemaMutator = schemaMutator;
  }

  [AllowNull]
  public override string ConnectionString
  {
    get => _connectionString;
    set => _connectionString = value ?? string.Empty;
  }

  public override string Database => "xBase";

  public override string DataSource => ConnectionString;

  public override string ServerVersion => "0.1";

  public override ConnectionState State => _state;

  public override void ChangeDatabase(string databaseName)
  {
    if (string.IsNullOrWhiteSpace(databaseName))
    {
      throw new ArgumentException("Value cannot be null or whitespace.", nameof(databaseName));
    }
  }

  public override void Open()
  {
    _state = ConnectionState.Open;
  }

  public override void Close()
  {
    _state = ConnectionState.Closed;
  }

  protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
  {
    _journal.BeginAsync().GetAwaiter().GetResult();

    return new XBaseTransaction(this, _journal, journalStarted: true);
  }

  protected override async ValueTask<DbTransaction> BeginDbTransactionAsync(
    IsolationLevel isolationLevel,
    CancellationToken cancellationToken = default)
  {
    await _journal.BeginAsync(cancellationToken).ConfigureAwait(false);

    return new XBaseTransaction(this, _journal, journalStarted: true);
  }

  public new ValueTask<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
  {
    return BeginTransactionAsync(IsolationLevel.Unspecified, cancellationToken);
  }

  public new ValueTask<DbTransaction> BeginTransactionAsync(
    IsolationLevel isolationLevel,
    CancellationToken cancellationToken = default)
  {
    return BeginDbTransactionAsync(isolationLevel, cancellationToken);
  }

  protected override DbCommand CreateDbCommand()
  {
    return new XBaseCommand(this, _cursorFactory, _schemaMutator);
  }

  private sealed class NoOpSchemaMutator : ISchemaMutator
  {
    public ValueTask<SchemaVersion> ExecuteAsync(
      SchemaOperation operation,
      string? author = null,
      CancellationToken cancellationToken = default)
    {
      return ValueTask.FromResult(SchemaVersion.Start);
    }

    public ValueTask<IReadOnlyList<SchemaLogEntry>> ReadHistoryAsync(
      string tableName,
      CancellationToken cancellationToken = default)
    {
      IReadOnlyList<SchemaLogEntry> empty = Array.Empty<SchemaLogEntry>();
      return ValueTask.FromResult(empty);
    }

    public ValueTask<IReadOnlyList<SchemaBackfillTask>> ReadBackfillQueueAsync(
      string tableName,
      CancellationToken cancellationToken = default)
    {
      IReadOnlyList<SchemaBackfillTask> empty = Array.Empty<SchemaBackfillTask>();
      return ValueTask.FromResult(empty);
    }
  }
}
