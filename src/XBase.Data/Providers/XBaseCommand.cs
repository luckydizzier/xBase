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
    Array.Empty<IIndexDescriptor>(),
    SchemaVersion.Start);

  private readonly XBaseConnection _connection;
  private readonly ICursorFactory _cursorFactory;
  private string _commandText = string.Empty;
  private readonly XBaseParameterCollection _parameters = new();
  private readonly ISchemaMutator _schemaMutator;
  private SchemaVersion? _lastSchemaVersion;

  public XBaseCommand(XBaseConnection connection, ICursorFactory cursorFactory, ISchemaMutator schemaMutator)
  {
    _connection = connection;
    _cursorFactory = cursorFactory;
    _schemaMutator = schemaMutator;
  }

  public SchemaVersion? LastSchemaVersion => _lastSchemaVersion;

  public string Author { get; set; } = Environment.UserName ?? "xbase";

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
    if (TryExecuteSchemaOperation(null).GetAwaiter().GetResult())
    {
      return 0;
    }

    return 0;
  }

  public override object? ExecuteScalar()
  {
    if (TryExecuteSchemaOperation(null).GetAwaiter().GetResult())
    {
      return _lastSchemaVersion;
    }

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

  public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
  {
    object? result = await ExecuteSchemaAsync(cancellationToken, returnScalar: false).ConfigureAwait(false);
    return result is int value ? value : 0;
  }

  public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
  {
    return ExecuteSchemaAsync(cancellationToken, returnScalar: true);
  }

  protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    DbDataReader reader = new XBaseDataReader(Array.Empty<ReadOnlySequence<byte>>());
    return Task.FromResult(reader);
  }

  private async Task<object?> ExecuteSchemaAsync(CancellationToken cancellationToken, bool returnScalar)
  {
    cancellationToken.ThrowIfCancellationRequested();
    bool executed = await TryExecuteSchemaOperation(cancellationToken).ConfigureAwait(false);
    if (!executed)
    {
      return returnScalar ? null : 0;
    }

    return returnScalar ? (object?)_lastSchemaVersion : 0;
  }

  private async Task<bool> TryExecuteSchemaOperation(CancellationToken? cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(CommandText))
    {
      return false;
    }

    if (!SchemaCommandParser.TryParse(CommandText, out SchemaOperation operation))
    {
      return false;
    }

    CancellationToken token = cancellationToken ?? CancellationToken.None;
    _lastSchemaVersion = await _schemaMutator
      .ExecuteAsync(operation, Author, token)
      .ConfigureAwait(false);
    return true;
  }
}
