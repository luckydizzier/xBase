# Release Process Guide — v0.1.0

This document provides instructions for completing the release process for xBase v0.1.0.

## Current Status

✅ **Completed Steps:**
1. Code formatting verified (`dotnet format`)
2. Solution built successfully in Release configuration
3. All tests passing (33/33)
4. NuGet packages generated in `artifacts/nuget/`:
   - XBase.Abstractions.1.0.0.nupkg
   - XBase.Core.1.0.0.nupkg
   - XBase.Data.1.0.0.nupkg
   - XBase.Diagnostics.1.0.0.nupkg
   - XBase.EFCore.1.0.0.nupkg
   - XBase.Expressions.1.0.0.nupkg
   - XBase.Tools.1.0.0.nupkg
5. Platform-specific CLI binaries created:
   - `artifacts/cli-binaries/xbase-win-x64.zip`
   - `artifacts/cli-binaries/xbase-linux-x64.tar.gz`
   - `artifacts/cli-binaries/xbase-osx-arm64.tar.gz`
6. SHA256 checksums generated (`artifacts/cli-binaries/checksums.txt`)
7. Release notes created (`docs/release/RELEASE-NOTES-v0.1.0.md`)
8. Checklist updated with completed items
9. ROADMAP and tasks.md updated (M6 marked complete)

## Next Steps

### 1. Sign NuGet Packages (Optional but Recommended)

If you have an organization code-signing certificate:

```bash
# Install sign tool if not already installed
dotnet tool install --global NuGetKeyVaultSignTool

# Sign each package
for package in artifacts/nuget/*.nupkg; do
  NuGetKeyVaultSignTool sign "$package" \
    --file-digest sha256 \
    --timestamp-rfc3161 http://timestamp.digicert.com \
    --azure-key-vault-url <your-vault-url> \
    --azure-key-vault-client-id <client-id> \
    --azure-key-vault-client-secret <secret>
done
```

**Note:** If you don't have a signing certificate, you can skip this step. NuGet.org accepts unsigned packages but signed packages provide additional trust.

### 2. Create Git Tag

Create and push the release tag:

```bash
cd /home/runner/work/xBase/xBase
git tag -a v0.1.0 -m "Release v0.1.0 - Phase A GA"
git push origin v0.1.0
```

### 3. Publish NuGet Packages

You'll need a NuGet.org API key. Get one from https://www.nuget.org/account/apikeys

```bash
# Set your API key (do this once)
export NUGET_API_KEY="your-api-key-here"

# Publish packages
cd artifacts/nuget
for package in *.nupkg; do
  dotnet nuget push "$package" \
    --source https://api.nuget.org/v3/index.json \
    --api-key $NUGET_API_KEY
done
```

**Alternative for testing:** Use a staging feed first:

```bash
# Push to test feed instead
dotnet nuget push "XBase.*.nupkg" \
  --source https://apiint.nugettest.org/v3/index.json \
  --api-key $NUGET_TEST_API_KEY
```

### 4. Create GitHub Release

#### Option A: Using GitHub CLI (gh)

```bash
gh release create v0.1.0 \
  --title "xBase v0.1.0 — Phase A GA" \
  --notes-file docs/release/RELEASE-NOTES-v0.1.0.md \
  artifacts/cli-binaries/xbase-win-x64.zip \
  artifacts/cli-binaries/xbase-linux-x64.tar.gz \
  artifacts/cli-binaries/xbase-osx-arm64.tar.gz \
  artifacts/cli-binaries/checksums.txt \
  artifacts/RELEASE-v0.1.0-SUMMARY.md
```

#### Option B: Using GitHub Web Interface

1. Go to https://github.com/luckydizzier/xBase/releases/new
2. Choose tag: `v0.1.0` (or create it)
3. Release title: `xBase v0.1.0 — Phase A GA`
4. Description: Copy content from `docs/release/RELEASE-NOTES-v0.1.0.md`
5. Attach files:
   - `artifacts/cli-binaries/xbase-win-x64.zip`
   - `artifacts/cli-binaries/xbase-linux-x64.tar.gz`
   - `artifacts/cli-binaries/xbase-osx-arm64.tar.gz`
   - `artifacts/cli-binaries/checksums.txt`
   - `artifacts/RELEASE-v0.1.0-SUMMARY.md`
6. Click "Publish release"

### 5. Verify Published Packages

After publishing, verify the packages are available:

```bash
# Check if packages are searchable on NuGet.org
dotnet add package XBase.Core --version 1.0.0 --dry-run
dotnet add package XBase.Data --version 1.0.0 --dry-run
dotnet add package XBase.EFCore --version 1.0.0 --dry-run
dotnet tool install --global XBase.Tools --version 1.0.0 --dry-run
```

### 6. Post-Release Activities

#### Update Phase A GA Checklist

Mark remaining items as complete in `docs/release/phase-a-ga-checklist.md`:

- [x] Create Git tag `v0.1.0` and push to origin
- [x] Publish NuGet packages via `dotnet nuget push` against `nuget.org`
- [x] Publish GitHub Release with notes + artifacts

#### Announce the Release

Consider announcing on:
- GitHub Discussions
- Twitter/LinkedIn
- .NET community forums
- Reddit r/dotnet
- Any organization-specific channels

Example announcement:

> 🎉 We're excited to announce xBase v0.1.0 — Phase A GA!
>
> xBase brings modern dBASE III+/IV file support to .NET 8.0 with:
> - Full ADO.NET and EF Core providers
> - Transaction support with WAL journaling
> - Online DDL operations
> - Cross-platform CLI tooling
>
> Download: https://github.com/luckydizzier/xBase/releases/tag/v0.1.0
> NuGet: dotnet add package XBase.Core --version 1.0.0

#### Archive CI Artifacts

If using a CI system, archive the run results:
- Test results and coverage reports
- Build logs
- Performance benchmarks (if available)

#### Open Phase B Kickoff Issue

Create a new GitHub issue for Phase B planning:

**Title:** Phase B Kickoff — FoxPro 2.x Support

**Description:**
```markdown
Phase A has been successfully released! 🎉

This issue tracks the kickoff of Phase B, focusing on FoxPro 2.x format support.

## Goals
- DBF/FPT/CDX parsing
- Compatibility validation with existing Phase A code
- Extended fixture coverage

See [ROADMAP.md](../ROADMAP.md) for detailed milestone breakdown.

## Timeline
Target: Q2 2025

## Dependencies
- Phase A packages published to NuGet.org ✅
- Community feedback from initial GA release
```

## Troubleshooting

### NuGet Push Fails with 409 Conflict

**Cause:** Version already exists on NuGet.org (versions are immutable)

**Solution:** 
- Don't republish the same version
- If needed, increment to 1.0.1 and follow the process again

### GitHub Release Creation Fails

**Cause:** Tag doesn't exist or insufficient permissions

**Solution:**
- Ensure the tag exists: `git tag -l v0.1.0`
- Check repository permissions
- Try using GitHub web interface instead of CLI

### CLI Binaries Don't Execute

**Cause:** Missing execution permissions (Linux/macOS)

**Solution:**
```bash
chmod +x XBase.Tools
./XBase.Tools --help
```

## References

- [NuGet Package Publishing](https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package)
- [GitHub Releases Guide](https://docs.github.com/en/repositories/releasing-projects-on-github)
- [Semantic Versioning](https://semver.org/)
- [.NET Package Signing](https://docs.microsoft.com/en-us/nuget/create-packages/sign-a-package)

---

**Last Updated:** 2025-10-01  
**Author:** xBase Release Team
