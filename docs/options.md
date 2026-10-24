# Read, write, and validation options

The format entry points on `CanOpenFile` share two option types:

- `CanOpenFileOptions` controls input limits, strict parsing, and input encoding.
- `CanOpenWriteOptions` controls pre-write validation, output encoding, and line endings.

Both option types are immutable (`init`-only) and may be shared across threads.

## Read options

### Input size limits

All read APIs apply a safe default input-size limit of **10 MiB**
(`IniParser.DefaultMaxInputSize`) to reduce denial-of-service risk from unexpectedly
large payloads. Override the limit per operation for trusted larger inputs:

```csharp
var xdd = CanOpenFile.Xdd.ReadFile(
    "large-device.xdd",
    new CanOpenFileOptions { MaxInputSize = 50L * 1024 * 1024 });
```

Set the limit only as high as necessary for the expected maximum file size.

### Strict parsing

Set `CanOpenFileOptions.StrictParsing = true` so silent read coercions fail with
`EdsParseException` instead of mapping to defaults. The default remains lenient. This
applies to reads through the `CanOpenFile` format entry points and legacy facade
overloads that accept options. Direct `*Reader` APIs without options stay lenient.

```csharp
var eds = CanOpenFile.Eds.ReadFile(
    "device.eds",
    new CanOpenFileOptions { StrictParsing = true });
```

Strict parsing currently covers:

- Duplicate keys within an INI section.
- Unknown XDD/XDC baud-rate strings (`supportedBaudRate`, `baudRate/@defaultValue`).
  `deviceCommissioning/@actualBaudRate` is a free string: an unlisted value is reported
  and preserved, not rejected.
- Unknown boolean tokens, CPJ present-flag tokens, access-type tokens, and XDD XML bools.
- Malformed EDS/DCF numeric object keys. Object-list counts become zero in lenient mode;
  a bad index entry is skipped while the object and the rest of the file are retained.
- EDS/DCF `FileVersion` / `FileRevision` and XDD/XDC `fileVersion` major/minor tooling
  forms (`1.0` / `1,0`). Zero-padded values such as `010` parse as decimal `10`.
- Missing XDD/XDC `index` on `CANopenObject` and missing or invalid `objectType`.
  Schema-valid unsignedByte forms such as `+9` and `-0` are accepted after trimming;
  a missing `CANopenSubObject` `subIndex` remains lenient.
- Malformed XDD/XDC unsigned numeric attributes (`subNumber`, `pDOmappingIndex`,
  general-feature counts, `networkNumber`; an optional leading sign is accepted after
  trimming).
- XDD/XDC `objFlags` (`xsd:hexBinary`, CiA 311 Annex A.1.4): hexadecimal digits only.
  A leading sign or `0x` prefix is rejected. An odd number of hex digits is accepted in
  lenient mode and rejected in strict mode.

XDD/XDC `fileVersion` is a free `xsd:string`. A spelling that is not a plain decimal
number (for example `vendor-r7`, `1.0`, `256`, or an empty value) is kept in
`EdsFileInfo.FileVersionText` and written back while `FileVersion` remains unchanged.
`FileVersion` then holds the best-effort major component, or `1` when no number can be
derived. A plain decimal such as `010` sets only `FileVersion` and is written back
normalised (`10`). EDS and DCF writers always use `FileVersion`.

### Input encoding

`CanOpenFileOptions.Encoding` controls how file and stream bytes are decoded. By default,
EDS/DCF/CPJ honor a byte-order mark, try strict UTF-8, and then fall back to ISO-8859-1
with a diagnostic. XDD/XDC honor a byte-order mark or the XML declaration and otherwise
use UTF-8.

An explicit encoding disables the INI fallback and overrides an XDD/XDC declaration.
Invalid bytes always fail with `EdsParseException`; they are never replaced with U+FFFD,
even when the supplied encoding has a replacement fallback. String overloads already
contain decoded text and ignore this option.

### Parse diagnostics

