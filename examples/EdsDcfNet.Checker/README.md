# edsdcf-check — EDS/DCF validity checker

Command-line example that validates CiA 306 **EDS** and **DCF** files (INI format only —
XDD/XDC are out of scope) and prints every problem with file, line, section, key and value.

It combines two passes:

1. **Raw pass** — a line-aware INI scan that never aborts on bad values, so *all* problems in
   a file are reported with their line numbers. Values are checked with
   `CanOpenValueConverter` against the entry's `DataType`.
2. **Library pass** — loads the file with `CanOpenFile.Eds/Dcf.ReadFileWithDiagnostics` and
   runs `CanOpenModelValidator`. Reader diagnostics are reported as `LIB001`, a reader abort as
   `LIB002`, remaining model-validator issues as `LIB003`.

## Usage

```bash
dotnet run --project examples/EdsDcfNet.Checker -- device.eds
dotnet run --project examples/EdsDcfNet.Checker -- --quiet path/to/folder
dotnet run --project examples/EdsDcfNet.Checker -- --json device.dcf > report.json
```

| Option | Meaning |
|---|---|
| `--json` | JSON output instead of text |
| `-q`, `--quiet` | Only print errors |
| `--warnings-as-errors` | Exit code 1 also for warnings |
| `--no-library` | Skip the EdsDcfNet reader/validator pass |

Exit codes: `0` valid, `1` errors found, `2` usage or I/O problem.

Example output:

```
device.eds: INVALID (3 error(s), 1 warning(s))
  device.eds:122: error VAL001 [2000] DefaultValue=1000: Value does not fit UNSIGNED8 (0..255).
  device.eds:132: error VAL003 [2001] DefaultValue=20: DefaultValue 20 is above HighLimit 10.
  device.eds:149: error FRM001 [2003] DefaultValue=$NODEID*2: Invalid $NODEID formula. ...
  device.eds:1737: warning VAL005 [2021sub3] DefaultValue=010000: Leading zero makes this an octal literal (= 4096 decimal). ...
```

## Rules

| Code | Severity | Check |
|---|---|---|
| INI001 | error | Malformed section header or line without `Key=Value` |
| INI002 | error | Duplicate section |
| INI003 | warning | Duplicate key within a section |
| INI004 | error | Entry outside of any section |
| OBJ001 | error | `ObjectType` not a number or not a CiA 306 object code |
| OBJ002 | error | `DataType` is not a number |
| OBJ003 | error/warning | `DataType` not a basic type / not defined by a DEFTYPE/DEFSTRUCT |
| OBJ004 | error | `DataType` missing |
| OBJ005 | error | `AccessType` missing or not `ro/wo/rw/rwr/rww/const` |
| OBJ006 | error | `SubNumber` does not match the sub-index sections (sub-index 0 included) |
| OBJ007 | error/warning | Sub-index 0 value vs. highest defined sub-index (read-only sub-index 0 only) |
| OBJ008 | error | `[XXXXsubY]` without parent `[XXXX]` |
| OBJ009 | error/warning | `SubNumber`/`CompactSubObj` not UNSIGNED8, `PDOMapping` not 0/1 |
| OBJ010 | error/warning | `ParameterName` missing or longer than 241 characters |
| VAL001 | error | `DefaultValue`/`LowLimit`/`HighLimit`/`ParameterValue` invalid or out of range for the data type (e.g. `1000` for UNSIGNED8, `08`) |
| VAL002 | error | `LowLimit` > `HighLimit` |
| VAL003 | error | `DefaultValue` outside `LowLimit`..`HighLimit` |
| VAL004 | error | `ParameterValue` outside `LowLimit`..`HighLimit` |
| VAL005 | warning | Leading zero → value is read as octal (`010` = 8) |
| VAL006 | warning | Limits on a non-numeric data type |
| VAL007 | warning | Hex literal for REAL32/REAL64 |
| FRM001 | error | Invalid `$NODEID` formula or operand |
| FRM002 | warning | `0x180+$NODEID` form — not understood by the EdsDcfNet reader |
| FRM003 | error | `$NODEID` formula on a non-integer data type |
| FRM004 | error | Formula result out of range (EDS: checked for node-ID 1 and 127, DCF: configured NodeID) |
| STD001 | error | Well-known CiA 301 entry (0x1000, 0x1018, PDO parameters, …) has the wrong `DataType` |
| LST001 | error | `SupportedObjects` missing or not matching the number of entries |
| LST002 | error | Object list references a missing section |
| LST003 | error | Object section not listed in any object list |
| LST004 | error | Object listed more than once |
| LST005 | error/warning | Mandatory object 0x1000/0x1001/0x1018 missing or in the wrong list |
| LST006 | error | Invalid object-list entry |
| PDO001 | error | PDO mapping references a missing object or invalid dummy type |
| PDO002 | error/warning | Mapped object not PDO-mappable, wrong access direction, dummy in TPDO |
| PDO003 | error | Mapping length does not match the mapped data type |
| PDO004 | error | PDO maps more than 64 bits |
| DCF001 | warning | DCF without configured `NodeID` |
| DCF002 | error | DCF `NodeID` outside 1..127 |
| LIB001–003 | | EdsDcfNet reader diagnostics / reader abort / model validator |
