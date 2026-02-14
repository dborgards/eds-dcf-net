namespace EdsDcfNet.Tests.Models;

using EdsDcfNet.Models;

/// <summary>
/// Safety-net tests verifying POCO model default initialization.
/// Ensures collections are never null and string properties default to expected values.
/// </summary>
public class ModelInitializationTests
{
    #region DeviceConfigurationFile

    [Fact]
    public void DeviceConfigurationFile_DefaultInit_CollectionsNotNull()
    {
        // Act
        var dcf = new DeviceConfigurationFile();

        // Assert
        dcf.FileInfo.Should().NotBeNull();
        dcf.DeviceInfo.Should().NotBeNull();
        dcf.DeviceCommissioning.Should().NotBeNull();
        dcf.ObjectDictionary.Should().NotBeNull();
        dcf.ConnectedModules.Should().NotBeNull().And.BeEmpty();
        dcf.SupportedModules.Should().NotBeNull().And.BeEmpty();
        dcf.AdditionalSections.Should().NotBeNull().And.BeEmpty();
        dcf.Comments.Should().BeNull();
    }

    #endregion

    #region ElectronicDataSheet

    [Fact]
    public void ElectronicDataSheet_DefaultInit_CollectionsNotNull()
    {
        // Act
        var eds = new ElectronicDataSheet();

        // Assert
        eds.FileInfo.Should().NotBeNull();
        eds.DeviceInfo.Should().NotBeNull();
        eds.ObjectDictionary.Should().NotBeNull();
        eds.SupportedModules.Should().NotBeNull().And.BeEmpty();
        eds.AdditionalSections.Should().NotBeNull().And.BeEmpty();
        eds.Comments.Should().BeNull();
    }

    #endregion

    #region ObjectDictionary

    [Fact]
    public void ObjectDictionary_DefaultInit_CollectionsNotNull()
    {
        // Act
        var od = new ObjectDictionary();

        // Assert
        od.MandatoryObjects.Should().NotBeNull().And.BeEmpty();
        od.OptionalObjects.Should().NotBeNull().And.BeEmpty();
        od.ManufacturerObjects.Should().NotBeNull().And.BeEmpty();
        od.Objects.Should().NotBeNull().And.BeEmpty();
        od.DummyUsage.Should().NotBeNull().And.BeEmpty();
    }

    #endregion

    #region CanOpenObject

    [Fact]
    public void CanOpenObject_DefaultInit_HasExpectedDefaults()
    {
        // Act
        var obj = new CanOpenObject();

        // Assert
        obj.Index.Should().Be(0);
        obj.ParameterName.Should().NotBeNull().And.BeEmpty();
        obj.ObjectType.Should().Be(0x7); // Default: VAR
        obj.DataType.Should().BeNull();
        obj.AccessType.Should().Be(AccessType.ReadOnly);
        obj.DefaultValue.Should().BeNull();
        obj.LowLimit.Should().BeNull();
        obj.HighLimit.Should().BeNull();
        obj.PdoMapping.Should().BeFalse();
        obj.ObjFlags.Should().Be(0u);
        obj.SubNumber.Should().BeNull();
        obj.SubObjects.Should().NotBeNull().And.BeEmpty();
        obj.CompactSubObj.Should().BeNull();
        obj.ObjectLinks.Should().NotBeNull().And.BeEmpty();
        obj.ParameterValue.Should().BeNull();
        obj.Denotation.Should().BeNull();
        obj.UploadFile.Should().BeNull();
        obj.DownloadFile.Should().BeNull();
    }

    #endregion

    #region CanOpenSubObject

    [Fact]
    public void CanOpenSubObject_DefaultInit_HasExpectedDefaults()
    {
        // Act
        var sub = new CanOpenSubObject();

        // Assert
        sub.SubIndex.Should().Be(0);
        sub.ParameterName.Should().NotBeNull().And.BeEmpty();
        sub.ObjectType.Should().Be(0x7);
        sub.DataType.Should().Be(0);
        sub.AccessType.Should().Be(AccessType.ReadOnly);
        sub.DefaultValue.Should().BeNull();
        sub.LowLimit.Should().BeNull();
        sub.HighLimit.Should().BeNull();
        sub.PdoMapping.Should().BeFalse();
        sub.ParameterValue.Should().BeNull();
        sub.Denotation.Should().BeNull();
    }

    #endregion

    #region DeviceCommissioning

