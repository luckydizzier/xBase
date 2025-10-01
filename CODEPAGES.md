# xBase for .NET — Code Page Strategy (CODEPAGES.md)

## Goals
- Guarantee deterministic encoding/decoding for dBASE III+/IV DBF/DBT assets across Windows, Linux, and macOS.
- Provide a self-describing registry that maps legacy DOS/OEM code pages to .NET `System.Text.Encoding` instances.
- Allow per-table overrides sourced from DBF headers, sidecar `.cpg` files, configuration, or runtime options.

## Registry Architecture
1. **Builtin Catalog** — JSON manifest embedded in `XBase.Core` exposes canonical IDs (e.g., `cp437`, `cp850`, `windows-1252`) with metadata:
   - Display name and description.
   - Preferred .NET encoding name.
   - OEM/ANSI flag to distinguish header semantics.
   - Normalized culture hints.
2. **Resolution Flow** — When opening a table:
   1. Inspect DBF header language driver byte.
   2. Consult adjacent `.cpg` file when present.
   3. Apply provider-level override from connection string (`Encoding=...`) or EF Core options.
   4. Fall back to repository default (`cp437`).
3. **Extensibility** — Providers call `EncodingRegistry.TryRegister(customEncoding)` to register new mappings. Collisions require an explicit `override: true` flag to avoid accidental shadowing.

## Validation Matrix
| Scenario | Expected Behavior | Test Coverage |
|----------|-------------------|---------------|
| Header byte recognized | Registry returns deterministic `EncodingInfo` | `EncodingRegistryTests.HeaderByte_Recognized_ReturnsEntry` |
| `.cpg` overrides header | `.cpg` wins with warning log | `EncodingRegistryTests.CpgOverridesHeader_EmitsWarning` |
| Unknown encoding | Throws `UnsupportedEncodingException` with guidance | `EncodingRegistryTests.UnknownEncoding_Throws` |
| Provider override | ADO.NET + EF Core accept `Encoding=` keyword | Integration tests in `XBase.Data.Tests` + `XBase.EFCore.Tests` |

## Operational Guidance
- **CLI** — `xbase dbfinfo` reports detected encoding, mismatches, and override suggestions.
- **Configuration** — `appsettings.json` schema supports `"xBase": { "defaultEncoding": "cp437" }` for hosting scenarios.
- **Monitoring** — Emit structured log `EncodingMismatch` when runtime overrides diverge from headers for audit trails.

## Phase B Outlook
- Expand registry with FoxPro-specific code pages (e.g., `windows-1251`, `koi8-r`).
- Allow per-column encoding overrides for memo vs. table character fields when FPT uses differing code pages.

**End of CODEPAGES.md**
