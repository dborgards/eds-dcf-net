# 11. Risks and Technical Debt

## 11.1 Risks

### R-1: Specification Changes (CiA DS 306)

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Risk**         | Future versions of the CiA DS 306 specification introduce new sections or fields. |
| **Likelihood**   | Medium (specification is periodically updated).                             |
| **Impact**       | New fields could be ignored or misinterpreted.                              |
| **Mitigation**   | `AdditionalSections` preserves unknown sections. New fields in known sections are treated as `null` (forward-compatible). |

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
| **Mitigation**   | Tolerant parsing for optional fields. Support for common deviations (e.g., misspelling `"DeviceComissioning"` instead of `"DeviceCommissioning"`). |

## 11.2 Technical Debt

### TD-1: No Inheritance Hierarchy Between EDS and DCF

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | `ElectronicDataSheet` and `DeviceConfigurationFile` share many properties but do not use a common base class. |
| **Impact**       | Code duplication in model classes.                                          |
| **Priority**     | Low (deliberate design decision favoring clear semantics, see ADR-3).       |

### TD-2: No EDS Writer

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | Only a `DcfWriter` exists, no `EdsWriter`. EDS files can only be read.      |
| **Impact**       | Users who want to programmatically create EDS files cannot do so.           |
| **Priority**     | Medium (no demand expressed so far, as EDS is typically supplied by the manufacturer). |

### TD-3: No Asynchronous API Variants

| Aspect           | Description                                                                 |
|------------------|-----------------------------------------------------------------------------|
| **Description**  | All file operations (`ReadEds`, `WriteDcf`) are synchronous.                |
| **Impact**       | In async-based applications (e.g., ASP.NET), this may block the thread pool. |
| **Priority**     | Low (EDS/DCF files are typically small, I/O is negligible).                 |
