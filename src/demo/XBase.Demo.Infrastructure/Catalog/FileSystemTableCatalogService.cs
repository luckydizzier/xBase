using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using XBase.Demo.Domain.Catalog;
using XBase.Demo.Domain.Services;

namespace XBase.Demo.Infrastructure.Catalog;

/// <summary>
/// Minimal file-system backed implementation that surfaces DBF tables and common index artifacts.
/// </summary>
public sealed class FileSystemTableCatalogService : ITableCatalogService
{
  private static readonly string[] SupportedIndexExtensions = [".ntx", ".mdx", ".ndx"];
  private readonly ILogger<FileSystemTableCatalogService> _logger;

  public FileSystemTableCatalogService(ILogger<FileSystemTableCatalogService> logger)
  {
    _logger = logger;
  }

  public Task<CatalogModel> LoadCatalogAsync(string rootPath, CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

    if (!Directory.Exists(rootPath))
    {
      throw new DirectoryNotFoundException($"Catalog root '{rootPath}' was not found.");
    }

    var absoluteRoot = Path.GetFullPath(rootPath);
    var tables = new List<TableModel>();

    foreach (var tableFile in Directory.EnumerateFiles(absoluteRoot, "*.dbf", SearchOption.TopDirectoryOnly))
    {
      cancellationToken.ThrowIfCancellationRequested();

      var tableName = Path.GetFileNameWithoutExtension(tableFile);
      var directory = Path.GetDirectoryName(tableFile)!;

      var indexes = new List<IndexModel>();
      foreach (var indexFile in Directory.EnumerateFiles(directory, $"{tableName}.*", SearchOption.TopDirectoryOnly))
      {
        var extension = Path.GetExtension(indexFile);
        if (!SupportedIndexExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
          continue;
        }

        var fileInfo = new FileInfo(indexFile);
        indexes.Add(new IndexModel(Path.GetFileName(indexFile), extension.ToUpperInvariant())
        {
          FullPath = fileInfo.FullName,
          SizeBytes = fileInfo.Exists ? fileInfo.Length : null,
          LastModifiedUtc = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : null
        });
      }

      tables.Add(new TableModel(tableName, tableFile, indexes));
    }

    _logger.LogInformation("Discovered {TableCount} tables under {RootPath}", tables.Count, absoluteRoot);
    return Task.FromResult(new CatalogModel(absoluteRoot, tables));
  }
}
