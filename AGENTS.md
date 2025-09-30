# Repository Guidelines

## Project Structure & Module Organization
XBase uses a multi-project .NET solution (`xBase.sln`). Source modules live under `src/`, grouped by concern: `XBase.Abstractions` for contracts, `XBase.Core` for runtime engine, `XBase.Data` + `XBase.EFCore` for storage providers, and `XBase.Tools` for CLI utilities. Tests mirror this structure under `tests/` with `XBase.<Module>.Tests` projects and shared fixtures under `tests/fixtures`. Reference docs sit in `docs/`, with high-level design captured in `architecture.md`.

## Build, Test, and Development Commands
Run `dotnet restore` once per clone to hydrate dependencies. Use `dotnet build xBase.sln -c Release` for a clean compile and analyzer pass. Execute `dotnet test xBase.sln --configuration Release` before every push; add `--collect:"XPlat Code Coverage"` when validating coverage. For module smoke checks, `dotnet run --project src/XBase.Tools/XBase.Tools.csproj -- --help` exercises the toolchain.

## Coding Style & Naming Conventions
Style is governed by `.editorconfig`: UTF-8, LF endings, two-space indentation. Follow standard C# casing (`PascalCase` types/members, `camelCase` locals, `SCREAMING_CASE` constants). Keep namespaces aligned with folder paths, e.g., `namespace XBase.Data.Expressions`. Run `dotnet format` prior to commits to enforce layout and using directives.

## Testing Guidelines
Prefer xUnit `[Fact]` tests for unit scenarios and `[Theory]` with fixture data for matrix coverage. Name test methods using `MethodUnderTest_State_Result`, e.g., `ParseHeader_WithValidBytes_ReturnsMetadata`. Place synthetic data in `tests/fixtures` and share via helper classes. New features require companion tests in the corresponding `XBase.<Module>.Tests` project and should pass `dotnet test` locally.

## Commit & Pull Request Guidelines
Commits follow Conventional Commits (`feat:`, `fix:`, `chore:`) as seen in `chore: scaffold xBase solution skeleton`; scope modules when helpful (`feat(core): ...`). Keep messages in imperative voice. Pull requests need a short summary, linked issues via `Fixes #123`, and screenshots or trace snippets for tooling updates. Note any manual steps or migrations in the PR body to aid reviewers.
