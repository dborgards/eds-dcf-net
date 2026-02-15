# 12. Glossary

| Term                          | Description                                                                                      |
|-------------------------------|--------------------------------------------------------------------------------------------------|
| **CANopen**                   | Communication protocol and device profile specification for embedded systems based on CAN (Controller Area Network). |
| **CAN (Controller Area Network)** | Serial bus system for networking control units, widely used in vehicles and industrial automation. |
| **CiA**                       | CAN in Automation e.V. -- international manufacturer and user association that maintains the CANopen specifications. |
| **CiA DS 306**                | Specification for Electronic Data Sheets (EDS) and Device Configuration Files (DCF) for CANopen devices. |
| **EDS (Electronic Data Sheet)** | Device description file in INI format that defines the communication capabilities and configurable parameters of a CANopen device. Serves as a template. |
| **DCF (Device Configuration File)** | Configured instance of an EDS file for a specific network node. Contains concrete parameter values, node ID, and baud rate. |
| **Object Dictionary (OD)**    | Central data object directory of a CANopen device. Each entry is addressable by a 16-bit index and optional 8-bit sub-index. |
| **Node ID**                   | Unique identifier (1-127) of a CANopen device on the network.                                    |
| **Baud rate**                 | Transmission speed on the CAN bus (typical values: 125, 250, 500, 1000 kbit/s).                  |
| **PDO (Process Data Object)** | Real-time data object for fast exchange of process data between CANopen devices.                  |
| **TPDO (Transmit PDO)**       | PDO sent by a device.                                                                            |
| **RPDO (Receive PDO)**        | PDO received by a device.                                                                        |
| **SDO (Service Data Object)** | Access mechanism to the Object Dictionary of a CANopen device for configuration and diagnostics.  |
| **SRDO (Safety-Relevant Data Object)** | Safety-relevant data object per EN 50325-5 (CANopen Safety).                             |
| **NULL**                      | Object Dictionary object type (0x00) indicating an object with no data fields.                    |
| **DOMAIN**                    | Object Dictionary object type (0x02) for arbitrary-length binary data (e.g. firmware transfer).   |
| **DEFTYPE**                   | Object Dictionary object type (0x05) for standard or vendor-specific data type definitions.       |
| **DEFSTRUCT**                 | Object Dictionary object type (0x06) for structure type definitions (can have sub-objects).        |
| **VAR (Variable)**            | Object Dictionary object type (0x07) for a single variable.                                      |
| **ARRAY**                     | Object Dictionary object type (0x08) for an array with homogeneous sub-objects.                   |
| **RECORD**                    | Object Dictionary object type (0x09) for a structure with heterogeneous sub-objects.              |
| **INI format**                | Simple text format with sections (`[Section]`) and key-value pairs (`Key=Value`). Basis for EDS/DCF files. |
| **$NODEID formula**           | Expression in DCF files (e.g., `$NODEID+0x600`) evaluated relative to the device's node ID.      |
| **CompactSubObj**             | Compact representation of sub-objects where not every sub-object needs to be defined individually. |
| **CompactPDO**                | Compact storage of PDO value and denotation information in dedicated sections.                    |
| **Round-trip fidelity**       | Property that a file does not lose information after being read and written back unchanged.        |
| **Semantic Versioning (SemVer)** | Versioning scheme with the format `MAJOR.MINOR.PATCH` that communicates the nature of changes. |
| **Conventional Commits**      | Convention for commit messages (`type(scope): description`) enabling automated versioning.        |
| **NuGet**                     | Package manager for .NET libraries. EdsDcfNet is published as a NuGet package.                    |
| **Source Link**               | Technology that links the debugger directly to source code on GitHub.                             |
| **LSS (Layer Setting Services)** | CANopen service for configuring node ID and baud rate over the CAN bus.                        |
| **Bus coupler**               | Modular CANopen device serving as a carrier for pluggable I/O modules.                            |
| **Dynamic Channels**          | Mechanism per CiA 302-4 for programmable devices that can dynamically extend their Object Dictionary. |
