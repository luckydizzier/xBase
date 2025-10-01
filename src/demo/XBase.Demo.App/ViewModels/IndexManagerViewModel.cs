using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using XBase.Demo.Domain.Diagnostics;
using XBase.Demo.Domain.Services;
using XBase.Demo.Domain.Services.Models;

namespace XBase.Demo.App.ViewModels;

/// <summary>
/// Handles index lifecycle management and rebuild diagnostics for the selected table.
/// </summary>
public sealed class IndexManagerViewModel : ReactiveObject, IDisposable
{
  private readonly IIndexManagementService _indexService;
  private readonly IDemoTelemetrySink _telemetrySink;
  private readonly ObservableCollection<IndexListItemViewModel> _indexes = new();
  private readonly CompositeDisposable _subscriptions = new();
  private TableListItemViewModel? _table;
  private IndexListItemViewModel? _selectedIndex;
  private string? _indexName;
  private string? _indexExpression;
  private string? _statusMessage;
  private string? _errorMessage;
  private bool _isBusy;
  private double _rebuildProgress;
  private bool _isRebuildInProgress;
  private string? _selectionSummary;
  private IndexPerformanceSnapshot? _lastPerformance;
  private string? _lastRebuildStage;
  private string? _activeOperationIndexName;

  public IndexManagerViewModel(IIndexManagementService indexService, IDemoTelemetrySink telemetrySink)
  {
    _indexService = indexService ?? throw new ArgumentNullException(nameof(indexService));
    _telemetrySink = telemetrySink ?? throw new ArgumentNullException(nameof(telemetrySink));

    Indexes = new ReadOnlyObservableCollection<IndexListItemViewModel>(_indexes);

    CreateIndexCommand = ReactiveCommand.CreateFromTask(ExecuteCreateIndexAsync);
    CreateIndexCommand
        .Subscribe(result => ApplyResult("create", result))
        .DisposeWith(_subscriptions);
    CreateIndexCommand.ThrownExceptions
        .Subscribe(exception => OnFault("create", exception))
        .DisposeWith(_subscriptions);

    DropIndexCommand = ReactiveCommand.CreateFromTask<IndexListItemViewModel, IndexOperationResult>(ExecuteDropIndexAsync);
    DropIndexCommand
        .Subscribe(result => ApplyResult("drop", result))
        .DisposeWith(_subscriptions);
    DropIndexCommand.ThrownExceptions
        .Subscribe(exception => OnFault("drop", exception))
        .DisposeWith(_subscriptions);

    var canRebuild = this.WhenAnyValue(vm => vm.SelectedIndex, vm => vm.IsBusy, (index, busy) => index is not null && !busy);
    RebuildIndexCommand = ReactiveCommand.CreateFromObservable(ExecuteRebuildIndex, canRebuild);
    RebuildIndexCommand
        .Subscribe(OnRebuildProgress)
        .DisposeWith(_subscriptions);
    RebuildIndexCommand.ThrownExceptions
        .Subscribe(exception => OnFault("rebuild", exception))
        .DisposeWith(_subscriptions);

    Observable.Merge(
            CreateIndexCommand.IsExecuting,
            DropIndexCommand.IsExecuting,
            RebuildIndexCommand.IsExecuting)
        .Subscribe(isExecuting => IsBusy = isExecuting)
        .DisposeWith(_subscriptions);

    RebuildIndexCommand.IsExecuting
        .Subscribe(inProgress =>
        {
          _lastRebuildStage = null;
          IsRebuildInProgress = inProgress;
          if (inProgress)
          {
            RebuildProgress = 0d;
          }
          else
          {
            _activeOperationIndexName = null;
          }
        })
        .DisposeWith(_subscriptions);

    this.WhenAnyValue(vm => vm.SelectedIndex)
        .Subscribe(index =>
        {
          SelectionSummary = index is null
              ? "Select an index to view diagnostics."
              : BuildSelectionSummary(index);
          LastPerformance = index?.LastPerformance;
        })
        .DisposeWith(_subscriptions);
  }

  public ReadOnlyObservableCollection<IndexListItemViewModel> Indexes { get; }

  public ReactiveCommand<Unit, IndexOperationResult> CreateIndexCommand { get; }

