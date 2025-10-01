using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using XBase.Demo.Domain.Catalog;
using XBase.Demo.Domain.Diagnostics;
using XBase.Demo.Domain.Recovery;
using XBase.Demo.Domain.Seed;
using XBase.Demo.Domain.Services;

namespace XBase.Demo.App.ViewModels;

/// <summary>
/// Coordinates seed imports, crash simulations, recovery replays, and support package exports.
/// </summary>
public sealed class SeedAndRecoveryViewModel : ReactiveObject, IDisposable
{
  private readonly ICsvImportService _csvImportService;
  private readonly IRecoveryWorkflowService _recoveryService;
  private readonly IDemoTelemetrySink _telemetrySink;
  private readonly CompositeDisposable _subscriptions = new();
  private readonly List<TableModel> _catalogTables = new();

  private TableListItemViewModel? _table;
  private string? _catalogRoot;
  private bool _isBusy;
  private string? _statusMessage;
  private string? _errorMessage;
  private string? _encodingOverride;
  private bool _truncateBeforeImport;
  private string? _lastDetectedEncoding;
  private int _lastImportedRowCount;
  private string? _lastImportReportPath;
  private bool _hasPendingCrash;
  private string? _crashMarkerPath;
  private string? _recoveryReportPath;
  private string? _supportPackagePath;
  private string? _targetTableName;
  private string _crashScenario = "write-interruption";

  public SeedAndRecoveryViewModel(ICsvImportService csvImportService, IRecoveryWorkflowService recoveryService, IDemoTelemetrySink telemetrySink)
  {
    _csvImportService = csvImportService ?? throw new ArgumentNullException(nameof(csvImportService));
    _recoveryService = recoveryService ?? throw new ArgumentNullException(nameof(recoveryService));
    _telemetrySink = telemetrySink ?? throw new ArgumentNullException(nameof(telemetrySink));

    SelectCsvFileInteraction = new Interaction<Unit, string?>();

    var canImport = this.WhenAnyValue(vm => vm.IsBusy, vm => vm.TargetTableName, (busy, table) => !busy && !string.IsNullOrWhiteSpace(table));
    ImportCsvCommand = ReactiveCommand.CreateFromTask<string?, CsvImportResult>(ExecuteImportCsvAsync, canImport);
    ImportCsvCommand.Subscribe(OnImportCompleted).DisposeWith(_subscriptions);
    ImportCsvCommand.ThrownExceptions.Subscribe(exception => OnFault("CsvImportFault", exception)).DisposeWith(_subscriptions);

    var canSimulate = this.WhenAnyValue(vm => vm.IsBusy, vm => vm.TargetTableName, (busy, table) => !busy && !string.IsNullOrWhiteSpace(table));
    SimulateCrashCommand = ReactiveCommand.CreateFromTask(ExecuteSimulateCrashAsync, canSimulate);
    SimulateCrashCommand.Subscribe(OnCrashSimulated).DisposeWith(_subscriptions);
    SimulateCrashCommand.ThrownExceptions.Subscribe(exception => OnFault("CrashSimulationFault", exception)).DisposeWith(_subscriptions);

    var canReplay = this.WhenAnyValue(vm => vm.IsBusy, vm => vm.HasPendingCrash, (busy, pending) => !busy && pending);
    ReplayRecoveryCommand = ReactiveCommand.CreateFromTask(ExecuteReplayRecoveryAsync, canReplay);
    ReplayRecoveryCommand.Subscribe(OnRecoveryReplayed).DisposeWith(_subscriptions);
    ReplayRecoveryCommand.ThrownExceptions.Subscribe(exception => OnFault("RecoveryReplayFault", exception)).DisposeWith(_subscriptions);

    var canExport = this.WhenAnyValue(vm => vm.IsBusy, vm => vm.CatalogRoot, (busy, root) => !busy && !string.IsNullOrWhiteSpace(root));
    ExportSupportPackageCommand = ReactiveCommand.CreateFromTask(ExecuteExportSupportPackageAsync, canExport);
    ExportSupportPackageCommand.Subscribe(OnSupportPackageExported).DisposeWith(_subscriptions);
    ExportSupportPackageCommand.ThrownExceptions.Subscribe(exception => OnFault("SupportPackageFault", exception)).DisposeWith(_subscriptions);

    Observable.Merge(
            ImportCsvCommand.IsExecuting,
            SimulateCrashCommand.IsExecuting,
            ReplayRecoveryCommand.IsExecuting,
            ExportSupportPackageCommand.IsExecuting)
        .Subscribe(executing => IsBusy = executing)
        .DisposeWith(_subscriptions);
  }

