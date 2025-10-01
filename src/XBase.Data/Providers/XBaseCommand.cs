using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Cursors;

namespace XBase.Data.Providers;

public sealed class XBaseCommand : DbCommand
{
  private readonly XBaseConnection _connection;
  private readonly ICursorFactory _cursorFactory;
  private string _commandText = string.Empty;
  private readonly XBaseParameterCollection _parameters = new();
  private readonly ISchemaMutator _schemaMutator;
  private readonly ITableResolver _tableResolver;
  private SchemaVersion? _lastSchemaVersion;

  public XBaseCommand(
    XBaseConnection connection,
    ICursorFactory cursorFactory,
    ISchemaMutator schemaMutator,
    ITableResolver tableResolver)
  {
    _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    _cursorFactory = cursorFactory ?? throw new ArgumentNullException(nameof(cursorFactory));
    _schemaMutator = schemaMutator ?? throw new ArgumentNullException(nameof(schemaMutator));
    _tableResolver = tableResolver ?? throw new ArgumentNullException(nameof(tableResolver));
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
    (bool executed, object? scalar) = TryExecuteSchemaCommandAsync(CancellationToken.None)
      .GetAwaiter()
      .GetResult();
    if (!executed)
    {
      return 0;
    }

    return scalar is int value ? value : 0;
  }

  public override object? ExecuteScalar()
  {
    (bool executed, object? scalar) = TryExecuteSchemaCommandAsync(CancellationToken.None)
      .GetAwaiter()
      .GetResult();
    return executed ? scalar : null;
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
    return ExecuteReaderCoreAsync(behavior, CancellationToken.None).GetAwaiter().GetResult();
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
    return ExecuteReaderCoreAsync(behavior, cancellationToken).AsTask();
  }

  private async Task<object?> ExecuteSchemaAsync(CancellationToken cancellationToken, bool returnScalar)
  {
    cancellationToken.ThrowIfCancellationRequested();
    (bool executed, object? scalar) = await TryExecuteSchemaCommandAsync(cancellationToken).ConfigureAwait(false);
    if (!executed)
    {
      return returnScalar ? null : 0;
    }

    if (returnScalar)
    {
      return scalar;
    }

    return scalar is int value ? value : 0;
  }

  private async ValueTask<DbDataReader> ExecuteReaderCoreAsync(CommandBehavior behavior, CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    (bool executed, _) = await TryExecuteSchemaCommandAsync(cancellationToken).ConfigureAwait(false);
    if (executed)
    {
      return XBaseDataReader.CreateEmpty();
    }

    if (string.IsNullOrWhiteSpace(CommandText))
    {
      return XBaseDataReader.CreateEmpty();
    }

    TableResolveResult? resolution = await _tableResolver
      .ResolveAsync(CommandText, cancellationToken)
      .ConfigureAwait(false);
    if (resolution is not TableResolveResult resolved)
    {
      return XBaseDataReader.CreateEmpty();
    }

    ICursor cursor = await _cursorFactory
      .CreateSequentialAsync(resolved.Table, resolved.Options, cancellationToken)
      .ConfigureAwait(false);

    bool closeConnection = (behavior & CommandBehavior.CloseConnection) == CommandBehavior.CloseConnection;
    try
    {
      return new XBaseDataReader(cursor, resolved.Columns, closeConnection ? _connection : null);
    }
    catch
    {
      await cursor.DisposeAsync().ConfigureAwait(false);
      throw;
    }
  }

  private async Task<(bool executed, object? scalar)> TryExecuteSchemaCommandAsync(CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(CommandText))
    {
      return (false, null);
    }

    if (!SchemaCommandParser.TryParse(CommandText, out SchemaCommand command))
    {
      return (false, null);
    }

    switch (command.Kind)
    {
      case SchemaCommandKind.Operation:
        SchemaOperation operation = command.Operation!;
        _lastSchemaVersion = await _schemaMutator
          .ExecuteAsync(operation, Author, cancellationToken)
          .ConfigureAwait(false);
        return (true, _lastSchemaVersion);
      case SchemaCommandKind.Pack:
        int removed = await _schemaMutator
          .PackAsync(command.TableName, cancellationToken)
          .ConfigureAwait(false);
        _lastSchemaVersion = null;
        return (true, removed);
      case SchemaCommandKind.Reindex:
        int rebuilt = await _schemaMutator
          .ReindexAsync(command.TableName, cancellationToken)
          .ConfigureAwait(false);
        _lastSchemaVersion = null;
        return (true, rebuilt);
      default:
        return (false, null);
    }
  }
}
