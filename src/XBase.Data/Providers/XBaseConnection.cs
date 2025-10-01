using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
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

  public XBaseConnection()
    : this(new NoOpCursorFactory(), new NoOpJournal())
  {
  }

  public XBaseConnection(ICursorFactory cursorFactory, IJournal journal)
  {
    _cursorFactory = cursorFactory;
    _journal = journal;
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
    return new XBaseTransaction(this, _journal);
  }

  protected override DbCommand CreateDbCommand()
  {
    return new XBaseCommand(this, _cursorFactory);
  }
}
