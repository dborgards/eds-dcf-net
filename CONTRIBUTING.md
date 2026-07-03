# Contributing to EdsDcfNet

Thank you for your interest in contributing!

## Branching model

This project uses a **develop → main** integration flow.

```
feat/my-feature ──┐
fix/some-bug    ──┤──► develop ──► main
refactor/xyz    ──┘      │           │
                    beta pre-release  stable release
```

| Branch | Role |
|---|---|
| `main` | Stable, production-ready code. Every merge here triggers a stable NuGet release. |
| `develop` | Integration branch. Every merge here triggers a beta pre-release (e.g., `1.5.0-beta.1`) on NuGet. |
| `feat/*`, `fix/*`, `refactor/*`, etc. | Short-lived work branches, always branched from `develop`. |

## How to contribute

1. **Fork / clone the repository.**

2. **Create a branch from `develop`:**
   ```sh
   git checkout develop
   git pull origin develop
   git checkout -b fix/my-fix
   ```

3. **Make your changes.** Follow the commit convention below.

4. **Run tests locally:**
   ```sh
   dotnet test --configuration Release
   ```

5. **Open a PR targeting `develop`** (not `main`).

6. If your change touches public surface area on `EdsDcfNet`, complete the
   [Public API compatibility checklist](#public-api-compatibility-checklist)
   below and confirm it in the PR template.

7. Wait for CI (build + tests) to pass and for review.

## Coding conventions

Repository-wide formatting and baseline analyzer severities are defined in
[`.editorconfig`](.editorconfig).

Analyzer/build enforcement flags (`EnableNETAnalyzers`, `TreatWarningsAsErrors`,
`AnalysisMode`) are centralized in [`Directory.Build.props`](Directory.Build.props),
while the core library project additionally pins the analyzer package version via
`Microsoft.CodeAnalysis.NetAnalyzers` in
[`src/EdsDcfNet/EdsDcfNet.csproj`](src/EdsDcfNet/EdsDcfNet.csproj).

### Analyzer policy

- .NET analyzers are enabled for `EdsDcfNet` and `EdsDcfNet.Tests`.
- `AnalysisMode` is `Recommended` for `EdsDcfNet` (core library).
- `EdsDcfNet.Tests` currently stays on SDK-default analyzer mode to avoid
  forcing a full test-naming cleanup in one step (for example CA1707).
- `TreatWarningsAsErrors=true` is enforced for these projects.
- Style and documentation analyzer categories are intentionally configured as
  suggestions in `.editorconfig` to avoid noisy CI failures.

### Analyzer rollout strategy

- New analyzer rules should start at `suggestion` (or project-default severity)
  and be promoted only after cleanup.
- Promote to `warning` only when findings are actionable and low-noise.
- Use dedicated cleanup PRs before enabling stricter severities that would
  break CI under warnings-as-errors.

## .NET SDK policy

- SDK resolution is pinned in [`global.json`](global.json).
- CI resolves the .NET SDK from that file in both workflows.
- To update the SDK baseline, submit one PR that updates `global.json` and
  briefly notes the change in the PR description.

## Boundary & regression test guide

Edge-case regressions in conversion/validation code have historically been
caught only *after* behavior changed:

- [#305](https://github.com/dborgards/eds-dcf-net/issues/305) /
  [PR #313](https://github.com/dborgards/eds-dcf-net/pull/313) — the
  `CanOpenWriteOptions.Validated` round-trip for XDD/XDC with an
  `ApplicationProcess` was not covered before validation tightened; integration
  tests were added reactively.
- [#311](https://github.com/dborgards/eds-dcf-net/issues/311) /
  [PR #320](https://github.com/dborgards/eds-dcf-net/pull/320) — the
  `ConvertToDcf` FileRevision increment wrapped `255 → 0`; the boundary
  regression test arrived with the fix instead of before it.

For any PR touching **parsers, writers, validators, or converters**, fill in
the matrix below in the PR description and add tests for every cell that
applies. Skipped dimensions should be marked `n/a` with a short reason.

### Matrix template

| Dimension | What to cover | This PR |
|---|---|---|
| Numeric boundaries | min, max, and wrap/clamp semantics of every touched numeric field (e.g. `FileRevision == 255`, Node-ID `1..127`) | |
| Round-trip fidelity | read → write → read for each affected format (EDS, DCF, CPJ, XDD, XDC) | |
| Validation modes | default write **and** `CanOpenWriteOptions.Validated` where validation differs | |
| Representative fixtures | minimal + realistic samples (e.g. `sample_device.*`, ApplicationProcess graphs) | |
| API contract assertions | reflection or source-compat tests where refactors can silently change public overload shapes or parameter names | |

Naming convention for boundary cases: suffix the scenario, e.g.
`ConvertToDcf_FileRevisionAtMaxValue_ClampsAt255` (`*_AtMaxValue`) and
`WriteFile_ValidatedRoundTrip_PreservesApplicationProcess`
(`*_ValidatedRoundTrip`).

### Example matrix (filled for the #311 `ConvertToDcf` fix)

| Dimension | What to cover | PR #320 |
|---|---|---|
| Numeric boundaries | `FileRevision` increment at max | `255` stays `255` (clamp, no wrap) — regression test at max value |
| Round-trip fidelity | EDS → DCF conversion output | conversion tests re-run on `sample_device.eds` |
| Validation modes | n/a | conversion path does not depend on write validation |
| Representative fixtures | minimal EDS with `FileRevision = 255` | added |
| API contract assertions | n/a | no public signature change |

## Commit convention

This project uses [Conventional Commits](https://www.conventionalcommits.org/).
semantic-release derives the next version number automatically from commit types.

```
<type>(<optional scope>): <description>
```

| Type | Version bump | When to use |
|---|---|---|
| `feat` | minor | New user-visible feature |
| `fix` | patch | Bug fix |
| `perf` | patch | Performance improvement |
| `revert` | patch | Reverts a previous commit |
| `docs` | — | Documentation only |
| `refactor` | — | Internal restructuring, no behaviour change |
| `test` | — | Adding or fixing tests |
| `build` | — | Build scripts, dependencies |
| `ci` | — | CI/CD configuration |
| `chore` | — | Everything else |

**Breaking changes** (major bump) — add a footer to the commit body:
```
feat: redesign public API

BREAKING CHANGE: CanOpenFile.Eds.ReadFile now returns a Result type
```

## Public API compatibility checklist

Use this checklist for any PR that adds, removes, renames, or reshapes public
members in `EdsDcfNet` — especially `CanOpenFile`, format entry points
(`.Eds`, `.Dcf`, `.Cpj`, `.Xdd`, `.Xdc`), and models consumed by library
callers.

Recent refactors surfaced gaps that were caught only after merge or by
downstream consumers. Each item below maps to a real incident so reviewers
know why it matters.

- [ ] **Binary ABI** — Do not remove existing public methods or overloads
  without a **major** release and an explicit `BREAKING CHANGE` note in the
  commit body or PR description. Precompiled consumers may still call
  signatures marked `[Obsolete]`; removing them causes
  `MissingMethodException` at runtime. When in doubt, keep the signature and
  mark it obsolete instead of deleting it.
  *(Incident: [#302](https://github.com/dborgards/eds-dcf-net/pull/302) —
  removed legacy `Write*` overloads broke precompiled consumers; binary-compatible
  overloads were restored.)*

- [ ] **Named arguments** — Parameter names on public methods are part of the
  **source contract**. Shared generic bases must not silently rename parameters
  that appear on derived or format-specific entry points; callers using named
  arguments will fail to compile even when overload resolution still succeeds.
  *(Incident: [#314](https://github.com/dborgards/eds-dcf-net/pull/314) /
  [#321](https://github.com/dborgards/eds-dcf-net/pull/321) — a shared generic
  base changed parameter names on format entry points; #321 restored
  named-argument compatibility.)*

- [ ] **Overload shape** — When slimming or redirecting facades, preserve or
  explicitly obsolete **every sibling overload** in the same PR — both
  default-argument and options-taking variants. Do not remove one overload
  shape while leaving its sibling in place without an `[Obsolete]` migration
  path.
  *(Incident: [#302](https://github.com/dborgards/eds-dcf-net/pull/302) —
  partial removal of `Write*` overload shapes; same facade refactor series as
  [#314](https://github.com/dborgards/eds-dcf-net/pull/314).)*

- [ ] **XML documentation / warnings-as-errors** — Public API changes must not
  introduce new CS15xx (documentation) or CS16xx (analyzer) warnings. The
  library builds with `TreatWarningsAsErrors=true`. Run
  `dotnet build --configuration Release` locally and resolve any new warnings
  on touched public members before opening the PR.
  *(Enforced by existing build policy; easy to miss during large refactors.)*

> **Optional follow-up (separate work):** evaluate automated enforcement such
> as `Microsoft.CodeAnalysis.PublicApiAnalyzers` or a compat baseline file.

## Release process

Releases are fully automated via semantic-release:

- **Beta pre-release**: merge a PR into `develop` → semantic-release publishes `X.Y.Z-beta.N` to NuGet.
- **Stable release**: merge `develop` into `main` (open a PR from `develop` → `main`) → semantic-release publishes `X.Y.Z` to NuGet and creates a GitHub release.

Maintainers decide when to promote `develop` → `main`.

## Recommended branch protection settings

| Branch | Require PR | Require status checks | Restrict direct push |
|---|---|---|---|
| `main` | Yes | `build` | Yes |
| `develop` | Yes | `build` | Yes |
