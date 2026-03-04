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

6. Wait for CI (build + tests) to pass and for review.

## Coding conventions

Repository-wide formatting and baseline analyzer severities are defined in
[`.editorconfig`](.editorconfig). Please keep local IDE formatting aligned with
that file.

## .NET SDK policy

- SDK resolution is pinned in [`global.json`](global.json).
- CI resolves the .NET SDK from that file in both workflows.
- To update the SDK baseline, submit one PR that updates `global.json` and
  briefly notes the change in the PR description.

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

BREAKING CHANGE: CanOpenFile.ReadEds now returns a Result type
```

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
