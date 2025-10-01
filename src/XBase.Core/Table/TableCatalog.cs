using System;
using System.Collections.Generic;
using System.IO;

namespace XBase.Core.Table;

public sealed class TableCatalog
{
  private readonly DbfTableLoader loader;

  public TableCatalog() : this(new DbfTableLoader())
  {
  }

  public TableCatalog(DbfTableLoader loader)
  {
    this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
  }

  public IReadOnlyList<DbfTableDescriptor> EnumerateTables(string directoryPath)
  {
    if (directoryPath is null)
    {
      throw new ArgumentNullException(nameof(directoryPath));
    }

    if (!Directory.Exists(directoryPath))
    {
      throw new DirectoryNotFoundException($"Directory '{directoryPath}' was not found.");
    }

    List<DbfTableDescriptor> tables = new();
    foreach (string dbfPath in Directory.EnumerateFiles(directoryPath, "*.dbf", SearchOption.TopDirectoryOnly))
    {
      tables.Add(loader.Load(dbfPath));
    }

    tables.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
    return tables;
  }
}
