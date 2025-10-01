using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XBase.Demo.Domain.Seed;
using XBase.Demo.Domain.Services;

namespace XBase.Demo.Infrastructure.Seed;

/// <summary>
/// File-system backed CSV import pipeline with simple encoding detection heuristics.
/// </summary>
public sealed class FileSystemCsvImportService : ICsvImportService
{
  private const int SampleByteCount = 4096;
  private readonly ILogger<FileSystemCsvImportService> _logger;

  static FileSystemCsvImportService()
  {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
  }

  public FileSystemCsvImportService(ILogger<FileSystemCsvImportService> logger)
  {
    _logger = logger;
  }

  public async Task<CsvImportResult> ImportAsync(CsvImportRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    if (!File.Exists(request.CsvPath))
    {
      return CsvImportResult.Failure($"CSV file '{request.CsvPath}' was not found.");
    }

    var diagnostics = new List<string>();
    var encoding = DetectEncoding(request, diagnostics);

    var tableDirectory = Path.GetDirectoryName(request.TablePath);
    if (string.IsNullOrWhiteSpace(tableDirectory))
    {
      return CsvImportResult.Failure($"Unable to resolve table directory for '{request.TablePath}'.");
    }

    Directory.CreateDirectory(tableDirectory);
    var tableName = Path.GetFileNameWithoutExtension(request.TablePath);
    var seedDirectory = Path.Combine(tableDirectory, "_seed");
    Directory.CreateDirectory(seedDirectory);

    var reportPath = Path.Combine(seedDirectory, $"{tableName}-import-report.json");
    var snapshotPath = Path.Combine(seedDirectory, $"{tableName}-snapshot.json");

    if (request.TruncateBeforeLoad && File.Exists(snapshotPath))
    {
      File.Delete(snapshotPath);
      diagnostics.Add($"Previous snapshot at '{snapshotPath}' was removed before import.");
    }

    var headers = new List<string>();
    var sampleRows = new List<IReadOnlyDictionary<string, object?>>();
    var totalRows = 0;
    var delimiter = ',';

    await using (var stream = new FileStream(request.CsvPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    using (var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false))
    {
      var headerLine = await reader.ReadLineAsync(cancellationToken);
      if (headerLine is null)
      {
        return CsvImportResult.Failure("CSV file did not contain a header row.", encoding.WebName, diagnostics);
      }

      delimiter = DetectDelimiter(headerLine);
      headers.AddRange(ParseCsvLine(headerLine, delimiter));

      string? line;
      while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(line))
        {
          continue;
        }

        var values = ParseCsvLine(line, delimiter);
        var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
          var header = headers[index];
          var value = index < values.Count ? values[index] : null;
          record[header] = value;
        }

        totalRows++;
        if (sampleRows.Count < 50)
        {
          sampleRows.Add(record);
        }
      }
    }

    var options = new JsonSerializerOptions
    {
      WriteIndented = true
    };

    var reportPayload = new
    {
      table = tableName,
      csv = request.CsvPath,
      encoding = encoding.WebName,
      delimiter = delimiter.ToString(CultureInfo.InvariantCulture),
      importedAtUtc = DateTimeOffset.UtcNow,
      rows = totalRows,
      headers,
      diagnostics
    };

    await using (var reportStream = new FileStream(reportPath, FileMode.Create, FileAccess.Write, FileShare.Read))
    {
      await JsonSerializer.SerializeAsync(reportStream, reportPayload, options, cancellationToken);
    }

    if (sampleRows.Count > 0)
    {
      await using var snapshotStream = new FileStream(snapshotPath, FileMode.Create, FileAccess.Write, FileShare.Read);
      await JsonSerializer.SerializeAsync(snapshotStream, sampleRows, options, cancellationToken);
      diagnostics.Add($"Sample snapshot written to '{snapshotPath}'.");
    }

    diagnostics.Add($"Imported {totalRows} rows using encoding '{encoding.WebName}'.");
    _logger.LogInformation("Imported {RowCount} rows from {Csv} into {Table}", totalRows, request.CsvPath, request.TablePath);

    var message = totalRows > 0
        ? $"Imported {totalRows} rows into table '{tableName}'."
        : $"CSV import completed for table '{tableName}' with no data rows detected.";

    return CsvImportResult.Success(message, encoding.WebName, totalRows, reportPath, diagnostics);
  }

  private static Encoding DetectEncoding(CsvImportRequest request, ICollection<string> diagnostics)
  {
    if (!string.IsNullOrWhiteSpace(request.EncodingName))
    {
      var explicitEncoding = Encoding.GetEncoding(request.EncodingName);
      diagnostics.Add($"Encoding override '{explicitEncoding.WebName}' applied.");
      return explicitEncoding;
    }

    using var stream = new FileStream(request.CsvPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    if (stream.Length == 0)
    {
      diagnostics.Add("Empty CSV detected. Defaulting to UTF-8.");
      return new UTF8Encoding(false);
    }

    var length = (int)Math.Min(stream.Length, SampleByteCount);
    var buffer = new byte[length];
    _ = stream.Read(buffer, 0, length);

    var bomEncoding = DetectBomEncoding(buffer);
    if (bomEncoding.encoding is not null)
    {
      diagnostics.Add(bomEncoding.reason);
      return bomEncoding.encoding;
    }

    foreach (var candidate in GetCandidateEncodings())
    {
      try
      {
        var encoding = Encoding.GetEncoding(candidate.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        encoding.GetString(buffer);
        diagnostics.Add($"Detected encoding '{encoding.WebName}' using heuristic scan.");
        return encoding;
      }
      catch (DecoderFallbackException)
      {
      }
      catch (ArgumentException)
      {
      }
    }

    diagnostics.Add("Encoding detection failed. Falling back to UTF-8.");
    return new UTF8Encoding(false);
  }

  private static (Encoding? encoding, string reason) DetectBomEncoding(ReadOnlySpan<byte> buffer)
  {
    if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
    {
      return (new UTF8Encoding(true), "Detected UTF-8 byte order mark.");
    }

    if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
    {
      return (new UnicodeEncoding(false, true), "Detected UTF-16 little-endian byte order mark.");
    }

    if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
    {
      return (new UnicodeEncoding(true, true), "Detected UTF-16 big-endian byte order mark.");
    }

    if (buffer.Length >= 4 && buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
    {
      return (new UTF32Encoding(false, true), "Detected UTF-32 little-endian byte order mark.");
    }

    if (buffer.Length >= 4 && buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
    {
      return (new UTF32Encoding(true, true), "Detected UTF-32 big-endian byte order mark.");
    }

    return (null, string.Empty);
  }

  private static IEnumerable<Encoding> GetCandidateEncodings()
  {
    yield return new UTF8Encoding(false);
    yield return new UnicodeEncoding(false, true);
    yield return new UnicodeEncoding(true, true);
    yield return new UTF32Encoding(false, true);

    foreach (var codePage in new[] { 1250, 1252, 28592, 28591, 852 })
    {
      Encoding? encoding = null;
      try
      {
        encoding = Encoding.GetEncoding(codePage);
      }
      catch (ArgumentException)
      {
      }

      if (encoding is not null)
      {
        yield return encoding;
      }
    }
  }

  private static IReadOnlyList<string> ParseCsvLine(string line, char delimiter)
  {
    var values = new List<string>();
    var builder = new StringBuilder();
    var inQuotes = false;

    for (var index = 0; index < line.Length; index++)
    {
      var character = line[index];
      if (inQuotes)
      {
        if (character == '"')
        {
          if (index + 1 < line.Length && line[index + 1] == '"')
          {
            builder.Append('"');
            index++;
          }
          else
          {
            inQuotes = false;
          }
        }
        else
        {
          builder.Append(character);
        }
      }
      else if (character == '"')
      {
        inQuotes = true;
      }
      else if (character == delimiter)
      {
        values.Add(builder.ToString());
        builder.Clear();
      }
      else
      {
        builder.Append(character);
      }
    }

    values.Add(builder.ToString());
    return values;
  }

  private static char DetectDelimiter(string headerLine)
  {
    var commaCount = headerLine.Count(ch => ch == ',');
    var semicolonCount = headerLine.Count(ch => ch == ';');
    if (semicolonCount > commaCount)
    {
      return ';';
    }

    return ',';
  }
}
