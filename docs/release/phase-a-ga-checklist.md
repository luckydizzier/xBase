# Phase A GA Release Checklist

## Pre-Flight
- [x] All milestones M1–M6 marked complete in `tasks.md` and ROADMAP updated.
- [x] Verify documentation set: README, requirements, architecture, CODEPAGES, INDEXES, TRANSACTIONS, cookbooks, configuration, release notes.
- [ ] Ensure schema + fixture repositories synchronized and tagged.

## Build & Validation
- [x] `dotnet restore xBase.sln`
- [x] `dotnet build xBase.sln -c Release`
- [x] `dotnet test xBase.sln -c Release --collect:"XPlat Code Coverage"`
- [x] `dotnet format --verify-no-changes`
- [x] `dotnet pack xBase.sln -c Release -o artifacts/nuget`
- [x] `dotnet tool run xbase -- verify`

## Packaging & Signing
- [x] Inspect generated NuGet packages (`XBase.Core`, `XBase.Data`, `XBase.EFCore`, `XBase.Tools`).
- [ ] Sign packages using organization code-signing certificate.
- [ ] Publish symbols to internal symbol server.

## Release Artifact Prep
- [x] Generate release notes highlighting closed issues + breaking changes.
- [x] Attach CLI binaries zipped per platform (win-x64, linux-x64, osx-arm64).
- [ ] Snapshot benchmark results and include in release attachments.

## Publication
- [ ] Create Git tag `v0.1.0` and push to origin.
- [ ] Publish NuGet packages via `dotnet nuget push` against `nuget.org` (or staging feed).
- [ ] Publish GitHub Release with notes + artifacts.

## Post-Release
- [ ] Announce release on community channels.
- [ ] Archive CI run results and coverage reports.
- [ ] Open Phase B kickoff issue referencing ROADMAP.

**End of phase-a-ga-checklist.md**
