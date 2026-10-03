# Deep Review: EdsDcfNet (dborgards/eds-dcf-net @ 86e7295, v1.14.0)

Stand: 2026-09-30. Review-Grundlage: vollständige Lektüre von `src/EdsDcfNet` (18.8k Zeilen), Stichproben in Tests/Examples/Benchmarks, drei Sub-Reviews (CI/Release, Docs, Checker/Benchmarks), eigener Build + Testlauf (net10.0, Linux) und ein Repro-Harness gegen die gebaute DLL. Spec-Abgleich gegen die hochgeladenen Dokumente CiA 306-1 v1.4.0, CiA 306-2 v1.2.0, CiA 306-3 v1.2.0, CiA 306 v1.3.0 und CiA 311 v1.1.0.

Alle als "verifiziert" markierten Befunde wurden mit dem Repro-Harness gegen die Bibliothek reproduziert; die anderen ergeben sich aus dem Code-Lesen. Zeilenangaben beziehen sich auf den Commit 86e7295.

---

## 0. Gesamtbild

| Bereich | Bewertung |
|---|---|
| Build / Tests | Release-Build 0 Fehler, 2376 Tests grün (net10.0), Zeilenabdeckung 99,35 %, Branchabdeckung 98,7 % |
| Code-Qualität Kern | hoch: konsequent `InvariantCulture`, `AsyncLocal`-Scopes, saubere Fehlerbehandlung, gute XML-Doku |
| Sicherheit Parser | gut: DTD verboten, XML-Tiefe begrenzt, Byte-Limit auch beim Streamen (TOCTOU-sicher) |
| Spec-Konformität CiA 306 | mehrere echte Verstöße (Details Teil 1), v. a. Round-Trip-Verluste bei Modulen und Default-Werten |
| Spec-Konformität CiA 311 | schwächer als beworben: erzeugte XDD/XDC sind nicht schemakonform, `uniqueIDRef` wird ignoriert |
| Release-Pipeline | sehr aufwendig; ein Repair-Pfad ist funktional falsch und kann develop-Releases dauerhaft blockieren |
| Dokumentation | README-API stimmt; Copilot-Instructions, arc42 §2/§11, Tech-Stack-Canvas und `docs/index.html` sind veraltet |

Die wichtigsten fünf Punkte:

1. **XDD mit `uniqueIDRef` (z. B. CANopenNode) verliert DataType/AccessType/DefaultValue** aller Objekte. Das Corpus-File `basicDevice.xdd` (343 Referenzen) wird zu einem EDS mit `DataType=0x0`. (Teil 1.2, Nr. X7)
2. **Modul-Sektionen `[MxSubExtends]`, `[MxSubExtxxxx]`, `[MxComments]` werden als "bekannt" markiert, aber weder geparst noch geschrieben** und gehen beim Round-Trip verloren. (Teil 1.1, Nr. S7)
3. **Release-Repair schreibt den falschen Channel** (`beta` statt `develop`) in die semantic-release-Notes; nach einer Reparatur würde der develop-Kanal keine neuen Betas mehr veröffentlichen. (Teil 3, CI-1)
4. **Der INI-Writer escaped nichts**: ein Zeilenumbruch in `ParameterName` erzeugt neue Sektionen/Keys, auch mit `CanOpenWriteOptions.Validated`. (Teil 2, Nr. L3)
5. **Eine CiA-Spezifikation (306 v1.3.0 PDF) ist im Repo eingecheckt** und wird mit dem Quellarchiv verteilt. (Teil 5, D-H1)

---

## 1. Verstöße gegen die CiA-Spezifikationen

### 1.1 CiA 306-1 v1.4.0 (EDS/DCF) und CiA 306-3 v1.2.0 (DynamicChannels, CPJ, Tools)

