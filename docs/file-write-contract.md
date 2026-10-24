# File-write contract matrix

This matrix is the contract for changes to `TextFileIo`, `PlatformFileSupport`, or
any public `WriteFile` / `WriteFileAsync` path. Update the relevant row and its
verification in the same change. Do not silently weaken an expected outcome.

## Commit and fallback semantics

On `net10.0`, output is serialized to a temporary file in the final target's
directory and committed with `File.Move` or `File.Replace`. The old target stays
in place until the commit. A failed write, flush, or commit removes the temporary
file when possible and does not intentionally modify the old target.

The `netstandard2.0` asset uses the same sequence when the host runtime supports
it. It deliberately falls back to an **in-place, non-atomic write** for a Windows
reparse point and for an existing Unix file when the host has no
`File.GetUnixFileMode` / `File.SetUnixFileMode` API. Serialization finishes in
memory before the target is opened, but an I/O failure after truncation can leave
partial output. Async cancellation is observed before opening the target; after
truncation, copying and flushing deliberately continue with
`CancellationToken.None` so cancellation cannot leave an empty or partial file.

Symbolic links are followed rather than replaced. On `net10.0`, relative,
root-relative, drive-relative, UNC, chained, and dangling targets are resolved by
`SymbolicLinkPath`. On `netstandard2.0`, reparse points use the in-place fallback.
Junctions and other reparse points are not promised atomic symlink semantics on
that target.

## Contract rows

“Automated” names the suite and environment that executes the row. “Manual” means
the repeatable verification below is required on the named host; a skipped test
because the host cannot create links or set a mode is not a successful
verification. A blank or untested row must not be treated as supported.

| Scenario | Asset / host | Expected outcome | Verification status |
|---|---|---|---|
| Create a missing regular file | all assets / Windows, Linux, macOS | Complete file, no temporary file | Automated: `AtomicFileWriteTests`; net10.0 on local Unix and Windows CI, net48/netstandard2.0 on Windows CI |
| Replace an existing regular file | all assets / Windows, Linux, macOS | Old content visible before commit; complete replacement; no backup or temporary file | Automated: `AtomicFileWriteTests` on the same hosts |
| Serialization or callback failure | all assets / all supported hosts | Old target unchanged; no target created when absent; temporary file removed | Automated: `AtomicFileWriteTests`; in-place serialization covered by the net48 fallback tests |
| Commit failure | all assets / all supported hosts | Error surfaces as the format's write exception; existing filesystem object and its content remain; no temporary file | Automated: `AtomicFileWriteTests.WriteFile*_CommitFails_*` |
| Absolute symlink to existing or dangling file | net10.0 / Windows, Linux, macOS | Final target is created or replaced; every link remains a link | Automated: `AtomicFileWriteTargetTests` (Windows requires link privilege; Unix local runs execute it) |
| Relative and chained symlinks | net10.0 / Windows, Linux, macOS | Each relative target resolves against its own link directory; final target receives content | Automated: `AtomicFileWriteTargetTests`, `SymbolicLinkPathTests` |
| Symlink loop | net10.0 / Windows, Linux, macOS | `IOException`; no target or temporary file is modified or left | Automated: `AtomicFileWriteTargetTests.WriteFileAtomic_SymbolicLinkLoop_*` |
| UNC and extended UNC symlink target | net10.0 / Windows | Rooted target is used verbatim, never prefixed with the current directory | Automated path contract: `SymbolicLinkPathTests.Next_WindowsRootedTarget_*`; manual end-to-end verification required after changing link resolution |
| Root-relative and drive-relative symlink target | net10.0 / Windows | Target is qualified against the link location and drive, not the process working directory | Automated path contract: `SymbolicLinkPathTests.Next_WindowsRootedButNotFullyQualifiedTarget_*`; manual end-to-end verification required after changing link resolution |
| Reparse point on netstandard2.0 | netstandard2.0 / Windows | Link/reparse point remains; target is written in place; pre-truncation cancellation preserves old content; later cancellation cannot interrupt copying | Automated fallback cancellation: `AtomicFileWriteNetStandardFallbackTests` under net48; manual link verification below |
| Existing Unix mode 0600/0640 | net10.0 / Linux, macOS | Replacement preserves the complete mode, including while content is written | Automated: `AtomicFileWriteTargetTests` |
| Existing Unix set-user-ID/set-group-ID bits | net10.0 / Linux, macOS | Special bits are reapplied after writing and preserved at commit | Automated: `AtomicFileWriteTargetTests` (skip means the host could not establish the fixture, not that the row passed) |
| Existing Unix file on a host without managed mode APIs | netstandard2.0 / legacy Unix host | In-place fallback preserves mode; serialization errors occur before truncation | Manual only; unsupported as an atomic-write configuration |
| Cancellation before atomic commit | all assets / all supported hosts | `OperationCanceledException`; old target unchanged; temporary file removed | Automated: `AtomicFileWriteTests.WriteFileAsync_AlreadyCancelled_*` and `WriteFileAtomicAsync_CancelledWhileWriting_*` |
| Cancellation before in-place truncation | netstandard2.0 / Windows CI and manual legacy Unix | `OperationCanceledException`; old target unchanged | Automated: `AtomicFileWriteNetStandardFallbackTests` under net48 |
| Cancellation after in-place truncation | netstandard2.0 / Windows CI and manual legacy Unix | Copy and flush finish; final content is complete; no cancellation exception from the committed copy | Automated: `AtomicFileWriteNetStandardFallbackTests` under net48 |
| Target name at the filesystem component limit | net10.0 / Windows, Linux, macOS | Bounded temporary name; commit succeeds; no temporary file | Automated: `AtomicFileWriteTests.WriteFile*_TargetNameAtComponentLimit_*` |

## Repeatable manual verification

Use a small console project that references the built `EdsDcfNet.csproj` and call
`CanOpenFile.Eds.WriteFileAsync` with a model read from
`tests/EdsDcfNet.Tests/Fixtures/sample_device.eds`. Force selection of the
`netstandard2.0` asset where the host permits it. Record OS, runtime, selected
asset, and result beside the relevant row in the PR.

1. **Windows reparse point:** enable Developer Mode or run with symlink
   privilege. Create `target.eds` with known old content and `link.eds` pointing
   to it, using absolute, relative, and chained targets in separate runs. Write
   through the link. Confirm the link still exists as a link and only the target
   content changed. Repeat with a UNC share for UNC rows.
2. **Cancellation timing:** cancel once during serialization and verify the old
   target remains. For the destructive phase, use a large model and cancel only
   after observing target truncation; verify the operation finishes with complete,
   readable content.
3. **Legacy Unix host:** on a host without managed Unix-mode APIs, set mode 0600,
   write, and verify mode and content. Induce a serialization error and verify the
   old content remains. An induced I/O error after truncation is explicitly not
   required to preserve content.
4. **Permissions:** when link creation or setting a special mode is denied, rerun
   on a suitably configured host or report the row as unverified. Never convert an
   environment skip into a passing contract claim.
