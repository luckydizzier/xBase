using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;
using XBase.Core.Table;

namespace XBase.Core.Cursors;

public sealed class DbfCursorFactory : ICursorFactory
{
  public ValueTask<ICursor> CreateSequentialAsync(
    ITableDescriptor table,
    CursorOptions options,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.FromResult<ICursor>(CreateCursor(table, options));
  }

  public ValueTask<ICursor> CreateIndexedAsync(
    ITableDescriptor table,
    IIndexDescriptor index,
    CursorOptions options,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.FromResult<ICursor>(CreateCursor(table, options));
  }

  private static ICursor CreateCursor(ITableDescriptor table, CursorOptions options)
  {
    if (table is not DbfTableDescriptor descriptor)
    {
      throw new InvalidOperationException(
        "DbfCursorFactory currently supports only DBF-backed table descriptors.");
    }

    if (string.IsNullOrWhiteSpace(descriptor.FilePath))
    {
      throw new InvalidOperationException(
        $"Table '{descriptor.Name}' is missing an associated file path.");
    }

    if (!File.Exists(descriptor.FilePath))
    {
      throw new FileNotFoundException(
        $"DBF file for table '{descriptor.Name}' was not found.",
        descriptor.FilePath);
    }

    return new DbfCursor(
      descriptor.FilePath,
      descriptor.HeaderLength,
      descriptor.RecordLength,
      options.IncludeDeleted);
  }
}
