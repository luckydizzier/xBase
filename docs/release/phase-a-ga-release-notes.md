# Phase A GA Release Notes

## Release Summary
- **Version**: v0.1.0 (Phase A General Availability)
- **Runtime**: .NET 8 LTS
- **Scope**: dBASE III+/IV compatibility with DBF/DBT tables, NTX/MDX indexes, journaling, and provider/tooling surface.

## Highlights
1. **Storage Engine Stabilization**
   - Completed WAL journal implementation with crash recovery coverage.
   - Finalized deferred index maintenance supporting online reindex/pack cycles.
2. **Provider Stack Readiness**
   - ADO.NET provider delivering full CRUD with transactional guarantees.
   - EF Core provider exposing LINQ translation, change tracking, and concurrency tokens.
3. **Toolchain Expansion**
   - CLI now includes `dbfdump`, `dbfpack`, `dbfreindex`, `dbfconvert`, and DDL management verbs with validation and dry-run flows.
   - Packaging strategy rehearsed end-to-end via `dotnet pack` and `xbase verify` automation.

## Breaking Changes
- None. Existing Phase A RC consumers can upgrade without code changes.

## Compatibility Notes
- Journaling defaults to synchronous mode; set `XBaseConnectionOptions.JournalFlushMode = FlushMode.Async` to trade durability for throughput.
- CLI commands honor the `--encoding` flag to override DBF code page detection for legacy archives.

## Closed Issues & Milestones
- **M1 – Foundation Bootstrap**: Solution scaffolding, baseline docs, and tooling entry points.
- **M2 – Metadata & Discovery**: DBF metadata loader, catalog discovery, expression/diagnostics surface.
- **M3 – Online DDL & Provider Enablement**: Schema-delta log, provider DDL verbs, tooling integration.
- **M4 – Journaling & Transactions**: WAL journal format, lock coordination, deferred index hooks.
- **M5 – Provider Integrations**: ADO.NET execution pipeline, EF Core provider services, configuration options.
- **M6 – Tooling, Docs & Release**: CLI hardening, documentation set completion, GA release pipeline rehearsal.

## Upgrade Guidance
| Area | Recommendation |
|------|----------------|
| Tooling | Run `dotnet tool update xbase` to receive the GA CLI commands. |
| Providers | Update package references to `XBase.Core`, `XBase.Data`, `XBase.EFCore` version `0.1.0`. |
| Configuration | Review `docs/configuration.md` for updated journaling and connection settings. |

## Next Steps
- Publish v0.1.0 NuGet packages and symbols.
- Tag repository with `v0.1.0` and publish GitHub Release attaching platform binaries and benchmark snapshots.
- Open Phase B kickoff issue referencing `ROADMAP.md` for FoxPro 2.x (DBF/FPT/CDX) enablement.

**End of Phase A GA Release Notes**
