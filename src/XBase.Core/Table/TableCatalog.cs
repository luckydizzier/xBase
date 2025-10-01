using System;
using System.Collections.Generic;
using System.IO;
using XBase.Abstractions;
using XBase.Core.Ddl;

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

  public IReadOnlyList<ITableDescriptor> EnumerateTables(string directoryPath)
  {
    if (directoryPath is null)
    {
      throw new ArgumentNullException(nameof(directoryPath));
    }

    if (!Directory.Exists(directoryPath))
    {
      throw new DirectoryNotFoundException($"Directory '{directoryPath}' was not found.");
    }

    List<ITableDescriptor> tables = new();
    foreach (string dbfPath in Directory.EnumerateFiles(directoryPath, "*.dbf", SearchOption.TopDirectoryOnly))
    {
      ITableDescriptor descriptor = loader.Load(dbfPath);
      SchemaVersion version = ResolveSchemaVersion(dbfPath);
      tables.Add(ApplyVersion(descriptor, version));
    }

    tables.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
    return tables;
  }

  private static ITableDescriptor ApplyVersion(ITableDescriptor descriptor, SchemaVersion version)
  {
    if (descriptor is DbfTableDescriptor dbf)
    {
      return dbf.WithSchemaVersion(version);
    }

    return new TableDescriptor(
      descriptor.Name,
      descriptor.MemoFileName,
      descriptor.Fields,
      descriptor.Indexes,
      version);
  }

  private static SchemaVersion ResolveSchemaVersion(string dbfPath)
  {
    string logPath = Path.ChangeExtension(dbfPath, ".ddl");
    if (logPath is null || !File.Exists(logPath))
    {
      return SchemaVersion.Start;
    }

    var log = new SchemaLog(logPath);
    return log.GetCurrentVersion();
  }
}
