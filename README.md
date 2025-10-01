# xBase for .NET

This repository hosts the xBase .NET framework. Phase A targets safe read/write support for dBASE III+/IV DBF, DBT, NTX, and MDX assets with modern ADO.NET and EF Core integration.

## Solution Layout

```text
xBase.sln
├─ src/
│  ├─ XBase.Abstractions           # Contracts shared across the stack
│  ├─ XBase.Core                   # Core engine and journaling primitives
│  ├─ XBase.Data                   # ADO.NET provider façade
│  ├─ XBase.EFCore                 # EF Core provider glue
│  ├─ XBase.Expressions            # Expression evaluator infrastructure
│  ├─ XBase.Diagnostics            # Logging and diagnostics helpers
│  └─ XBase.Tools                  # CLI utilities (verify/clean/restore/build/test/publish)
├─ tests/
│  ├─ XBase.*.Tests                # xUnit suites mirrored by module
│  └─ fixtures/                    # Reserved for binary test data
└─ docs/                           # architecture.md, requirements.md, etc.
```

## Build & Validation Pipeline

Run the orchestration CLI to execute the standard pipeline:

```bash
# Verify assets, clean, restore, build, test, and pack release artifacts
dotnet run --project src/XBase.Tools -- verify clean restore build test publish
```

Individual steps can be executed with the corresponding command (e.g., `verify`, `clean`, `restore`, `build`, `test`, `publish`).

## Additional Documentation

- [requirements.md](requirements.md)
- [architecture.md](architecture.md)
