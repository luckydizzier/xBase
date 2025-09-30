# xBase for .NET — Architecture (ARCHITECTURE.md)

> **Status**: Draft v0.1  
> **Target runtime**: .NET 8 LTS (Windows/Linux/macOS, x64 & ARM64)  
> **Scope**: Phase A (dBASE III+/IV DBF + DBT, NTX/MDX) with writable EF/ADO.NET; Phase B (FoxPro 2.x DBF/FPT, CDX)

---

## 1. Architectural Overview
The framework is a **modular, layered system** that separates binary storage concerns from provider interfaces and high-level integrations:

```
Applications / Tools
      ▲
      │         EF Core Provider (XBase.EFCore)
      │         ADO.NET Provider (XBase.Data)
      │
Abstractions (XBase.Abstractions)  ← public SPIs & contracts
      │
Core Engine (XBase.Core)  ← DBF/DBT, NTX/MDX, journaling, locking, codepages, cursors
      │
Diagnostics & Expressions (XBase.Diagnostics / XBase.Expressions)
      │
File System & OS (memory-mapped I/O, file locks)
```

**Key principles:**
- **Portability-first:** No mandatory native dependencies. MMAP and file locks via .NET APIs with fallbacks.
- **Safety over cleverness:** Crash-safe journaling; single-writer default; deterministic error contracts.
- **Pushdown where possible:** Filters/order executed as close to storage as possible.
- **Composable providers:** ADO.NET and EF Core layers are thin adapters over the same cursor APIs.

---

## 2. Projects & Responsibilities

### 2.1 XBase.Core
- **Binary formats**: DBF (dBASE III+/IV), DBT (memo), NTX/MDX (B-tree indexes).
- **Record model**: fixed-length rows, deleted-flag handling, optional null bitmap (if present), RECNO addressing.
- **Memo management**: block size detection, chain following, slack space reuse, safe growth.
- **Index engine**: Node/page cache, key comparers, collations, tag metadata, rebuild/reindex.
- **Transactions**: user-space **WAL/journal** (`.trx`), atomic commits, recovery on open.
- **Locking**: OS file locks; optional `.lck` sidecar; record-level locks with coarse-grained file lock guard.
- **Codepages**: LDID map; heuristics when LDID is missing; override policies; transcoding utilities.
- **Cursors**: sequential & indexed scans, predicate/order pushdown, pagination.

### 2.2 XBase.Abstractions
- Contracts: `ITable`, `IIndex`, `ICursor`, `ISchemaProvider`, `ITransaction`, `IJournal`, `ILocker`, `IValueEncoder`, `IPageCache`.
- SPI for adding future format modules (Phase B CDX/FPT, future VFP module).

### 2.3 XBase.Data (ADO.NET)
- Implements `DbConnection`, `DbCommand`, `DbDataReader`, `DbTransaction`, `DbParameter`.
- SQL-subset parser → query plan builder (index selection + scan).
- Connection string policy parsing (journal/locking/codepage/cache/deleted-flag).

### 2.4 XBase.EFCore
- Provider glue: `UseXBase(...)` extension, `TypeMapping`, `MemberTranslator`, `QuerySqlGenerator`-equivalent pipeline for pushdown.
- Keys: synthetic **RECNO** if none present; concurrency token via record checksum.
- Write model: I/U/D delegates to Core with journaling; migrations via copy-rebuild.

### 2.5 XBase.Expressions
- dBASE/Clipper/FoxPro **expression subset** used for index keys & predicate evaluation (Phase A subset; extended in Phase B).
- Pluggable evaluators & function registry (e.g., `UPPER`, `TRIM`, arithmetic, date ops; collation-aware comparisons).

### 2.6 XBase.Diagnostics
- Structured logging, event counters (cache hits/misses, bytes read/written, journal commits, lock waits).
- Validators: header/index consistency checkers.

### 2.7 XBase.Tools
- CLI utilities: `dbfinfo`, `dbfdump`, `dbfreindex`, `dbfpack`, `dbfconvert`.

---

## 3. Data Flow & Execution Model

1. **Open**: `XBaseConnection` resolves directory, discovers table/index/memo files, loads headers lazily.  
2. **Plan**: ADO.NET/EF build a logical plan → Core **Planner** selects index/tag or sequential scan; builds a **Cursor**.  
3. **Execute**: Cursor yields records; **Predicate pushdown** applies comparisons before materialization; **Order pushdown** uses index order when possible.  
4. **Write**: EF/ADO.NET writes via Core **Mutator** → journal append → on commit: fsync journal, apply changes, fsync data, rotate journal.  
5. **Recovery**: On open, any non-empty journal is replayed (redo/undo depending on last consistent marker).

---

## 4. File Format Handling

### 4.1 DBF
- Header parsing: version, date, header size, record size, field descriptors, LDID.
- Record access: fixed-offset views; deleted flag; optional null bitmap (later variants); RECNO = 1-based index.
- Type mapping: `C/N/F/D/L/M` → .NET types (`string/decimal/double/DateOnly/bool/Stream or string` for memo).

### 4.2 Memo (DBT)
- Block size from header; first block directory; fragmented chains.
- Writes allocate new blocks; compaction optional; safe growth with fsync checkpoints.

### 4.3 Indexes (NTX/MDX)
- B-tree with fixed fan-out; key serialization; page cache; tag metadata.
- Key expressions (subset) compiled to evaluator delegates; **deterministic collation**.
- Reindex builds new index side-by-side and atomically swaps.

