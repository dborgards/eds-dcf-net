using EdsDcfNet;
using EdsDcfNet.Extensions;
using EdsDcfNet.Models;
using System.Globalization;
namespace EdsDcfNet.Examples;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== EdsDcfNet Library Examples ===\n");

        // Example 1: Create a simple EDS programmatically
        Example1_CreateSimpleEds();

        // Example 2: Read EDS file (if it exists)
        Example2_ReadEds();

        // Example 3: Convert EDS to DCF
        Example3_EdsToDcf();

        // Example 4: Working with Object Dictionary
        Example4_ObjectDictionary();

        Console.WriteLine("\n=== Examples Complete ===");
    }

    static void Example1_CreateSimpleEds()
    {
        Console.WriteLine("Example 1: Create a simple EDS");
        Console.WriteLine("--------------------------------");

        var eds = new ElectronicDataSheet
        {
            FileInfo = new EdsFileInfo
            {
                FileName = "example_device.eds",
                FileVersion = 1,
                FileRevision = 0,
                EdsVersion = "4.0",
                Description = "Example CANopen Device",
                CreationDate = DateTime.Now.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture),
                CreationTime = DateTime.Now.ToString("hh:mmtt", CultureInfo.InvariantCulture),
                CreatedBy = "EdsDcfNet Example"
            },
            DeviceInfo = new DeviceInfo
            {
                VendorName = "Example Vendor",
                VendorNumber = 0x1234,
                ProductName = "Example IO Device",
                ProductNumber = 0x5678,
                RevisionNumber = 0x0001,
                OrderCode = "EX-IO-001",
                Granularity = 8,
                NrOfRxPdo = 4,
                NrOfTxPdo = 4,
                SimpleBootUpSlave = true
            }
        };

        // Set supported baud rates
        eds.DeviceInfo.SupportedBaudRates.BaudRate125 = true;
        eds.DeviceInfo.SupportedBaudRates.BaudRate250 = true;
        eds.DeviceInfo.SupportedBaudRates.BaudRate500 = true;
        eds.DeviceInfo.SupportedBaudRates.BaudRate1000 = true;

        // Add mandatory object 0x1000 (Device Type)
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7, // VAR
            DataType = 0x0007, // UNSIGNED32
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000000",
            PdoMapping = false
        };

        // Add mandatory object 0x1001 (Error Register)
        eds.ObjectDictionary.MandatoryObjects.Add(0x1001);
        eds.ObjectDictionary.Objects[0x1001] = new CanOpenObject
        {
            Index = 0x1001,
            ParameterName = "Error Register",
            ObjectType = 0x7, // VAR
            DataType = 0x0005, // UNSIGNED8
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0",
            PdoMapping = true
        };

        Console.WriteLine($"Created EDS for: {eds.DeviceInfo.ProductName}");
        Console.WriteLine($"Vendor: {eds.DeviceInfo.VendorName}");
        Console.WriteLine($"Mandatory Objects: {eds.ObjectDictionary.MandatoryObjects.Count}");
        Console.WriteLine();
    }

    static void Example2_ReadEds()
    {
        Console.WriteLine("Example 2: Read EDS file");
        Console.WriteLine("------------------------");

        // Create a sample EDS content
        var sampleEds = @"[FileInfo]
FileName=sample.eds
FileVersion=1
FileRevision=0
EDSVersion=4.0
Description=Sample Device
CreatedBy=Example

[DeviceInfo]
VendorName=Sample Vendor
VendorNumber=0x00000042
ProductName=Sample Device
ProductNumber=0x00001234
RevisionNumber=0x00010000
OrderCode=SAMPLE-001
BaudRate_125=1
BaudRate_250=1
BaudRate_500=1
BaudRate_1000=1
SimpleBootUpSlave=1
Granularity=8
NrOfRXPDO=2
NrOfTXPDO=2
LSS_Supported=0

[MandatoryObjects]
SupportedObjects=2
1=0x1000
2=0x1001

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x00000000
PDOMapping=0

[1001]
ParameterName=Error Register
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
";

        try
        {
            var eds = CanOpenFile.ReadEdsFromString(sampleEds);

            Console.WriteLine($"Device: {eds.DeviceInfo.ProductName}");
            Console.WriteLine($"Vendor: {eds.DeviceInfo.VendorName} (ID: 0x{eds.DeviceInfo.VendorNumber:X})");
            Console.WriteLine($"Product Number: 0x{eds.DeviceInfo.ProductNumber:X}");
            Console.WriteLine($"Mandatory Objects: {eds.ObjectDictionary.MandatoryObjects.Count}");
            Console.WriteLine($"  - 0x1000: {eds.ObjectDictionary.GetObject(0x1000)?.ParameterName}");
            Console.WriteLine($"  - 0x1001: {eds.ObjectDictionary.GetObject(0x1001)?.ParameterName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading EDS: {ex.Message}");
        }

        Console.WriteLine();
    }

    static void Example3_EdsToDcf()
    {
        Console.WriteLine("Example 3: Convert EDS to DCF");
        Console.WriteLine("------------------------------");

        var sampleEds = @"[FileInfo]
FileName=device.eds
FileVersion=1
FileRevision=0
EDSVersion=4.0

[DeviceInfo]
VendorName=Example Inc.
VendorNumber=0x100
ProductName=IO Module 4x4
ProductNumber=0x1001
RevisionNumber=0x1
OrderCode=IO-4X4-001
BaudRate_250=1
BaudRate_500=1
BaudRate_1000=1
SimpleBootUpSlave=1
Granularity=8
NrOfRXPDO=1
NrOfTXPDO=1
LSS_Supported=0

[MandatoryObjects]
SupportedObjects=2
1=0x1000
2=0x1001

[1000]
ParameterName=Device Type
ObjectType=0x7
DataType=0x0007
AccessType=ro
DefaultValue=0x00000191
PDOMapping=0

[1001]
ParameterName=Error Register
ObjectType=0x7
DataType=0x0005
AccessType=ro
DefaultValue=0
PDOMapping=1
";

        try
        {
            // Read EDS
            var eds = CanOpenFile.ReadEdsFromString(sampleEds);

            // Convert to DCF with Node ID 3 and 500 kbit/s
            var dcf = CanOpenFile.EdsToDcf(eds, nodeId: 3, baudrate: 500, nodeName: "IO_Module_Node3");

            Console.WriteLine($"Created DCF for Node {dcf.DeviceCommissioning.NodeId}");
            Console.WriteLine($"Node Name: {dcf.DeviceCommissioning.NodeName}");
            Console.WriteLine($"Baudrate: {dcf.DeviceCommissioning.Baudrate} kbit/s");
            Console.WriteLine($"Device: {dcf.DeviceInfo.ProductName}");

            // Generate DCF content
            var dcfContent = CanOpenFile.WriteDcfToString(dcf);
            Console.WriteLine($"\nDCF Content Length: {dcfContent.Length} bytes");
            Console.WriteLine("First 200 characters:");
            Console.WriteLine(dcfContent.Substring(0, Math.Min(200, dcfContent.Length)));
            Console.WriteLine("...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    static void Example4_ObjectDictionary()
    {
        Console.WriteLine("Example 4: Working with Object Dictionary");
        Console.WriteLine("------------------------------------------");

        var dcf = new DeviceConfigurationFile();

        // Add some objects
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000000",
            ParameterValue = "0x00000191" // Configured value
        };

        dcf.ObjectDictionary.OptionalObjects.Add(0x1018);
        dcf.ObjectDictionary.Objects[0x1018] = new CanOpenObject
        {
            Index = 0x1018,
            ParameterName = "Identity Object",
            ObjectType = 0x9, // RECORD
            SubNumber = 4
        };

        // Add sub-objects
        dcf.ObjectDictionary.Objects[0x1018].SubObjects[0] = new CanOpenSubObject
        {
            SubIndex = 0,
            ParameterName = "Number of Entries",
            DataType = 0x0005,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "4"
        };

        dcf.ObjectDictionary.Objects[0x1018].SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Vendor ID",
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly,
            DefaultValue = "0x00000100",
            ParameterValue = "0x00000100"
        };

        // Use extension methods
        Console.WriteLine("Using extension methods:");
        Console.WriteLine($"Object 0x1000 value: {dcf.ObjectDictionary.GetParameterValue(0x1000)}");

        var identityObj = dcf.ObjectDictionary.GetObject(0x1018);
        if (identityObj != null)
        {
            Console.WriteLine($"Object 0x1018: {identityObj.ParameterName}");
            Console.WriteLine($"  Sub-objects: {identityObj.SubObjects.Count}");

            var vendorIdSubObj = dcf.ObjectDictionary.GetSubObject(0x1018, 1);
            if (vendorIdSubObj != null)
            {
                Console.WriteLine($"  - {vendorIdSubObj.ParameterName}: {vendorIdSubObj.ParameterValue}");
            }
        }

        // Modify a value
        dcf.ObjectDictionary.SetParameterValue(0x1000, "0x00000192");
        Console.WriteLine($"\nModified 0x1000 value: {dcf.ObjectDictionary.GetParameterValue(0x1000)}");

        Console.WriteLine();
    }
}
