using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XBase.Demo.Domain.Services;
using XBase.Demo.Domain.Services.Models;

namespace XBase.Demo.Infrastructure.Indexes;

/// <summary>
/// Minimal index lifecycle service that creates placeholder index files on disk.
/// </summary>
public sealed class FileSystemIndexManagementService : IIndexManagementService
{
  private readonly ILogger<FileSystemIndexManagementService> _logger;

  public FileSystemIndexManagementService(ILogger<FileSystemIndexManagementService> logger)
  {
    _logger = logger;
  }

  public async Task<IndexOperationResult> CreateIndexAsync(IndexCreateRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    try
    {
      var tableDirectory = ResolveTableDirectory(request.TablePath);
      var indexPath = Path.Combine(tableDirectory, request.IndexName);
      if (File.Exists(indexPath))
      {
        return IndexOperationResult.Failure($"Index '{request.IndexName}' already exists.");
      }

      Directory.CreateDirectory(tableDirectory);
      await using var stream = new FileStream(indexPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
      var content = Encoding.UTF8.GetBytes($"// Placeholder index for expression: {request.Expression}{Environment.NewLine}");
      await stream.WriteAsync(content, 0, content.Length, cancellationToken);
      await stream.FlushAsync(cancellationToken);

      _logger.LogInformation("Created index placeholder {Index} for table {Table}", indexPath, request.TablePath);
      return IndexOperationResult.Success($"Index '{request.IndexName}' created successfully.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to create index {Index} for table {Table}", request.IndexName, request.TablePath);
      return IndexOperationResult.Failure($"Failed to create index '{request.IndexName}'. {ex.Message}", ex);
    }
  }

  public Task<IndexOperationResult> DropIndexAsync(IndexDropRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    try
    {
      var tableDirectory = ResolveTableDirectory(request.TablePath);
      var indexPath = Path.Combine(tableDirectory, request.IndexName);
      if (!File.Exists(indexPath))
      {
        return Task.FromResult(IndexOperationResult.Failure($"Index '{request.IndexName}' was not found."));
      }

      File.Delete(indexPath);
      _logger.LogInformation("Dropped index placeholder {Index} for table {Table}", indexPath, request.TablePath);
      return Task.FromResult(IndexOperationResult.Success($"Index '{request.IndexName}' dropped successfully."));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to drop index {Index} for table {Table}", request.IndexName, request.TablePath);
      return Task.FromResult(IndexOperationResult.Failure($"Failed to drop index '{request.IndexName}'. {ex.Message}", ex));
    }
  }

  private static string ResolveTableDirectory(string tablePath)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(tablePath);
    var tableDirectory = Path.GetDirectoryName(tablePath);
    if (string.IsNullOrWhiteSpace(tableDirectory))
    {
      throw new DirectoryNotFoundException($"Unable to resolve directory for table path '{tablePath}'.");
    }

    return tableDirectory;
  }
}
