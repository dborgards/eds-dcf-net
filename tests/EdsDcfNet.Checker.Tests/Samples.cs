namespace EdsDcfNet.Checker.Tests;

/// <summary>Sample files shared by the CLI tests.</summary>
internal static class Samples
{
    /// <summary>An EDS without findings, with or without the library pass.</summary>
    public const string CleanEds = """
        [FileInfo]
        FileName=a.eds
        FileVersion=1
        FileRevision=0
        EDSVersion=4.0
        Description=Sample
        CreationTime=10:00AM
        CreationDate=01-01-2026
        CreatedBy=Me
        ModificationTime=10:00AM
        ModificationDate=01-01-2026
        ModifiedBy=Me

        [DeviceInfo]
        VendorName=Vendor
        VendorNumber=0x10
        ProductName=Product
        ProductNumber=0x20
        RevisionNumber=0x30
        OrderCode=OC
        BaudRate_10=0
        BaudRate_20=0
        BaudRate_50=0
        BaudRate_125=0
        BaudRate_250=1
        BaudRate_500=0
        BaudRate_800=0
        BaudRate_1000=0
        SimpleBootUpMaster=0
        SimpleBootUpSlave=1
        Granularity=8
        DynamicChannelsSupported=0
        GroupMessaging=0
        NrOfRXPDO=0
        NrOfTXPDO=0
        LSS_Supported=0

        [MandatoryObjects]
        SupportedObjects=3
        1=0x1000
        2=0x1001
        3=0x1018

        [1000]
        ParameterName=Device type
        ObjectType=0x7
        DataType=0x0007
        AccessType=ro
        DefaultValue=0
        PDOMapping=0

        [1001]
        ParameterName=Error register
        ObjectType=0x7
        DataType=0x0005
        AccessType=ro
        DefaultValue=0
        PDOMapping=0

        [1018]
        ParameterName=Identity
        ObjectType=0x9
        SubNumber=4

        [1018sub0]
        ParameterName=Highest sub-index
        ObjectType=0x7
        DataType=0x0005
        AccessType=ro
        DefaultValue=3
        PDOMapping=0

        [1018sub1]
        ParameterName=Vendor-ID
        ObjectType=0x7
        DataType=0x0007
        AccessType=ro
        DefaultValue=0x10
        PDOMapping=0

        [1018sub2]
        ParameterName=Product code
        ObjectType=0x7
        DataType=0x0007
        AccessType=ro
        DefaultValue=0x20
        PDOMapping=0

        [1018sub3]
        ParameterName=Revision number
        ObjectType=0x7
        DataType=0x0007
        AccessType=ro
        DefaultValue=0x30
        PDOMapping=0

        """;

    /// <summary>
    /// <see cref="CleanEds"/> plus a duplicate key (INI003, warning), an out-of-range value
    /// (VAL001, error) and a leading-zero value (VAL005, warning).
    /// </summary>
    public const string Findings = CleanEds + """

        [Comments]
        Lines=0
        Lines=0

        [2000]
        ParameterName=Setting
        ObjectType=0x7
        DataType=0x0005
        AccessType=rw
        DefaultValue=1000
        PDOMapping=0

        [2001]
        ParameterName=Octal
        ObjectType=0x7
        DataType=0x0005
        AccessType=rw
        DefaultValue=010
        PDOMapping=0

        """;
}
