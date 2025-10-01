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
  private readonly ITableResolver? _tableResolver;
  private readonly SqlTableResolver _defaultTableResolver;

  public XBaseConnection()
    : this(new DbfCursorFactory(), new NoOpJournal(), new NoOpSchemaMutator(), tableResolver: null)
  {
  }

  public XBaseConnection(ICursorFactory cursorFactory, IJournal journal, ISchemaMutator schemaMutator)
    : this(cursorFactory, journal, schemaMutator, tableResolver: null)
  {
  }

  public XBaseConnection(
    ICursorFactory cursorFactory,
    IJournal journal,
    ISchemaMutator schemaMutator,
    ITableResolver? tableResolver)
  {
    _cursorFactory = cursorFactory ?? throw new ArgumentNullException(nameof(cursorFactory));
    _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    _schemaMutator = schemaMutator ?? throw new ArgumentNullException(nameof(schemaMutator));
    _tableResolver = tableResolver;
    _defaultTableResolver = new SqlTableResolver(() => Options);
  }

  public XBaseConnectionOptions Options { get; private set; } = XBaseConnectionOptions.Default;

  [AllowNull]
  public override string ConnectionString
  {
    get => _connectionString;
    set
    {
      string newValue = value ?? string.Empty;
      _connectionString = newValue;
      Options = XBaseConnectionOptions.Parse(newValue);
    }
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
    ITableResolver resolver = _tableResolver ?? _defaultTableResolver;
    return new XBaseCommand(this, _cursorFactory, _schemaMutator, resolver);
  }
}
