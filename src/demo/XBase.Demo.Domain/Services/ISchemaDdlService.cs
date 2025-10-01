using System.Threading;
using System.Threading.Tasks;
using XBase.Demo.Domain.Schema;

namespace XBase.Demo.Domain.Services;

/// <summary>
/// Provides provider-aware schema DDL preview generation for create/alter/drop operations.
/// </summary>
public interface ISchemaDdlService
{
  Task<DdlPreview> BuildCreateTablePreviewAsync(TableSchemaDefinition schema, CancellationToken cancellationToken = default);

  Task<DdlPreview> BuildAlterTablePreviewAsync(TableAlterationDefinition alteration, CancellationToken cancellationToken = default);

  Task<DdlPreview> BuildDropTablePreviewAsync(string tableName, CancellationToken cancellationToken = default);
}