  public Interaction<Unit, string?> SelectCsvFileInteraction { get; }

  public ReactiveCommand<string?, CsvImportResult> ImportCsvCommand { get; }

  public ReactiveCommand<Unit, CrashSimulationResult> SimulateCrashCommand { get; }

  public ReactiveCommand<Unit, RecoveryReplayResult> ReplayRecoveryCommand { get; }

  public ReactiveCommand<Unit, SupportPackageResult> ExportSupportPackageCommand { get; }

  public string? CatalogRoot
  {
    get => _catalogRoot;
    private set => this.RaiseAndSetIfChanged(ref _catalogRoot, value);
  }

  public bool IsBusy
  {
    get => _isBusy;
    private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
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

  public string? EncodingOverride
  {
    get => _encodingOverride;
    set => this.RaiseAndSetIfChanged(ref _encodingOverride, value);
  }

  public bool TruncateBeforeImport
  {
    get => _truncateBeforeImport;
    set => this.RaiseAndSetIfChanged(ref _truncateBeforeImport, value);
  }

  public string? LastDetectedEncoding
  {
    get => _lastDetectedEncoding;
    private set => this.RaiseAndSetIfChanged(ref _lastDetectedEncoding, value);
  }

  public int LastImportedRowCount
  {
    get => _lastImportedRowCount;
    private set => this.RaiseAndSetIfChanged(ref _lastImportedRowCount, value);
  }

  public string? LastImportReportPath
  {
    get => _lastImportReportPath;
    private set => this.RaiseAndSetIfChanged(ref _lastImportReportPath, value);
  }

  public bool HasPendingCrash
  {
    get => _hasPendingCrash;
    private set => this.RaiseAndSetIfChanged(ref _hasPendingCrash, value);
  }

  public string? CrashMarkerPath
  {
    get => _crashMarkerPath;
    private set => this.RaiseAndSetIfChanged(ref _crashMarkerPath, value);
  }

  public string? RecoveryReportPath
  {
    get => _recoveryReportPath;
    private set => this.RaiseAndSetIfChanged(ref _recoveryReportPath, value);
  }

  public string? SupportPackagePath
  {
    get => _supportPackagePath;
    private set => this.RaiseAndSetIfChanged(ref _supportPackagePath, value);
  }

  public string? TargetTableName
  {
    get => _targetTableName;
    private set => this.RaiseAndSetIfChanged(ref _targetTableName, value);
  }

  public string CrashScenario
  {
    get => _crashScenario;
    set => this.RaiseAndSetIfChanged(ref _crashScenario, string.IsNullOrWhiteSpace(value) ? "write-interruption" : value);
  }

  public void SetCatalogContext(string? catalogRoot, IEnumerable<TableListItemViewModel>? tables)
  {
    CatalogRoot = catalogRoot;
    _catalogTables.Clear();

    if (!string.IsNullOrWhiteSpace(catalogRoot))
    {
      foreach (var table in tables ?? Array.Empty<TableListItemViewModel>())
      {
        if (table?.Model is not null)
        {
          _catalogTables.Add(table.Model);
        }
      }
    }
    else
    {
      StatusMessage = null;
      SupportPackagePath = null;
    }

    RefreshCrashState();
  }

  public void SetTargetTable(TableListItemViewModel? table)
  {
    _table = table;
    TargetTableName = table?.Name;
    StatusMessage = null;
    ErrorMessage = null;
    LastImportReportPath = null;
    LastDetectedEncoding = null;
    LastImportedRowCount = 0;
    RefreshCrashState();
  }

  public void Dispose()
    => _subscriptions.Dispose();

  private async Task<CsvImportResult> ExecuteImportCsvAsync(string? csvPath)
  {
    if (_table is null)
    {
      throw new InvalidOperationException("Select a table before running a CSV import.");
    }

    var resolvedPath = csvPath;
    if (string.IsNullOrWhiteSpace(resolvedPath))
    {
      resolvedPath = await SelectCsvFileInteraction.Handle(Unit.Default);
    }

    if (string.IsNullOrWhiteSpace(resolvedPath))
    {
      return CsvImportResult.Cancelled("CSV import was cancelled.");
    }

    var request = CsvImportRequest.Create(
        _table.Model.Path,
        resolvedPath,
        string.IsNullOrWhiteSpace(EncodingOverride) ? null : EncodingOverride,
        TruncateBeforeImport);

    PublishTelemetry("CsvImportRequested", new Dictionary<string, object?>
    {
      ["table"] = _table.Name,
      ["csv"] = resolvedPath,
      ["encodingOverride"] = EncodingOverride
    });

    return await _csvImportService.ImportAsync(request);
  }

  private async Task<CrashSimulationResult> ExecuteSimulateCrashAsync()
  {
    if (_table is null)
    {
      throw new InvalidOperationException("Select a table before simulating a crash.");
    }

    var request = CrashSimulationRequest.Create(_table.Model.Path, CrashScenario);
    return await _recoveryService.SimulateCrashAsync(request);
  }

  private async Task<RecoveryReplayResult> ExecuteReplayRecoveryAsync()
  {
    if (_table is null)
    {
      throw new InvalidOperationException("Select a table before replaying recovery.");
    }

    var request = RecoveryReplayRequest.Create(_table.Model.Path);
    return await _recoveryService.ReplayRecoveryAsync(request);
  }

  private async Task<SupportPackageResult> ExecuteExportSupportPackageAsync()
  {
    if (string.IsNullOrWhiteSpace(CatalogRoot))
    {
      throw new InvalidOperationException("Open a catalog before exporting a support package.");
    }

    var request = SupportPackageRequest.Create(CatalogRoot!, _catalogTables.ToArray());
    return await _recoveryService.CreateSupportPackageAsync(request);
  }

  private void OnImportCompleted(CsvImportResult result)
  {
    if (result.WasCancelled)
    {
      StatusMessage = result.Message;
      ErrorMessage = null;
      return;
    }

    if (result.Succeeded)
    {
      StatusMessage = result.Message;
      ErrorMessage = null;
      LastDetectedEncoding = result.EncodingName;
      LastImportedRowCount = result.RowsImported;
      LastImportReportPath = result.ReportPath;

      PublishTelemetry("CsvImportCompleted", new Dictionary<string, object?>
      {
        ["table"] = _table?.Name,
        ["rows"] = result.RowsImported,
        ["encoding"] = result.EncodingName,
        ["report"] = result.ReportPath
      });
    }
    else
    {
      StatusMessage = null;
      ErrorMessage = result.Message;

      PublishTelemetry("CsvImportFailed", new Dictionary<string, object?>
      {
        ["table"] = _table?.Name,
        ["encoding"] = result.EncodingName,
        ["message"] = result.Message
      });
    }
  }

  private void OnCrashSimulated(CrashSimulationResult result)
  {
    if (result.Succeeded)
    {
      StatusMessage = result.Message;
      ErrorMessage = null;
      CrashMarkerPath = result.CrashMarkerPath;
      HasPendingCrash = !string.IsNullOrWhiteSpace(result.CrashMarkerPath) && File.Exists(result.CrashMarkerPath);
      RecoveryReportPath = null;
    }
    else
    {
      StatusMessage = null;
      ErrorMessage = result.Message;
    }
  }

  private void OnRecoveryReplayed(RecoveryReplayResult result)
  {
    if (result.Succeeded)
    {
      StatusMessage = result.Message;
      ErrorMessage = null;
      RecoveryReportPath = result.ReportPath;
      CrashMarkerPath = null;
      HasPendingCrash = false;

      PublishTelemetry("RecoveryReplayCompleted", new Dictionary<string, object?>
      {
        ["table"] = _table?.Name,
        ["report"] = result.ReportPath,
        ["steps"] = result.Steps.Count
      });
    }
    else
    {
      StatusMessage = null;
      ErrorMessage = result.Message;
    }
  }

  private void OnSupportPackageExported(SupportPackageResult result)
  {
    if (result.Succeeded)
    {
      StatusMessage = result.Message;
      ErrorMessage = null;
      SupportPackagePath = result.PackagePath;
    }
    else
    {
      StatusMessage = null;
      ErrorMessage = result.Message;
    }
  }

  private void OnFault(string eventName, Exception exception)
  {
    StatusMessage = null;
    ErrorMessage = exception.Message;

    PublishTelemetry(eventName, new Dictionary<string, object?>
    {
      ["table"] = _table?.Name,
      ["message"] = exception.Message
    });
  }

  private void RefreshCrashState()
  {
    if (_table is null)
    {
      HasPendingCrash = false;
      CrashMarkerPath = null;
      return;
    }

    var directory = Path.GetDirectoryName(_table.Model.Path) ?? CatalogRoot ?? string.Empty;
    var marker = Path.Combine(directory, $"{_table.Name}.crash");
    CrashMarkerPath = File.Exists(marker) ? marker : null;
    HasPendingCrash = CrashMarkerPath is not null;
  }

  private void PublishTelemetry(string name, IReadOnlyDictionary<string, object?> payload)
    => _telemetrySink.Publish(new DemoTelemetryEvent(name, DateTimeOffset.UtcNow, payload));
}
