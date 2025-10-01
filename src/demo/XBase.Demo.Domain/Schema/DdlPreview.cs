using System;
using System.Collections.Generic;

namespace XBase.Demo.Domain.Schema;

/// <summary>
/// Represents a set of statements generated for a schema operation preview.
/// </summary>
/// <param name="Operation">Logical operation identifier (e.g. CreateTable, AlterTable).</param>
/// <param name="UpStatements">Statements executed to apply the change.</param>
/// <param name="DownStatements">Statements that undo the change, when available.</param>
public sealed record DdlPreview(string Operation, IReadOnlyList<string> UpStatements, IReadOnlyList<string> DownStatements)
{
  public static DdlPreview Empty(string operation)
      => new(operation, Array.Empty<string>(), Array.Empty<string>());
}
