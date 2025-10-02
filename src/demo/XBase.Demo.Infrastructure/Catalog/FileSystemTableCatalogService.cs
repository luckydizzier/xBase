using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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

    var tableFiles = Directory
      .EnumerateFiles(absoluteRoot, "*", SearchOption.TopDirectoryOnly)
      .Where(file => string.Equals(Path.GetExtension(file), ".dbf", StringComparison.OrdinalIgnoreCase))
      .Distinct(StringComparer.OrdinalIgnoreCase);

    foreach (var tableFile in tableFiles)
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
        var metadata = TryReadIndexMetadata(fileInfo);
        indexes.Add(new IndexModel(Path.GetFileName(indexFile), metadata?.Expression ?? extension.ToUpperInvariant())
        {
          FullPath = fileInfo.FullName,
          SizeBytes = fileInfo.Exists ? fileInfo.Length : null,
          LastModifiedUtc = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : null,
          Signature = metadata?.Signature,
          ActiveRecordCount = metadata?.ActiveRecords,
          TotalRecordCount = metadata?.TotalRecords
        });
      }

      tables.Add(new TableModel(tableName, tableFile, indexes));
    }

    _logger.LogInformation("Discovered {TableCount} tables under {RootPath}", tables.Count, absoluteRoot);
    return Task.FromResult(new CatalogModel(absoluteRoot, tables));
  }

  private static IndexMetadata? TryReadIndexMetadata(FileInfo fileInfo)
  {
    try
    {
      if (!fileInfo.Exists)
      {
        return null;
      }

      using var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      using var document = JsonDocument.Parse(stream);
      var root = document.RootElement;

      if (!root.TryGetProperty("formatVersion", out _))
      {
        return null;
      }

      if (!root.TryGetProperty("expression", out var expressionElement))
      {
        return null;
      }

      var expression = expressionElement.GetString();
      if (string.IsNullOrWhiteSpace(expression))
      {
        return null;
      }

      string? signature = null;
      if (root.TryGetProperty("signature", out var signatureElement))
      {
        signature = signatureElement.GetString();
      }

      int? activeRecords = null;
      int? totalRecords = null;
      if (root.TryGetProperty("statistics", out var statsElement))
      {
        if (statsElement.TryGetProperty("activeRecords", out var activeElement))
        {
          activeRecords = activeElement.GetInt32();
        }

        if (statsElement.TryGetProperty("totalRecords", out var totalElement))
        {
          totalRecords = totalElement.GetInt32();
        }
      }

      return new IndexMetadata(expression, signature, activeRecords, totalRecords);
    }
    catch (JsonException)
    {
      return null;
    }
    catch (IOException)
    {
      return null;
    }
  }

  private sealed record IndexMetadata(string Expression, string? Signature, int? ActiveRecords, int? TotalRecords);
}
