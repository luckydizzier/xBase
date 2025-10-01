# Verification Report – 2025-10-01

## Vertical Validation
| Project | Build Command | Result | Test Command | Result |
|---------|---------------|--------|--------------|--------|
| XBase.Abstractions | `dotnet build src/XBase.Abstractions/XBase.Abstractions.csproj -c Release` | Success | `dotnet test tests/XBase.Abstractions.Tests/XBase.Abstractions.Tests.csproj --configuration Release` | Success |
| XBase.Core | `dotnet build src/XBase.Core/XBase.Core.csproj -c Release` | Success | `dotnet test tests/XBase.Core.Tests/XBase.Core.Tests.csproj --configuration Release` | Success |
| XBase.Data | `dotnet build src/XBase.Data/XBase.Data.csproj -c Release` | Success | `dotnet test tests/XBase.Data.Tests/XBase.Data.Tests.csproj --configuration Release` | Success |
| XBase.Diagnostics | `dotnet build src/XBase.Diagnostics/XBase.Diagnostics.csproj -c Release` | Success | `dotnet test tests/XBase.Diagnostics.Tests/XBase.Diagnostics.Tests.csproj --configuration Release` | Success |
| XBase.EFCore | `dotnet build src/XBase.EFCore/XBase.EFCore.csproj -c Release` | Success | `dotnet test tests/XBase.EFCore.Tests/XBase.EFCore.Tests.csproj --configuration Release` | Success |
| XBase.Expressions | `dotnet build src/XBase.Expressions/XBase.Expressions.csproj -c Release` | Success | `dotnet test tests/XBase.Expressions.Tests/XBase.Expressions.Tests.csproj --configuration Release` | Success |
| XBase.Tools | `dotnet build src/XBase.Tools/XBase.Tools.csproj -c Release` | Success | `dotnet test tests/XBase.Tools.Tests/XBase.Tools.Tests.csproj --configuration Release` | Success |

## Horizontal Validation
- `dotnet build xBase.sln -c Release`
- `dotnet test xBase.sln --configuration Release`

## Packaging
- `dotnet run --project src/XBase.Tools/XBase.Tools.csproj -- publish`
- Generated `XBase.*.1.0.0.nupkg` artifacts under `artifacts/packages/` (readme warnings acknowledged).

## Notes
- All commands executed on .NET SDK 8.0.120.
- No manual code changes were required; this report captures the verification evidence for auditability.
