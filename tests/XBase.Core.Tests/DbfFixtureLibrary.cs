using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace XBase.Core.Tests;

internal static class DbfFixtureLibrary
{
  private static readonly ImmutableArray<DbfFixtureDescriptor> Fixtures =
    [
      new DbfFixtureDescriptor(
        "dBASE_III_OEM",
        "dBASE_III_OEM.dbf",
        Version: 0x03,
        LanguageDriverId: 0x03,
        HeaderLength: 97,
        RecordLength: 25,
        RecordCount: 12,
        FieldCount: 2,
        Base64Payload:
          "A3wDDwwAAABhABkAAAAAAAAAAAAAAAAAAAAAAAADAABJRAAAAAAAAAAAAE4AAAAABAAAAAAAAAAAAAAAAAAAAE5B" +
          "TUUAAAAAAAAAQwAAAAAUAAIAAAAAAAAAAAAAAAAADQ=="),
      new DbfFixtureDescriptor(
        "dBASE_IV_Memo",
        "dBASE_IV_Memo.dbf",
        Version: 0x83,
        LanguageDriverId: 0x57,
        HeaderLength: 97,
        RecordLength: 17,
        RecordCount: 3,
        FieldCount: 2,
        Base64Payload:
          "g3sHBQMAAABhABEAAAAAAAAAAAAAAAAAAAAAAABXAABET0NJRAAAAAAAAE4AAAAABgAAAAAAAAAAAAAAAAAAAE5P" +
          "VEUAAAAAAAAATQAAAAAKAAAAAAAAAAAAAAAAAAAADQ=="),
      new DbfFixtureDescriptor(
        "FoxPro_26",
        "FoxPro_26.dbf",
        Version: 0x30,
        LanguageDriverId: 0xC9,
        HeaderLength: 129,
        RecordLength: 22,
        RecordCount: 7,
        FieldCount: 3,
        Base64Payload:
          "MHoLHgcAAACBABYAAAAAAAAAAAAAAAAAAAAAAADJAABDT0RFAAAAAAAAAEMAAAAADAAAAAAAAAAAAAAAAAAAAEFD" +
          "VElWRQAAAAAATAAAAAABAAAAAAAAAAAAAAAAAAAAQU1PVU5UAAAAAABOAAAAAAgCAAAAAAAAAAAAAAAAAAAN")
    ];

  public static IEnumerable<DbfFixtureDescriptor> All => Fixtures;

  public static DbfFixtureDescriptor Get(string name)
  {
    DbfFixtureDescriptor? descriptor = Fixtures.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
    if (descriptor is null)
    {
      throw new ArgumentException($"Fixture '{name}' was not found.", nameof(name));
    }

    return descriptor;
  }
}

public sealed record DbfFixtureDescriptor(
  string Name,
  string FileName,
  byte Version,
  byte LanguageDriverId,
  ushort HeaderLength,
  ushort RecordLength,
  uint RecordCount,
  int FieldCount,
  string Base64Payload)
{
  public string EnsureMaterialized()
  {
    string directory = FixturePaths.Root;
    Directory.CreateDirectory(directory);

    string path = Path.Combine(directory, FileName);
    byte[] content = Convert.FromBase64String(Base64Payload);
    File.WriteAllBytes(path, content);

    return path;
  }

  public string CopyTo(string directory)
  {
    if (string.IsNullOrWhiteSpace(directory))
    {
      throw new ArgumentException("Directory must be provided.", nameof(directory));
    }

    Directory.CreateDirectory(directory);
    string targetPath = Path.Combine(directory, FileName);
    string sourcePath = EnsureMaterialized();
    File.Copy(sourcePath, targetPath, overwrite: true);
    return targetPath;
  }
}

internal static class FixturePaths
{
  private static readonly Lazy<string> RootLazy = new(ResolveRoot, isThreadSafe: true);

  public static string Root => RootLazy.Value;

  private static string ResolveRoot()
  {
    string? directory = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(directory))
    {
      string solutionPath = Path.Combine(directory, "xBase.sln");
      if (File.Exists(solutionPath))
      {
        return Path.Combine(directory, "tests", "fixtures", "dbf");
      }

      directory = Directory.GetParent(directory)?.FullName;
    }

    throw new InvalidOperationException("Unable to locate repository root for DBF fixtures.");
  }
}
