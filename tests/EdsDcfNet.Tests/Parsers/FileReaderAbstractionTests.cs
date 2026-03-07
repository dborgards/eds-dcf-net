namespace EdsDcfNet.Tests.Parsers;

using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

public class FileReaderAbstractionTests
{
    [Fact]
    public void ReaderTypes_ImplementIFileReaderContract()
    {
        typeof(IFileReader<ElectronicDataSheet>).IsAssignableFrom(typeof(EdsReader)).Should().BeTrue();
        typeof(IFileReader<DeviceConfigurationFile>).IsAssignableFrom(typeof(DcfReader)).Should().BeTrue();
        typeof(IFileReader<NodelistProject>).IsAssignableFrom(typeof(CpjReader)).Should().BeTrue();
        typeof(IFileReader<ElectronicDataSheet>).IsAssignableFrom(typeof(XddReader)).Should().BeTrue();
        typeof(IFileReader<DeviceConfigurationFile>).IsAssignableFrom(typeof(XdcReader)).Should().BeTrue();
    }

    [Fact]
    public void ReadString_CanBeUsedThroughIFileReaderPolymorphism()
    {
        IFileReader<ElectronicDataSheet> edsReader = new EdsReader();
        IFileReader<DeviceConfigurationFile> dcfReader = new DcfReader();
        IFileReader<NodelistProject> cpjReader = new CpjReader();

        var eds = edsReader.ReadString(
            """
            [DeviceInfo]
            VendorName=Vendor
            """);

        var dcf = dcfReader.ReadString(
            """
            [DeviceInfo]
            VendorName=Vendor
            """);

        var cpj = cpjReader.ReadString(
            """
            [Topology]
            Nodes=0x00
            """);

        eds.DeviceInfo.VendorName.Should().Be("Vendor");
        dcf.DeviceInfo.VendorName.Should().Be("Vendor");
        cpj.Networks.Should().HaveCount(1);
    }
}
