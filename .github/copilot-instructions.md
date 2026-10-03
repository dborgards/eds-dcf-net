# Copilot Instructions for EdsDcfNet

## Project Overview

EdsDcfNet is a C# library for reading and writing CANopen device description and configuration files in five formats: CiA 306 EDS (Electronic Data Sheet), DCF (Device Configuration File) and CPJ (nodelist project), plus the CiA 311 XML formats XDD (XML Device Description) and XDC (XML Device Configuration). Zero external dependencies. Dual-targets **netstandard2.0** and **net10.0**.

## Critical Constraints

### .NET Standard 2.0 Compatibility

This library must compile against netstandard2.0. The **Polyfill** package is included as a source generator with no runtime dependency for consumers:
- `PrivateAssets="all"` — the package reference does not flow transitively to consumers.
- `IncludeAssets="compile; build; analyzers; contentfiles"` — `native` and `buildtransitive` are excluded. `contentfiles` is required because Polyfill ships some polyfills (e.g. `System.Index`/`System.Range`) as source content files rather than purely through the Roslyn generator.

Thanks to Polyfill, modern APIs can be used directly:

- `string.Contains(char)` ✓
- `string.Contains(string, StringComparison)` ✓
- `string.StartsWith(char)` / `string.EndsWith(char)` ✓
- Range indexers (`[n..]`, `[..n]`, `[^n]`) ✓
- `[NotNullWhen]`, `[MemberNotNull]` attributes ✓

The following API is still **not available** and must be avoided:

- `string.Replace(string, string, StringComparison)` — use `IndexOf` + range indexer for case-insensitive replacement

### Invariant Culture

All numeric and date formatting/parsing **must** use `CultureInfo.InvariantCulture`. EDS/DCF files are culture-independent INI-style files. This applies to:

- `int.TryParse`, `uint.Parse`, `ushort.TryParse`, `byte.Parse` — always pass `CultureInfo.InvariantCulture`
- `.ToString()` on numeric types — always pass `CultureInfo.InvariantCulture`
- String interpolation with numbers in section headers (e.g., `[M{n}ModuleInfo]`) — use `string.Format(CultureInfo.InvariantCulture, ...)` instead of `$""` interpolation

## Architecture

```
file/stream → Reader (Parsers/) → Models → [Validation/] → Writer (Writers/) → file/stream
```

Source layout under `src/EdsDcfNet`:

- `CanOpenFile.cs`, `*CanOpenOperations.cs`, `CanOpenFileOptions.cs`, `CanOpenWriteOptions.cs` — public facade and format entry points
- `Parsers/` — readers (`EdsReader`, `DcfReader`, `CpjReader`, `XddReader`, `XdcReader`), `IniParser`, XDD sub-parsers, `SecureXmlParser`
- `Writers/` — writers (`EdsWriter`, `DcfWriter`, `CpjWriter`, `XddWriter`, `XdcWriter`) and shared builders/helpers
- `Validation/` — `CanOpenModelValidator`, `CanOpenValidationOptions`, `ValidationIssue`
- `Models/` — format models and their parts
- `Utilities/` — `ValueConverter`, `CanOpenValueConverter`, `TextFileIo`, cloners and section helpers
- `Diagnostics/` — `CanOpenReadResult`, `ParseDiagnostic` and related types for lenient-mode reads
- `Exceptions/`, `Extensions/` — exception types and `ObjectDictionary` extensions

- **`CanOpenFile`** — static facade. The **canonical format entry points** are `CanOpenFile.Eds`, `.Dcf`, `.Cpj`, `.Xdd`, `.Xdc` (read/write/options per format). Prefer them for new code. The legacy facade overloads (`ReadEds`, `WriteDcf`, `EdsToDcf` without timestamp, etc.) are kept for backward compatibility and all 111 of them are marked `[Obsolete]` (advisory) in favour of the entry points. `CanOpenFile.Validate*` / `EnsureValid*` and the `EdsToDcf` overload with an explicit timestamp are not obsolete.
- **`EdsCanOpenOperations`**, **`DcfCanOpenOperations`**, **`CpjCanOpenOperations`**, **`XddCanOpenOperations`**, **`XdcCanOpenOperations`** — format-specific operations behind the entry points, built on the shared `FormatCanOpenOperations<TModel>`
- **`IniParser`** — low-level INI section/key-value parsing (case-insensitive), used by the EDS/DCF/CPJ readers
- **`EdsReader`** / **`DcfReader`** / **`CpjReader`** — INI readers producing `ElectronicDataSheet` / `DeviceConfigurationFile` / `NodelistProject`
- **`XddReader`** / **`XdcReader`** — XML (`System.Xml.Linq`) readers, hardened via `SecureXmlParser`
- **`EdsWriter`** / **`DcfWriter`** / **`CpjWriter`** / **`XddWriter`** / **`XdcWriter`** — serialize the models back to their format
- **`ValueConverter`** — parses integers (decimal/hex `0x`/octal `0`+digit), booleans, `$NODEID` formulas, AccessType enum

### Key Models

- `ElectronicDataSheet` — EDS template (no configured node)
- `DeviceConfigurationFile` — DCF instance (includes `DeviceCommissioning` with nodeId/baudrate)
- `ObjectDictionary` — `CanOpenObject` entries indexed by `ushort`, each with optional `CanOpenSubObject` entries
- Unknown sections are preserved as `Dictionary<string, Dictionary<string, string>>` for round-trip fidelity

## Testing Conventions

- **Framework:** XUnit + AwesomeAssertions
- **Naming:** `MethodName_Scenario_ExpectedBehavior`
- **Pattern:** Arrange-Act-Assert (AAA)
- **Fixture data:** `tests/EdsDcfNet.Tests/Fixtures/sample_device.eds`