### 4.4 Phase B (Preview)
- FoxPro 2.x **FPT/CDX** introduce compound indexes and richer collation; isolated module keeps Core stable.

---

## 5. Transactions & Locking

### 5.1 Journaled Transactions
- **Append-only journal** entries: `{TxnId, Op(Insert|Update|Delete|IndexOp), Table, RecNo, Before, After, Checksum}`.
- **Commit protocol**: `fsync(journal)` → apply to data files → `fsync(data)` → write commit marker → truncate/rotate journal.
- **Atomicity**: rename/swap patterns ensure power-failure safety on POSIX/NTFS.

### 5.2 Concurrency Model
- Default **single-writer, multi-reader**. Optional record-level locks for long updates.
- Readers respect file locks; writers acquire exclusive table locks during commit window.
- EF `SaveChanges` wraps mutations in a transaction; optimistic concurrency via record checksum column.

---

## 6. Codepages & Collations
- **LDID-first** detection; if missing: **heuristics** (invalid byte ratios, Hungarian accent profile, fallback CP852).  
- Collation strategies: binary (default) + optional locale-aware uppercasing for comparisons; documented and consistent.
- Tooling `dbfconvert` supports transcode to UTF‑8 copies for migration workflows.

---

## 7. Query & Expression Pipeline

### 7.1 Predicates
- Supported pushdown: equality/inequalities on `C/N/F/D/L`, `BETWEEN`, prefix `LIKE`, small `IN` lists.  
- Non-pushdown expressions evaluated client-side (EF) with logged warnings.

### 7.2 Ordering & Paging
- If an index prefix matches the `ORDER BY`, use index order; otherwise stable in-memory sort with spill-to-temp when needed.
- Pagination via `LIMIT/OFFSET` materialized on cursor.

### 7.3 Expression Subset (Phase A)
- Literals, arithmetic, logical ops, string funcs (`SUBSTR`, `LEFT`, `RIGHT`, `UPPER`, `TRIM`), date diff/add (limited).
- Deterministic serialization for index keys.

---

## 8. ADO.NET Provider Design

- **Connection**: `xbase://path=<dir>;readonly=<bool>;journal=<on|off>;locking=<file|record|none>;codepage=<auto|cp852|...>;deleted=<hide|show>;cacheSize=<int>`.
- **Command**: SQL-subset → logical plan; parameters `@p` mapped to DBF types.
- **Reader**: column ordinals map to field descriptors; memo streams on demand.
- **Transaction**: wraps journal begin/commit/rollback.

---

## 9. EF Core Provider Design

- **Model building**: conventions map DBF fields to .NET; RECNO as shadow key if needed.
- **Query translation**: attempt pushdown; unsupported features fallback with warnings; joins default to client-eval.
- **Change tracking**: serialize-before/after for checksum; concurrency token compares at save time.
- **Migrations**: copy-rebuild pattern with progress callbacks.

---

## 10. Performance Architecture

- **I/O**: Memory-mapped reads where safe; buffered writes with durable fsync boundaries.
- **Caches**: Record page cache and index node cache (LRU); configurable sizes.
- **Batching**: Grouped writes; deferred index updates per transaction.
- **Metrics**: event counters for throughput/latency; optional simple flame traces.

---

## 11. Error Handling & Diagnostics

- Typed exceptions: `XBaseFileFormatException`, `XBaseCodepageException`, `XBaseLockException`, `XBaseTransactionException`.
- Messages include file, table, recno, tag, and remediation hints.
- `Explain` endpoint (debug only) prints query plan and pushdown decisions.

---

## 12. Security Considerations
- Bounds-checked parsing; limit record and memo sizes; guard against path traversal in index/memo resolution.
- No dynamic codegen from untrusted expressions; expression subset is compiled with a safe interpreter/JIT.

---

## 13. Testing & CI
- Fixture-based tests on real DBF/DBT/NTX/MDX.
- Property-based invariants: read→write→read equivalence; reindex equivalence; crash simulations for journal recovery.
- CI matrix: Windows/Linux/macOS, x64/ARM64.

---

## 14. Extensibility
- SPI in `XBase.Abstractions` for new formats (CDX/FPT, future VFP).
- Pluggable collations and codepage providers.
- Expression function registry and custom translators.

---

## 15. Deployment & Packaging
- NuGet packages per module (`XBase.Core`, `XBase.Data`, `XBase.EFCore`, `XBase.Tools`).
- Signed packages, SemVer, source link, symbols.
- Minimal runtime dependencies; enable trimming where possible.

---

## 16. Open Questions (to be tracked)
- Precise null semantics per variant (documented matrix).
- Record-level locks cross-platform consistency (advisory vs mandatory).
- Large file limits and safe behavior above 2–4 GB boundaries for legacy tools.

---

## 17. Appendix: Example Plans

**Example**: `SELECT Name FROM Products WHERE Name LIKE 'P%' ORDER BY Name LIMIT 100`
- **Plan**: Use `Name` ascending index (if exists) → prefix `LIKE` pushdown → scan until `'Q'` boundary → return first 100.

**Example**: `WHERE Price BETWEEN 100 AND 200`
- **Plan**: Use `Price` index tag range scan; if absent, sequential scan with predicate pushdown.

---

**End of ARCHITECTURE.md**

