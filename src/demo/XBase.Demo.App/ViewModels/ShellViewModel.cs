using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System.Reactive.Threading.Tasks;
using XBase.Demo.Domain.Catalog;
using XBase.Demo.Domain.Diagnostics;
using XBase.Demo.Domain.Services;

namespace XBase.Demo.App.ViewModels;

public class ShellViewModel : ReactiveObject
{
  private readonly ITableCatalogService _catalogService;
  private readonly ITablePageService _pageService;
  private readonly IDemoTelemetrySink _telemetrySink;
  private readonly ILogger<ShellViewModel> _logger;

  private string? _catalogRoot;
  private bool _isBusy;
  private TableListItemViewModel? _selectedTable;
  private string? _catalogStatus;

  public ShellViewModel(
      ITableCatalogService catalogService,
      ITablePageService pageService,
      IDemoTelemetrySink telemetrySink,
      SchemaDesignerViewModel schemaDesigner,
      IndexManagerViewModel indexManager,
      ILogger<ShellViewModel> logger)
  {
    _catalogService = catalogService;
    _pageService = pageService;
    _telemetrySink = telemetrySink;
    _logger = logger;

    Tables = new ReadOnlyObservableCollection<TableListItemViewModel>(_tables);
    TelemetryEvents = new ReadOnlyObservableCollection<DemoTelemetryEvent>(_telemetryEvents);
    TablePage = new TablePageViewModel();
    SchemaDesigner = schemaDesigner;
    IndexManager = indexManager;

    var isIdle = this.WhenAnyValue(x => x.IsBusy).Select(isBusy => !isBusy);

    OpenCatalogCommand = ReactiveCommand.CreateFromTask<string, CatalogModel>(ExecuteOpenCatalogAsync, isIdle);
    OpenCatalogCommand.Subscribe(OnCatalogLoaded);
    OpenCatalogCommand.ThrownExceptions.Subscribe(OnOpenCatalogFault);

    BrowseCatalogCommand = ReactiveCommand.CreateFromTask(ExecuteBrowseCatalogAsync, isIdle);
    BrowseCatalogCommand.ThrownExceptions.Subscribe(ex => _logger.LogError(ex, "Catalog browse failed"));

    var canRefresh = this.WhenAnyValue(x => x.CatalogRoot, x => x.IsBusy, (root, busy) => !busy && !string.IsNullOrWhiteSpace(root));
    RefreshCatalogCommand = ReactiveCommand.CreateFromTask(ExecuteRefreshCatalogAsync, canRefresh);

    LoadTableCommand = ReactiveCommand.CreateFromTask<TableListItemViewModel>(ExecuteLoadTableAsync);
    LoadTableCommand.ThrownExceptions.Subscribe(OnLoadTableFault);

    Observable.CombineLatest(
            OpenCatalogCommand.IsExecuting.StartWith(false),
            LoadTableCommand.IsExecuting.StartWith(false),
            (isCatalogExecuting, isTableExecuting) => isCatalogExecuting || isTableExecuting)
        .DistinctUntilChanged()
        .Subscribe(executing => IsBusy = executing);

    this.WhenAnyValue(x => x.SelectedTable)
        .WhereNotNull()
        .InvokeCommand(LoadTableCommand);

    this.WhenAnyValue(x => x.SelectedTable)
        .Subscribe(table =>
        {
          SchemaDesigner.SetTargetTable(table);
          IndexManager.SetTargetTable(table);
        });
  }

  private readonly ObservableCollection<TableListItemViewModel> _tables = new();
  private readonly ObservableCollection<DemoTelemetryEvent> _telemetryEvents = new();

  public Interaction<Unit, string?> SelectCatalogFolderInteraction { get; } = new();

  public ReactiveCommand<string, CatalogModel> OpenCatalogCommand { get; }

  public ReactiveCommand<Unit, Unit> BrowseCatalogCommand { get; }

  public ReactiveCommand<Unit, Unit> RefreshCatalogCommand { get; }

  public ReactiveCommand<TableListItemViewModel, Unit> LoadTableCommand { get; }

  public string? CatalogRoot
  {
    get => _catalogRoot;
    private set => this.RaiseAndSetIfChanged(ref _catalogRoot, value);
  }

  public string? CatalogStatus
  {
    get => _catalogStatus;
    private set => this.RaiseAndSetIfChanged(ref _catalogStatus, value);
  }

  public bool IsBusy
  {
    get => _isBusy;
    private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
  }

  public ReadOnlyObservableCollection<TableListItemViewModel> Tables { get; }

