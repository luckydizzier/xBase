# xBase Demo Solution Tasks

Source blueprint: [docs/demo/avalonia-reactiveui-demo-plan.md](../../docs/demo/avalonia-reactiveui-demo-plan.md)

## Milestone M1 – App Shell & Catalog Browser
- [x] Scaffold Avalonia desktop host with ReactiveUI composition root.
- [x] Implement catalog scanning service and basic directory selection flow.
- [x] Build table/index listing view models with paging placeholders.
- [x] Wire diagnostics/logging sinks and sample telemetry events.
- [x] Add integration smoke harness once basic navigation is ready.

## Milestone M2 – DDL & Index Basics
- [x] Generate DDL preview scripts for create/alter/drop operations.
- [x] Implement index create/drop services with error surfacing.
- [x] Extend ViewModels with command handlers for schema updates.

## Milestone M3 – Rebuild & Diagnostics
- [ ] Add side-by-side index rebuild orchestration with progress observables.
- [ ] Surface performance metrics and index selection feedback in UI.
- [ ] Expand diagnostics feed for journal/index events.

## Milestone M4 – Seed & Recovery Demo
- [ ] Implement CSV import pipeline with encoding detection.
- [ ] Simulate crash scenarios and recovery replay workflow.
- [ ] Publish diagnostic/export reports for support packages.