    [Fact]
    public void DeviceCommissioning_DefaultInit_HasExpectedDefaults()
    {
        // Act
        var dc = new DeviceCommissioning();

        // Assert
        dc.NodeId.Should().Be(0);
        dc.NodeName.Should().NotBeNull().And.BeEmpty();
        dc.Baudrate.Should().Be(0);
        dc.NetNumber.Should().Be(0u);
        dc.NetworkName.Should().NotBeNull().And.BeEmpty();
        dc.CANopenManager.Should().BeFalse();
        dc.LssSerialNumber.Should().BeNull();
    }

    #endregion

    #region EdsFileInfo

    [Fact]
    public void EdsFileInfo_DefaultInit_HasExpectedDefaults()
    {
        // Act
        var fi = new EdsFileInfo();

        // Assert
        fi.FileName.Should().NotBeNull().And.BeEmpty();
        fi.FileVersion.Should().Be(1);
        fi.FileRevision.Should().Be(0);
        fi.EdsVersion.Should().Be("4.0");
        fi.Description.Should().NotBeNull().And.BeEmpty();
        fi.CreationTime.Should().NotBeNull().And.BeEmpty();
        fi.CreationDate.Should().NotBeNull().And.BeEmpty();
        fi.CreatedBy.Should().NotBeNull().And.BeEmpty();
        fi.ModificationTime.Should().NotBeNull().And.BeEmpty();
        fi.ModificationDate.Should().NotBeNull().And.BeEmpty();
        fi.ModifiedBy.Should().NotBeNull().And.BeEmpty();
        fi.LastEds.Should().BeNull();
    }

    #endregion

    #region DeviceInfo

    [Fact]
    public void DeviceInfo_DefaultInit_HasExpectedDefaults()
    {
        // Act
        var di = new DeviceInfo();

        // Assert
        di.VendorName.Should().NotBeNull().And.BeEmpty();
        di.VendorNumber.Should().Be(0u);
        di.ProductName.Should().NotBeNull().And.BeEmpty();
        di.ProductNumber.Should().Be(0u);
        di.RevisionNumber.Should().Be(0u);
        di.OrderCode.Should().NotBeNull().And.BeEmpty();
        di.SupportedBaudRates.Should().NotBeNull();
        di.SimpleBootUpMaster.Should().BeFalse();
        di.SimpleBootUpSlave.Should().BeFalse();
        di.Granularity.Should().Be(8);
        di.DynamicChannelsSupported.Should().Be(0);
        di.GroupMessaging.Should().BeFalse();
        di.NrOfRxPdo.Should().Be(0);
        di.NrOfTxPdo.Should().Be(0);
        di.LssSupported.Should().BeFalse();
        di.CompactPdo.Should().Be(0);
    }

    #endregion

    #region BaudRates

    [Fact]
    public void BaudRates_DefaultInit_AllFalse()
    {
        // Act
        var br = new BaudRates();

        // Assert
        br.BaudRate10.Should().BeFalse();
        br.BaudRate20.Should().BeFalse();
        br.BaudRate50.Should().BeFalse();
        br.BaudRate125.Should().BeFalse();
        br.BaudRate250.Should().BeFalse();
        br.BaudRate500.Should().BeFalse();
        br.BaudRate800.Should().BeFalse();
        br.BaudRate1000.Should().BeFalse();
    }

    #endregion

    #region ModuleInfo

    [Fact]
    public void ModuleInfo_DefaultInit_HasExpectedDefaults()
    {
        // Act
        var mi = new ModuleInfo();

        // Assert
        mi.ModuleNumber.Should().Be(0);
        mi.ProductName.Should().NotBeNull().And.BeEmpty();
        mi.ProductVersion.Should().Be(0);
        mi.ProductRevision.Should().Be(0);
        mi.OrderCode.Should().NotBeNull().And.BeEmpty();
        mi.FixedObjects.Should().NotBeNull().And.BeEmpty();
        mi.FixedObjectDefinitions.Should().NotBeNull().And.BeEmpty();
        mi.SubExtends.Should().NotBeNull().And.BeEmpty();
        mi.SubExtensionDefinitions.Should().NotBeNull().And.BeEmpty();
        mi.Comments.Should().BeNull();
    }

    #endregion

    #region Comments

    [Fact]
    public void Comments_DefaultInit_HasExpectedDefaults()
    {
        // Act
        var c = new Comments();

        // Assert
        c.Lines.Should().Be(0);
        c.CommentLines.Should().NotBeNull().And.BeEmpty();
    }

    #endregion
}
