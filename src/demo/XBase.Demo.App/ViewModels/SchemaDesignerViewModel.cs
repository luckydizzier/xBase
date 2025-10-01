using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using XBase.Demo.Domain.Schema;
using XBase.Demo.Domain.Services;

namespace XBase.Demo.App.ViewModels;

/// <summary>
/// Provides commands for generating schema DDL previews.
/// </summary>
public sealed class SchemaDesignerViewModel : ReactiveObject
{
  private readonly ISchemaDdlService _ddlService;
  private readonly ObservableCollection<SchemaColumnViewModel> _columns = new();
  private readonly ObservableCollection<SchemaColumnChangeViewModel> _alterations = new();
  private string? _tableName;
  private string? _previewOperation;
  private string _previewUpScript = string.Empty;
  private string _previewDownScript = string.Empty;
  private string? _errorMessage;

  public SchemaDesignerViewModel(ISchemaDdlService ddlService)
  {
    _ddlService = ddlService;

    Columns = new ReadOnlyObservableCollection<SchemaColumnViewModel>(_columns);
    Alterations = new ReadOnlyObservableCollection<SchemaColumnChangeViewModel>(_alterations);

    GenerateCreatePreviewCommand = ReactiveCommand.CreateFromTask(ExecuteGenerateCreatePreviewAsync);
    GenerateCreatePreviewCommand.Subscribe(ApplyPreview);
    GenerateCreatePreviewCommand.ThrownExceptions.Subscribe(OnPreviewFault);

    GenerateAlterPreviewCommand = ReactiveCommand.CreateFromTask(ExecuteGenerateAlterPreviewAsync);
    GenerateAlterPreviewCommand.Subscribe(ApplyPreview);
    GenerateAlterPreviewCommand.ThrownExceptions.Subscribe(OnPreviewFault);

    GenerateDropPreviewCommand = ReactiveCommand.CreateFromTask(ExecuteGenerateDropPreviewAsync);
    GenerateDropPreviewCommand.Subscribe(ApplyPreview);
    GenerateDropPreviewCommand.ThrownExceptions.Subscribe(OnPreviewFault);
  }

  public ReadOnlyObservableCollection<SchemaColumnViewModel> Columns { get; }

  public ReadOnlyObservableCollection<SchemaColumnChangeViewModel> Alterations { get; }

  public ReactiveCommand<Unit, DdlPreview> GenerateCreatePreviewCommand { get; }

  public ReactiveCommand<Unit, DdlPreview> GenerateAlterPreviewCommand { get; }

  public ReactiveCommand<Unit, DdlPreview> GenerateDropPreviewCommand { get; }

  public string? TableName
  {
    get => _tableName;
    set => this.RaiseAndSetIfChanged(ref _tableName, value);
  }

  public string? PreviewOperation
  {
    get => _previewOperation;
    private set => this.RaiseAndSetIfChanged(ref _previewOperation, value);
  }

  public string PreviewUpScript
  {
    get => _previewUpScript;
    private set => this.RaiseAndSetIfChanged(ref _previewUpScript, value);
  }

  public string PreviewDownScript
  {
    get => _previewDownScript;
    private set => this.RaiseAndSetIfChanged(ref _previewDownScript, value);
  }

  public string? ErrorMessage
  {
    get => _errorMessage;
    private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
  }

  public void SetTargetTable(TableListItemViewModel? table)
  {
    TableName = table?.Name;
    ErrorMessage = null;
  }

  public void SetColumns(params SchemaColumnViewModel[] columns)
  {
    _columns.Clear();
    foreach (var column in columns)
    {
      _columns.Add(column);
    }
  }

  public void SetAlterations(params SchemaColumnChangeViewModel[] alterations)
  {
    _alterations.Clear();
    foreach (var alteration in alterations)
    {
      _alterations.Add(alteration);
    }
  }

  private async Task<DdlPreview> ExecuteGenerateCreatePreviewAsync()
  {
    if (string.IsNullOrWhiteSpace(TableName))
    {
      throw new InvalidOperationException("Table name is required to generate DDL.");
    }

    if (_columns.Count == 0)
    {
      throw new InvalidOperationException("At least one column is required to generate a CREATE TABLE script.");
    }

    var schema = new TableSchemaDefinition(TableName!, _columns.Select(column => column.ToDefinition()).ToArray());
    var preview = await _ddlService.BuildCreateTablePreviewAsync(schema);
    return preview;
  }

  private async Task<DdlPreview> ExecuteGenerateAlterPreviewAsync()
  {
    if (string.IsNullOrWhiteSpace(TableName))
    {
      throw new InvalidOperationException("Table name is required to generate DDL.");
    }

    if (_alterations.Count == 0)
    {
      throw new InvalidOperationException("At least one alteration is required to generate an ALTER TABLE script.");
    }

    var definition = new TableAlterationDefinition(TableName!, _alterations.Select(change => change.ToDefinition()).ToArray());
    return await _ddlService.BuildAlterTablePreviewAsync(definition);
  }

  private async Task<DdlPreview> ExecuteGenerateDropPreviewAsync()
  {
    if (string.IsNullOrWhiteSpace(TableName))
    {
      throw new InvalidOperationException("Table name is required to generate DDL.");
    }

    return await _ddlService.BuildDropTablePreviewAsync(TableName!);
  }

  private void ApplyPreview(DdlPreview preview)
  {
    PreviewOperation = preview.Operation;
    PreviewUpScript = string.Join(Environment.NewLine + Environment.NewLine, preview.UpStatements);
    PreviewDownScript = string.Join(Environment.NewLine + Environment.NewLine, preview.DownStatements);
    ErrorMessage = null;
  }

  private void OnPreviewFault(Exception exception)
  {
    ErrorMessage = exception.Message;
  }
}