  public TableListItemViewModel? SelectedTable
  {
    get => _selectedTable;
    set => this.RaiseAndSetIfChanged(ref _selectedTable, value);
  }

  public TablePageViewModel TablePage { get; }

  public SchemaDesignerViewModel SchemaDesigner { get; }

  public IndexManagerViewModel IndexManager { get; }

  public ReadOnlyObservableCollection<DemoTelemetryEvent> TelemetryEvents { get; }

  private async Task<Unit> ExecuteBrowseCatalogAsync()
  {
    var folder = await SelectCatalogFolderInteraction.Handle(Unit.Default);
    if (string.IsNullOrWhiteSpace(folder))
    {
      return Unit.Default;
    }

    await OpenCatalogCommand.Execute(folder).ToTask();
    return Unit.Default;
  }

  private async Task<Unit> ExecuteRefreshCatalogAsync()
  {
    var root = CatalogRoot;
    if (string.IsNullOrWhiteSpace(root))
    {
      return Unit.Default;
    }

    await OpenCatalogCommand.Execute(root).ToTask();
    return Unit.Default;
  }

  private async Task<CatalogModel> ExecuteOpenCatalogAsync(string rootPath)
  {
    if (string.IsNullOrWhiteSpace(rootPath))
    {
      return new CatalogModel(string.Empty, Array.Empty<TableModel>());
    }

    CatalogRoot = rootPath;
    var catalog = await _catalogService.LoadCatalogAsync(rootPath);
    return catalog;
  }

  private void OnOpenCatalogFault(Exception exception)
  {
    _logger.LogError(exception, "Catalog load failed");
    var payload = new Dictionary<string, object?>
    {
      ["message"] = exception.Message
    };
    RecordTelemetry(new DemoTelemetryEvent("CatalogLoadFailed", DateTimeOffset.UtcNow, payload));
    CatalogStatus = "Catalog load failed. Review diagnostics for details.";
    _tables.Clear();
    SelectedTable = null;
    SchemaDesigner.SetTargetTable(null);
    IndexManager.SetTargetTable(null);
  }

  private void OnCatalogLoaded(CatalogModel catalog)
  {
    CatalogRoot = catalog.RootPath;
    _tables.Clear();

    foreach (var table in catalog.Tables.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
    {
      _tables.Add(new TableListItemViewModel(table));
    }

    CatalogStatus = _tables.Count > 0
        ? $"{_tables.Count} tables discovered. Select a table to preview rows."
        : "Catalog scanned successfully with no tables detected.";

    if (_tables.Count == 0)
    {
      var emptyRows = Array.Empty<IDictionary<string, object?>>();
      TablePage.Apply(new TablePage(emptyRows, 0, 0, TablePage.PageSize));
    }

    var payload = new Dictionary<string, object?>
    {
      ["root"] = catalog.RootPath,
      ["tableCount"] = catalog.Tables.Count
    };
    RecordTelemetry(new DemoTelemetryEvent("CatalogLoaded", DateTimeOffset.UtcNow, payload));

    SelectedTable = _tables.FirstOrDefault();
  }

  private async Task ExecuteLoadTableAsync(TableListItemViewModel table)
  {
    var request = new TablePageRequest(0, 25);
    var page = await _pageService.LoadPageAsync(table.Model, request);
    TablePage.Apply(page);

    CatalogStatus = _tables.Count > 0
        ? $"{_tables.Count} tables available. Showing {table.Name}."
        : CatalogStatus;

    var payload = new Dictionary<string, object?>
    {
      ["table"] = table.Name,
      ["indexes"] = table.Indexes.Count,
      ["rows"] = page.Rows.Count
    };
    RecordTelemetry(new DemoTelemetryEvent("TablePreviewLoaded", DateTimeOffset.UtcNow, payload));
  }

  private void OnLoadTableFault(Exception exception)
  {
    _logger.LogError(exception, "Table preview load failed");
    var payload = new Dictionary<string, object?>
    {
      ["message"] = exception.Message
    };
    RecordTelemetry(new DemoTelemetryEvent("TablePreviewFailed", DateTimeOffset.UtcNow, payload));
    CatalogStatus = "Table preview failed. Check diagnostics for more information.";
  }

  private void RecordTelemetry(DemoTelemetryEvent telemetryEvent)
  {
    _telemetrySink.Publish(telemetryEvent);

    _telemetryEvents.Insert(0, telemetryEvent);
    while (_telemetryEvents.Count > 64)
    {
      _telemetryEvents.RemoveAt(_telemetryEvents.Count - 1);
    }
  }
}
