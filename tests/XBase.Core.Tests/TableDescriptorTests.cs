using XBase.Abstractions;
using XBase.Core.Table;

namespace XBase.Core.Tests;

public sealed class TableDescriptorTests
{
  [Fact]
  public void Constructor_PopulatesCollections()
  {
    IFieldDescriptor[] fields =
    {
      new FieldDescriptor("Name", "C", 10, 0, false)
    };

    IIndexDescriptor[] indexes =
    {
      new IndexDescriptor("Name", "UPPER(Name)", false)
    };

    var descriptor = new TableDescriptor("Products", "Products.dbt", fields, indexes);

    Assert.Equal("Products", descriptor.Name);
    Assert.Equal("Products.dbt", descriptor.MemoFileName);
    Assert.Single(descriptor.Fields);
    Assert.Single(descriptor.Indexes);
  }
}
