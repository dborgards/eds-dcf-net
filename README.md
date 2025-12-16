# EdsDcfNet

Eine umfassende, einfach zu bedienende C# .NET-Bibliothek für CiA DS 306 - Electronic Data Sheet (EDS) und Device Configuration File (DCF) für CANopen-Geräte.

## Features

✨ **Einfache API** - Intuitiver, fluent API-Stil für schnelle Integration
📖 **EDS lesen** - Vollständiges Parsen von Electronic Data Sheets
📝 **DCF lesen & schreiben** - Device Configuration Files verarbeiten und erstellen
🔄 **EDS zu DCF Konvertierung** - Einfache Umwandlung mit Konfigurationsparametern
🎯 **Type-Safe** - Vollständig typisierte Modelle für alle CANopen-Objekte
📦 **Modular** - Unterstützung für modulare Geräte (Bus-Koppler + Module)
✅ **CiA DS 306 v1.3 konform** - Implementiert nach offizieller Spezifikation

## Schnellstart

### EDS-Datei lesen

```csharp
using EdsDcfNet;

// EDS-Datei einlesen
var eds = CanOpenFile.ReadEds("device.eds");

// Geräteinformationen ausgeben
Console.WriteLine($"Device: {eds.DeviceInfo.ProductName}");
Console.WriteLine($"Vendor: {eds.DeviceInfo.VendorName}");
Console.WriteLine($"Product Number: 0x{eds.DeviceInfo.ProductNumber:X}");
```

### DCF-Datei lesen

```csharp
using EdsDcfNet;

// DCF-Datei einlesen
var dcf = CanOpenFile.ReadDcf("configured_device.dcf");

Console.WriteLine($"Node ID: {dcf.DeviceCommissioning.NodeId}");
Console.WriteLine($"Baudrate: {dcf.DeviceCommissioning.Baudrate} kbit/s");
```

### EDS zu DCF konvertieren

```csharp
using EdsDcfNet;

// EDS einlesen
var eds = CanOpenFile.ReadEds("device.eds");

// Zu DCF konvertieren mit Node-ID und Baudrate
var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 2, baudrate: 500, nodeName: "MyDevice");

// DCF speichern
CanOpenFile.WriteDcf(dcf, "device_node2.dcf");
```

### Mit Object Dictionary arbeiten

```csharp
using EdsDcfNet.Extensions;

var dcf = CanOpenFile.ReadDcf("device.dcf");

// Objekt abrufen
var deviceType = dcf.ObjectDictionary.GetObject(0x1000);

// Wert setzen
dcf.ObjectDictionary.SetParameterValue(0x1000, "0x00000191");

// PDO-Objekte durchsuchen
var tpdos = dcf.ObjectDictionary.GetPdoCommunicationParameters(transmit: true);
```

## API-Übersicht

### Hauptklasse: `CanOpenFile`

```csharp
// EDS lesen
ElectronicDataSheet ReadEds(string filePath)
ElectronicDataSheet ReadEdsFromString(string content)

// DCF lesen
DeviceConfigurationFile ReadDcf(string filePath)
DeviceConfigurationFile ReadDcfFromString(string content)

// DCF schreiben
void WriteDcf(DeviceConfigurationFile dcf, string filePath)
string WriteDcfToString(DeviceConfigurationFile dcf)

// EDS zu DCF konvertieren
DeviceConfigurationFile EdsToDcf(ElectronicDataSheet eds, byte nodeId,
                                  ushort baudrate = 250, string? nodeName = null)
```

## Unterstützte Features

- ✅ Vollständiges EDS-Parsing
- ✅ Vollständiges DCF-Parsing und Schreiben
- ✅ Alle Object Types (VAR, ARRAY, RECORD)
- ✅ Sub-Objekte und Sub-Indizes
- ✅ Compact Storage (CompactSubObj, CompactPDO)
- ✅ Object Links
- ✅ Modulares Gerätekonzept
- ✅ Hexadezimale, Dezimale und Oktale Zahlen
- ✅ Kommentare und zusätzliche Sektionen

## Beispiele

Vollständige Beispiele finden Sie im `examples/EdsDcfNet.Examples`-Projekt.

## Projektstruktur

```
eds-dcf-net/
├── src/
│   └── EdsDcfNet/              # Hauptbibliothek
│       ├── Models/             # Datenmodelle
│       ├── Parsers/            # EDS/DCF Parser
│       ├── Writers/            # DCF Writer
│       ├── Utilities/          # Helper-Klassen
│       ├── Exceptions/         # Custom Exceptions
│       └── Extensions/         # Extension Methods
├── examples/
│   └── EdsDcfNet.Examples/     # Beispielanwendung
└── docs/
    └── cia/                    # CiA DS 306 Spezifikation
```

## Anforderungen

- .NET 10.0 oder höher
- C# 12.0

## Lizenz

MIT License - siehe [LICENSE](LICENSE) Datei

## Spezifikation

Basiert auf **CiA DS 306 Version 1.3** (01. Januar 2005)
"Electronic data sheet specification for CANopen"

## Support

Bei Fragen oder Problemen:
- GitHub Issues: https://github.com/dborgards/eds-dcf-net/issues

---

**EdsDcfNet** - Professionelle CANopen EDS/DCF-Verarbeitung in C# .NET
