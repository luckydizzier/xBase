# xBase for .NET — Roadmap (ROADMAP.md)

> **Release Horizon**: Phase A (M1–M6)
> **Target Runtime**: .NET 8 LTS

---

## Milestone Overview

| Milestone | Focus | Key Deliverables | Acceptance Signals |
|-----------|-------|------------------|--------------------|
| **M1 – Core Read** | Table metadata ingestion and read path bootstrap | DBF/DBT open/read, NTX/MDX navigation, `dbfinfo` tool | Fixtures load with correct schema + record counts |
| **M2 – Core Write & Journal** | Durable write path with crash recovery | Insert/update/delete pipeline, WAL journal format, locking baseline | Crash simulations recover without data loss |
| **M3 – Online DDL + ADO.NET** | In-Place Online DDL/IPOD plus provider surfacing | Schema-delta `.ddl` log, lazy backfill orchestration, provider DDL verbs, CLI `ddl apply/checkpoint/pack`, retained ADO.NET SELECT subset/parameters/transactions | Schema log replay tested, providers execute DDL end-to-end, tooling passes validation/dry-run scenarios |
| **M4 – EF Core Read/Write** | LINQ translation and change tracking | EF Core provider, concurrency tokens, CRUD flows | CRUD round-trips with optimistic concurrency tests |
| **M5 – Indexing & Tooling Enhancements** | Performance tuning and maintenance tooling | Predicate/order pushdown metrics, reindex/pack automation | Diagnostics show >70% index utilization, tooling scenarios green |
| **M6 – Tooling, Docs & Release Readiness** | Finalize documentation, tooling polish, Phase A GA pipeline | Docs set (CODEPAGES/INDEXES/TRANSACTIONS/cookbooks), CI + packaging, release checklist rehearsed | CI runs green, `dotnet pack` artifacts validated, GA checklist signed |
| **Phase B Kickoff** | FoxPro 2.x ingestion | DBF/FPT/CDX parsing, compatibility validation | FoxPro fixtures load with correct tag enumeration |

---

## Sequence & Dependencies

1. **M1 → M2** lay the foundation for journal-aware mutation, a prerequisite for safe Online DDL replay.
2. **M3** extends the mutation stack with schema-delta logging and provider/tooling integration so that ADO.NET clients can drive DDL without breaking online workloads.
3. **M4** builds on M3 by reusing schema version metadata inside EF Core migrations, while M5 tunes performance and operational tooling ahead of the Phase A release.
4. **M6** starts Phase B with FoxPro read support, leveraging the same IPOD pipeline where applicable.

---

## Status Tracking

- **M1**: ✅ completed.
- **M2**: ✅ completed.
- **M3**: ✅ completed (Online DDL/IPOD + provider enablement).
- **M4–M5**: ✅ completed.
- **M6**: ✅ completed (GA readiness sign-off).

---

**End of ROADMAP.md**
