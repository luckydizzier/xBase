using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XBase.Abstractions;
using XBase.Core.Table;
using Xunit;

namespace XBase.Core.Tests;

public sealed class DbfTableLoaderTests
{
  public static IEnumerable<object[]> FixtureMetadata()
  {
    foreach (DbfFixtureDescriptor fixture in DbfFixtureLibrary.All)
    {
      yield return new object[] { fixture };
    }
  }

  [Theory]
  [MemberData(nameof(FixtureMetadata))]
  public void Load_FromFile_ParsesHeader(DbfFixtureDescriptor fixture)
  {
    string path = fixture.EnsureMaterialized();
    var loader = new DbfTableLoader();

    DbfTableDescriptor descriptor = loader.LoadDbf(path);

    Assert.Equal(fixture.Version, descriptor.Version);
    Assert.Equal(fixture.LanguageDriverId, descriptor.LanguageDriverId);
    Assert.Equal(fixture.HeaderLength, descriptor.HeaderLength);
    Assert.Equal(fixture.RecordLength, descriptor.RecordLength);
    Assert.Equal(fixture.RecordCount, descriptor.RecordCount);
    Assert.Equal(fixture.LastUpdated, descriptor.LastUpdated);
    Assert.Equal(fixture.FieldCount, descriptor.Fields.Count);

    var expectedSchemas = fixture.ExpectedFields;
    DbfFieldAssertions.SequenceEqual(expectedSchemas, descriptor.FieldSchemas);
    DbfFieldAssertions.SequenceEqual(expectedSchemas, descriptor.Fields);
  }

  [Fact]
  public void Load_WithMemoAndIndexSidecars_DetectsCompanionFiles()
  {
    DbfFixtureDescriptor fixture = DbfFixtureLibrary.Get("dBASE_IV_Memo");
    using var workspace = new TemporaryWorkspace();
    string tablePath = fixture.CopyTo(workspace.DirectoryPath);
    string tableName = Path.GetFileNameWithoutExtension(tablePath)!;

    string memoPath = Path.Combine(workspace.DirectoryPath, tableName + ".dbt");
    File.WriteAllBytes(memoPath, Array.Empty<byte>());

    string ntxPath = Path.Combine(workspace.DirectoryPath, tableName + ".ntx");
    File.WriteAllBytes(ntxPath, Array.Empty<byte>());

    string mdxPath = Path.Combine(workspace.DirectoryPath, tableName + ".mdx");
    File.WriteAllBytes(mdxPath, Array.Empty<byte>());

    var loader = new DbfTableLoader();

    DbfTableDescriptor descriptor = loader.LoadDbf(tablePath);

    Assert.Equal(Path.GetFileName(memoPath), descriptor.MemoFileName);
    Assert.Contains(Path.GetFileName(ntxPath), descriptor.Sidecars.IndexFileNames);
    Assert.Contains(Path.GetFileName(mdxPath), descriptor.Sidecars.IndexFileNames);

    IReadOnlyList<IIndexDescriptor> indexes = descriptor.Indexes;
    Assert.Equal(2, indexes.Count);
    Assert.All(indexes, index => Assert.Equal(tableName, index.Name));
  }

  [Fact]
  public void Load_FromStream_UsesSidecarContext()
  {
    DbfFixtureDescriptor fixture = DbfFixtureLibrary.Get("FoxPro_26");
    using var workspace = new TemporaryWorkspace();
    string tablePath = fixture.CopyTo(workspace.DirectoryPath);
    string tableName = Path.GetFileNameWithoutExtension(tablePath)!;

    string memoPath = Path.Combine(workspace.DirectoryPath, tableName + ".fpt");
    File.WriteAllBytes(memoPath, Array.Empty<byte>());

    var loader = new DbfTableLoader();

    using FileStream stream = File.OpenRead(tablePath);
    DbfTableDescriptor descriptor = loader.LoadDbf(stream, tableName, workspace.DirectoryPath);

    Assert.Equal(fixture.Version, descriptor.Version);
    Assert.Equal(fixture.RecordCount, descriptor.RecordCount);
    Assert.Equal(Path.GetFileName(memoPath), descriptor.MemoFileName);
    Assert.Empty(descriptor.Sidecars.IndexFileNames);
  }

  [Fact]
  public void Load_WithCodePage852FieldNames_ResolvesEncoding()
  {
    DbfFixtureDescriptor fixture = DbfFixtureLibrary.Get("dBASE_III_CP852");
    string path = fixture.EnsureMaterialized();
    var loader = new DbfTableLoader();

    DbfTableDescriptor descriptor = loader.LoadDbf(path);

    Assert.Equal("Číslo", descriptor.FieldSchemas.Single().Name);
    Assert.Equal("Číslo", descriptor.Fields.Single().Name);
  }

  [Fact]
  public void Load_WithDefaultLanguageDriverId_FallsBackToCodePage437()
  {
    DbfFixtureDescriptor fixture = DbfFixtureLibrary.Get("dBASE_III_DefaultLdid");
    string path = fixture.EnsureMaterialized();
    var loader = new DbfTableLoader();

    DbfTableDescriptor descriptor = loader.LoadDbf(path);

    Assert.Equal("ÄPFEL", descriptor.FieldSchemas.Single().Name);
    Assert.Equal("ÄPFEL", descriptor.Fields.Single().Name);
  }
}
