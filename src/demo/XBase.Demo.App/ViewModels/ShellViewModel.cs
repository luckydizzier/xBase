using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReactiveUI;
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
  private string? _tableSummary;
  private bool _isBusy;

  public ShellViewModel(
      ITableCatalogService catalogService,
      ITablePageService pageService,
      IDemoTelemetrySink telemetrySink,
      ILogger<ShellViewModel> logger)
  {
    _catalogService = catalogService;
    _pageService = pageService;
    _telemetrySink = telemetrySink;
    _logger = logger;

    OpenCatalogCommand = ReactiveCommand.CreateFromTask<string>(ExecuteOpenCatalogAsync);
    OpenCatalogCommand.ThrownExceptions.Subscribe(OnOpenCatalogFault);
  }

  public ReactiveCommand<string, Unit> OpenCatalogCommand { get; }

  public string? CatalogRoot
  {
    get => _catalogRoot;
    private set => this.RaiseAndSetIfChanged(ref _catalogRoot, value);
  }

  public string? TableSummary
  {
    get => _tableSummary;
    private set => this.RaiseAndSetIfChanged(ref _tableSummary, value);
  }

  public bool IsBusy
  {
    get => _isBusy;
    private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
  }

  private async Task ExecuteOpenCatalogAsync(string rootPath)
  {
    if (string.IsNullOrWhiteSpace(rootPath))
    {
      return;
    }

    try
    {
      IsBusy = true;
      CatalogRoot = rootPath;

      var catalog = await _catalogService.LoadCatalogAsync(rootPath);
      var payload = new Dictionary<string, object?>
      {
        ["root"] = catalog.RootPath,
        ["tableCount"] = catalog.Tables.Count
      };
      _telemetrySink.Publish(new DemoTelemetryEvent("CatalogLoaded", DateTimeOffset.UtcNow, payload));

      if (catalog.Tables.Count > 0)
      {
        var firstTable = catalog.Tables[0];
        var page = await _pageService.LoadPageAsync(firstTable, new TablePageRequest(0, 25));
        TableSummary = $"{catalog.Tables.Count} tables discovered. Preview rows: {page.Rows.Count} from {firstTable.Name}.";
      }
      else
      {
        TableSummary = "Catalog scanned successfully with no tables detected.";
      }
    }
    finally
    {
      IsBusy = false;
    }
  }

  private void OnOpenCatalogFault(Exception exception)
  {
    _logger.LogError(exception, "Catalog load failed");
    var payload = new Dictionary<string, object?>
    {
      ["message"] = exception.Message
    };
    _telemetrySink.Publish(new DemoTelemetryEvent("CatalogLoadFailed", DateTimeOffset.UtcNow, payload));
    TableSummary = "Catalog load failed. Review diagnostics for details.";
  }
}
