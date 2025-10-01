using System.Collections.Generic;
using XBase.Abstractions;
using XBase.Core.Table;
using Xunit;

namespace XBase.Core.Tests;

internal static class DbfFieldAssertions
{
  public static void Equal(DbfFieldSchema expected, DbfFieldSchema actual)
  {
    Assert.NotNull(actual);
    Assert.Equal(expected.Name, actual.Name);
    Assert.Equal(expected.Type, actual.Type);
    Assert.Equal(expected.Length, actual.Length);
    Assert.Equal(expected.DecimalCount, actual.DecimalCount);
    Assert.Equal(expected.IsNullable, actual.IsNullable);
  }

  public static void Equal(DbfFieldSchema expected, IFieldDescriptor actual)
  {
    Assert.NotNull(actual);
    Assert.Equal(expected.Name, actual.Name);
    Assert.Equal(expected.Type.ToString(), actual.Type);
    Assert.Equal(expected.Length, actual.Length);
    Assert.Equal(expected.DecimalCount, actual.DecimalCount);
    Assert.Equal(expected.IsNullable, actual.IsNullable);
  }

  public static void SequenceEqual(IReadOnlyList<DbfFieldSchema> expected, IReadOnlyList<DbfFieldSchema> actual)
  {
    Assert.Equal(expected.Count, actual.Count);
    for (int index = 0; index < expected.Count; index++)
    {
      Equal(expected[index], actual[index]);
    }
  }

  public static void SequenceEqual(IReadOnlyList<DbfFieldSchema> expected, IReadOnlyList<IFieldDescriptor> actual)
  {
    Assert.Equal(expected.Count, actual.Count);
    for (int index = 0; index < expected.Count; index++)
    {
      Equal(expected[index], actual[index]);
    }
  }
}
