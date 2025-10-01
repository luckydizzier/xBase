using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace XBase.EFCore.Extensions;

public static class XBaseDbContextOptionsBuilderExtensions
{
  public static DbContextOptionsBuilder UseXBase(this DbContextOptionsBuilder builder, string connectionString)
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentNullException.ThrowIfNull(connectionString);

    var extension = GetOrCreateExtension(builder);
    extension = extension.WithConnectionString(connectionString);
    ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);

    var relationalExtension = GetOrCreateRelationalExtension(builder);
    relationalExtension = relationalExtension.WithConnectionString(connectionString);
    ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(relationalExtension);
    return builder;
  }

  public static DbContextOptionsBuilder<TContext> UseXBase<TContext>(this DbContextOptionsBuilder<TContext> builder, string connectionString)
    where TContext : DbContext
  {
    UseXBase((DbContextOptionsBuilder)builder, connectionString);
    return builder;
  }

  private static XBaseOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder builder)
  {
    return builder.Options.FindExtension<XBaseOptionsExtension>() ?? new XBaseOptionsExtension();
  }

  private static XBaseRelationalOptionsExtension GetOrCreateRelationalExtension(DbContextOptionsBuilder builder)
  {
    return builder.Options.FindExtension<XBaseRelationalOptionsExtension>() ?? new XBaseRelationalOptionsExtension();
  }
}
