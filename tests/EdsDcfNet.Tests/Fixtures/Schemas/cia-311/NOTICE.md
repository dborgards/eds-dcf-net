# Schema source: CiA 311 (CANopen XML schema definition)

- Origin: CiA 311 version 1.1.0, "CANopen XML schema definition", Annex A
  (normative), sections A.1.1 to A.1.4
- Rights holder: CAN in Automation (CiA) e. V. The schema files are **not**
  covered by this repository's MIT licence.
- Purpose: test fixtures only. They let the test suite validate generated
  XDD/XDC documents against the normative schema. They are not part of the
  `EdsDcfNet` NuGet package. The specification document itself is not
  redistributed in this repository.
- Added: 2026-10-01

| File | Annex | Note |
|---|---|---|
| `ISO15745ProfileContainer.xsd` | A.1.1 | |
| `CommonElements.xsd` | A.1.2 | |
| `ProfileBody_Device_CANopen.xsd` | A.1.3 | |
| `ProfileBody_Network_CANopen.xsd` | A.1.4 | The annex heading names this file `ProfileBody_CommunicationNetwork_CANopen.xsd`; the type it defines is `ProfileBody_CommunicationNetwork_CANopen` either way. |

The content of all four files was compared against the text of Annex A
(whitespace ignored) and matches, apart from the two changes below.

## Changes against the delivered files

1. **Line endings normalised to LF.** The delivered files used bare CR line
   terminators, which Git treats as non-text and which makes diffs unusable.
   XML processors normalise line ends, so the schema is unaffected.
2. **`sortStep` default corrected from `"1"` to `"01"`** in
   `ProfileBody_Network_CANopen.xsd`. The attribute is typed `xsd:hexBinary`,
   which requires an even number of hex digits. With the value as printed in
   Annex A.1.4 the schema does not compile (checked with libxml2 and with
   .NET `XmlSchemaSet`). `Cia311SchemaValidationTests` guards this value.

## Not included

- `CANopen.xsd`: a convenience schema that includes the four files above. It
  is not part of Annex A, and it declares no target namespace while including
  schemas that have one, which XSD does not allow. The tests load the two
  `ProfileBody_*` schemas directly instead.
- `CANopen_TextResource.xsd` (CiA 311 B.4.3): text resource files are not
  read or written by this library.