| Nr. | Schwere | Spec-Stelle | Befund | Code | Status |
|---|---|---|---|---|---|
| S7 | **Hoch** | 306-1 §8.3 Tab. 15-17 (`[MxComments]`, `[MxSubExtends]`, `[MxSubExtxxxx]` mit `Count`/`ObjExtend`) | Der Reader klassifiziert die Sektionen als bekannt (`IsModuleSection`), parst sie aber nicht: `ModuleInfo.SubExtends`, `SubExtensionDefinitions`, `Comments` bleiben leer. Der Writer schreibt sie nicht. Ergebnis: **stiller Verlust beim Round-Trip, 0 Diagnostics.** `[MxFixedxxxx]` überlebt nur zufällig über `AdditionalSections` (wird nicht in `FixedObjectDefinitions` geparst). | `Parsers/CanOpenReaderBase.cs:706-727`, `Parsers/CanOpenSectionParsers.cs:117-140`, `Writers/IniWriterBase.cs:494-515` | verifiziert |
| S4 | **Hoch** | 306-3 §5.2.2 Tab. 2, Fig. 2/3: `PPOffsetX=[offset], [address difference]` für BOOLEAN-Segmente | `PPOffset1=0, 1` (Spec-Beispiel) wirft `EdsParseException` und bricht das gesamte EDS-Lesen ab, auch im Lenient-Modus. | `Parsers/CanOpenSectionParsers.cs:161`, Modell `DynamicChannelSegment.PPOffset` ist `uint` | verifiziert |
| S1a | Mittel | 306-1 Tab. 7 NOTE 1: "In case the value of ObjectType is empty, it equals ObjectType VAR" | `ObjectType=` (leer) ergibt ObjectType `0x0` (NULL) statt `0x7`; wird so auch zurückgeschrieben. | `Parsers/CanOpenReaderBase.cs:294-302` mit `Utilities/ValueConverter.cs:183-188` (leer → 0) | verifiziert |
| S3 | Mittel | 306-1 Tab. 7: für DEFSTRUCT/ARRAY/RECORD ohne CompactSubObj sind `DataType`, `AccessType`, `PDOMapping` "n" (nicht unterstützt) | Der Writer schreibt für jedes Objekt unbedingt `AccessType=…` und `PDOMapping=…`, also auch für RECORD/ARRAY-Hauptobjekte. Der Validator meldet das nicht. | `Writers/IniWriterBase.cs:131,148`; `Validation/CanOpenModelValidator.cs:412-535` | verifiziert |
| S2 | Mittel | 306-1 §6.2 ("additional entries inside of the sections … support future extensions"), §6.5 (reservierte Einträge `ProductVersion`, `LMT_*`, `ExtendedBootUp*`) | Unbekannte oder reservierte Keys in `[FileInfo]`, `[DeviceInfo]`, `[DeviceComissioning]`, `[Comments]`, `[Tools]`/`[ToolX]`, `[DynamicChannels]` werden beim Round-Trip verworfen. Nur Objekt-/Subobjekt-Sektionen haben `RemainingEntries`. | `Parsers/CanOpenSectionParsers.cs:27-64`, Modelle `DeviceInfo`, `EdsFileInfo`, `DeviceCommissioning`, `ToolInfo` ohne Rest-Dictionary | verifiziert |
| S5 | Mittel | 306-1 §6.3: "octet strings … stored as a sequence of hexadecimal bytes **without leading 0x**" | `CanOpenValueConverter.Format(byte[], OCTET_STRING)` erzeugt `0x01A105`. `SetParameterValue(index, byte[])` schreibt damit nicht-konforme Werte. Parse akzeptiert beides. | `Utilities/CanOpenValueConverter.cs:559-582` | verifiziert |
| S1b | Niedrig | 306-1 Tab. 1 Fußnote a: "If the entry [EDSVersion] is missing, this is equal to 3.0" | Fehlender `EDSVersion` wird als `4.0` gelesen (Modell-Default und `GetValue`-Default). | `Parsers/CanOpenReaderBase.cs:144`, `Models/EdsFileInfo.cs:37` | verifiziert |
| S1c | Niedrig | 306-1 Tab. 7: DOMAIN-Ersatzwerte `DataType=(DOMAIN)`, `AccessType=(rw)` | Fehlender AccessType eines DOMAIN-Objekts wird `ro`, DataType bleibt `null` statt `0x000F`. | `Parsers/CanOpenReaderBase.cs:305-321` | verifiziert |
| S9a | Niedrig | 306-1 §6.2: "characters encoded according to ISO 646", Zeilenlänge ≤ 255 | Weder Validator noch Writer prüfen ASCII oder Zeilenlänge; Testausgabe mit 313 Zeichen und Nicht-ASCII wird ohne Issue geschrieben (UTF-8 ist als bewusste Abweichung dokumentiert, aber nicht per Option prüfbar). | `Writers/IniWriterBase.cs:16`, Validator ohne Regel | verifiziert |
| S10 | Niedrig | 306-1 Tab. 4/5: Listen-Bereiche (Optional 1000h-1FFFh & 6000h-9FFFh, Manufacturer 2000h-5FFFh), `SupportedObjects` = Anzahl Einträge, Nummerierung 1..n | Keine Prüfung, ob ein Index in der richtigen Liste steht; `SupportedObjects=5` bei einem Eintrag bleibt unbemerkt; Einträge > `SupportedObjects` werden still ignoriert. | `Validation/CanOpenModelValidator.cs:321-410`, `Parsers/LenientIniNumber.cs:169-204` | verifiziert |
| S11 | Niedrig | 306-1 Tab. 6/§6.6.3.2: SubNumber = Anzahl beschriebener Sub-Indizes inkl. 00h | Writer-Fallback bei nicht gesetztem `SubNumber` schreibt den **höchsten Sub-Index** (4 statt 5 bei sub0..sub4); der eigene Strict-Validator meldet die so erzeugte Datei anschließend als fehlerhaft. | `Writers/IniWriterBase.cs:234-250` | verifiziert |
| S12 | Niedrig | 306-1 Tab. 7: `SubNumber` "n" für VAR/DEFTYPE/DOMAIN | Writer emittiert `SubNumber`, sobald `SubObjects` existieren, unabhängig vom ObjectType; Validator prüft ObjectType-Konsistenz nicht. | `Writers/IniWriterBase.cs:117-121` | Code |
| S13 | Niedrig | 306-1 §6.6.3.3: ObjFlags-Bits 2..31 "shall be 0" | Keine Validierung reservierter Bits. | Validator | Code |
| S14 | Niedrig | 306-1 §6.6.5 / §8.3: `Line<n>` ≤ 249 (bzw. 248) Zeichen; §7.3.3 `ParamRefd` ≤ 249; §7.3.1 `UploadFile` ≤ 244, `DownloadFile` ≤ 242 | Diese Längen werden nicht validiert (im Gegensatz zu ParameterName, NodeName usw.). `Comments.Lines` wird unabhängig von `CommentLines.Count` geschrieben. | `Validation/CanOpenModelValidator.cs`, `Writers/IniWriterBase.cs:518-529` | Code |
| S15 | Niedrig | 306-1 Tab. 12: `NodeID` ist UNSIGNED8 ohne weitere Einschränkung in 306-1 | DcfReader wirft bei `NodeID=0` (Sektion vorhanden) auch im Lenient-Modus statt ein Validation-Issue zu liefern; ebenso `Baudrate`, `NetNumber`, `LSS_SerialNumber` und alle `[DeviceInfo]`-Zahlen (`VendorNumber=0x1G` bricht das Lesen ab). Die in `CanOpenFileOptions.StrictParsing` dokumentierte Lenient-Abdeckung ist damit unvollständig. | `Parsers/DcfReader.cs:261-283`, `Parsers/CanOpenSectionParsers.cs:36-61` | verifiziert |
| S16 | Niedrig | 306-1 §7.3.5: Sektionsname `[DeviceComissioning]` | Bei gleichzeitigem Vorkommen beider Schreibweisen gewinnt die nicht-normative `DeviceCommissioning`; der Checker entscheidet umgekehrt. | `Parsers/DcfReader.cs:252-256` | Code |
| S17 | Niedrig | 306-3 §6.2.2 Tab. 3: `Nodes` (hex, mandatory), `NodeXPresent` "all other values reserved" | `Nodes` wird nicht gelesen/validiert; `Node3Present=0x02` wird still zu `false`; keine Diagnostic. | `Parsers/CpjReader.cs:147-185`, Validator | verifiziert |
| S18 | Info | 306-1 §6.6.3.4.3: bei CompactSubObj "SubNumber … shall be 0, empty or not appear" | Writer schreibt SubNumber trotzdem, wenn erweiterte Sub-Objekte oberhalb des Compact-Bereichs existieren. Dokumentierte, bewusste Erweiterung. | `Writers/IniWriterBase.cs:252-268` | Code |
| S19 | Info | 306-1 §6.3: `$NODEID { "+" number }` | Zusätzlich wird `$NODEID-n` akzeptiert (dokumentierte Erweiterung). Konform ist auch die Oktal-Regel (`012` = 10). | `Utilities/ValueConverter.cs:559-617` | Code |