For a non-throwing account of lenient repairs, use the `Read*WithDiagnostics` methods on
the format entry points. They return the parsed model and every `ParseDiagnostic`; see
the [README](../README.md#parse-diagnostics-report-repairs-keep-the-model) for an example.

## Write options

### Output encoding

All formats write **UTF-8 without BOM** by default for file and stream output. Set
`CanOpenWriteOptions.Encoding` to select another encoding for byte output; XDD/XDC XML
declarations name the same encoding:

```csharp
using System.Text;

var options = new CanOpenWriteOptions
{
    Encoding = Encoding.GetEncoding("us-ascii"),
    NewLine = "\r\n"
};
CanOpenFile.Dcf.WriteFile(dcf, "device_ascii.dcf", options);
```

Unrepresentable characters are never silently replaced. EDS, DCF, and CPJ throw the
format's write exception with an inner `EncoderFallbackException`, leaving an existing
destination unchanged. XDD and XDC emit numeric character references in text and
attribute values. `WriteToString` returns a .NET string, so `Encoding` does not change it;
its XDD/XDC declaration remains UTF-8.

### Line endings

By default output uses `Environment.NewLine`, producing CRLF on Windows and LF on
Linux/macOS. For byte-identical output across platforms, set `CanOpenWriteOptions.NewLine`
for any format and for file, stream, string, synchronous, or asynchronous writes:

```csharp
var options = new CanOpenWriteOptions { NewLine = "\n" };   // or "\r\n"
CanOpenFile.Eds.WriteFile(eds, "device.eds", options);
CanOpenFile.Xdd.WriteFile(xdd, "device.xdd", options);
```

Only `"\n"` and `"\r\n"` are accepted. Any other value throws `ArgumentException` when
the property is set, preventing INI section injection and malformed XML. EDS, DCF, and
CPJ output contains only the chosen line ending. XDD and XDC use it for indentation and
line breaks inside text content; line breaks inside attribute values are written as
character references and preserved exactly.

### Validated writes

Pass `CanOpenWriteOptions.Validated` to validate automatically before writing:

```csharp
var dcf = CanOpenFile.Dcf.ReadFile("configured_device.dcf");
CanOpenFile.Dcf.WriteFile(dcf, "updated.dcf", CanOpenWriteOptions.Validated);
```

The write throws `ModelValidationException` when validation finds issues. The same option
works on every format entry point. Async write methods await validation and honor the
write call's `CancellationToken`; synchronous writes keep synchronous validation.

A validated EDS or DCF write also applies INI-specific rules: `ObjFlags` bits 2..31 are
reserved (CiA 306-1 Table 8; XDD/XDC also define bit 2), comment lines are at most 249
characters (248 in a module's `[MxComments]`), and `ParamRefd`, `UploadFile`, and
`DownloadFile` of a DCF are at most 249, 244, and 242 characters. The EDS/DCF writers
always write `Lines` as the number of comment lines.

## Validation options

`CanOpenFile.Validate(...)` returns path-based `ValidationIssue` entries from the full
model validator:

```csharp
using EdsDcfNet;
using EdsDcfNet.Validation;

IReadOnlyList<ValidationIssue> issues = CanOpenFile.Validate(dcf);
foreach (var issue in issues)
    Console.WriteLine(issue);
```

Default checks cover commissioning constraints, device-info constraints, object
dictionary consistency, and object-level constraints. Stricter CiA 306 conformance checks
are opt-in through `CanOpenValidationOptions`, so existing `Validate` calls and validated
writes keep their behavior:

| Option | Checks |
|---|---|
| `CheckSubNumberCount` | `SubNumber` equals the number of described sub-indexes including sub-index 00h (CiA 306-1 §6.6.3.2) |
| `CheckValueRanges` | `DefaultValue`, `LowLimit`, `HighLimit`, `ParameterValue` fit the integer/BOOLEAN/REAL `DataType`; `LowLimit <= HighLimit`; default/parameter values are within limits. `$NODEID` formulas use the DCF node ID, or node IDs 1 and 127 in an EDS |
| `RequireMandatoryEntries` | Objects 1000h/1001h/1018h, non-empty `ParameterName`, `DataType` for VAR entries, `FileName`/`VendorName`/`ProductName`, configured DCF commissioning |
| `CheckObjectListRanges` | `OptionalObjects` indexes lie in 1000h-1FFFh or 6000h-9FFFh and `ManufacturerObjects` indexes in 2000h-5FFFh (CiA 306-1 Table 4) |
| `RequireIso646` | EDS/DCF text uses 7-bit ISO/IEC 646 characters only (CiA 306-1 clause 6.2). The library writes UTF-8, a documented deviation, so this rule is opt-in |
| `CheckLineLength` | No EDS/DCF line is longer than 255 characters (CiA 306-1 clause 6.2) |
| `CheckObjectListEntries` | No numbered object-list entry remains in `SectionRemainingEntries`, where it would be written above `SupportedObjects` or replaced by a generated entry |

```csharp
// Every opt-in rule set.
var strictIssues = CanOpenFile.Validate(dcf, CanOpenValidationOptions.Strict);

// Or select an individual rule set.
var rangeIssues = CanOpenFile.Validate(
    dcf,
    new CanOpenValidationOptions { CheckValueRanges = true });
```

The public `CanOpenNodeId` constants expose the node-ID range used by these checks:

```csharp
bool valid = CanOpenNodeId.IsInRange(nodeId);  // true for 1..127
byte min = CanOpenNodeId.MinValue;             // 1
byte max = CanOpenNodeId.MaxValue;             // 127
string range = CanOpenNodeId.RangeDescription; // "1..127"
```

### Async validation

For large models, `ValidateAsync` and `EnsureValidAsync` run validation on a thread-pool
thread with cooperative cancellation for EDS, DCF, and CPJ models:

```csharp
IReadOnlyList<ValidationIssue> issues =
    await CanOpenFile.ValidateAsync(dcf, cancellationToken);
await CanOpenFile.EnsureValidAsync(dcf, cancellationToken);
```

Cancellation is observed at iteration boundaries, per object-dictionary entry or network
node, so large validation runs can stop mid-run.
