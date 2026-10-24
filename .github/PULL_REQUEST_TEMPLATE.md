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

## Boundary & regression matrix

Required when this PR touches **parsers, writers, validators, or converters**
(lessons from #305/#313, #311/#320). See the
[Boundary & regression test guide](https://github.com/dborgards/eds-dcf-net/blob/develop/CONTRIBUTING.md#boundary--regression-test-guide)
in `CONTRIBUTING.md`. Use `*_AtMaxValue` / `*_ValidatedRoundTrip` naming where
applicable. Mark unused dimensions `n/a` with a short reason.

- [ ] **n/a** — this PR does not touch parsers/writers/validators/converters
- [ ] **Filled** — matrix below completed and tests added

<details>
<summary>Matrix (expand and fill when applicable)</summary>

| Dimension | What to cover | This PR |
|---|---|---|
| Numeric boundaries | min, max, and wrap/clamp semantics of every touched numeric field | |
| Round-trip fidelity | read → write → read for each affected format (EDS, DCF, CPJ, XDD, XDC) | |
| Validation modes | default write **and** `CanOpenWriteOptions.Validated` where validation differs | |
| Representative fixtures | minimal + realistic samples (e.g. `sample_device.*`, ApplicationProcess graphs) | |
| API contract assertions | reflection or source-compat tests where refactors can silently change public overload shapes or parameter names | |

</details>

## Validation-rule checklist

Required when this PR adds or changes a validation rule. Complete the
[validation-rule checklist](https://github.com/dborgards/eds-dcf-net/blob/develop/CONTRIBUTING.md#validation-rule-checklist):
null/empty model states, cancellation at user-controlled iteration boundaries,
default/strict/opt-in behavior, downstream writers/converters/examples/checker,
and a representative end-to-end acceptance test.

- [ ] **n/a** — no validation rule changed
- [ ] **Filled** — all applicable items completed and covered by tests

## File-write contract

Required when this PR changes the file-writing pipeline. Update the
[file-write contract matrix](https://github.com/dborgards/eds-dcf-net/blob/develop/docs/file-write-contract.md)
and record any required manual platform verification.

- [ ] **n/a** — no file-writing pipeline change
- [ ] **Filled** — matrix and verification updated
