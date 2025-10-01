using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using XBase.Demo.Domain.Services;
using XBase.Demo.Domain.Services.Models;

namespace XBase.Demo.App.ViewModels;

/// <summary>
/// Handles index create/drop operations for the selected table.
/// </summary>
public sealed class IndexManagerViewModel : ReactiveObject
{
  private readonly IIndexManagementService _indexService;
  private TableListItemViewModel? _table;
  private string? _indexName;
  private string? _indexExpression;
  private string? _statusMessage;
  private string? _errorMessage;
  private bool _isBusy;

  public IndexManagerViewModel(IIndexManagementService indexService)
  {
    _indexService = indexService;

    CreateIndexCommand = ReactiveCommand.CreateFromTask(ExecuteCreateIndexAsync);
    CreateIndexCommand.Subscribe(ApplyResult);
    CreateIndexCommand.ThrownExceptions.Subscribe(OnFault);

    DropIndexCommand = ReactiveCommand.CreateFromTask<IndexListItemViewModel, IndexOperationResult>(ExecuteDropIndexAsync);
    DropIndexCommand.Subscribe(ApplyResult);
    DropIndexCommand.ThrownExceptions.Subscribe(OnFault);

    Observable.Merge(
            CreateIndexCommand.IsExecuting,
            DropIndexCommand.IsExecuting)
        .Subscribe(isExecuting => IsBusy = isExecuting);
  }

  public ReactiveCommand<Unit, IndexOperationResult> CreateIndexCommand { get; }

  public ReactiveCommand<IndexListItemViewModel, IndexOperationResult> DropIndexCommand { get; }

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

  public void SetTargetTable(TableListItemViewModel? table)
  {
    _table = table;
    StatusMessage = null;
    ErrorMessage = null;
    if (table is null)
    {
      IndexName = null;
      IndexExpression = null;
    }
  }

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

    var request = IndexDropRequest.Create(_table.Model.Path, index.Name);
    return await _indexService.DropIndexAsync(request);
  }

  private void ApplyResult(IndexOperationResult result)
  {
    if (result.Succeeded)
    {
      StatusMessage = result.Message;
      ErrorMessage = null;
    }
    else
    {
      StatusMessage = null;
      ErrorMessage = result.Message;
    }
  }

  private void OnFault(Exception exception)
  {
    StatusMessage = null;
    ErrorMessage = exception.Message;
  }
}
