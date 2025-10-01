# xBase for .NET — In-Place Online DDL Strategy (DDL.md)

> **Milestone**: M3 (ADO.NET + Online DDL enablement)
> **Status**: Draft v0.1

---

## 1. Goals & Scope
- Deliver **In-Place Online DDL (IPOD)** that allows schema evolution without full table copy while keeping read workloads and
the single-writer pipeline active.
- Guarantee **crash safety** by sequencing data journal replay before schema log replay and requiring idempotent deltas.
- Support DDL verbs: `CREATE TABLE`, `ALTER TABLE ADD/DROP/RENAME/MODIFY COLUMN`, `DROP TABLE`, `CREATE INDEX`, `DROP INDEX`.
- Apply to Phase A storage formats (DBF/DBT with NTX/MDX indexes); design extensible for Phase B (FPT/CDX).

---

## 2. Core Components
### 2.1 Schema-Delta `.ddl` Log
- Append-only UTF-8 or binary log stored alongside tables: `<table>.ddl`.
- Entry shape: `{Version, Timestamp, Author, Operation, Payload, Checksum}` with monotonic `Version` (uint64).
- Indexed by `Version`; truncated only after checkpoint consolidates up to `Version`.
- Stored under journal directory for transactional durability: write to temp → fsync → rename.

### 2.2 Versioned Projections
- `TableCatalog` tracks `ActiveVersion` and `PreviousVersion` descriptors.
- Cursors request a target schema version; if data row precedes the version, adapters pad defaults / mark tombstoned columns.
- Readers pinned to snapshot version to avoid shape changes mid-read; writers use latest version.

### 2.3 Lazy Backfill
- Queue maintained per table for rows requiring reshape (e.g., new column default, dropped column tombstone).
- Triggered by write path (on read-modify-write) and background worker invoked by tooling.
- Bounded by throttles (max rows per second) and yields to transaction commit.

### 2.4 Atomic Checkpoints
- `ddl checkpoint` command acquires short exclusive DDL lock, flushes journal, applies pending deltas to header, rewrites schema
catalog, and compacts `.ddl` log up to `CheckpointVersion`.
- Optionally runs `pack` if flagged, ensuring deleted rows removed only after schema stabilization.

### 2.5 Short Exclusive DDL Lock
- Distinct from write lock; only held during header rewrite, index swap rename, or checkpoint consolidation.
- Enforced via OS advisory lock on `<table>.ddl.lock` to avoid interfering with standard read/write operations.

### 2.6 Side-by-Side Index Swaps
- Index create/modify builds new NTX/MDX under temp suffix (e.g., `.new`), validated via checksum/scan.
- Swap performed atomically via rename while holding DDL lock; rollback removes temp artifact.

---

## 3. Operation Semantics
### 3.1 CREATE TABLE
1. Validate schema (field types, lengths, LDID) and allocate initial header.
2. Emit `.ddl` entry `CreateTable` with version `v1` capturing schema + memo/index metadata.
3. Apply header write under DDL lock, create empty DBF/DBT, initialize journal & `.ddl` log.
4. Post-create checkpoint marks base version and clears staging artifacts.

### 3.2 ALTER TABLE
- **ADD COLUMN**
  1. Append `.ddl` entry describing column name, type, default, nullability, placement.
  2. Update catalog version; new writes emit column value; backfill queue enqueues existing rows with default value lazily.
  3. Checkpoint rewrites header to include column once majority backfill done.
- **DROP COLUMN**
  1. Emit `.ddl` entry marking column tombstoned; update catalog to hide column from new projections.
  2. Backfill queue truncates column data lazily by writing tombstone markers; checkpoint rewrites header without column and
     optionally triggers `pack`.
- **RENAME COLUMN**
  1. `.ddl` entry maps `OldName → NewName`; projections provide alias view instantly.
  2. Header rename deferred to checkpoint to reduce lock window; indexes referencing column updated during swap.
