using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XBase.Abstractions;

public interface ITableResolver
{
  ValueTask<TableResolveResult?> ResolveAsync(string commandText, CancellationToken cancellationToken = default);
}

public sealed record TableResolveResult
{
  public TableResolveResult(ITableDescriptor table, IReadOnlyList<TableColumn> columns, CursorOptions options)
  {
    Table = table ?? throw new ArgumentNullException(nameof(table));
    Columns = columns ?? throw new ArgumentNullException(nameof(columns));
    Options = options;
  }

  public ITableDescriptor Table { get; }

  public IReadOnlyList<TableColumn> Columns { get; }

  public CursorOptions Options { get; }
}

public sealed record TableColumn
{
  public TableColumn(string name, Type clrType, Func<ReadOnlySequence<byte>, object?> valueAccessor)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      throw new ArgumentException("Column name must be provided.", nameof(name));
    }

    Name = name;
    ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
    ValueAccessor = valueAccessor ?? throw new ArgumentNullException(nameof(valueAccessor));
  }

  public string Name { get; }

  public Type ClrType { get; }

  public Func<ReadOnlySequence<byte>, object?> ValueAccessor { get; }
}
