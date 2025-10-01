using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using XBase.Abstractions;
using XBase.Data.Providers;

namespace XBase.EFCore.Internal;

internal sealed class XBaseRelationalConnection : RelationalConnection
{
  private readonly XBaseConnection? _providedConnection;
  private readonly string? _connectionString;
  private readonly ICursorFactory _cursorFactory;
  private readonly IJournal _journal;
  private readonly ISchemaMutator _schemaMutator;
  private readonly ITableResolver _tableResolver;

  public XBaseRelationalConnection(
    RelationalConnectionDependencies dependencies,
    XBaseConnection? connection,
    string? connectionString,
    ICursorFactory cursorFactory,
    IJournal journal,
    ISchemaMutator schemaMutator,
    ITableResolver tableResolver)
    : base(dependencies)
  {
    _providedConnection = connection;
    _connectionString = connectionString;
    _cursorFactory = cursorFactory ?? throw new ArgumentNullException(nameof(cursorFactory));
    _journal = journal ?? throw new ArgumentNullException(nameof(journal));
    _schemaMutator = schemaMutator ?? throw new ArgumentNullException(nameof(schemaMutator));
    _tableResolver = tableResolver ?? throw new ArgumentNullException(nameof(tableResolver));
  }

  protected override DbConnection CreateDbConnection()
  {
    if (_providedConnection is not null)
    {
      if (!string.IsNullOrEmpty(_connectionString))
      {
        _providedConnection.ConnectionString = _connectionString;
      }

      return _providedConnection;
    }

    var connection = new XBaseConnection(_cursorFactory, _journal, _schemaMutator, _tableResolver);
    if (!string.IsNullOrEmpty(_connectionString))
    {
      connection.ConnectionString = _connectionString;
    }

    return connection;
  }
}
