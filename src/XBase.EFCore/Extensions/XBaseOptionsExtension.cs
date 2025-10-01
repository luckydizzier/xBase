using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XBase.Data.Providers;
using XBase.EFCore.Internal;

namespace XBase.EFCore.Extensions;

public sealed class XBaseOptionsExtension : IDbContextOptionsExtension
{
  private DbContextOptionsExtensionInfo? _info;

  public string? ConnectionString { get; private init; }

  public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

  public XBaseOptionsExtension WithConnectionString(string connectionString)
  {
    return new XBaseOptionsExtension
    {
      ConnectionString = connectionString
    };
  }

  public void ApplyServices(IServiceCollection services)
  {
    services.AddEntityFrameworkXBase();
    services.TryAddScoped<IRelationalConnection>(provider =>
    {
      var options = provider.GetRequiredService<IDbContextOptions>();
      var extension = options.FindExtension<XBaseOptionsExtension>();
      var dependencies = provider.GetRequiredService<RelationalConnectionDependencies>();
      XBaseConnection? connection = provider.GetService<XBaseConnection>();
      return new XBaseRelationalConnection(dependencies, connection, extension?.ConnectionString);
    });
  }

  public void Validate(IDbContextOptions options)
  {
  }

  private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
  {
    private readonly XBaseOptionsExtension _extension;

    public ExtensionInfo(XBaseOptionsExtension extension)
      : base(extension)
    {
      _extension = extension;
    }

    public override bool IsDatabaseProvider => true;

    public override string LogFragment => "using XBase ";

    public override int GetServiceProviderHashCode()
    {
      return _extension.ConnectionString?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }

    public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
    {
      if (other is not ExtensionInfo extensionInfo)
      {
        return false;
      }

      return string.Equals(_extension.ConnectionString, extensionInfo._extension.ConnectionString, StringComparison.Ordinal);
    }

    public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
    {
      debugInfo["XBase:ConnectionString"] = _extension.ConnectionString ?? string.Empty;
    }
  }
}
