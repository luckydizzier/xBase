using System;
using System.Collections.Generic;

namespace XBase.Demo.Domain.Catalog;

/// <summary>
/// Represents a catalog of xBase tables discovered under a root directory.
/// </summary>
/// <param name="RootPath">The file system root the catalog was scanned from.</param>
/// <param name="Tables">The set of tables available for browsing.</param>
public sealed record CatalogModel(string RootPath, IReadOnlyList<TableModel> Tables);

/// <summary>
/// Represents a single table, including metadata necessary for browsing.
/// </summary>
/// <param name="Name">Logical table name.</param>
/// <param name="Path">Absolute path to the DBF file.</param>
/// <param name="Indexes">Associated indexes discovered for the table.</param>
public sealed record TableModel(string Name, string Path, IReadOnlyList<IndexModel> Indexes);

/// <summary>
/// Represents an index that can be applied while browsing.
/// </summary>
/// <param name="Name">Index identifier.</param>
/// <param name="Expression">The expression used to compute the index key.</param>
/// <param name="Order">Optional ordering hint for display.</param>
public sealed record IndexModel(string Name, string Expression, int Order = 0)
{
  /// <summary>
  /// Gets the absolute path to the index artifact when known.
  /// </summary>
  public string? FullPath { get; init; }

  /// <summary>
  /// Gets the recorded size of the index artifact in bytes, if available.
  /// </summary>
  public long? SizeBytes { get; init; }

  /// <summary>
  /// Gets the last modified timestamp of the index artifact expressed in UTC.
  /// </summary>
  public DateTimeOffset? LastModifiedUtc { get; init; }

  /// <summary>
  /// Gets the signature derived from the indexed keys for change detection.
  /// </summary>
  public string? Signature { get; init; }

  /// <summary>
  /// Gets the number of non-deleted records captured in the index artifact, when available.
  /// </summary>
  public int? ActiveRecordCount { get; init; }

  /// <summary>
  /// Gets the total number of records (including deleted entries) captured when building the index.
  /// </summary>
  public int? TotalRecordCount { get; init; }
}