  public ReactiveCommand<IndexListItemViewModel, IndexOperationResult> DropIndexCommand { get; }

  public ReactiveCommand<Unit, IndexRebuildProgress> RebuildIndexCommand { get; }

  public bool IsBusy
  {
    get => _isBusy;
    private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
  }

  public string? IndexName
  {
    get => _indexName;
    set => this.RaiseAndSetIfChanged(ref _indexName, value);
  }

  public string? IndexExpression
  {
    get => _indexExpression;
    set => this.RaiseAndSetIfChanged(ref _indexExpression, value);
  }

  public string? StatusMessage
  {
    get => _statusMessage;
    private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
  }

  public string? ErrorMessage
  {
    get => _errorMessage;
    private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
  }

  public IndexListItemViewModel? SelectedIndex
  {
    get => _selectedIndex;
    set => this.RaiseAndSetIfChanged(ref _selectedIndex, value);
  }

  public double RebuildProgress
  {
    get => _rebuildProgress;
    private set => this.RaiseAndSetIfChanged(ref _rebuildProgress, value);
  }

  public bool IsRebuildInProgress
  {
    get => _isRebuildInProgress;
    private set => this.RaiseAndSetIfChanged(ref _isRebuildInProgress, value);
  }

  public string? SelectionSummary
  {
    get => _selectionSummary;
    private set => this.RaiseAndSetIfChanged(ref _selectionSummary, value);
  }

  public IndexPerformanceSnapshot? LastPerformance
  {
    get => _lastPerformance;
    private set
    {
      this.RaiseAndSetIfChanged(ref _lastPerformance, value);
      this.RaisePropertyChanged(nameof(PerformanceSummary));
    }
  }

  public string? PerformanceSummary
      => LastPerformance is null
          ? null
          : string.Format(
              CultureInfo.InvariantCulture,
              "Last rebuild completed in {0:N0} ms at {1:N1} KB/s",
              LastPerformance.Duration.TotalMilliseconds,
              LastPerformance.BytesPerSecond / 1024d);

  public void SetTargetTable(TableListItemViewModel? table)
  {
    _table = table;
    StatusMessage = null;
    ErrorMessage = null;
    IndexName = null;
    IndexExpression = null;
    _indexes.Clear();
    SelectedIndex = null;

    if (table is null)
    {
      SelectionSummary = "Select a table to view diagnostics.";
      return;
    }

    foreach (var index in table.Indexes)
    {
      _indexes.Add(index);
    }

    SelectedIndex = _indexes.FirstOrDefault();
    if (_indexes.Count == 0)
    {
      SelectionSummary = "No indexes discovered for this table.";
    }
  }

  public void Dispose()
    => _subscriptions.Dispose();

  private async Task<IndexOperationResult> ExecuteCreateIndexAsync()
  {
    if (_table is null)
    {
      throw new InvalidOperationException("Select a table before creating an index.");
    }

    if (string.IsNullOrWhiteSpace(IndexName) || string.IsNullOrWhiteSpace(IndexExpression))
    {
      throw new InvalidOperationException("Index name and expression are required.");
    }

    _activeOperationIndexName = IndexName;
    var request = IndexCreateRequest.Create(_table.Model.Path, IndexName!, IndexExpression!);
    return await _indexService.CreateIndexAsync(request);
  }

  private async Task<IndexOperationResult> ExecuteDropIndexAsync(IndexListItemViewModel index)
  {
    ArgumentNullException.ThrowIfNull(index);
    if (_table is null)
    {
      throw new InvalidOperationException("Select a table before dropping an index.");
    }

    _activeOperationIndexName = index.Name;
    var request = IndexDropRequest.Create(_table.Model.Path, index.Name);
    return await _indexService.DropIndexAsync(request);
  }

  private IObservable<IndexRebuildProgress> ExecuteRebuildIndex()
  {
    if (_table is null)
    {
      return Observable.Throw<IndexRebuildProgress>(new InvalidOperationException("Select a table before rebuilding an index."));
    }

    if (SelectedIndex is null)
    {
      return Observable.Throw<IndexRebuildProgress>(new InvalidOperationException("Select an index to rebuild."));
    }

    _activeOperationIndexName = SelectedIndex.Name;
    var request = IndexRebuildRequest.Create(_table.Model.Path, SelectedIndex.Name);
    PublishTelemetry("IndexRebuildRequested", new Dictionary<string, object?>
    {
      ["table"] = _table.Name,
      ["index"] = SelectedIndex.Name
    });

    return _indexService.RebuildIndex(request);
  }

