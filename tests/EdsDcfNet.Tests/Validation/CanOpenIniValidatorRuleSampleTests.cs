namespace EdsDcfNet.Tests.Checker;

using EdsDcfNet.Validation;

/// <summary>
/// One minimal sample per rule family that has no dedicated test elsewhere: each sample adds a
/// single defect to a clean EDS/DCF and must report exactly the expected rule.
/// </summary>
public class CanOpenIniValidatorRuleSampleTests
{
    [Fact]
    public void Baseline_IsClean()
    {
        Describe(CanOpenIniValidator.ValidateString(Eds(), CanOpenIniFormat.Eds)).Should().BeEmpty();
        Describe(CanOpenIniValidator.ValidateString(Dcf(), CanOpenIniFormat.Dcf)).Should().BeEmpty();
    }

    public static TheoryData<string, CanOpenIniFormat, string> Samples()
    {
        var data = new TheoryData<string, CanOpenIniFormat, string>();
        void Add(string code, string text, bool dcf = false) =>
            data.Add(code, dcf ? CanOpenIniFormat.Dcf : CanOpenIniFormat.Eds, text);

        Add("OBJ002", Manufacturer("DataType=abc\nAccessType=rw\nDefaultValue=1\n"));
        Add("OBJ003", Manufacturer("DataType=0x0099\nAccessType=rw\nDefaultValue=1\n"));
        Add("OBJ009", Manufacturer("DataType=0x0005\nAccessType=rw\nDefaultValue=1\n", pdoMapping: "2"));
        Add("OBJ010", Manufacturer("DataType=0x0005\nAccessType=rw\nDefaultValue=1\n", name: null));
        Add("VAL002", Manufacturer("DataType=0x0005\nAccessType=rw\nDefaultValue=7\nLowLimit=10\nHighLimit=5\n"));
        Add("VAL003", Manufacturer("DataType=0x0005\nAccessType=rw\nDefaultValue=20\nLowLimit=0\nHighLimit=10\n"));
        Add("VAL005", Manufacturer("DataType=0x0005\nAccessType=rw\nDefaultValue=010\n"));
        Add("VAL006", Manufacturer("DataType=0x0009\nAccessType=rw\nDefaultValue=abc\nLowLimit=1\n"));
        Add("VAL007", Manufacturer("DataType=0x0008\nAccessType=rw\nDefaultValue=0x3F800000\n"));
        Add("FRM001", Manufacturer("DataType=0x0007\nAccessType=rw\nDefaultValue=$NODEID+abc\n"));
        Add("FRM002", Manufacturer("DataType=0x0007\nAccessType=rw\nDefaultValue=0x180+$NODEID\n"));
        Add("FRM003", Manufacturer("DataType=0x0009\nAccessType=rw\nDefaultValue=$NODEID+1\n"));
        Add("FRM004", Manufacturer("DataType=0x0005\nAccessType=rw\nDefaultValue=$NODEID+0xFF\n"));
        Add("FRM005", Manufacturer("DataType=0x0006\nAccessType=rw\nDefaultValue=$NODEID-1\n"));
        Add("STD001", Eds(
            optionalList: "SupportedObjects=1\n1=0x1017\n",
            extra: "\n[1017]\nParameterName=Producer heartbeat time\nObjectType=0x7\nDataType=0x0007\nAccessType=rw\nDefaultValue=0\nPDOMapping=0\n"));
        Add("LST001", Eds(mandatoryList: "SupportedObjects=x\n1=0x1000\n2=0x1001\n3=0x1018\n"));
        Add("LST004", Eds(mandatoryList: "SupportedObjects=4\n1=0x1000\n2=0x1001\n3=0x1018\n4=0x1000\n"));
        Add("LST005", Eds(mandatoryList: "SupportedObjects=2\n1=0x1000\n2=0x1018\n", optionalList: "SupportedObjects=1\n1=0x1001\n"));
        Add("LST006", Eds(mandatoryList: "SupportedObjects=4\n1=0x1000\n2=0x1001\n3=0x1018\n4=xyz\n"));
        Add("LST007", Eds(
            optionalList: "SupportedObjects=1\n1=0x2000\n",
            extra: "\n[2000]\nParameterName=Value\nObjectType=0x7\nDataType=0x0005\nAccessType=rw\nDefaultValue=1\nPDOMapping=0\n"));
        Add("PDO003", Tpdo("0x20000010", 1));
        Add("PDO004", Tpdo("0x20000008", 9));
        Add("MND001", "[FileInfo]\nFileName=a.eds\n");
        Add("MND002", Eds(fileInfo: "FileName=sample.eds\n"));
        Add("MND004", Eds(deviceInfo: DefaultDeviceInfo.Replace("BaudRate_250=1", "BaudRate_250=0")));
        Add("MND005", Eds(deviceInfo: DefaultDeviceInfo.Replace("VendorNumber=0x10", "VendorNumber=0x11")));
        Add("DCF001", Dcf(commissioning: "NodeName=Node\nBaudrate=250\nNetNumber=1\nNetworkName=Net\n"), dcf: true);
        return data;
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Sample_ReportsRule(string code, CanOpenIniFormat format, string text)
    {
        var issues = CanOpenIniValidator.ValidateString(text, format);

        Describe(issues).Should().Contain(d => d.Contains(" " + code + " ", StringComparison.Ordinal) || d.Contains(" " + code + ":", StringComparison.Ordinal));
        CanOpenValidationRuleCatalog.TryGet(code, out _).Should().BeTrue();
    }

    private static string Manufacturer(string body, string pdoMapping = "0", string? name = "Value") =>
        Eds("[2000]\n" + (name is null ? string.Empty : "ParameterName=" + name + "\n") + "ObjectType=0x7\n" + body + "PDOMapping=" + pdoMapping + "\n");

    private static string Tpdo(string mapping, int count) =>
        Eds(
            "[2000]\nParameterName=Mapped\nObjectType=0x7\nDataType=0x0005\nAccessType=ro\nDefaultValue=1\nPDOMapping=1\n",
            optionalList: "SupportedObjects=1\n1=0x1A00\n",
            extra: "\n[1A00]\nParameterName=TPDO1 mapping\nObjectType=0x8\nDataType=0x0007\nAccessType=rw\nDefaultValue=" + mapping +
                "\nPDOMapping=0\nCompactSubObj=" + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");

    private static List<string> Describe(IEnumerable<IniValidationIssue> issues) =>
        issues.Select(i => i.ToDisplayString()).ToList();

    internal static string Eds(
        string manufacturerObjects = "",
        string fileInfo = "FileName=sample.eds\nFileVersion=1\nDescription=Sample\nCreatedBy=Me\nModificationTime=10:00AM\nModificationDate=01-01-2026\nModifiedBy=Me\nFileRevision=0\nEDSVersion=4.0\nCreationTime=10:00AM\nCreationDate=01-01-2026\n",
        string deviceInfo = DefaultDeviceInfo,
        string mandatoryList = "SupportedObjects=3\n1=0x1000\n2=0x1001\n3=0x1018\n",
        string optionalList = "",
        string extra = "") =>
        "[FileInfo]\n" + fileInfo +
        "\n[DeviceInfo]\n" + deviceInfo +
        "\n[MandatoryObjects]\n" + mandatoryList +
        MandatorySections +
        (optionalList.Length > 0 ? "\n[OptionalObjects]\n" + optionalList : string.Empty) +
        ManufacturerList(manufacturerObjects) +
        extra;

    internal static string Dcf(string commissioning = "NodeID=5\nNodeName=Node\nBaudrate=250\nNetNumber=1\nNetworkName=Net\n", string manufacturerObjects = "") =>
        Eds(manufacturerObjects).Replace("FileName=sample.eds", "FileName=sample.dcf") +
        "\n[DeviceComissioning]\n" + commissioning;

    private const string DefaultDeviceInfo =
        "VendorName=Vendor\nVendorNumber=0x10\nProductName=Product\nProductNumber=0x20\nRevisionNumber=0x30\n" +
        "OrderCode=OC\nBaudRate_10=0\nBaudRate_20=0\nBaudRate_50=0\nBaudRate_125=0\nBaudRate_250=1\nBaudRate_500=0\n" +
        "BaudRate_800=0\nBaudRate_1000=0\nSimpleBootUpMaster=0\nSimpleBootUpSlave=1\nGranularity=8\nDynamicChannelsSupported=0\n" +
        "GroupMessaging=0\nNrOfRXPDO=0\nNrOfTXPDO=0\nLSS_Supported=0\n";

    private const string MandatorySections = @"
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
";

    private static string ManufacturerList(string sections)
    {
        var indexes = System.Text.RegularExpressions.Regex.Matches(sections, @"^\[([0-9A-Fa-f]{4})\]", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Groups[1].Value)
            .ToList();
        if (indexes.Count == 0)
        {
            return string.Empty;
        }

        var list = "\n[ManufacturerObjects]\nSupportedObjects=" + indexes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n";
        for (var i = 0; i < indexes.Count; i++)
        {
            list += (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "=0x" + indexes[i] + "\n";
        }

        return list + "\n" + sections;
    }
}
