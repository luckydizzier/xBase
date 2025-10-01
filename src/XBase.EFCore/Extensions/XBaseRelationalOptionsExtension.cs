using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace XBase.EFCore.Extensions;

internal sealed class XBaseRelationalOptionsExtension : RelationalOptionsExtension
{
  private DbContextOptionsExtensionInfo? _info;

  public XBaseRelationalOptionsExtension()
  {
  }

  private XBaseRelationalOptionsExtension(XBaseRelationalOptionsExtension copy)
    : base(copy)
  {
  }

  protected override RelationalOptionsExtension Clone()
  {
    return new XBaseRelationalOptionsExtension(this);
  }

  public override DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

  public override void ApplyServices(IServiceCollection services)
  {
    services.AddEntityFrameworkXBase();
  }

  public new XBaseRelationalOptionsExtension WithConnectionString(string connectionString)
  {
    return (XBaseRelationalOptionsExtension)base.WithConnectionString(connectionString);
  }

  private sealed class ExtensionInfo : RelationalExtensionInfo
  {
    private readonly XBaseRelationalOptionsExtension _extension;

    public ExtensionInfo(XBaseRelationalOptionsExtension extension)
      : base(extension)
    {
      _extension = extension;
    }

    public override bool IsDatabaseProvider => true;

    public override string LogFragment => "using XBase ";

    public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
    {
      debugInfo["XBase:ConnectionString"] = _extension.ConnectionString ?? string.Empty;
    }
  }
}