Korrekt umgesetzt (stichprobenartig gegen die Spec geprüft): Sektions-/Keyname-Case-Insensitivität, Trim-Regel, dezimal/hex/oktal, `$NODEID`-Summenformel, `[DummyUsage]`-Syntax, CompactSubObj-Synthese (Namen, sub0 = NrOfObjects/UNSIGNED8/ro/nicht mappbar, Limits leer), `[xxxxName]`/`[xxxxValue]`/`[xxxxDenotation]`, `ObjectLinks`, `LastEDS`, `UploadFile`/`DownloadFile`, `Denotation`, `ParamRefd`, `ConnectedModules`, Annex A (`CANopenSafetySupported`, `SRDOMapping`, `InvertedSRAD`), `[Tools]`/`[ToolX]`, Node-ID-Bereich 1..127 im Validator, Längenlimits 241/243/244/245/246/249.

### 1.2 CiA 311 v1.1.0 (XDD/XDC)

Die README bewirbt "CiA 311 v1.1 Compliant"; arc42 spricht korrekt von einem "Subset". Der Abgleich gegen Kapitel 6.4/6.5 und das normative XSD (Annex A) zeigt:

| Nr. | Schwere | Spec-Stelle | Befund | Code | Status |
|---|---|---|---|---|---|
| X7 | **Hoch** | §6.5.2.3.2.1 Tab. 48/49: bei `uniqueIDRef` "shall be defined by the referenced element in the application process part" | `uniqueIDRef` auf `CANopenObject`/`CANopenSubObject` wird komplett ignoriert. `ApplicationProcess` wird zwar geparst (311 Parameter im Corpus-File), aber nicht aufgelöst. Beim CANopenNode-Corpus `basicDevice.xdd` (343 Referenzen) fehlen dadurch DataType/AccessType/DefaultValue aller Objekte; die EDS-Konvertierung liefert `DataType=0x0` für z. B. 1018sub1 (Vendor-ID). Der Corpus-Test bemerkt das nicht, weil er nur "strukturell stabilen Round-Trip" prüft. | `Parsers/XddCommNetProfileParser.cs:101-191` (kein `uniqueIDRef`), `Parsers/XddApplicationProcessParser.cs` | verifiziert |
| X3 | **Hoch** | §6.5.2.5.2 Tab. 51 / XSD: `dynamicChannel` mit `accessType ∈ {readOnly, writeOnly, readWriteOutput}`, `maxNumber` und `addressOffset` **required** | Writer schreibt `accessType="ro"` und lässt `maxNumber`/`addressOffset` weg (schema-invalid); Reader lehnt das spec-konforme `readOnly` im Strict-Modus ab und mappt lenient auf `ro`. Zusätzlich wird das nicht existierende Attribut `pDOmappingIndex` gelesen/geschrieben. | `Writers/XddProfileBuilder.cs:104-129`, `Parsers/XddCommNetProfileParser.cs:417-449` | verifiziert |
| X1 | Mittel | Annex A: `targetNamespace="http://www.canopen.org/xml/1.1"` | Erzeugte XDD/XDC-Dokumente haben **keinen Default-Namespace** (`<ISO15745ProfileContainer xmlns:xsi=…>`), validieren also nicht gegen das CiA-311-Schema. Der Reader matcht per LocalName und ist davon unabhängig. | `Writers/XddWriter.cs:182-192` | verifiziert |
| X2 | Mittel | Tab. 48/49: `objFlags` ist `xsd:hexBinary` "four hex digits" | Reader parst `objFlags` dezimal (`"0010"` → 10 statt 16), Writer schreibt dezimal und ungerade Stellenzahl (`objFlags="3"`, hexBinary verlangt gerade Ziffernzahl). | `Parsers/XddCommNetProfileParser.cs:122-129`, `Writers/XddWriter.cs:297-299` | verifiziert |
| X6 | Mittel | Tab. 59 / XSD: `deviceCommissioning` mit `nodeName`, `actualBaudRate`, `networkName` **required** | XdcWriter lässt leere Werte weg: `<deviceCommissioning nodeID="5" networkNumber="1" CANopenManager="false"/>` ist schema-invalid. | `Writers/XdcWriter.cs:236-253` | verifiziert |
| X5 | Mittel | Tab. 5/45: `fileVersion` ist `xsd:string` ("vendor specific") | `fileVersion="1.0"` wird im Strict-Modus abgelehnt, lenient mit Warnung auf den Major-Teil reduziert. CiA 311 erlaubt hier jede Zeichenkette; die Unsigned8-Interpretation stammt aus CiA 306. | `Parsers/XddDeviceProfileParser.cs:27-86` | verifiziert |
| X4 | Mittel | Tab. 55/56: gültige Werte enthalten `100 Kbps` und `auto-baudRate` | Beide werden abgelehnt (strict: Exception, lenient: 0 + Diagnostic). | `Parsers/XddParsingPrimitives.cs:237-279` | verifiziert |
| X8 | Mittel | §6.4.2 DeviceIdentity (`orderNumber`, `version`), §6.5.4.2/6.5.4.3 (`selfStartingDevice`, `SDORequestingDevice`, `flyingMaster`, `SDOManager`, `configurationManager`, `layerSettingServiceMaster`) | `OrderCode`, `RevisionNumber` und `Comments` gehen bei EDS→XDD→EDS verloren; die genannten CANopen*Features-Attribute werden weder gelesen noch geschrieben (XDD→XDD-Verlust). | `Writers/XddProfileBuilder.cs:74-83,134-159`, `Parsers/XddDeviceProfileParser.cs:104-125` | verifiziert |
| X9 | Mittel | 306-1 Tab. 4 / CiA 301: 1018h ist mandatory | XDD-Reader klassifiziert nur 1000h/1001h als mandatory; `CanOpenValidationOptions.Strict` meldet für **jedes** gelesene XDD "0x1018 must be listed in MandatoryObjects". Interner Widerspruch Reader ↔ Validator. | `Parsers/XddCommNetProfileParser.cs:300-322` vs. `Validation/CanOpenModelValidator.cs:35` | verifiziert |
| X10 | Niedrig | Tab. 5: `fileCreator`, `fileCreationDate` required; `fileCreationTime` ist `xsd:time` | Writer lässt leere Pflichtattribute weg; `CreationTime` im EDS-Format `hh:mmAM` wird unkonvertiert als `xsd:time` geschrieben und umgekehrt. | `Writers/XddProfileBuilder.cs:41-69`, `Parsers/XddDeviceProfileParser.cs:92-99` | Code |
| X11 | Niedrig | Tab. 48/49: `accessType` nur `const/ro/wo/rw` | Reader akzeptiert zusätzlich `rwr`/`rww` (lenient, sinnvoll); Writer mappt korrekt auf `rw`. | `Parsers/XddParsingPrimitives.cs:112-136` | Code |
| X12 | Info | §6.5.1: unbekannte Kinder des CommunicationNetwork-ProfileBody | Werden nur als Attribut-Dictionary in `AdditionalSections` gespiegelt und vom Writer nicht re-emittiert (in arc42 §8.4 korrekt dokumentiert, in `docs/index.html` falsch als Round-Trip beworben). | `Parsers/XddCommNetProfileParser.cs:63-81` | Code |