  private void ApplyResult(string operation, IndexOperationResult result)
  {
    var indexName = _activeOperationIndexName ?? SelectedIndex?.Name ?? IndexName ?? string.Empty;
    if (result.Succeeded)
    {
      StatusMessage = result.Message;
      ErrorMessage = null;
      PublishTelemetry($"Index{ToOperationName(operation)}Succeeded", new Dictionary<string, object?>
      {
        ["table"] = _table?.Name,
        ["index"] = indexName,
        ["message"] = result.Message
      });
    }
    else
    {
      StatusMessage = null;
      ErrorMessage = result.Message;
      PublishTelemetry($"Index{ToOperationName(operation)}Failed", new Dictionary<string, object?>
      {
        ["table"] = _table?.Name,
        ["index"] = indexName,
        ["message"] = result.Message
      });
    }

    _activeOperationIndexName = null;
  }

  private void OnRebuildProgress(IndexRebuildProgress progress)
  {
    ErrorMessage = null;
    RebuildProgress = progress.PercentComplete;
    StatusMessage = progress.Message;

    if (!string.Equals(_lastRebuildStage, progress.Stage, StringComparison.Ordinal))
    {
      PublishTelemetry($"IndexRebuild{progress.Stage}", new Dictionary<string, object?>
      {
        ["table"] = _table?.Name,
        ["index"] = SelectedIndex?.Name,
        ["stage"] = progress.Stage,
        ["progress"] = progress.PercentComplete,
        ["bytesProcessed"] = progress.BytesProcessed,
        ["totalBytes"] = progress.TotalBytes
      });
      _lastRebuildStage = progress.Stage;
    }

    if (progress.Performance is not null)
    {
      LastPerformance = progress.Performance;
    }

    if (progress.IsCompleted && SelectedIndex is not null && _table is not null)
    {
      var tableDirectory = Path.GetDirectoryName(_table.Model.Path) ?? string.Empty;
      var indexPath = SelectedIndex.FullPath ?? Path.Combine(tableDirectory, SelectedIndex.Name);
      var fileInfo = new FileInfo(indexPath);
      SelectedIndex.UpdateDiagnostics(
          fileInfo.Exists ? fileInfo.Length : progress.BytesProcessed,
          fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTimeOffset.UtcNow,
          progress.Performance);
      SelectionSummary = BuildSelectionSummary(SelectedIndex);

      PublishTelemetry("IndexRebuildCompleted", new Dictionary<string, object?>
      {
        ["table"] = _table.Name,
        ["index"] = SelectedIndex.Name,
        ["durationMs"] = progress.Performance?.Duration.TotalMilliseconds,
        ["throughputBytesPerSecond"] = progress.Performance?.BytesPerSecond,
        ["bytesProcessed"] = progress.BytesProcessed
      });

      _activeOperationIndexName = null;
    }
  }

  private void OnFault(string operation, Exception exception)
  {
    StatusMessage = null;
    ErrorMessage = exception.Message;

    PublishTelemetry($"Index{ToOperationName(operation)}Fault", new Dictionary<string, object?>
    {
      ["table"] = _table?.Name,
      ["index"] = _activeOperationIndexName ?? SelectedIndex?.Name ?? IndexName,
      ["message"] = exception.Message
    });

    _activeOperationIndexName = null;
  }

  private static string BuildSelectionSummary(IndexListItemViewModel index)
      => $"{index.Name} • {index.MetricsSummary}";

  private static string ToOperationName(string operation)
      => operation switch
      {
        "create" => "Create",
        "drop" => "Drop",
        "rebuild" => "Rebuild",
        _ => "Operation"
      };

  private void PublishTelemetry(string eventName, IReadOnlyDictionary<string, object?> payload)
    => _telemetrySink.Publish(new DemoTelemetryEvent(eventName, DateTimeOffset.UtcNow, payload));
}
