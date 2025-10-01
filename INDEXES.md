# xBase for .NET — Index Strategy (INDEXES.md)

## Scope
Phase A covers dBASE III+/IV NTX (single-tag) and MDX (multi-tag) index families with memo integration. Phase B extends to FoxPro CDX.

## Engine Architecture
- **Index Catalog** — `IndexDescriptor` models logical tags (expression, filter, sort order, collation) and binds to physical files.
- **Reader Pipeline** — `IndexCursor` exposes seek/scan primitives backed by `BTreeNavigator` with lazy page loading and cache-aware readahead.
- **Writer Pipeline** — Mutation journal buffers delta pages, flushed through `IndexRebuilder` with crash-safe checkpoints. Deferred maintenance hooks coordinate with `TransactionScope` to avoid torn updates.

## Maintenance Operations
| Operation | CLI Command | Provider API | Notes |
|-----------|-------------|--------------|-------|
| Rebuild single index | `xbase dbfreindex --table <path> --tag <name>` | `IndexMaintenance.RebuildAsync` | Uses shadow file then swap |
| Pack deleted records | `xbase dbfpack --table <path>` | `TableMaintenance.PackAsync` | Triggers index vacuum afterwards |
| Diagnose stats | `xbase dbfinfo --detail indexes` | `IndexDiagnostics.GetUsageAsync` | Reports selectivity + fragmentation |

## Consistency Guarantees
1. Primary data writes land in WAL before index updates.
2. Index delta pages are idempotent; recovery replays operations until tag root LSN matches journal checkpoint.
3. Online DDL leverages `IndexDefinitionChanged` events so providers can rebuild or discard tags without blocking readers.

## Testing Strategy
- Unit: `BTreeNavigatorTests`, `IndexExpressionCompilerTests`.
- Integration: `IndexMaintenanceTests` across NTX/MDX fixtures with concurrent mutation scenarios.
- Performance: Benchmark harness (`XBase.Benchmarks`) to validate seek latency, index hit ratio (>70%) for M5 acceptance.

## Phase B Preview
- Extend collation support for FoxPro CDX (compound and descending indexes).
- Introduce structural compression for large tag trees.

**End of INDEXES.md**
