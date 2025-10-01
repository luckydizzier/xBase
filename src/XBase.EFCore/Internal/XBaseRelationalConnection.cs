using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using XBase.Data.Providers;

namespace XBase.EFCore.Internal;

internal sealed class XBaseRelationalConnection : RelationalConnection
{
  private readonly XBaseConnection? _providedConnection;
  private readonly string? _connectionString;

  public XBaseRelationalConnection(RelationalConnectionDependencies dependencies, XBaseConnection? connection, string? connectionString)
    : base(dependencies)
  {
    _providedConnection = connection;
    _connectionString = connectionString;
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

    var connection = new XBaseConnection();
    if (!string.IsNullOrEmpty(_connectionString))
    {
      connection.ConnectionString = _connectionString;
    }

    return connection;
  }
}
