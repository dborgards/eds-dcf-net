# 11. Risks and Technical Debt

## 11.1 Risks

### R-1: Specification Changes (CiA DS 306 / CiA 311)

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Risk**         | Future versions of CiA DS 306 or CiA 311 introduce new sections/elements or attributes. |
| **Likelihood**   | Medium (specification is periodically updated).                             |
| **Impact**       | New fields could be ignored or misinterpreted.                              |
| **Mitigation**   | INI unknown sections are preserved in `AdditionalSections`; XML handling is kept strict to the supported mapped profile subset and extended incrementally with tests. Unmodelled XDD/XDC content is kept as XML fragments and written back at its schema position; `AdditionalSections` additionally mirrors unknown CommunicationNetwork ProfileBody children as attribute-only maps (see §8.4). |

### R-2: netstandard2.0 API Limitations

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Risk**         | New features require APIs not available in `netstandard2.0`.                |
| **Likelihood**   | Medium.                                                                     |
| **Impact**       | Workarounds needed or feature only available for `net10.0`.                 |
| **Mitigation**   | `#if` preprocessor directives for platform-specific code. Regular review of whether `netstandard2.0` is still relevant. |

### R-3: Non-Compliant EDS/DCF Files

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Risk**         | Real-world EDS/DCF files from device manufacturers sometimes deviate from the specification. |
| **Likelihood**   | High (commonly encountered in practice).                                    |
| **Impact**       | `EdsParseException` on otherwise usable files.                              |
| **Mitigation**   | Tolerant parsing for optional fields. Support for common deviations (e.g., `"DeviceCommissioning"` (two m) besides the CiA 306 spelling `"DeviceComissioning"`, which is also what the DCF writer emits; Polarion-style `FileVersion=1,0` / `1.0` accepted as major `1` unless `StrictParsing` is enabled). |

### R-4: Non-Compliant or Tool-Specific XDD/XDC XML

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Risk**         | Real-world XDD/XDC exports can vary between tooling vendors and may include unsupported XML constructs. |
| **Likelihood**   | Medium.                                                                     |
| **Impact**       | Parse failures or partial data mapping.                                     |
| **Mitigation**   | Keep parser behavior explicit, add fixture-based compatibility tests for encountered variants, and extend mappings conservatively. |

## 11.2 Technical Debt

### TD-1: No Inheritance Hierarchy Between EDS and DCF

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | `ElectronicDataSheet` and `DeviceConfigurationFile` share many properties but do not use a common base class. |
| **Impact**       | Code duplication in model classes.                                          |
| **Priority**     | Low (deliberate design decision favoring clear semantics, see ADR-3).       |

### TD-2: Obsolete Static Facade Overloads

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | The legacy static `Read*` / `Write*` / `EdsToDcf` overloads directly on `CanOpenFile` (111 `[Obsolete]` members in `CanOpenFile.cs`) duplicate the format entry points (`CanOpenFile.Eds`, `.Dcf`, `.Cpj`, `.Xdd`, `.Xdc`) and only delegate to them. |
| **Impact**       | Larger public surface; the overloads can only be removed in a major release (public API compatibility, see CONTRIBUTING). |
| **Priority**     | Low (advisory `[Obsolete]` and README "Migration Guide" are in place).      |
| **Status**       | Open.                                                                       |

### TD-3: Automatic Octal Interpretation of Leading-Zero Integers

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | Integers with a leading zero (`010`) are parsed as C-style octal (decision #411, §8.3). Zero-padded decimal values in real EDS/DCF files are therefore misread (`08`/`09` raise a parse error). `FileVersion` / `FileRevision` are exempt (plain decimal). |
| **Impact**       | Silent misreading of padded decimal values from some tools.                 |
| **Priority**     | Medium; changing the default is a breaking behavior change and deferred to a major release or an opt-in. |
| **Status**       | Open (deliberate decision, documented in §8.3 and README).                  |

### TD-4: XDD/XDC Content Outside the Typed Model

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | `AdditionalSections` is INI-shaped and cannot hold XML. Unmodelled XDD/XDC content is now kept as XML fragments and written back at its schema position (§8.4). Remaining limits: additional `ISO15745Profile` elements beyond the two read, and XML comments between modelled elements, are not preserved. |
| **Impact**       | Residual loss on XDD/XDC round-trips for such files.                        |
| **Priority**     | Low.                                                                        |
| **Status**       | Largely resolved (unmodelled-content preservation: #618; documentation: #620, tracked in #581 as WP-43/WP-23/WP-30); the residual limits above are open. |

### TD-5: ApplicationProcess Only for XDD/XDC

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | The typed `ApplicationProcess` graph (CiA 311 §6.4.5) is populated only when reading XDD/XDC and is `null` for EDS/DCF sources; the INI formats have no counterpart. |
| **Impact**       | EDS and DCF sources carry no application-process data.         |
| **Priority**     | Low (inherent to the format difference, see §5.2.3).                        |
| **Status**       | Open.                                                                       |

### TD-6: Thread-Safety Tests Cover Only EDS and XDD

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | `ThreadSafetyTests` exercise concurrent read/write/validate for EDS and XDD (one INI and one XML path). DCF, CPJ and XDC share the same stateless entry-point implementation but are not covered by the saturation tests (§8.8). |
| **Impact**       | A regression specific to one of the other formats would not be caught by these tests. |
| **Priority**     | Low.                                                                        |
| **Status**       | Open.                                                                       |

### TD-7: Static Writer Instance

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | `EdsWriter` and `DcfWriter` hold a `private static readonly` `Instance` of themselves only to call the instance method `WriteObject` from static code. |
| **Impact**       | Minor design smell, no functional effect.                                   |
| **Priority**     | Low.                                                                        |
| **Status**       | Open; planned as part of WP-35 (L10, make `WriteObject` static), which is not merged yet (#581). |

### TD-8: Test Project Not on AnalysisMode Recommended

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | `AnalysisMode` is `Recommended` for `EdsDcfNet` only (`Directory.Build.props`). `EdsDcfNet.Tests` stays on the SDK default to avoid a full test-naming cleanup in one step (for example CA1707, see CONTRIBUTING "Analyzer policy"). |
| **Impact**       | Test code is checked with weaker analyzer rules than library code.          |
| **Priority**     | Low.                                                                        |
| **Status**       | Open.                                                                       |

### TD-9: No Recorded Performance Baseline

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | `benchmarks/EdsDcfNet.Benchmarks/BASELINE.md` defines the baseline scenarios, but its tracking table has no row: no baseline was measured yet. |
| **Impact**       | Performance regressions cannot be compared against a reference.             |
| **Priority**     | Low.                                                                        |
| **Status**       | Partly resolved: the "capture pending" placeholder row was removed and the file now states that nothing is recorded (WP-36, #584). A measurement on an idle machine is still missing. |
