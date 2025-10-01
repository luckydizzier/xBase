using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace XBase.EFCore.Internal;

internal sealed class XBaseTypeMappingSource : RelationalTypeMappingSource
{
  private static readonly RelationalTypeMapping IntegerMapping = new IntTypeMapping("INTEGER");
  private static readonly RelationalTypeMapping StringMapping = new StringTypeMapping("TEXT", DbType.String, unicode: true, size: null);

  private static readonly IReadOnlyDictionary<string, RelationalTypeMapping> StoreTypeMappings = new Dictionary<string, RelationalTypeMapping>(StringComparer.OrdinalIgnoreCase)
  {
    ["INTEGER"] = IntegerMapping,
    ["INT"] = IntegerMapping,
    ["CHAR"] = StringMapping,
    ["TEXT"] = StringMapping
  };

  private static readonly IReadOnlyDictionary<Type, RelationalTypeMapping> ClrTypeMappings = new Dictionary<Type, RelationalTypeMapping>
  {
    [typeof(int)] = IntegerMapping,
    [typeof(string)] = StringMapping
  };

  public XBaseTypeMappingSource(TypeMappingSourceDependencies dependencies, RelationalTypeMappingSourceDependencies relationalDependencies)
    : base(dependencies, relationalDependencies)
  {
  }

  protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
  {
    if (mappingInfo.ClrType is Type clrType && ClrTypeMappings.TryGetValue(clrType, out var clrMapping))
    {
      return clrMapping;
    }

    if (!string.IsNullOrEmpty(mappingInfo.StoreTypeName) && StoreTypeMappings.TryGetValue(mappingInfo.StoreTypeName!, out var storeMapping))
    {
      return storeMapping;
    }

    if (mappingInfo.ClrType?.IsEnum == true)
    {
      return IntegerMapping;
    }

    return base.FindMapping(mappingInfo);
  }
}
