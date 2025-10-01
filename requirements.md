# xBase for .NET — Requirements Specification (requirements.md)

> **Status**: Draft v0.1  
> **License of the project**: Apache-2.0 (recommended)  
> **Target runtime**: .NET 8 LTS (cross‑platform)

---

## 1. Purpose & Scope
**Purpose.** Provide a robust, cross‑platform, open‑source framework to read/write and query legacy **xBase** databases (dBASE/Clipper/FoxPro families) from .NET, with modern integrations (ADO.NET, EF Core) and production‑grade tooling.

**Scope (Phase A → Phase B).**
- **Phase A (MVP, write‑capable):** dBASE III+/IV **DBF** + **DBT** memo, **NTX** (Clipper) & **MDX** (dBASE IV) indexes.  
  Includes: ADO.NET provider (read/write), EF Core provider (read/write with journaling), transactions (user‑space journal), record/file locking, codepage detection, core CLI tools.
- **Phase B:** FoxPro 2.x **DBF** + **FPT** memo, **CDX** (compound/structural) indexes.  
  Includes: expression evaluator extensions, richer collation, and performance work for CDX.
- **Out of Scope (initially):** Visual FoxPro DBC metadata, VFP‑specific field types, OLE/General blobs beyond pass‑through, SQL DDL, stored procedures.

## 2. Stakeholders & Personas
- **Library Consumers**: .NET developers integrating legacy xBase data into new apps (ERP, billing, migration tools).
- **Data Migrators**: Analysts/engineers converting DBF systems to modern stores.
- **Ops**: Teams needing read‑only analytics or batch updates on shared filesystems.

## 3. Goals & Non‑Goals
### 3.1 Goals
- **G1**: High‑fidelity, **lossless** read/write for Phase‑A formats.
- **G2**: **Cross‑platform** support (Windows/Linux/macOS; works under Docker/containers).
- **G3**: **ADO.NET provider** implementing `DbConnection`, `DbCommand`, `DbDataReader`, `DbTransaction`, `DbParameter`.
- **G4**: **EF Core provider** with LINQ translation for common operators; supports Insert/Update/Delete with journaling.
- **G5**: **Transactions** via user‑space **WAL/journal**, with crash recovery and record/file locking.
- **G6**: **Index support**: NTX/MDX read/write; predicate and order pushdown; reindex.
- **G7**: **Codepage/LDID** detection with heuristics and override; UTF‑8 export tool.
- **G8**: Tooling: `dbfinfo`, `dbfdump`, `dbfpack`, `dbfreindex`, `dbfconvert`.
- **G9**: Comprehensive **tests** with real fixtures, property‑based tests, and CI.

### 3.2 Non‑Goals (initial)
- Full SQL engine, multi‑user server, or network protocol.  
- VFP DBC schema semantics and advanced VFP data types in Phase A.

