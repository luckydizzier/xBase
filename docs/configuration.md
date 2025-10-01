# Configuration Guide

This document describes how to configure xBase .NET connections, including the connection string format and journaling controls exposed by the provider surface.

## Connection String Format

xBase connections use a semicolon-delimited key/value syntax with an optional `xbase://` prefix. The `path` option is required and must point to a directory that contains DBF/DBT/NTX/MDX assets.

```
xbase://path=/data/southwind;readonly=false;journal=wal;locking=file;deleted=hide
```

### Supported Options

| Option | Values | Default | Description |
| --- | --- | --- | --- |
| `path` | Directory path | _(required)_ | Base directory containing the database files. |
| `readonly` | `true`, `false` | `false` | When `true`, disables mutations and journaling. |
| `journal` | `wal`, `on`, `off` | `wal` | Enables (`wal`/`on`) or disables (`off`) the write-ahead log. |
| `journal.directory` | Directory path | Connection `path` | Overrides the directory where the journal file is stored. |
| `journal.file` | File name | `xbase.trx` | Sets the journal file name. |
| `journal.flushOnWrite` | `true`, `false` | `true` | Controls whether WAL writes flush buffers after each append. |
| `journal.flushToDisk` | `true`, `false` | `true` | Enables durable flushes via `Flush(true)` for crash safety. |
| `journal.autoResetOnCommit` | `true`, `false` | `true` | Truncates the journal after commits/rollbacks when `true`. |
| `locking` | `none`, `file`, `record` | `file` | Chooses the lock manager mode for concurrent access. |
| `deleted` | `hide`, `show` | `hide` | Determines whether soft-deleted rows are visible to queries. |

Boolean options accept `true/false`, `on/off`, `yes/no`, or `1/0`.

## Journaling Options

Journaling is enabled by default through a write-ahead log (`xbase.trx`). `XBaseConnectionOptions` exposes a strongly typed view over the connection string so callers can inspect or generate the necessary settings.

- `Journal.Mode`: `WriteAheadLog` when journaling is active, `Disabled` otherwise.
- `Journal.DirectoryPath`: Directory containing the journal; defaults to the connection `path` when not specified.
- `Journal.FileName`: File name of the WAL file.
- `Journal.FlushOnWrite`: Applies `FlushAsync` on each append when `true`.
- `Journal.FlushToDisk`: Issues `Flush(true)` for durable commits.
- `Journal.AutoResetOnCommit`: Resets the WAL file after commit/rollback.

`XBaseJournalOptions.CreateWalOptions(rootPath)` transforms the high-level settings into `WalJournalOptions` that are consumed by the core journaling engine. The method validates that the journal directory can be resolved from either `journal.directory` or the connection `path`.

## Entity Framework Core Integration

`DbContextOptionsBuilder.UseXBase(connectionString)` forwards the raw connection string to `XBaseConnection`. The provider parses the string into `XBaseConnectionOptions`, enabling applications to inspect `XBaseConnection.Options` for diagnostics or to construct a custom `IJournal` using the derived `WalJournalOptions`.

Sample registration with dependency injection:

```csharp
services.AddDbContext<AppDbContext>(options =>
{
  options.UseXBase("xbase://path=/data/app;journal=wal");
});
```

When running in read-only mode (`readonly=true`), journaling is automatically disabled even if `journal=wal` is supplied.
