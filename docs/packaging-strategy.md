# Packaging Strategy — Phase A GA

## Objectives
- Deliver NuGet packages for runtime components and CLI tool aligned with Semantic Versioning (`0.1.0` for GA).
- Produce self-contained CLI bundles for Windows, Linux, macOS.
- Ensure reproducible builds across CI and local environments.

## NuGet Artifacts
| Package | Contents | Notes |
|---------|----------|-------|
| `XBase.Core` | Core abstractions, storage engine primitives | `net8.0` + analyzers |
| `XBase.Data` | ADO.NET provider, metadata readers, journaling | Depends on `XBase.Core` |
| `XBase.EFCore` | EF Core provider extensions | Targets `net8.0` with analyzers |
| `XBase.Tools` | CLI commands, packaging metadata | Ships as both NuGet tool and framework-dependent binaries |

### Build Commands
```bash
dotnet pack xBase.sln -c Release -o artifacts/nuget /p:ContinuousIntegrationBuild=true
```
- Embeds deterministic source link + repository metadata.
- Generates symbol packages (`.snupkg`).

## CLI Bundles
```bash
dotnet publish src/XBase.Tools/XBase.Tools.csproj -c Release \
  -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o artifacts/win-x64
```
Repeat for `linux-x64` and `osx-arm64`. Validate `xbase --help` smoke test post-publish.

## Versioning & Channels
- GA uses branch `release/phase-a` with Git tag `v0.1.0`.
- Hotfixes increment patch version (`0.1.x`).
- Nightly builds produced from `main` as `0.2.0-alpha.<build>` and pushed to internal feed.

## Integrity & Distribution
- Sign NuGet packages using Azure SignTool or dotnet `SignTool` integration.
- Attach SBOM generated via `dotnet sbom` to release assets.
- Publish SHA256 checksums for CLI bundles.

## Automation Hooks
- CI pipeline publishes artifacts on tagged builds.
- GitHub Release workflow consumes packaged outputs and updates release notes automatically using `release-notes.md` template.

**End of packaging-strategy.md**
