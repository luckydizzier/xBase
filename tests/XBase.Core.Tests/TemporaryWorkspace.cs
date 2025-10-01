using System;
using System.IO;
using System.Linq;

namespace XBase.Core.Tests;

internal sealed class TemporaryWorkspace : IDisposable
{
  public TemporaryWorkspace()
  {
    DirectoryPath = Path.Combine(Path.GetTempPath(), $"xbase-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(DirectoryPath);
  }

  public string DirectoryPath { get; }

  public string Combine(params string[] segments) => Path.Combine(new[] { DirectoryPath }.Concat(segments).ToArray());

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
}
