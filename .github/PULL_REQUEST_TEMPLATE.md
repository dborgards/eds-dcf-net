## Description

<!-- What does this PR do? Link related issues with "Closes #…" when applicable. -->

## Type of change

- [ ] Bug fix (`fix:`)
- [ ] New feature (`feat:`)
- [ ] Refactor (`refactor:`)
- [ ] Documentation (`docs:`)
- [ ] Tests (`test:`)
- [ ] CI / build (`ci:` / `build:`)
- [ ] Other (`chore:`)

## Public API changes

Does this PR modify public members in `EdsDcfNet` (for example `CanOpenFile`,
format entry points, or models consumed by library callers)?

- [ ] **No** public API changes
- [ ] **Yes** — I completed the
      [Public API compatibility checklist](https://github.com/dborgards/eds-dcf-net/blob/develop/CONTRIBUTING.md#public-api-compatibility-checklist)
      in `CONTRIBUTING.md` (binary ABI, named arguments, overload shape, XML
      doc / warnings-as-errors)

<!-- If "Yes", briefly note preserved overloads, `[Obsolete]` attributes, or
     parameter renames reviewers should verify. -->

## Testing

- [ ] `dotnet test --configuration Release` passes locally
- [ ] `dotnet build --configuration Release` passes locally (required when
      public API or XML docs changed)
