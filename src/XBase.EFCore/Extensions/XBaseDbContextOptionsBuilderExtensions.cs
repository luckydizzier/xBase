using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace XBase.EFCore.Extensions;

public static class XBaseDbContextOptionsBuilderExtensions
{
  public static DbContextOptionsBuilder UseXBase(this DbContextOptionsBuilder builder, string connectionString)
  {
    var infrastructure = (IDbContextOptionsBuilderInfrastructure)builder;
    var extension = builder.Options.FindExtension<XBaseOptionsExtension>() ?? new XBaseOptionsExtension();
    extension = extension.WithConnectionString(connectionString);
    infrastructure.AddOrUpdateExtension(extension);
    return builder;
  }
}
