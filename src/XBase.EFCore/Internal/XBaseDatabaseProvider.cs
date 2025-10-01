using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using XBase.EFCore.Extensions;

namespace XBase.EFCore.Internal;

public sealed class XBaseDatabaseProvider : IDatabaseProvider
{
  public string Name => "XBase";

  public bool IsConfigured(IDbContextOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);
    return options.Extensions.OfType<XBaseOptionsExtension>().Any();
  }
}
