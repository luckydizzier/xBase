# Release Notes — xBase v0.1.0 (Phase A GA)

**Release Date**: 2025-10-01  
**Target Framework**: .NET 8.0 LTS

## Overview

This is the General Availability (GA) release of xBase for .NET Phase A. This release provides comprehensive support for dBASE III+/IV file formats with modern .NET integration through ADO.NET and Entity Framework Core providers.

## Key Features

### Core Engine
- **DBF/DBT Table Support**: Full read/write support for dBASE III+ and dBASE IV table formats with memo field support
- **Index Support**: NTX and MDX index navigation and maintenance
- **Journaling & Transactions**: Write-Ahead Logging (WAL) with crash recovery and transaction support
- **Online DDL**: In-Place Online DDL (IPOD) operations with lazy backfill and version tracking

### Provider Integration
- **ADO.NET Provider**: Complete `XBaseConnection`, `XBaseCommand`, and `XBaseDataReader` implementation
- **EF Core Provider**: Full LINQ support with query translation, change tracking, and migrations
- **Connection String Support**: Flexible configuration with journaling options

### CLI Tooling (`XBase.Tools`)
Command-line utilities for database operations:
- `verify`: Validate solution assets
- `build`/`test`/`publish`: Development pipeline commands
- `dbfinfo`: Display table metadata and schema information
- `dbfdump`: Export table data to CSV or JSON Lines
- `dbfpack`: Compact deleted records and rewrite tables
- `dbfreindex`: Rebuild index files
- `dbfconvert`: Create transcoded copies with different code pages
- `ddl`: Manage online DDL operations (apply/checkpoint/pack/reindex)

## Milestones Completed

### M1 – Foundation Bootstrap ✅
- Scaffolded multi-project solution structure
- Baseline documentation (README, requirements, architecture)
- Initial CLI orchestrator and tooling

### M2 – Metadata & Discovery ✅
- DBF metadata loader with encoding support
- Table catalog directory discovery
- Expression evaluator and diagnostics framework

### M3 – Online DDL & Provider Enablement ✅
- Schema-delta log with version tracking
- Lazy backfill queues and recovery
- ADO.NET provider DDL support
- CLI DDL commands with validation

### M4 – Journaling & Transactions ✅
- WAL journal format implementation
- Transaction coordination and crash recovery
- File and record-level locking
- Deferred index maintenance

### M5 – Provider Integrations ✅
- ADO.NET command execution pipeline
- EF Core provider services (type mappings, query translation)
- Configuration and connection string support

### M6 – Tooling, Docs & Release ✅
- Complete CLI tool set
- Documentation set (ROADMAP, CODEPAGES, INDEXES, TRANSACTIONS, cookbooks)
- CI pipeline and packaging strategy
- Release checklist and validation

## NuGet Packages

This release includes the following NuGet packages:

| Package | Version | Description |
|---------|---------|-------------|
| `XBase.Core` | 1.0.0 | Core abstractions and storage engine primitives |
| `XBase.Data` | 1.0.0 | ADO.NET provider with metadata readers and journaling |
| `XBase.EFCore` | 1.0.0 | Entity Framework Core provider extensions |
| `XBase.Tools` | 1.0.0 | CLI utilities for database operations |

## Breaking Changes

This is the initial GA release, so there are no breaking changes from previous versions.

## Known Issues

- None identified at GA release

## Installation

### NuGet Packages
```bash
dotnet add package XBase.Core --version 1.0.0
dotnet add package XBase.Data --version 1.0.0
dotnet add package XBase.EFCore --version 1.0.0
```

### CLI Tool
```bash
dotnet tool install --global XBase.Tools --version 1.0.0
```

### Platform-Specific Binaries
Download standalone executables for your platform from the release assets:
- `xbase-win-x64.zip` — Windows 64-bit
- `xbase-linux-x64.tar.gz` — Linux 64-bit
- `xbase-osx-arm64.tar.gz` — macOS ARM64 (Apple Silicon)

## Documentation

- [README](../../README.md) — Getting started guide
- [requirements.md](../../requirements.md) — Detailed requirements and specifications
- [architecture.md](../../architecture.md) — System architecture overview
- [CODEPAGES.md](../../CODEPAGES.md) — Code page support and encoding
- [INDEXES.md](../../INDEXES.md) — Index format specifications
- [TRANSACTIONS.md](../../TRANSACTIONS.md) — Transaction and journaling details
- [ROADMAP.md](../../ROADMAP.md) — Future development plans
- [Configuration Guide](../configuration.md) — Connection strings and options
- [Cookbooks](../cookbooks/) — Usage examples and recipes

## Requirements

- .NET 8.0 SDK or later
- Windows, Linux, or macOS

## Building from Source

```bash
git clone https://github.com/luckydizzier/xBase.git
cd xBase
dotnet restore xBase.sln
dotnet build xBase.sln -c Release
dotnet test xBase.sln -c Release
```

## What's Next

Phase B will focus on:
- FoxPro 2.x format support (DBF/FPT/CDX)
- Enhanced compatibility validation
- Performance optimizations
- Extended index utilization metrics

See [ROADMAP.md](../../ROADMAP.md) for the complete development plan.

## Contributors

Special thanks to all contributors who made this release possible!

## Support

- GitHub Issues: https://github.com/luckydizzier/xBase/issues
- Documentation: https://github.com/luckydizzier/xBase/tree/main/docs

---

**Full Changelog**: https://github.com/luckydizzier/xBase/commits/v0.1.0
