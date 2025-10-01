using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using XBase.Abstractions;
using XBase.Core.Table;
using Xunit;

namespace XBase.Core.Tests;

public sealed class DbfTableLoaderTests
{
  private static readonly DbfFixtureDefinition CustomersFixture = new(
    "Customers",
    Version: 0x03,
    LanguageDriverId: 0x57,
    Fields: new[]
    {
      new DbfFieldDefinition("Id", 'N', 10),
      new DbfFieldDefinition("Name", 'C', 20, IsNullable: true)
    });

  private static readonly DbfFixtureDefinition OrdersFixture = new(
    "Orders",
    Version: 0x03,
    LanguageDriverId: 0x03,
    Fields: new[]
    {
      new DbfFieldDefinition("OrderId", 'N', 10),
      new DbfFieldDefinition("CustomerId", 'N', 6)
    });

  [Theory]
  [MemberData(nameof(TableFixtures))]
  public void Load_ReadsHeaderMetadata(DbfFixtureDefinition definition)
  {
    using var workspace = new DbfFixtureWorkspace();
    string path = workspace.CreateTable(definition, recordCount: 2);
    var loader = new DbfTableLoader();

    DbfTableDescriptor descriptor = loader.Load(path);

    Assert.Equal(definition.TableName, descriptor.Name);
    Assert.Equal(definition.ExpectedRecordLength, descriptor.RecordLength);
    Assert.Equal(definition.Fields.Count, descriptor.Fields.Count);
    Assert.Equal(definition.LanguageDriverId, descriptor.LanguageDriverId);
  }

  [Fact]
  public void Load_WithMemoAndIndexSidecars_DetectsCompanions()
  {
    using var workspace = new DbfFixtureWorkspace();
    string path = workspace.CreateTable(OrdersFixture);
    workspace.CreateSidecar("Orders.dbt");
    workspace.CreateSidecar("Orders.ntx");
    var loader = new DbfTableLoader();

    DbfTableDescriptor descriptor = loader.Load(path);

    Assert.Equal("Orders.dbt", descriptor.MemoFileName);
    Assert.Contains("Orders.ntx", descriptor.Sidecars.IndexFileNames);
    IndexDescriptor index = Assert.IsType<IndexDescriptor>(Assert.Single(descriptor.Indexes));
    Assert.Equal("Orders", index.Name);
    Assert.Equal("Orders.ntx", index.FileName);
  }

  [Fact]
  public void Load_FromStream_ReadsMetadata()
  {
    using var workspace = new DbfFixtureWorkspace();
    string path = workspace.CreateTable(CustomersFixture, recordCount: 5);
    var loader = new DbfTableLoader();

    using FileStream stream = File.OpenRead(path);
    DbfTableDescriptor descriptor = loader.Load(stream, CustomersFixture.TableName, workspace.DirectoryPath);

    Assert.Equal(CustomersFixture.TableName, descriptor.Name);
    Assert.Equal(CustomersFixture.ExpectedRecordLength, descriptor.RecordLength);
    Assert.Equal((uint)5, descriptor.RecordCount);
  }

  [Fact]
  public void TableCatalog_EnumerateTables_ReturnsSortedDescriptors()
  {
    using var workspace = new DbfFixtureWorkspace();
    workspace.CreateTable(OrdersFixture);
    workspace.CreateTable(CustomersFixture);
    var catalog = new TableCatalog(new DbfTableLoader());

    IReadOnlyList<ITableDescriptor> tables = catalog.EnumerateTables(workspace.DirectoryPath);

    Assert.Equal(2, tables.Count);
    Assert.Collection(
      tables,
      first => Assert.Equal("Customers", first.Name),
      second => Assert.Equal("Orders", second.Name));
  }

  public static IEnumerable<object[]> TableFixtures()
  {
    yield return new object[] { CustomersFixture };
    yield return new object[] { OrdersFixture };
  }
}

public sealed record DbfFieldDefinition(string Name, char Type, byte Length, byte DecimalCount = 0, bool IsNullable = false);

public sealed record DbfFixtureDefinition(string TableName, byte Version, byte LanguageDriverId, IReadOnlyList<DbfFieldDefinition> Fields)
{
  public ushort ExpectedRecordLength => (ushort)(1 + Fields.Sum(field => field.Length));
}

public sealed class DbfFixtureWorkspace : IDisposable
{
  public DbfFixtureWorkspace()
  {
    DirectoryPath = Path.Combine(Path.GetTempPath(), $"xbase-dbf-{Guid.NewGuid():N}");
    Directory.CreateDirectory(DirectoryPath);
  }

  public string DirectoryPath { get; }

  public string CreateTable(DbfFixtureDefinition definition, uint recordCount = 0)
  {
    if (definition is null)
    {
      throw new ArgumentNullException(nameof(definition));
    }

    string path = Path.Combine(DirectoryPath, definition.TableName + ".dbf");
    using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read);
    WriteDbf(stream, definition, recordCount);
    return path;
  }

  public string CreateSidecar(string fileName)
  {
    string path = Path.Combine(DirectoryPath, fileName);
    File.WriteAllBytes(path, Array.Empty<byte>());
    return path;
  }

  public void Dispose()
  {
    try
    {
      if (Directory.Exists(DirectoryPath))
      {
        Directory.Delete(DirectoryPath, recursive: true);
      }
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
  }

  private static void WriteDbf(FileStream stream, DbfFixtureDefinition definition, uint recordCount)
  {
    DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
    ushort headerLength = (ushort)(32 + definition.Fields.Count * 32 + 1);
    ushort recordLength = definition.ExpectedRecordLength;

    Span<byte> header = stackalloc byte[32];
    header.Clear();
    header[0] = definition.Version;
    header[1] = (byte)Math.Clamp(today.Year - 1900, 0, 255);
    header[2] = (byte)today.Month;
    header[3] = (byte)today.Day;
    BinaryPrimitives.WriteUInt32LittleEndian(header[4..], recordCount);
    BinaryPrimitives.WriteUInt16LittleEndian(header[8..], headerLength);
    BinaryPrimitives.WriteUInt16LittleEndian(header[10..], recordLength);
    header[29] = definition.LanguageDriverId;
    stream.Write(header);

    Span<byte> descriptor = stackalloc byte[32];
    foreach (DbfFieldDefinition field in definition.Fields)
    {
      descriptor.Clear();
      Encoding.ASCII.GetBytes(field.Name, descriptor);
      descriptor[11] = (byte)field.Type;
      descriptor[16] = field.Length;
      descriptor[17] = field.DecimalCount;
      descriptor[18] = field.IsNullable ? (byte)0x02 : (byte)0x00;
      stream.Write(descriptor);
    }

    stream.WriteByte(0x0D);
  }
}
