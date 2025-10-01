# ADO.NET Provider Cookbook

## Quick Start
```bash
dotnet add package XBase.Data --version 0.1.0-preview
```
```csharp
await using var connection = new XBaseConnection("Data Source=/data/ledger.dbf;JournalMode=Sync;Encoding=cp437");
await connection.OpenAsync();

await using var command = connection.CreateCommand();
command.CommandText = "SELECT account_no, balance FROM ledger WHERE balance < @threshold";
command.Parameters.Add(new XBaseParameter("@threshold", 0m));

await using var reader = await command.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    Console.WriteLine($"{reader.GetString(0)} => {reader.GetDecimal(1)}");
}
```

## Connection String Reference
| Keyword | Description | Example |
|---------|-------------|---------|
| `Data Source` | Path to DBF table or directory catalog | `/data/ledger.dbf` |
| `JournalMode` | `Sync`, `Async`, `Off` | `Sync` |
| `Encoding` | Override code page | `cp850` |
| `TableCache` | Max open table handles | `32` |
| `SchemaCache` | TTL for schema metadata | `00:05:00` |

## Command Patterns
- **Parameter Binding** — Supports positional (`?`) and named (`@p0`) syntax. Internally binds using schema metadata to infer types.
- **Bulk Inserts** — Use `XBaseBulkCopy` for streaming loads; honors transactions and memo expansion.
- **Transactions** — Wrap commands in `XBaseTransaction` to enforce commit/rollback and propagate to journaling subsystem.

## Diagnostics
- Enable `XBase.Data` category logging for command traces and journaling metrics.
- `DbProviderFactories.GetFactory("XBase.Data")` supported for legacy configuration.

## Troubleshooting
| Symptom | Resolution |
|---------|------------|
| `UnsupportedEncodingException` | Add `Encoding=...` override or register custom mapping (see CODEPAGES.md). |
| `JournalFullException` | Run `xbase transactions checkpoint` or relocate journal. |
| Schema drift errors | Execute `xbase ddl checkpoint` to reconcile Online DDL changes. |

**End of ado-net-provider.md**
