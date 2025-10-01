# Task Tracker

## M1 – Foundation Bootstrap ✅
- [x] Scaffolded `xBase.sln` with module projects under `src/` and mirrored test projects under `tests/`, governed by `Directory.Build.props`.
- [x] Authored baseline documentation (`README.md`, `requirements.md`, `architecture.md`) describing scope, goals, and architecture.
- [x] Added `XBase.Tools` pipeline orchestrator with `verify/clean/restore/build/test/publish` commands and initial `dbfinfo` workflow.

## M2 – Metadata & Discovery ✅
- [x] Implemented DBF metadata loader (`DbfTableLoader`, `TableDescriptor`, encoding registry) with sidecar detection and fixture-backed tests.
- [x] Added `TableCatalog` directory discovery plus expression and diagnostics scaffolding (`ExpressionEvaluator`, `XBaseLogger`) covered by unit tests.
- [x] Exercised loaders via fixture assertions in `XBase.Core.Tests` and smoke-tested the tooling pipeline through `XBase.Tools.Tests`.

## M3 – Data Engine 🚧
- [ ] Implement sequential/indexed cursor implementations that materialize records from DBF/DBT files.
- [ ] Provide memo (DBT) block reader/writer with cache management and tests over fixtures.
- [ ] Wire table mutators to support insert/update/delete against in-memory row buffers (beyond no-op stubs).

## M4 – Journaling & Transactions ⏳
- [ ] Design and implement the WAL journal format covering FR-WT-1 – FR-WT-4 requirements with crash-recovery tests.
- [ ] Introduce lock coordination (file + optional record locks) and acceptance tests simulating concurrent access.
- [ ] Implement deferred index maintenance hooks to enable safe reindex/pack flows.

## M5 – Provider Integrations ⏳
- [ ] Complete ADO.NET command execution pipeline (parser, plan builder, `XBaseDataReader`) returning real records.
- [ ] Implement EF Core provider services (type mappings, query translation, change tracking) backed by integration tests.
- [ ] Deliver connection string/journaling options surface plus configuration docs.

## M6 – Tooling, Docs & Release ⏳
- [ ] Expand CLI with `dbfdump`, `dbfpack`, `dbfreindex`, `dbfconvert`, including smoke tests.
- [ ] Author remaining documentation set (ROADMAP.md, CODEPAGES.md, INDEXES.md, TRANSACTIONS.md, provider cookbooks).
- [ ] Establish CI pipeline, packaging strategy, and release checklist for Phase A GA.
