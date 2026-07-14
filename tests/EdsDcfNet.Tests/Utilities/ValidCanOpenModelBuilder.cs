namespace EdsDcfNet.Tests.Utilities;

using EdsDcfNet.Models;

/// <summary>
/// Builds minimally valid CANopen models for validation and write-guard tests.
/// </summary>
internal static class ValidCanOpenModelBuilder
{
    public static ElectronicDataSheet CreateValidEds()
    {
        var eds = new ElectronicDataSheet();
        eds.ObjectDictionary.MandatoryObjects.Add(0x1000);
        eds.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly
        };

        return eds;
    }

    public static DeviceConfigurationFile CreateValidDcf()
    {
        var dcf = new DeviceConfigurationFile
        {
            DeviceCommissioning = new DeviceCommissioning { NodeId = 5, Baudrate = 500 }
        };
        dcf.ObjectDictionary.MandatoryObjects.Add(0x1000);
        dcf.ObjectDictionary.Objects[0x1000] = new CanOpenObject
        {
            Index = 0x1000,
            ParameterName = "Device Type",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadOnly
        };

        return dcf;
    }

    public static NodelistProject CreateValidCpj()
    {
        var cpj = new NodelistProject();
        var network = new NetworkTopology { NetName = "Main Network" };
        network.Nodes[2] = new NetworkNode { NodeId = 2, Present = true, Name = "Node-2" };
        cpj.Networks.Add(network);
        return cpj;
    }
}