- **MODIFY COLUMN** (type/length change within compatible family)
  1. Validate compatibility (e.g., `C(20) → C(40)`, `N(8,2) → N(10,2)`).
  2. `.ddl` entry records transformation function; adapters coerce reads; backfill rewrites rows via lazy process.
  3. Incompatible changes rejected; require copy-rebuild tool.

### 3.3 DROP TABLE
1. `.ddl` entry `DropTable` created, marking table as pending drop.
2. Acquire DDL lock, ensure no active transactions, fsync journal, rename artifacts to trash directory.
3. Update catalog removing table; recovery honors drop by ignoring table files beyond drop version.

### 3.4 CREATE INDEX
1. `.ddl` entry `CreateIndex` capturing tag name, expression, order, key length, filter.
2. Build index side-by-side; track progress in journal for crash recovery.
3. On completion, acquire DDL lock and atomically rename `.new` file; checkpoint records final metadata.

### 3.5 DROP INDEX
1. `.ddl` entry `DropIndex` appended.
2. Acquire DDL lock briefly to unlink index file; mark as inactive immediately in catalog.
3. Recovery replays entry to ensure stray files removed.

---

## 4. Recovery & Locking Limits
- Recovery order: **data journal → schema `.ddl` log → backfill queue resume**.
- Backfill tasks persisted in journal to avoid replay divergence.
- Lock escalation: if backfill lags beyond threshold, throttle new writes or request operator checkpoint.
- Maximum DDL lock hold time target: < 250 ms for ALTER operations; < 2 s for checkpoint (with memo/index fsync).
- Detect conflicting DDL by comparing `CurrentVersion` before appending new entry; providers retry with updated metadata.

---

## 5. Provider Integrations
### 5.1 ADO.NET Provider
- Extend parser to accept `CREATE TABLE`, `ALTER TABLE`, `DROP TABLE`, `CREATE INDEX`, `DROP INDEX` statements mapped to
  `ISchemaMutator` API.
- `DbCommand` executes DDL under implicit transaction, emitting schema version results (e.g., `SchemaVersion` output parameter).
- `GetSchema` includes columns: `SchemaVersion`, `PendingBackfill`, `LastCheckpoint`.

### 5.2 EF Core Provider
- Update migrations pipeline to emit `.ddl` deltas instead of copy-rebuild for supported mutations.
- Migrations annotate operations with expected `PreviousVersion`; runtime compares before applying to detect drift.
- Provide extension method `UseXBaseOnlineMigrations()` toggling IPOD path; fallback to copy-rebuild when operation unsupported.
- Track pending backfill metrics surfaced via `IDiagnosticsLogger` for application awareness.

---

## 6. Tooling & Automation
- `xbase ddl apply <path>`: stream `.ddl` script(s) into table logs with validation, dry-run mode, and batch checkpoint option.
- `xbase ddl checkpoint <table>`: force checkpoint, optionally `--pack` to combine with record compaction.
- `xbase ddl pack <table>`: run pack with schema awareness, ensuring drop-column tombstones cleared before header rewrite.
- Tool commands honor `--lock-timeout`, `--throttle`, and `--resume` flags for long-running operations.
- CLI emits structured progress events for integration with CI or operations dashboards.

---

## 7. Testing & Acceptance (M3)
- **Unit Tests**: schema log serialization/deserialization, version arithmetic, conflict detection.
- **Integration Tests**: run ALTER scenarios with concurrent readers and ensure consistent projections; simulate crash mid-DDL and
  verify recovery completes without data loss.
- **Performance Tests**: measure DDL lock durations, backfill throughput, index swap latency.
- **Acceptance Criteria**:
  - All FR-OD-* requirements satisfied with automated coverage.
  - EF Core migration applying ADD COLUMN with concurrent reads passes without blocking longer than target thresholds.
  - Tooling commands produce idempotent results across reruns; checkpoint reduces `.ddl` log size as expected.
  - Documentation updated (ARCHITECTURE.md, requirements.md, this file) and referenced in ROADMAP milestone M3.

---

**End of DDL.md**
