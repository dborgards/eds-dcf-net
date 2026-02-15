# 1. Introduction and Goals

## 1.1 Requirements Overview

EdsDcfNet is a C# library for reading and writing **CiA DS 306 EDS** (Electronic Data Sheet) and **DCF** (Device Configuration File) files for CANopen devices.

CANopen is a communication protocol for industrial automation systems based on the CAN bus. Every CANopen device is described by an EDS file that defines its communication capabilities and configurable parameters. A DCF file is a configured instance of an EDS file containing concrete values for a specific network node.

### Key Features

| Feature                      | Description                                                     |
|-----------------------------|-----------------------------------------------------------------|
| Read EDS                    | Complete parsing of Electronic Data Sheets                      |
| Read and write DCF          | Processing and generation of Device Configuration Files         |
| EDS-to-DCF conversion       | Conversion with configuration parameters (node ID, baud rate)   |
| Type safety                 | Fully typed models for all CANopen objects                      |
| Modular devices             | Support for bus couplers with pluggable modules                 |
| CiA DS 306 v1.4 compliant   | Implementation according to official specification              |

## 1.2 Quality Goals

The following table describes the core quality goals, sorted by priority:

| Priority | Quality Goal            | Description                                                                    |
|----------|-------------------------|--------------------------------------------------------------------------------|
| 1        | **Correctness**         | Specification-compliant processing of EDS/DCF per CiA DS 306 v1.4             |
| 2        | **Portability**         | Runs on all .NET platforms via `netstandard2.0` and `net10.0`                  |
| 3        | **Simplicity**          | Intuitive, easy-to-understand API with minimal learning curve                  |
| 4        | **Reliability**         | Robust error handling with meaningful error messages                           |
| 5        | **Independence**        | No external runtime dependencies (zero dependencies)                          |

## 1.3 Stakeholders

| Role                          | Expectation / Interest                                                      |
|-------------------------------|-----------------------------------------------------------------------------|
| **CANopen device developers**  | Reliable tool for reading and validating EDS/DCF files                     |
| **System integrators**         | Programmatic creation and modification of DCF files for networks           |
| **Tool vendors**               | Embeddable library for CANopen configuration tools                         |
| **Library maintainers**        | Maintainable, extensible codebase with clear architecture                  |
| **NuGet consumers**            | Stable API, semantic versioning, easy integration via package manager      |