### Boundary & regression matrix

Changes to **parsers, writers, validators, or converters** must fill in the
**[Boundary & regression test guide](../CONTRIBUTING.md#boundary--regression-test-guide)**
matrix from `CONTRIBUTING.md` in the PR description (numeric boundaries,
round-trip fidelity per format, validation modes, representative fixtures,
API contract assertions). Boundary tests use the `*_AtMaxValue` /
`*_ValidatedRoundTrip` naming convention. This is upfront work — do not add
edge-case tests only reactively (see incidents #305/#313 and #311/#320).

## Commit Convention

This project uses **Conventional Commits** and **semantic-release** for automated versioning and NuGet publishing. All commit messages **must** follow this format:

```
<type>(<optional scope>): <description>
```

### Types and their effect on versioning

| Type | Release | Description |
|---|---|---|
| `feat` | **minor** | A new feature |
| `fix` | **patch** | A bug fix |
| `perf` | **patch** | A performance improvement |
| `revert` | **patch** | Reverts a previous commit |
| `docs` | none | Documentation only |
| `style` | none | Formatting, missing semicolons, etc. |
| `refactor` | none | Code change that neither fixes a bug nor adds a feature |
| `test` | none | Adding or correcting tests |
| `build` | none | Changes to the build system or dependencies |
| `ci` | none | Changes to CI configuration |
| `chore` | none | Other changes that don't modify src or test files |

### Breaking changes

For a **major** release, add `BREAKING CHANGE:` in the commit body or footer:

```
feat: redesign public API

BREAKING CHANGE: CanOpenFile.Eds.ReadFile now returns a Result type
```

### Examples

```
feat: add support for CompactPDO mapping
fix: correct hex parsing for negative values
docs: update README with new API examples
build: bump AwesomeAssertions to 9.x
ci: add codecov upload to build workflow
refactor(parser): simplify IniParser section lookup
test: add round-trip tests for modular devices
```

## Branching Strategy

This project uses a **develop → main** integration model:

| Branch | Purpose | Release |
|---|---|---|
| `main` | Stable, production-ready code | Stable NuGet release (e.g., `1.5.0`) |
| `develop` | Integration branch for ongoing work | Pre-release NuGet (e.g., `1.5.0-beta.1`) |
| `feat/*`, `fix/*`, etc. | Short-lived feature/fix branches | None |

### Workflow

1. Branch off `develop`:
   ```
   git checkout -b feat/my-feature develop
   ```
2. Open a PR from the feature branch **into `develop`** (never directly into `main`).
   Merge it with **squash** (or rebase), never a merge commit; the develop ruleset
   enforces this (see `CONTRIBUTING.md` § "Merge strategy"). The squash commit
   message must be a valid Conventional Commit, because semantic-release analyzes it.
3. On merge to `develop`, semantic-release publishes a `beta` pre-release to NuGet automatically.
4. When ready for a stable release, open a PR from `develop` → `main`.
   **Important:** merge this PR with a **regular merge commit** (not squash, not rebase).
   Squash-merging collapses all individual `fix:`/`feat:` commits into one commit whose
   type (`release:`) is not recognised by semantic-release, so the stable release is
   silently skipped. See `.releaserc.json` and
   `tools/semantic-release-analyze-commits.sh` for the orphaned-prerelease
   fallback that partially mitigates this, but a regular merge remains the correct approach.
5. On merge to `main`, semantic-release publishes the stable release to NuGet.

### CI Behaviour

- **Push to a feature branch** → no run on its own; the PR (`pull_request`)
  runs `build.yml`, so a PR branch is not built twice. A branch without a PR
  is not built (open a draft PR or use `workflow_dispatch`). A newer push to
  a PR cancels that PR's run in progress.
- **PR targeting `develop` or `main`** → `build.yml` runs (build + test as gate).
- **Push to `develop`** → `build.yml` runs as well (alongside `semantic-release.yml`).
- **Documentation-only PR into `develop`** (root `*.md`, `docs/**`,
  `.github/*.md`, `LICENSE*`) → the `changes` job detects it; build, tests,
  coverage and ApiCompat steps are skipped via `if:`, while `build` and
  `coverage/threshold` still report success. No workflow-level `paths-ignore`
  (it would leave required checks unreported). PRs into `main` always run in full.
- **Merge to `develop`** → `semantic-release.yml` runs (build + test + beta pre-release).
- **Merge to `main`** → `semantic-release.yml` runs (build + test + stable release).
- **Test TFMs:** `EdsDcfNet.Tests` / `EdsDcfNet.TestHost` multi-target `net10.0`
  and `net48`. The `net48` host runs against the library’s `netstandard2.0`
  asset so `#else` runtime paths are executed in CI (not only compile-checked).
  Build/test CI uses `windows-latest` because `net48` requires .NET Framework.

Direct commits to `main` or `develop` are not allowed; all changes go through PRs.

## Public API compatibility

When a change touches public surface area on `EdsDcfNet` (for example
`CanOpenFile`, format entry points, or public models), follow the
**[Public API compatibility checklist](../CONTRIBUTING.md#public-api-compatibility-checklist)**
in `CONTRIBUTING.md` before opening a PR. It covers:

- **Binary ABI** — no silent removal of public overloads (see #302)
- **Named arguments** — parameter names are a source contract; shared generic
  bases must not rename parameters visible on format entry points (see #314,
  #321)
- **Overload shape** — preserve or obsolete every sibling overload in the same
  PR (see #302, #314)
- **XML doc / warnings-as-errors** — no new CS15xx/CS16xx under
  `TreatWarningsAsErrors`

PRs that modify public API should confirm the checklist in the PR template.

## Code Style

- XML doc comments on all public members
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- File-scoped namespaces (`namespace Foo;`)
- No external dependencies in the main library