Empfehlung zur Außendarstellung: "CiA 311 Compliant" in README/`docs/index.html` durch "CiA 311 Subset (kein Schema-Validierungsziel)" ersetzen, bis X1/X2/X3/X6/X7 behoben sind, oder einen Schema-Validierungstest gegen das CiA-311-XSD einführen.

---

## 2. Bibliothek: Korrektheit und Robustheit (unabhängig von der Spec)

| Nr. | Schwere | Befund | Code | Status |
|---|---|---|---|---|
| L1 | **Hoch** | **Unreferenzierte Objekt-Sektionen und hex-artige Sektionsnamen gehen still verloren.** `IsKnownSection` behandelt jeden 1-4-stelligen Hex-Namen als Objektsektion (`[Face]`, `[Bad]`, `[Add]`, `[2000]`). Ist der Index in keiner Objektliste, wird die Sektion weder geparst noch in `AdditionalSections` bewahrt; 0 Diagnostics. | `Parsers/CanOpenReaderBase.cs:587-605`, `:160-181` | verifiziert |
| L2 | **Hoch** | **Kein Encoding-Handling.** Dateien werden als UTF-8 dekodiert; Windows-1252/Latin-1 (in älteren Hersteller-EDS häufig) wird still zu U+FFFD (`Gerät` → `Ger�t`), 0 Diagnostics, und beim Zurückschreiben dauerhaft zerstört. `CanOpenFileOptions` hat keine Encoding-Option. | `Parsers/IniParser.cs:86-87,179,204`, `Parsers/SecureXmlParser.cs:103-108` | verifiziert |
| L3 | **Hoch** | **Writer escaped keine Steuerzeichen.** `ParameterName = "Evil\n[FileInfo]\nFileName=pwned.eds"` passiert `CanOpenWriteOptions.Validated` und erzeugt beim Wiedereinlesen `FileName=pwned.eds`. Keys mit `=` (RemainingEntries) werden beim Lesen an der ersten `=`-Position gespalten. Bewertung: Datenintegrität, bei Fremddaten auch Injection. | `Writers/IniWriterBase.cs:16`, Validator ohne Prüfung | verifiziert |
| L4 | Mittel | **Malformierte Sektionsköpfe leiten Keys in die vorige Sektion.** `[2000] ; manufacturer` (Trailing-Kommentar) oder `[2000` erfüllt `EndsWith(']')` nicht, die Zeile wird still verworfen und alle folgenden Keys überschreiben `[1000]`. Ebenso werden Zeilen ohne `=` auch im Strict-Modus still ignoriert; doppelte Sektionsköpfe werden ohne Diagnostic gemerged. | `Parsers/IniParser.cs:469-484` | verifiziert |
| L5 | Mittel | **Zeilennummern aus `ReadString` stimmen nicht.** `ParseString` entfernt leere Zeilen vor dem Zählen; dieselbe Datei liefert per Stream Zeile 5, per String Zeile 3. Diagnostics/Exceptions aus `ReadString` zeigen auf falsche Zeilen (im Code als bekannt kommentiert). | `Parsers/IniParser.cs:249-254` | verifiziert |
| L6 | Mittel | `File.WriteAllText`/`FileMode.Create` ohne Temp-Datei + Rename: bricht ein Write ab, bleibt eine verkürzte Zieldatei zurück. | `Writers/EdsWriter.cs:28`, `DcfWriter.cs:28`, `CpjWriter.cs:26`, `XddWriter.cs:30`, `XdcWriter.cs:47`, `Utilities/TextFileIo.cs:54-61` | Code |
| L7 | Mittel | `AdditionalSections` verlieren Reihenfolge: Sektionen werden alphabetisch und immer am Dateiende geschrieben, Keys innerhalb alphabetisch sortiert (`Zeta`,`Alpha` → `Alpha`,`Zeta`). Widerspricht der README-Aussage "round-trip fidelity". | `Writers/IniWriterBase.cs:568-578`, `EdsWriter.cs:187-195` | verifiziert |
| L8 | Niedrig | `ConvertToDcf` und `EdsCanOpenOperations` nutzen `$"{nodeId}"`-Interpolation für Zahlen entgegen der eigenen Regel in `copilot-instructions.md`; für `byte`/`uint` ohne Kultureffekt, aber inkonsistent (12 Stellen im Kern). | `EdsCanOpenOperations.cs:86`, `Utilities/ValueConverter.cs:436` u. a. | Code |
| L9 | Niedrig | Zeilenende hängt von der Plattform ab (`AppendLine` → CRLF auf Windows, LF auf Linux). Spec erlaubt beides, aber die Ausgabe ist nicht deterministisch über CI-Runner hinweg. | `Writers/IniWriterBase.cs` | Code |
| L10 | Niedrig | `EdsWriter.WriteObjects` ruft eine `protected`-Instanzmethode über ein statisches `Instance`; die CONTRIBUTING-Regel "kein Instanzzustand in IniWriterBase" existiert nur wegen dieser Konstruktion. `WriteObject` könnte statisch sein. | `Writers/EdsWriter.cs:15,207`, `DcfWriter.cs:15,409` | Code |
| L11 | Niedrig | `null`-Modell an `WriteFile` liefert `EdsWriteException` mit innerer `NullReferenceException` statt `ArgumentNullException`. | `Writers/EdsWriter.cs:23-38` | Code |
| L12 | Niedrig | Fünf nahezu identische `WriteFile/WriteStream/WriteFileAsync/WriteStreamAsync`-Blöcke (EDS/DCF/CPJ/XDD/XDC) und drei `WriteSection`/`ThrowIfNull`-Kopien; ein generischer Basistyp würde ~300 Zeilen sparen. | `Writers/*` | Code |
| L13 | Niedrig | Formatartefakte: 25 XML-Doc-Kommentare in `CanOpenFile.cs` mit 8-Space-Einzug und 4 Leerzeilen vor dem `[Obsolete]`-Attribut (Zeilen 409-413 u. a.); zwei Zeilen mit Trailing-Whitespace trotz `.editorconfig`; drei deutsche Kommentare in einer sonst englischen Codebasis. | `CanOpenFile.cs:407-493`, `FormatCanOpenOperations.cs:121,329,356,376-381` | verifiziert |
| L14 | Info | Positiv: `ByteLimitingStream` (+1-Overshoot-Logik, `long.MaxValue`-Schutz), `SecureXmlParser` (DTD Prohibit, Resolver null, `MaxCharactersInDocument`, Tiefenlimit 64), `StrictParsingScope`/`ParseDiagnosticScope` als `AsyncLocal`, `OrderedStringDictionary` mit vollständiger `IDictionary`-Implementierung, `CanOpenValueConverter` mit exakter 24/40/48/56-Bit-Bereichsprüfung und `$NODEID`-Signed-Arithmetik. | | |