## 4. Functional Requirements
### 4.1 File Formats
- **FR‑FF‑1**: DBF **read/write** for dBASE III+/IV with types: `C` (Char), `N` (Numeric), `F` (Float), `D` (Date), `L` (Logical), `M` (Memo).  
- **FR‑FF‑2**: **DBT** memo read/write (block size detection, chain following).  
- **FR‑FF‑3**: **NTX**/**MDX** index read/write (B‑tree).  
- **FR‑FF‑4 (Phase B)**: FoxPro 2.x **DBF/FPT**, **CDX** (read/write).

### 4.2 Schema & Encoding
- **FR‑SC‑1**: Parse header, fields, record length, **deleted flag**, last update date.  
- **FR‑SC‑2**: **LDID‑based** codepage detection; if missing/0, **heuristic** selection among CP852/850/437/1250.  
- **FR‑SC‑3**: Provide **override** via connection string/API; default to **CP852** when uncertain.  
- **FR‑SC‑4**: Support null semantics if present (later dBASE variants) and map to .NET `Nullable<T>`.

### 4.3 Cursor & Query Engine
- **FR‑CQ‑1**: Sequential and index‑based scans; filter and order **pushdown**.  
- **FR‑CQ‑2**: Predicates: `=`, `<`, `<=`, `>`, `>=`, `BETWEEN`, prefix `LIKE`, `IN` (small sets).  
- **FR‑CQ‑3**: Ordering by indexed columns; stable sort fallback when no index matches.  
- **FR‑CQ‑4**: Pagination: `LIMIT/OFFSET` equivalent.  
- **FR‑CQ‑5**: Deleted records excluded by default; toggle to include.

### 4.4 Write Path & Transactions
- **FR‑WT‑1**: Insert/Update/Delete with **journaled commit**.  
- **FR‑WT‑2**: **Journal format**: append‑only log entries (op, recno, before/after), checksums.  
- **FR‑WT‑3**: **Commit protocol**: fsync journal → update main files atomically → fsync → truncate/rotate journal.  
- **FR‑WT‑4**: **Crash recovery**: on open, redo/undo to consistent state.  
- **FR‑WT‑5**: **Index maintenance**: deferred updates within transaction; `REINDEX` tool.  
- **FR‑WT‑6**: **Locking**: OS file locks + optional `.lck`; **single‑writer/multi‑reader**; optional record‑level locks.  
- **FR‑WT‑7**: **Pack/Zap**: implement with safety checks and backups.

### 4.5 ADO.NET Provider
- **FR‑AD‑1**: Connection string: `xbase://path=<dir>;readonly=<bool>;journal=<on|off>;locking=<file|record|none>;codepage=<auto|cp852|...>;deleted=<hide|show>;cacheSize=<int>`.  
- **FR‑AD‑2**: SQL‑subset parser for: `SELECT` (projection, where, order, limit/offset).  
- **FR‑AD‑3**: Parameters with inferred DBF type mapping; named parameters `@p`.  
- **FR‑AD‑4**: Transactions via `DbTransaction` bound to journal mechanism.  
- **FR‑AD‑5**: Schema discovery APIs (`GetSchema`, table/column metadata).

### 4.6 EF Core Provider
- **FR‑EF‑1**: `UseXBase(connectionString)`; model conventions for DBF → .NET types.  
- **FR‑EF‑2**: Keys: if no natural PK, synthesize using **RECNO** (exposed as read‑only shadow key).  
- **FR‑EF‑3**: Query translation: `Where`, `OrderBy`, `Select`, `Skip`, `Take`; **Join**: client evaluation by default; warn in logs.  
- **FR‑EF‑4**: Change Tracking: optimistic concurrency using checksum/hash of serialized record or timestamp surrogate.  
- **FR‑EF‑5**: Migrations: **copy‑rebuild** strategy (create new DBF with target schema, copy data).  
- **FR‑EF‑6**: Bulk ops: optional batched write API; document performance trade‑offs.

### 4.7 Tooling (CLI)
- **FR‑TL‑1**: `dbfinfo` – print header, fields, LDID, codepage guess, record count, index tags.
- **FR‑TL‑2**: `dbfdump` – export to CSV/JSON (UTF‑8), toggle include deleted.
- **FR‑TL‑3**: `dbfpack` – remove deleted records safely (backup + journal sync).
- **FR‑TL‑4**: `dbfreindex` – rebuild NTX/MDX (CDX in Phase B).
- **FR‑TL‑5**: `dbfconvert` – transcode codepage → UTF‑8, or copy to new schema.

### 4.8 Online DDL (M3)
- **FR‑OD‑1**: Deliver **In-Place Online DDL (IPOD)** with schema-delta `.ddl` log, versioned projections, lazy backfill,
  atomic checkpoints, and short exclusive DDL locks.
- **FR‑OD‑2**: Support `CREATE TABLE`, `ALTER TABLE ADD/DROP/RENAME/MODIFY COLUMN`, `DROP TABLE`, `CREATE INDEX`, `DROP INDEX`
  with transactional guarantees and side-by-side index swaps.
- **FR‑OD‑3**: Maintain replayable `.ddl` log entries with checksums, monotonic schema version numbers, and bounded retention.
- **FR‑OD‑4**: Integrate schema replay with journal recovery: apply data log first, `.ddl` log second; resume incomplete backfill
  work queues.
- **FR‑OD‑5**: Extend ADO.NET/EF Core providers to parse/emit DDL statements and surface schema version metadata to callers.
- **FR‑OD‑6**: Tooling must provide `xbase ddl apply`, `xbase ddl checkpoint`, and `xbase ddl pack` with validation/dry-run
  switches.
- **FR‑OD‑7**: Acceptance coverage: concurrent reader safety, writer throttling under backfill, recovery from mid-DDL crash, and
  index swap correctness.

## 5. Non‑Functional Requirements (NFR)
### 5.1 Performance & Scalability
- **NFR‑P‑1**: Efficient **read path** using memory‑mapped I/O where safe (platform fallback to buffered).  
- **NFR‑P‑2**: Page caches for DBF records and index nodes; configurable sizes; LRU eviction.  
- **NFR‑P‑3**: Target baselines (indicative on mid‑range hardware):
  - Sequential read: ≥ 150 MB/s effective scanning of fixed‑length records.
  - Indexed point lookup: p50 ≤ 1 ms (warm cache); p99 ≤ 15 ms.
  - Insert throughput: ≥ 5k rec/s without index; ≥ 1k rec/s with 2 indexes.  
- **NFR‑P‑4**: Large file support: single DBF sizes ≥ 2 GB (subject to format limits).

### 5.2 Reliability & Consistency
- **NFR‑R‑1**: Crash‑safe commit (no torn writes on supported filesystems).  
- **NFR‑R‑2**: Recovery time: O(size of journal), with bounded upper limit via rotation.
- **NFR‑R‑3**: Checksums on journal entries; optional periodic snapshot.

### 5.3 Portability
- **NFR‑X‑1**: Windows, Linux, macOS; x64 & ARM64.  
- **NFR‑X‑2**: Compatible with containerized deployments; no platform‑specific P/Invoke hard dependency (only optional).

### 5.4 Security & Compliance
- **NFR‑S‑1**: No remote code execution; safe parsing with bounds checks.  
- **NFR‑S‑2**: Optional file permission checks; no elevation required.  
- **NFR‑S‑3**: Supply‑chain safety: signed releases, reproducible builds where possible.

### 5.5 Observability
- **NFR‑O‑1**: Structured logging (Microsoft.Extensions.Logging).  
- **NFR‑O‑2**: Event counters: cache hits/misses, I/O bytes, journal commits, lock waits.  
- **NFR‑O‑3**: Diagnostics hooks to inspect query plans and index usage.

## 6. Compatibility & Interop
- **CMP‑1**: Read DBF/DBT/NTX/MDX created by common dBASE/Clipper tools.  
- **CMP‑2**: Write files that remain readable by those tools where format allows.  
- **CMP‑3**: Codepage round‑trip tests; document known edge cases.

## 7. Configuration
- Connection string and fluent API options as per FR‑AD‑1 and FR‑EF‑1.  
- Global options: codepage policy (`auto|force`), default locking mode, cache sizes, deleted record visibility, null policy.

## 8. Error Handling
- Deterministic exception types (e.g., `XBaseFileFormatException`, `XBaseCodepageException`, `XBaseLockException`, `XBaseTransactionException`).  
- Clear remediation guidance in messages; include file path, table, recno, index tag.

## 9. Project Structure (proposed)
```
xBase.sln
  /src
    /XBase.Core
    /XBase.Abstractions
    /XBase.Data            // ADO.NET provider
    /XBase.EFCore          // EF Core provider
    /XBase.Tools           // CLI tools
    /XBase.Expressions     // expr & index key evaluator
    /XBase.Diagnostics
  /tests
    /XBase.Core.Tests
    /XBase.Data.Tests
    /XBase.EFCore.Tests
    /XBase.Tools.Tests
    /fixtures              // real DBF/DBT/NTX/MDX samples
  /docs
    ARCHITECTURE.md
    ROADMAP.md
    CODEPAGES.md
    INDEXES.md
    TRANSACTIONS.md
    EFCORE_LIMITATIONS.md
LICENSE (Apache-2.0)
README.md
```

## 10. Testing Strategy
- **Unit tests** for parsers, encoders, index operations, journal commit/recovery.  
- **Property‑based** tests: read→write→read equivalence; reindex invariants.  
- **Fuzzing** for corrupted headers/memos/index pages.  
- **Concurrency** tests: single‑writer with parallel readers; lock contention.  
- **Cross‑tool** validation using third‑party DBF readers (when license permits).

## 11. Documentation & Examples
- Quickstart for ADO.NET and EF Core; mapping tables for types; codepage guide.  
- Tooling manuals; migration cookbook (DBF → CSV/Parquet/SQL).  
- Performance tuning guide (cache, locking, batch strategies).

## 12. Release & Licensing
- **License**: **Apache‑2.0** (patent grant, enterprise‑friendly).  
- SemVer releases; signed NuGet packages; GitHub Actions CI; SBOM.

## 13. Risks & Mitigations
- **R‑1**: Index expression dialect differences → define a **documented subset**, pluggable evaluator.
- **R‑2**: Codepage ambiguity → LDID first, then heuristics, then explicit override; provide `dbfconvert`.
- **R‑3**: Data corruption from concurrent writers → enforce **single‑writer** by default; strong warnings and lock enforcement.
- **R‑4**: EF Core JOIN expectations → default to **client‑eval**, log warnings, provide guidance.

## 14. Milestones & Acceptance Criteria
- **M1 (Core Read)**: DBF/DBT read; NTX/MDX read; codepage auto; `dbfinfo` works.  
  *Acceptance*: open 20+ fixtures, correct schema and record counts.
- **M2 (Core Write + Journal)**: I/U/D; journal commit/recover; basic locking.  
  *Acceptance*: crash simulation tests pass; no data loss.
- **M3 (ADO.NET)**: SELECT subset; parameters; transactions.  
  *Acceptance*: ADO.NET samples run cross‑platform; perf baselines met.
- **M4 (EF Core Read/Write)**: LINQ subset; tracking; concurrency tokens.  
  *Acceptance*: CRUD round‑trips; concurrency tests pass.
- **M5 (Indexes & Tools)**: Reindex/pack tools; predicate/order pushdown measured.  
  *Acceptance*: >70% queries use index according to diagnostics counters.
- **M6 (Phase B Read)**: FoxPro 2.x DBF/FPT/CDX read.  
  *Acceptance*: Open known CDX datasets correctly; tag enumeration.

## 15. User Stories
- **US‑01**: As a developer, I can connect via ADO.NET and `SELECT` filtered rows with paging and ordering.  
- **US‑02**: As an EF Core user, I can `Add/Update/Delete` entities and commit atomically with crash safety.  
- **US‑03**: As an operator, I can run `dbfpack` and `dbfreindex` to maintain file health.  
- **US‑04**: As a migrator, I can `dbfdump` to UTF‑8 CSV/JSON preserving accents.

## 16. Glossary
- **DBF/DBT/FPT**: Table & memo file formats.  
- **NTX/MDX/CDX**: Index formats (Clipper/dBASE/FoxPro).  
- **LDID**: Language Driver ID (codepage indicator).  
- **RECNO**: Record number (1‑based).  
- **PACK/ZAP**: Physical deletion/clear operations.

---

**End of requirements.md**

