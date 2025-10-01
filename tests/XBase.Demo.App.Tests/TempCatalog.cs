using System;
using System.IO;

namespace XBase.Demo.App.Tests;

internal sealed class TempCatalog : IDisposable
{
  private readonly DirectoryInfo _directory;

  public TempCatalog()
  {
    _directory = Directory.CreateTempSubdirectory("xbase-demo-");
  }

  public string Path => _directory.FullName;

  public void AddTable(string tableName, bool addPlaceholderIndex = true)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

    var tablePath = System.IO.Path.Combine(Path, tableName + ".dbf");
    File.WriteAllBytes(tablePath, Array.Empty<byte>());

    if (addPlaceholderIndex)
    {
      AddIndex(tableName, tableName + ".ntx");
    }
  }

  public void AddIndex(string tableName, string indexName)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
    ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

    var indexPath = System.IO.Path.Combine(Path, indexName);
    File.WriteAllBytes(indexPath, Array.Empty<byte>());
  }

  public void Dispose()
  {
    try
    {
      _directory.Delete(true);
    }
    catch
    {
      // best effort cleanup
    }
  }
}
