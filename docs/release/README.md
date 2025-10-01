# Release Documentation Index — v0.1.0

This directory contains all documentation related to the xBase v0.1.0 (Phase A GA) release.

## Release Documents

### [RELEASE-NOTES-v0.1.0.md](RELEASE-NOTES-v0.1.0.md)
**Comprehensive release notes** covering:
- Overview and key features
- Completed milestones (M1-M6)
- NuGet packages and CLI tools
- Installation instructions
- Breaking changes (none for initial release)
- Known issues
- Documentation links
- What's next (Phase B preview)

**Target Audience:** All users, developers, and stakeholders

---

### [QUICK-REFERENCE-v0.1.0.md](QUICK-REFERENCE-v0.1.0.md)
**Quick reference card** with:
- Installation commands
- Basic usage examples (ADO.NET, EF Core, CLI)
- Supported formats matrix
- Configuration options
- Connection string reference
- SHA256 checksums
- Common commands cheat sheet

**Target Audience:** Developers getting started with xBase

---

### [RELEASE-PROCESS-GUIDE.md](RELEASE-PROCESS-GUIDE.md)
**Step-by-step publication guide** including:
- Current status summary
- Package signing instructions
- Git tagging procedure
- NuGet.org publication steps
- GitHub Release creation (CLI and web)
- Package verification steps
- Post-release activities
- Troubleshooting common issues

**Target Audience:** Release managers and maintainers

---

### [phase-a-ga-checklist.md](phase-a-ga-checklist.md)
**Official GA release checklist** tracking:
- Pre-flight verification
- Build and validation steps
- Packaging and signing tasks
- Release artifact preparation
- Publication steps
- Post-release activities

**Target Audience:** Release team and project managers

---

### [2025-10-01-validation-report.md](2025-10-01-validation-report.md)
**Validation audit report** documenting:
- Vertical validation (per-project build/test)
- Horizontal validation (solution-wide)
- Packaging verification
- SDK version and environment details

**Target Audience:** QA team and compliance auditors

---

## Artifacts

Release artifacts are **not committed** to version control. They are generated during the release process and stored locally in the `artifacts/` directory (gitignored).

### Generated Artifacts

#### NuGet Packages (`artifacts/nuget/`)
- `XBase.Abstractions.1.0.0.nupkg` (12K)
- `XBase.Core.1.0.0.nupkg` (43K)
- `XBase.Data.1.0.0.nupkg` (25K)
- `XBase.Diagnostics.1.0.0.nupkg` (4.1K)
- `XBase.EFCore.1.0.0.nupkg` (8.1K)
- `XBase.Expressions.1.0.0.nupkg` (4.2K)
- `XBase.Tools.1.0.0.nupkg` (23K)

#### CLI Binaries (`artifacts/cli-binaries/`)
- `xbase-win-x64.zip` (29 MB) — Windows 64-bit
- `xbase-linux-x64.tar.gz` (29 MB) — Linux 64-bit
- `xbase-osx-arm64.tar.gz` (27 MB) — macOS ARM64
- `checksums.txt` — SHA256 checksums for binaries
- Platform-specific executables with debug symbols

#### Release Summary (`artifacts/`)
- `RELEASE-v0.1.0-SUMMARY.md` — Build validation and artifact summary

---

## Build Commands

### Reproduce Artifacts Locally

```bash
# 1. Clean and restore
dotnet clean xBase.sln
dotnet restore xBase.sln

# 2. Build and test
dotnet build xBase.sln -c Release
dotnet test xBase.sln -c Release --collect:"XPlat Code Coverage"
dotnet format --verify-no-changes

# 3. Generate NuGet packages
dotnet pack xBase.sln -c Release -o artifacts/nuget /p:ContinuousIntegrationBuild=true

# 4. Build CLI binaries
dotnet publish src/XBase.Tools/XBase.Tools.csproj -c Release \
  -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true \
  -o artifacts/cli-binaries/win-x64

dotnet publish src/XBase.Tools/XBase.Tools.csproj -c Release \
  -r linux-x64 -p:PublishSingleFile=true -p:SelfContained=true \
  -o artifacts/cli-binaries/linux-x64

dotnet publish src/XBase.Tools/XBase.Tools.csproj -c Release \
  -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true \
  -o artifacts/cli-binaries/osx-arm64

# 5. Create archives and checksums
cd artifacts/cli-binaries
zip -j xbase-win-x64.zip win-x64/XBase.Tools.exe
tar -czf xbase-linux-x64.tar.gz -C linux-x64 XBase.Tools
tar -czf xbase-osx-arm64.tar.gz -C osx-arm64 XBase.Tools
sha256sum xbase-*.zip xbase-*.tar.gz > checksums.txt
```

---

## Quality Metrics

### Build Status
- **Build**: ✅ Success (Release configuration)
- **Tests**: ✅ 33/33 passed (100%)
- **Code Formatting**: ✅ Verified with dotnet format
- **Test Duration**: 24.2 seconds

### Coverage
Code coverage reports available in:
```
tests/XBase.*.Tests/TestResults/*/coverage.cobertura.xml
```

### Test Projects
- XBase.Abstractions.Tests
- XBase.Core.Tests
- XBase.Data.Tests
- XBase.Diagnostics.Tests
- XBase.EFCore.Tests
- XBase.Expressions.Tests
- XBase.Tools.Tests

---

## Publication Status

### ✅ Completed
- [x] Code formatted and validated
- [x] All tests passing
- [x] NuGet packages generated
- [x] CLI binaries built for all platforms
- [x] Checksums generated
- [x] Release documentation complete
- [x] Checklist updated
- [x] ROADMAP and tasks.md updated (M6 complete)

### ⏳ Pending
- [ ] Sign NuGet packages (requires organization certificate)
- [ ] Create Git tag `v0.1.0`
- [ ] Publish packages to NuGet.org
- [ ] Create GitHub Release
- [ ] Announce release

---

## Key Links

- **Repository**: https://github.com/luckydizzier/xBase
- **Release Page**: https://github.com/luckydizzier/xBase/releases/tag/v0.1.0
- **NuGet Packages**: https://www.nuget.org/packages?q=xbase
- **Documentation**: https://github.com/luckydizzier/xBase/tree/main/docs

---

## Version Information

- **Version**: 0.1.0 (Phase A GA)
- **Release Date**: 2025-10-01
- **Target Framework**: .NET 8.0 LTS
- **Phase**: A (dBASE III+/IV support)
- **Next Phase**: B (FoxPro 2.x support)

---

**Last Updated**: 2025-10-01  
**Maintained By**: xBase Release Team