---

## 3. CI / Release-Pipeline

Sub-Review mit Installation des gelockten `package-lock.json` und Lektüre der semantic-release-Quellen; die beiden wichtigsten Punkte habe ich selbst gegen `semantic-release/lib/branches/normalize.js` nachgeprüft.

| Nr. | Schwere | Befund | Datei | Status |
|---|---|---|---|---|
| CI-1 | **Hoch** | `ensure_git_notes` schreibt `{"channels":["beta"]}`; semantic-release verwendet als Channel den **Branch-Namen** (`develop`, normalize.js:97). Ein reparierter Tag ist für semantic-release unsichtbar; der nächste develop-Lauf plant dieselbe Version erneut, findet den Tag, sieht die (falsche) Note und endet mit Exit 0, bei jedem Push. Der Pfad ist unabhängig von `RELEASE_VERIFY_OBSERVE_ONLY` erreichbar (tag-exists-Branch). | `tools/semantic-release-run.sh:82-90` | verifiziert |
| CI-2 | Mittel | develop→main-Release-PRs haben normalerweise einen `[skip ci]`-HEAD (letzter Beta-Release-Commit), GitHub überspringt dann alle `pull_request`-Jobs, also auch `apicompat`, `npm-lockfile`, `breaking-intent`. Kommentar in `build.yml` und CONTRIBUTING beschreiben einen Check, der im Normalfall nicht läuft (verifiziert an PR #565/#529-Historie). | `.github/workflows/build.yml:200-218` | Code/Historie |
| CI-3 | Mittel | Der Workflow postet selbst einen Status mit Kontext `codecov/patch`, wartet dafür bis 4 min auf Codecov und überschreibt dessen Urteil mit dem 95-%-Gesamtgate; der Name ist irreführend, die Wartezeit unnötig. | `build.yml:88-149,466-488` | Code |
| CI-4 | Mittel | Coverage-Gate durchsucht das gesamte Checkout nach `coverage.cobertura.xml`; eine eingecheckte Datei mit hohen Zählern hebt das Gate aus. Zudem fließt der net10.0-Report des `EdsDcfNet.Checker` (ProjectReference) mit ein. | `tools/enforce-coverage-threshold.sh:38` | Code |
| CI-5 | Mittel | `release-notes-generator` hat keine `parserOpts.noteKeywords`, matcht daher `BREAKING-CHANGE` per Default; genau das hat in 1.13.0 eine falsche "⚠ BREAKING CHANGES"-Sektion im CHANGELOG erzeugt (Commit `fffcb2c`), während der Analyzer korrekt kein Major-Bump machte. | `.releaserc.json:31-58`, `CHANGELOG.md` 1.13.0 | verifiziert (CHANGELOG) |
| CI-6 | Mittel | Repository-weite Concurrency-Group kann ein wartendes main-Release durch einen develop-Push verdrängen (im Kommentar selbst dokumentiert). Ursache ist, dass develop stabile Tags "repariert". | `.github/workflows/semantic-release.yml:18-48`, `tools/semantic-release-run.sh:491` | Code |
| CI-7 | Mittel | Committen von `CHANGELOG.md` + `<Version>` auf beiden Kanälen erzwingt einen main→develop-Back-Merge, der nirgends dokumentiert ist (Historie: mehrere "Merge branch 'main' into develop"). | `.releaserc.json:76-83`, CONTRIBUTING | Historie |
| CI-8 | Mittel | `tools/npm-cli-stub` (425 Zeilen Windows-Shim-Logik) ersetzt das npm-Paket für einen Pfad, den CI nie ausführt; der Rekursionsschutz ist genau im `npm run`-Kontext deaktiviert. Ein Fail-Fast-Stub genügt. | `tools/npm-cli-stub/lib/forward.js:186-194` | Code |
| CI-9..16 | Niedrig | `releasedLabels` wirkungslos bei `successComment:false`; `issues: write`/`pull-requests: write` und `statuses/checks: write` breiter als nötig; Step-Output in Bash-Body interpoliert; Codecov-Upload als harter Release-Blocker; `RELEASE_VERIFY_OBSERVE_ONLY` dauerhaft 1 ohne Schalter; ApiCompat-Tool 10.0.400 vs. global.json 10.0.401; awk-Vergleich nicht locale-fest; Breaking-Intent-Gate setzt Merge-Commits voraus (Squash würde `feat!:` verstecken). | diverse | Code |

Positiv: Alle Actions SHA-gepinnt, `persist-credentials: false`, ApiCompat vergleicht beide TFMs in korrekter Richtung, `versionsort.suffix`-Handling korrekt, jq-Gate mit eigener Fixture-Testsuite, `npm-lockfile`-Job reproduziert den Release-Installschritt.

---

## 4. Tests

Eigener Lauf: 2376 Tests, 0 Fehler, 26 s (net10.0). Coverage netto 99,35 % Zeilen / 98,71 % Branches; niedrigste Klassen `CanOpenFile` (72,8 %, Obsolete-Facade) und `EdsWriter.WriteFileAsync` (63,6 %). Der geplante vertiefte Test-Review-Agent wurde durch die Session-Unterbrechung abgebrochen; die folgenden Punkte stammen aus eigener Sichtung.

| Nr. | Schwere | Befund |
|---|---|---|
| T1 | **Hoch** | **Jeder in Teil 1 und 2 verifizierte Verstoß passiert die Suite.** Hohe Coverage misst hier Ausführung, nicht Spezifikationstreue. Konkrete Lücken ohne Test: leeres `ObjectType=`, `[MxSubExtends]`/`[MxSubExt*]`/`[MxComments]`-Round-Trip, `PPOffset=0, 1`, OCTET_STRING-Formatierung ohne `0x`, unbekannte `[DeviceInfo]`-Keys, `uniqueIDRef`-Auflösung, XDD-Namespace/Schema-Validität, `readOnly`-dynamicChannel, `auto-baudRate`, Encoding-Fallback, Steuerzeichen im Writer, unreferenzierte Objektsektionen. |
| T2 | Mittel | `RealWorldCorpusTests` prüft nur "strukturell stabilen" Round-Trip und Diagnostics-Snapshots. Für `basicDevice.xdd` wird damit ein Modell mit 300+ leeren DataTypes als korrekt eingefroren (X7). Ein Snapshot des **Modells** (z. B. DataType/AccessType pro Objekt) würde das sofort aufdecken. |
| T3 | Mittel | Es gibt keinen XSD-Validierungstest für erzeugte XDD/XDC gegen das CiA-311-Schema (das Schema liegt in Annex A der Spec vor). |
| T4 | Niedrig | Sehr große Testdateien (`DcfReaderTests` 3119, `XddReaderTests` 2757, `EdsReaderTests` 2706 Zeilen) und drei Test-Kopien der Facade (`CanOpenFileTests`, `FormatCanOpenOperationsTests`, `EdsCanOpenOperationsTests`) für dieselbe Delegation. |
| T5 | Niedrig | Tests des Beispielprojekts (`tests/EdsDcfNet.Tests/Checker/*`, 2762 Zeilen) machen den "Example"-Checker faktisch zu einem Produktbestandteil (siehe Teil 6). |
| T6 | Info | Positiv: Corpus mit Lizenz/NOTICE, Diagnostics-Snapshots mit `UPDATE_CORPUS_SNAPSHOTS`, Parameter-Namen-Baseline gegen Named-Argument-Brüche, `ModelClonerCompletenessTests` per Reflection, net48-Host für die `#else`-Pfade, Thread-Saturation-Tests. |

---

## 5. Dokumentation (Sub-Review, alle Zitate gegen Quelle geprüft)

| Nr. | Schwere | Befund | Stelle |
|---|---|---|---|
| D-H1 | **Hoch** | **CiA-Spezifikations-PDF ist eingecheckt** (`docs/cia/306v01030002.pdf`, 366 KB, seit Commit `40355d2`). `.gitignore` ignoriert explizit die anderen vier CiA-PDFs, dieses rutschte durch. Urheberrechtlich nicht unter MIT verteilbar; zusätzlich ist es v1.3.0, während README v1.4.0 beansprucht. | `docs/cia/`, `.gitignore:428-433`, `README.md:855,883` |
| D-H2 | **Hoch** | `.github/copilot-instructions.md` (per CLAUDE.md "single source of truth") beschreibt das Projekt als EDS/DCF-only und behauptet, nur Default-Overloads seien `[Obsolete]`; tatsächlich sind alle 111 Legacy-Overloads obsolet und es gibt fünf Formate. | `copilot-instructions.md:5,38-45` |
| D-M1 | Mittel | arc42 §2 und ADR-4 listen `string.Contains(char)` usw. als "nicht verfügbar", obwohl Polyfill 11.4.0 genau diese liefert und der Code sie nutzt. | `docs/architecture/02-constraints.md:15-23`, `09-architecture-decisions.md:99` |
| D-M2 | Mittel | `docs/index.html`: nicht deployt (`has_pages: false`), Fallback-Version 1.9.1, behauptet fälschlich XDC-Round-Trip für AdditionalSections. | `docs/index.html:299` |
| D-M3 | Mittel | `docs/tech-stack-canvas.md`: C# 12/13 statt 14, coverlet 8.0.0 statt 10.0.1, ubuntu-latest/.NET 8 statt windows-latest/net48, Node 22 statt 24, nur obsolete API gelistet. | Zeilen 55,124,137,138,76-100 |
| D-M4 | Mittel | README "CiA DS 306 v1.4 / CiA 311 v1.1 **Compliant**" vs. arc42 "Subset"; nach Teil 1 ist "Compliant" für beide Normen derzeit nicht haltbar. | `README.md:38` |
| D-M5 | Mittel | arc42 §11 nennt ein einziges Technical-Debt-Item; mindestens acht weitere sind im Code/anderen Kapiteln dokumentiert (Obsolete-Facade, Oktal-Entscheidung #411, XDD-AdditionalSections, ApplicationProcess nur XDD, Thread-Tests nur EDS/XDD, statisches Writer-`Instance`, Tests nicht auf `AnalysisMode Recommended`, BASELINE.md leer). | `docs/architecture/11-risks-and-technical-debt.md` |
| D-M6 | Mittel | README ist mit 893 Zeilen als NuGet-`PackageReadmeFile` zu lang; Migration-Guide, Options-Extension-Pattern und Thread-Safety-Interna gehören nach `docs/`. | `README.md` |
| D-L | Niedrig | ~20 Detail-Drifts: "10 MB" statt 10 MiB, C# 13 statt 14, fehlendes `using EdsDcfNet;` in einem Sample, Test-README mit alten Paketversionen, arc42-Diagramme mit obsoleten Methoden und fehlender `WriteException`-Basisklasse, CONTRIBUTING ohne den "regular merge, never squash"-Hinweis, Branch-Protection-Tabelle ohne `codecov/patch`, BASELINE.md seit März "capture pending". | siehe Sub-Review |

Positiv: README-Codebeispiele kompilieren gegen die aktuelle API; Thread-Safety-Aussagen in README, CONTRIBUTING, XML-Doku und arc42 §8.8 sind konsistent und stimmen mit dem Code; SECURITY.md-Aussagen (10 MiB, Tiefenlimit 64, keine Runtime-Dependencies) verifiziert; `sbom/README.md` stimmt Flag für Flag mit dem Publish-Skript.

---

## 6. Beispiele, Checker, Benchmarks (Sub-Review)

| Nr. | Schwere | Befund | Stelle |
|---|---|---|---|
| E-1 | Mittel | `edsdcf-check` ist kein Beispiel mehr: 2500 Zeilen Regel-Engine, eigene Tests (2762 Zeilen), ProjectReference aus der Testsuite, `fix(checker)`-Commits bumpen die Library-Version, `[assembly: ExcludeFromCodeCoverage]` versteckt es vor dem Gate. Entscheidung nötig: als `src/EdsDcfNet.Checker` (dotnet tool) promoten oder Regel-Engine in `EdsDcfNet.Validation` ziehen. | `examples/EdsDcfNet.Checker/*` |
| E-2 | Mittel | Der Checker kopiert Library-Internals wörtlich (`TryParseCompactListSubIndex`, `IsHexDigits`, `TrySplitToolingMajorMinor`, `DescribeType`, `ComparableValue`, `ResolveNodeIds`, Längenkonstanten) und ist per Kommentar an Reader-Internas gekoppelt; die fünf `fix(checker)`-Commits in 1.14.0 sind genau dieses Nachziehen. | `RawObjectChecker.cs:1473-1663`, `MandatoryFieldsChecker.cs:408-468` |
| E-3 | Mittel | Erste unlesbare Datei im Verzeichnis-Sweep beendet den Lauf mit Exit 2 ohne Report für die übrigen Dateien; `EnumerateFiles` ohne `IgnoreInaccessible`. | `Program.cs:83-129` |
| E-4 | Mittel | Bei beiden `DeviceCom(m)issioning`-Schreibweisen prüft der Checker eine andere NodeID als der Reader lädt. | `RawObjectChecker.cs:109` vs. `DcfReader.cs:252-256` |
| E-5 | Niedrig | Checker behandelt `#` als Kommentar (Reader nicht); Objekt mit ungültigem ObjectType und Sub-Sektionen wird gar nicht geprüft; DOMAIN ohne `CheckAccessType`; `Severity.Info` ist toter Code. | diverse |
| E-6 | Mittel | `examples/sample_device.eds` wird von `EdsDcfNet.Examples` nie gelesen; Beispiel 4 modelliert ein ungültiges 1018 (`SubNumber=4` bei zwei Sub-Objekten); kein CPJ/XDD/XDC/Async/Strict-Beispiel. | `examples/EdsDcfNet.Examples/Program.cs` |
| E-7 | Mittel | Benchmarks: kein CI-Hook, `BASELINE.md` seit 2026-03-06 "capture pending", nur String-Pfade gemessen, Fixture-Kopie mit Pre-#563-`SubNumber`-Zählung, drei `GetFixturePath`-Kopien. | `benchmarks/**` |

Positiv: Culture-Disziplin im Checker vollständig; Regeltreue zum Reader hoch (OBJ011/FFh/Compact-Keys/`$NODEID`); Examples nutzen ausschließlich die kanonische API.

---

## 7. Empfohlene Reihenfolge

1. **Spec-Blocker mit Datenverlust** (je eigener `fix:`-PR nach develop): X7 `uniqueIDRef`-Auflösung, S7 Modul-Sektionen (parsen + schreiben, sonst als `AdditionalSections` bewahren), S4 `PPOffset` als String/Tuple, L1 unreferenzierte Sektionen bewahren + Diagnostic, S2 Rest-Entries für alle Standardsektionen.
2. **Writer-Korrektheit**: L3 Steuerzeichen validieren/ablehnen, S3 Tab.-7-Matrix im Writer, S11 SubNumber = Count, S5 OCTET_STRING ohne `0x`, S1a/b/c Default-Werte.
3. **XDD-Schema**: X1 Namespace, X2 objFlags hexBinary, X3 dynamicChannel-Attribute, X6/X10 Pflichtattribute, X4/X5 Wertebereiche, X9 1018 mandatory; dazu ein XSD-Validierungstest (T3) und Modell-Snapshots im Corpus-Test (T2).
4. **Pipeline**: CI-1 Channel-Note (`GITHUB_REF_NAME`), CI-5 `parserOpts` für den Notes-Generator, CI-2 `[skip ci]` durch `if:`-Guard ersetzen, CI-4 Suchpfad des Coverage-Gates einschränken.
5. **Repo-Hygiene**: D-H1 PDF entfernen (inkl. History-Rewrite), D-H2/D-M1 Instruktionsdateien korrigieren, README-Claim "Compliant" anpassen, L2 Encoding-Option in `CanOpenFileOptions`, E-1 Checker-Entscheidung.

Nicht behandelt (out of scope oder nicht anwendbar): CiA 306-2 (CODB-Profildatenbank) wird von der Bibliothek nicht implementiert; ein Abgleich war daher nicht möglich. Der net48-Testlauf konnte auf Linux nicht ausgeführt werden.
