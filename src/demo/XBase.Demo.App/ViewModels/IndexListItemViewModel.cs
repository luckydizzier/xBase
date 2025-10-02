using System;
using System.Collections.Generic;
using System.Globalization;
using ReactiveUI;
using XBase.Demo.Domain.Catalog;
using XBase.Demo.Domain.Services.Models;

namespace XBase.Demo.App.ViewModels;

/// <summary>
/// Represents an index entry for the selected table.
/// </summary>
public sealed class IndexListItemViewModel : ReactiveObject
{
  private long? _sizeBytes;
  private DateTimeOffset? _lastModifiedUtc;
  private IndexPerformanceSnapshot? _lastPerformance;

  public IndexListItemViewModel(IndexModel model)
  {
    Model = model ?? throw new ArgumentNullException(nameof(model));
    Name = model.Name;
    Expression = model.Expression;
    Order = model.Order;
    FullPath = model.FullPath;
    _sizeBytes = model.SizeBytes;
    _lastModifiedUtc = model.LastModifiedUtc;
  }

  public IndexModel Model { get; private set; }

  public string Name { get; }

  public string Expression { get; }

  public int Order { get; }

  public string? FullPath { get; }

  public long? SizeBytes
  {
    get => _sizeBytes;
    private set
    {
      this.RaiseAndSetIfChanged(ref _sizeBytes, value);
      this.RaisePropertyChanged(nameof(MetricsSummary));
    }
  }

  public DateTimeOffset? LastModifiedUtc
  {
    get => _lastModifiedUtc;
    private set
    {
      this.RaiseAndSetIfChanged(ref _lastModifiedUtc, value);
      this.RaisePropertyChanged(nameof(MetricsSummary));
    }
  }

  public IndexPerformanceSnapshot? LastPerformance
  {
    get => _lastPerformance;
    private set
    {
      this.RaiseAndSetIfChanged(ref _lastPerformance, value);
      this.RaisePropertyChanged(nameof(MetricsSummary));
    }
  }

  public string MetricsSummary
  {
    get
    {
      var segments = new List<string>();
      if (Model.ActiveRecordCount is { } active)
      {
        if (Model.TotalRecordCount is { } total && total != active)
        {
          segments.Add(string.Format(CultureInfo.InvariantCulture, "{0:N0}/{1:N0} records", active, total));
        }
        else
        {
          segments.Add(string.Format(CultureInfo.InvariantCulture, "{0:N0} records", active));
        }
      }
      else if (Model.TotalRecordCount is { } totalOnly)
      {
        segments.Add(string.Format(CultureInfo.InvariantCulture, "{0:N0} records", totalOnly));
      }

      if (SizeBytes is { } size)
      {
        segments.Add(string.Format(CultureInfo.InvariantCulture, "{0:N0} bytes", size));
      }

      if (LastModifiedUtc is { } modified)
      {
        segments.Add($"updated {modified.ToLocalTime():g}");
      }

      if (LastPerformance is { } performance)
      {
        var throughput = performance.BytesPerSecond / 1024d;
        var duration = performance.Duration.TotalMilliseconds;
        segments.Add($"rebuilt in {duration:N0} ms @ {throughput:N1} KB/s");
      }

      if (!string.IsNullOrWhiteSpace(Model.Signature))
      {
        var signature = Model.Signature!;
        var previewLength = Math.Min(8, signature.Length);
        segments.Add($"sig {signature[..previewLength]}…");
      }

      return segments.Count == 0
          ? "No diagnostics captured yet."
          : string.Join(" • ", segments);
    }
  }

  public void UpdateFromModel(IndexModel model)
  {
    ArgumentNullException.ThrowIfNull(model);
    Model = model;
    SizeBytes = model.SizeBytes;
    LastModifiedUtc = model.LastModifiedUtc;
    this.RaisePropertyChanged(nameof(Model));
    this.RaisePropertyChanged(nameof(MetricsSummary));
  }

  public void UpdateDiagnostics(long? sizeBytes, DateTimeOffset? lastModifiedUtc, IndexPerformanceSnapshot? performance)
  {
    SizeBytes = sizeBytes;
    LastModifiedUtc = lastModifiedUtc;
    LastPerformance = performance;
  }
}
