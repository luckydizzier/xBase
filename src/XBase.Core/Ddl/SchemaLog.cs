using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XBase.Abstractions;

namespace XBase.Core.Ddl;

public sealed class SchemaLog
{
  private const int MaxEntryLength = 4 * 1024 * 1024;
  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    WriteIndented = false
  };
  private static readonly StringComparer PropertyComparer = StringComparer.OrdinalIgnoreCase;

  private readonly string _filePath;

  public SchemaLog(string filePath)
  {
    _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
  }

  public string FilePath => _filePath;

  public IReadOnlyList<SchemaLogEntry> ReadEntries()
  {
    if (!File.Exists(_filePath))
    {
      return Array.Empty<SchemaLogEntry>();
    }

    using FileStream stream = new(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    List<SchemaLogEntry> entries = new();
    Span<byte> lengthBuffer = stackalloc byte[sizeof(int)];
    while (stream.Position < stream.Length)
    {
      int readLength = stream.Read(lengthBuffer);
      if (readLength != sizeof(int))
      {
        break;
      }

      int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
      if (payloadLength <= 0 || payloadLength > MaxEntryLength)
      {
        break;
      }

      byte[] payload = new byte[payloadLength];
      int actualRead = stream.Read(payload, 0, payloadLength);
      if (actualRead != payloadLength)
      {
        break;
      }

      SchemaLogEntry? entry = JsonSerializer.Deserialize<SchemaLogEntry>(payload, SerializerOptions);
      if (entry is null)
      {
        break;
      }

      if (!VerifyChecksum(entry))
      {
        break;
      }

      entries.Add(entry);
    }

    return entries;
  }

  public SchemaVersion GetCurrentVersion()
  {
    IReadOnlyList<SchemaLogEntry> entries = ReadEntries();
    return entries.Count == 0 ? SchemaVersion.Start : entries[^1].Version;
  }

  public async ValueTask<SchemaVersion> AppendAsync(
    SchemaLogEntry entry,
    CancellationToken cancellationToken = default)
  {
    SchemaVersion nextVersion = GetCurrentVersion().Next();
    SchemaLogEntry prepared = new(
      nextVersion,
      entry.Timestamp,
      entry.Author,
      entry.Kind,
      CloneProperties(entry.Properties),
      string.Empty);

    SchemaLogEntry sealedEntry = new(
      prepared.Version,
      prepared.Timestamp,
      prepared.Author,
      prepared.Kind,
      prepared.Properties,
      ComputeChecksum(prepared));

    await WriteEntryAsync(sealedEntry, cancellationToken).ConfigureAwait(false);
    return sealedEntry.Version;
  }

  public async ValueTask<SchemaVersion> AppendOperationAsync(
    SchemaOperation operation,
    string author,
    IReadOnlyDictionary<string, string> properties,
    CancellationToken cancellationToken = default)
  {
    SchemaLogEntry entry = new(
      SchemaVersion.Start,
      DateTimeOffset.UtcNow,
      string.IsNullOrWhiteSpace(author) ? "unknown" : author!,
      operation.Kind,
      CloneProperties(properties),
      string.Empty);

    return await AppendAsync(entry, cancellationToken).ConfigureAwait(false);
  }

  public async ValueTask<SchemaCheckpoint> CreateCheckpointAsync(CancellationToken cancellationToken = default)
  {
    SchemaVersion current = GetCurrentVersion();
    SchemaCheckpoint checkpoint = new(current, DateTimeOffset.UtcNow);
    string checkpointPath = _filePath + ".checkpoint";
    string? directory = Path.GetDirectoryName(checkpointPath);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    await using FileStream stream = new(checkpointPath, FileMode.Create, FileAccess.Write, FileShare.None);
    await JsonSerializer.SerializeAsync(stream, checkpoint, SerializerOptions, cancellationToken).ConfigureAwait(false);
    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    stream.Flush(true);
    return checkpoint;
  }

  public async ValueTask CompactAsync(
    SchemaVersion retainFrom,
    CancellationToken cancellationToken = default)
  {
    IReadOnlyList<SchemaLogEntry> entries = ReadEntries();
    List<SchemaLogEntry> survivors = entries
      .Where(entry => entry.Version > retainFrom)
      .ToList();

    string? directory = Path.GetDirectoryName(_filePath);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    string tempPath = _filePath + ".tmp";
    await using (FileStream stream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
    {
      foreach (SchemaLogEntry entry in survivors)
      {
        WriteEntry(stream, entry);
      }

      await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
      stream.Flush(true);
    }
    File.Copy(tempPath, _filePath, overwrite: true);
    File.Delete(tempPath);
  }

  private async ValueTask WriteEntryAsync(SchemaLogEntry entry, CancellationToken cancellationToken)
  {
    string? directory = Path.GetDirectoryName(_filePath);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    using FileStream stream = new(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
    WriteEntry(stream, entry);
    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    stream.Flush(true);
  }

  private static void WriteEntry(FileStream stream, SchemaLogEntry entry)
  {
    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(entry, SerializerOptions);
    Span<byte> lengthBuffer = stackalloc byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, payload.Length);
    stream.Write(lengthBuffer);
    stream.Write(payload);
  }

  private static IReadOnlyDictionary<string, string> CloneProperties(IReadOnlyDictionary<string, string> properties)
  {
    Dictionary<string, string> clone = new(PropertyComparer);
    if (properties is not null)
    {
      foreach (KeyValuePair<string, string> pair in properties)
      {
        clone[pair.Key] = pair.Value;
      }
    }

    return clone;
  }

  private static bool VerifyChecksum(SchemaLogEntry entry)
  {
    string expected = ComputeChecksum(new SchemaLogEntry(
      entry.Version,
      entry.Timestamp,
      entry.Author,
      entry.Kind,
      entry.Properties,
      string.Empty));
    return PropertyComparer.Equals(expected, entry.Checksum);
  }

  private static string ComputeChecksum(SchemaLogEntry entry)
  {
    var builder = new StringBuilder();
    builder.Append(entry.Version.Value)
      .Append('|')
      .Append(entry.Timestamp.UtcDateTime.Ticks)
      .Append('|')
      .Append(entry.Author)
      .Append('|')
      .Append(entry.Kind);

    foreach (KeyValuePair<string, string> pair in entry.Properties.OrderBy(pair => pair.Key, PropertyComparer))
    {
      builder
        .Append('|')
        .Append(pair.Key)
        .Append('=')
        .Append(pair.Value);
    }

    byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
    return Convert.ToHexString(hash);
  }
}

public sealed record SchemaCheckpoint(SchemaVersion Version, DateTimeOffset Timestamp);
