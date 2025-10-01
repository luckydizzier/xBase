using System;
using System.Buffers;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Cursors;
using XBase.Core.Table;

namespace XBase.Data.Providers;

public sealed class XBaseCommand : DbCommand
{
  private static readonly ITableDescriptor EmptyTable = new TableDescriptor(
    "_command",
    null,
    Array.Empty<IFieldDescriptor>(),
    Array.Empty<IIndexDescriptor>());

  private readonly XBaseConnection _connection;
  private readonly ICursorFactory _cursorFactory;
  private string _commandText = string.Empty;
  private readonly XBaseParameterCollection _parameters = new();

  public XBaseCommand(XBaseConnection connection, ICursorFactory cursorFactory)
  {
    _connection = connection;
    _cursorFactory = cursorFactory;
  }

  [AllowNull]
  public override string CommandText
  {
    get => _commandText;
    set => _commandText = value ?? string.Empty;
  }

  public override int CommandTimeout { get; set; } = 30;

  public override CommandType CommandType { get; set; } = CommandType.Text;

  public override bool DesignTimeVisible { get; set; }

  public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.None;

  protected override DbConnection? DbConnection
  {
    get => _connection;
    set => throw new NotSupportedException();
  }

  protected override DbParameterCollection DbParameterCollection => _parameters;

  protected override DbTransaction? DbTransaction { get; set; }

  public override void Cancel()
  {
  }

  public override int ExecuteNonQuery()
  {
    return 0;
  }

  public override object? ExecuteScalar()
  {
    return null;
  }

  public override void Prepare()
  {
  }

  protected override DbParameter CreateDbParameter()
  {
    return new XBaseParameter();
  }

  protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
  {
    var cursor = _cursorFactory
      .CreateSequentialAsync(EmptyTable, new CursorOptions(false, null, null))
      .GetAwaiter()
      .GetResult();

    try
    {
      return new XBaseDataReader(Array.Empty<ReadOnlySequence<byte>>());
    }
    finally
    {
      cursor.DisposeAsync().GetAwaiter().GetResult();
    }
  }

  public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(0);
  }

  public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult<object?>(null);
  }

  protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    DbDataReader reader = new XBaseDataReader(Array.Empty<ReadOnlySequence<byte>>());
    return Task.FromResult(reader);
  }
}
