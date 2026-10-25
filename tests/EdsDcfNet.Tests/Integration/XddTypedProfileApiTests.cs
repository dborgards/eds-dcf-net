namespace EdsDcfNet.Tests.Integration;

using System.Collections;
using System.Xml.Linq;
using EdsDcfNet.Models;
using EdsDcfNet.Tests.Infrastructure;

public class XddTypedProfileApiTests
{
    private const string Fixture = "Fixtures/preserved_content.xdd";
    private static readonly XNamespace Co = "http://www.canopen.org/xml/1.1";

    [Fact]
    public void Read_ExposesTypedFunctionsAndBothModuleContexts()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);

        var function = eds.DeviceFunctions.Should().ContainSingle().Subject;
        var characteristic = function.CharacteristicsLists.Single().Characteristics.Single();
        characteristic.Name.Items.Single().Value.Should().Be("Inputs");
        characteristic.Contents.Single().Items.Single().Value.Should().Be("8 digital");
        eds.DeviceManager!.ModuleManagement!.Interfaces.Single().Id.Should().Be("if_main");
        eds.NetworkModuleManagement!.Interfaces.Single().Ranges.Single().BaseIndex.Should().Be("3000");

        characteristic.Contents.Single().Items.Single().Value = "16 digital";
        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds));
        written.Descendants(Co + "characteristicContent").Single().Element("label")!.Value.Should().Be("16 digital");
        written.Descendants(Co + "characteristicsList").Single().Nodes().OfType<XComment>()
            .Select(comment => comment.Value).Should().Equal("kept with its fragment");
        Cia311Schema.Validate(written.ToString()).Should().BeEmpty();
    }

    [Fact]
    public void NewFunction_AllTypedProperties_RoundTripAndValidate()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);
        eds.DeviceFunctions.Clear();
        var function = new XddDeviceFunction();
        eds.DeviceFunctions.Add(function);

        var list = function.CharacteristicsLists.Single();
        list.Category = new XddTextGroup();
        list.Category.Items.Add(new XddText(XddTextKind.Description)
            { Language = "de", Value = "Kategorie", Uri = "docs/category" });
        var characteristic = list.Characteristics.Single();
        characteristic.Name = new XddTextGroup();
        characteristic.Name.Add("Voltage", "en");
        characteristic.Name.Add("Spannung", "de");
        characteristic.Contents.Clear();
        characteristic.Contents.Add(new XddTextGroup());
        characteristic.Contents.Single().Add("24 V", "en");
        var reference = new XddText(XddTextKind.LabelReference)
            { DictionaryId = "dict1", TextId = "voltage", Value = "texts.xml" };
        characteristic.Name.Items.Add(reference);
        reference.Kind = XddTextKind.DescriptionReference;
        reference.Kind.Should().Be(XddTextKind.DescriptionReference);

        function.Standards.Add(new XddStandardCompliance
        {
            Name = "CiA 311", Range = XddStandardRange.Internal
        });
        function.Standards.Single().Texts.Add("CANopen XML", "en");
        function.Pictures.Add(new XddPicture { Uri = "images/device.png", Number = 2 });
        function.Pictures.Single().Texts.Add("Device", "en");
        function.Dictionaries.Add(new XddTextResource
        {
            Language = "en", Id = "dict1", Uri = "texts.xml"
        });

        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds));
        Cia311Schema.Validate(written.ToString()).Should().BeEmpty();
        written.Descendants(Co + "compliantWith").Single().Attribute("range")!.Value.Should().Be("internal");
        written.Descendants(Co + "picture").Single().Attribute("number")!.Value.Should().Be("2");
        written.Descendants("descriptionRef").Single().Attribute("dictID")!.Value.Should().Be("dict1");

        var reread = CanOpenFile.Xdd.ReadString(written.ToString());
        var actual = reread.DeviceFunctions.Single();
        actual.Standards.Single().Range.Should().Be(XddStandardRange.Internal);
        actual.Pictures.Single().Number.Should().Be(2);
        actual.Dictionaries.Single().Uri.Should().Be("texts.xml");
        actual.CharacteristicsLists.Single().Category!.Items.Single().Kind.Should().Be(XddTextKind.Description);
    }

    [Fact]
    public void DeviceManager_LedsAndModuleInterface_RoundTripAndValidate()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);
        var manager = eds.DeviceManager!;
        manager.Indicators.Count.Should().Be(0);

        var led = new XddLed { Colors = XddLedColors.Bicolor, Type = XddLedType.IO };
        led.Texts.Add("Run LED", "en");
        led.States.Add(new XddLedState
        {
            Id = "run_green", State = XddLedVisualState.On, Color = XddLedColor.Green
        });
        led.States.Last().Texts.Add("Running", "en");
        led.States.Add(new XddLedState
        {
            Id = "run_red", State = XddLedVisualState.Flashing, Color = XddLedColor.Red,
            FlashingPeriod = 200, ImpulseWidth = 25, NumberOfImpulses = 2
        });
        led.States.Last().Texts.Add("Failure", "en");
        manager.Indicators.Add(led);
        var combined = new XddCombinedState();
        combined.Texts.Add("Run failure", "en");
        combined.StateReferences.Add("run_green");
        combined.StateReferences.Add("run_red");
        manager.CombinedStates.Add(combined);

        var modules = manager.ModuleManagement!;
        var deviceInterface = modules.Interfaces.Single();
        deviceInterface.MaxChildren = 8;
        deviceInterface.UnusedSlots = true;
        deviceInterface.MultipleChildren = false;
        deviceInterface.SearchPath = "modules/new/";
        deviceInterface.Texts.Add("Main bus", "en");
        deviceInterface.ModuleTypes.Add("DigitalModule");
        deviceInterface.ConnectedModuleIds.Add("module1");

        var written = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds));
        Cia311Schema.Validate(written.ToString()).Should().BeEmpty(written.ToString());
        var managerXml = written.Descendants(Co + "DeviceManager").Single();
        managerXml.Elements().Select(e => e.Name.LocalName).Should().Equal("indicatorList", "moduleManagement");
        managerXml.Descendants(Co + "LEDstate").Last().Attribute("impulsWidth")!.Value.Should().Be("25");
        managerXml.Descendants("connectedModule").Single().Attribute("childIDRef")!.Value.Should().Be("module1");

        var reread = CanOpenFile.Xdd.ReadString(written.ToString());
        reread.DeviceManager!.Indicators.Single().States.Last().NumberOfImpulses.Should().Be(2);
        reread.DeviceManager!.CombinedStates.Single().StateReferences.Should().Equal("run_green", "run_red");
    }

    [Fact]
    public void NetworkModules_AllRangeProperties_RoundTripAndValidate()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);
        var network = eds.NetworkModuleManagement!.Interfaces.Single();
        network.Addressing = XddAddressing.Auto;
        var range = network.Ranges.Single();
        range.Name = "IO range";
        range.ModuleType = "mt_digital";
        range.BaseIndex = "4000";
        range.MaximumIndex = "4FFF";
        range.MaximumSubIndex = "FE";
        range.SortMode = XddModuleSortMode.SubIndex;
        range.SortNumber = XddModuleSortNumber.Continuous;
        range.SortStep = "02";

        var written = CanOpenFile.Xdd.WriteToString(eds);
        Cia311Schema.Validate(written).Should().BeEmpty();
        var reread = CanOpenFile.Xdd.ReadString(written).NetworkModuleManagement!.Interfaces.Single().Ranges.Single();
        reread.SortMode.Should().Be(XddModuleSortMode.SubIndex);
        reread.SortNumber.Should().Be(XddModuleSortNumber.Continuous);
        reread.MaximumSubIndex.Should().Be("FE");
        reread.SortStep.Should().Be("02");
    }

    [Fact]
    public void SingleModuleForms_AndChoiceReplacement_RoundTrip()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);
        eds.DeviceManager = new XddDeviceManager
        {
            ModuleManagement = new XddDeviceModuleManagement
            {
                Module = new XddDeviceModuleReference { ChildId = "child1", Type = "io", MaxCount = 3 }
            }
        };
        eds.NetworkModuleManagement = new XddNetworkModuleManagement
        {
            Module = new XddNetworkModuleReference
            {
                ChildReference = "child1", MinimumAddress = 2, MaximumAddress = 20,
                Addressing = XddModuleAddressing.Manual
            }
        };

        var first = CanOpenFile.Xdd.WriteToString(eds);
        Cia311Schema.Validate(first).Should().BeEmpty();
        var reread = CanOpenFile.Xdd.ReadString(first);
        reread.DeviceManager!.ModuleManagement!.Module!.MaxCount.Should().Be(3);
        reread.NetworkModuleManagement!.Module!.MaximumAddress.Should().Be(20);

        reread.DeviceManager!.ModuleManagement!.Interfaces.Add(new XddDeviceModuleInterface
        {
            Id = "if_new", MaxChildren = 2, UnusedSlots = false, MultipleChildren = true,
            SearchPath = "modules/"
        });
        reread.DeviceManager!.ModuleManagement!.Interfaces.Single().Texts.Add("New interface", "en");
        reread.DeviceManager!.ModuleManagement!.Interfaces.Single().ModuleTypes.Add("io");
        reread.NetworkModuleManagement!.Interfaces.Add(new XddNetworkModuleInterface
        {
            InterfaceReference = "if_new", Addressing = XddAddressing.Manual
        });
        var range = new XddModuleRange
        {
            Name = "io", ModuleType = "mt_io", BaseIndex = "2000", MaximumIndex = "2FFF",
            SortMode = XddModuleSortMode.Index, SortNumber = XddModuleSortNumber.Address
        };
        reread.NetworkModuleManagement!.Interfaces.Single().Ranges.Add(range);

        var second = XDocument.Parse(CanOpenFile.Xdd.WriteToString(reread));
        Cia311Schema.Validate(second.ToString()).Should().BeEmpty();
        second.Descendants(Co + "DeviceManager").Single().Elements(Co + "moduleManagement")
            .Single().Elements().Single().Name.LocalName.Should().Be("moduleInterfaceList");
        second.Descendants("ApplicationLayers").Single().Element("moduleManagement")!
            .Elements().Single().Name.LocalName.Should().Be("interfaceList");
    }

    [Fact]
    public void TypedContent_SurvivesDcfConversionWithoutSharingXml()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);
        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, 3, new DateTime(2026, 1, 2), 250);

        dcf.DeviceFunctions.Single().CharacteristicsLists.Single().Characteristics.Single()
            .Name.Items.Single().Value = "Outputs";

        eds.DeviceFunctions.Single().CharacteristicsLists.Single().Characteristics.Single()
            .Name.Items.Single().Value.Should().Be("Inputs");
        var written = XDocument.Parse(CanOpenFile.Xdc.WriteToString(dcf));
        written.Descendants(Co + "characteristicName").Single().Element("label")!.Value.Should().Be("Outputs");
        written.Descendants(Co + "DeviceManager").Should().NotBeEmpty();
        written.Descendants("ApplicationLayers").Single().Element("moduleManagement").Should().NotBeNull();
    }

    [Fact]
    public void Collections_SupportStandardOperationsAndRejectNull()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);
        var functions = eds.DeviceFunctions;
        functions.IsReadOnly.Should().BeFalse();
        var existing = functions.Single();
        functions.Contains(existing).Should().BeTrue();
        var array = new XddDeviceFunction[2];
        functions.CopyTo(array, 1);
        array[1].CharacteristicsLists.Single().Characteristics.Single().Name.Items.Single().Value.Should().Be("Inputs");
        ((IEnumerable)functions).GetEnumerator().MoveNext().Should().BeTrue();
        functions.Remove(new XddDeviceFunction()).Should().BeFalse();
        functions.Remove(null!).Should().BeFalse();
        functions.Contains(null!).Should().BeFalse();
        var add = () => functions.Add(null!);
        add.Should().Throw<ArgumentNullException>();

        var strings = eds.DeviceManager!.ModuleManagement!.Interfaces.Single().ModuleTypes;
        strings.IsReadOnly.Should().BeFalse();
        strings.Contains("missing").Should().BeFalse();
        strings.Remove("missing").Should().BeFalse();
        var values = new string[2];
        strings.CopyTo(values, 1);
        values[1].Should().Be("IOModule");
        ((IEnumerable)strings).GetEnumerator().MoveNext().Should().BeTrue();
        var addString = () => strings.Add(null!);
        addString.Should().Throw<ArgumentNullException>();
        strings.Add("Temporary");
        strings.Contains("Temporary").Should().BeTrue();
        strings.Remove("Temporary").Should().BeTrue();
        strings.Clear();
        strings.Count.Should().Be(0);

        functions.Clear();
        functions.Count.Should().Be(0);
        CanOpenFile.Xdd.WriteToString(eds).Should().Contain("Product name");
    }

    [Fact]
    public void XdcModel_SupportsTheSameMutableTypedApi()
    {
        var dcf = new DeviceConfigurationFile();
        dcf.DeviceManager.Should().BeNull();
        dcf.NetworkModuleManagement.Should().BeNull();
        dcf.DeviceFunctions.Count.Should().Be(0);
        dcf.DeviceManager = new XddDeviceManager();
        dcf.NetworkModuleManagement = new XddNetworkModuleManagement();
        dcf.DeviceFunctions.Add(new XddDeviceFunction());

        dcf.DeviceManager.Should().NotBeNull();
        dcf.NetworkModuleManagement.Should().NotBeNull();
        dcf.DeviceFunctions.Single().CharacteristicsLists.Count.Should().Be(1);
        dcf.DeviceFunctions.Remove(dcf.DeviceFunctions.Single()).Should().BeTrue();
        dcf.DeviceManager = null;
        dcf.NetworkModuleManagement = null;
        dcf.DeviceManager.Should().BeNull();
        dcf.NetworkModuleManagement.Should().BeNull();
    }

    [Fact]
    public void TypedModels_CoverEmptyFactoriesDefaultsAndInvalidSourceValues()
    {
        var characteristic = new XddCharacteristic();
        characteristic.Name.Items.Count.Should().Be(0);
        characteristic.Contents.Count.Should().Be(1);
        var list = new XddCharacteristicsList();
        list.Characteristics.Add(characteristic);
        list.Category = null;
        list.Characteristics.Single().Contents.Count.Should().Be(1);

        var deviceInterface = new XddDeviceModuleInterface();
        deviceInterface.SearchPath.Should().BeEmpty();
        deviceInterface.SearchPath = "modules/";
        deviceInterface.SearchPath.Should().Be("modules/");
        var resource = new XddTextResource { Uri = "texts.xml" };
        resource.Uri = "other.xml";
        resource.Uri.Should().Be("other.xml");

        var source = File.ReadAllText(Fixture).Replace("unusedSlots=\"false\"", "unusedSlots=\"1\"")
            .Replace("addressing=\"manual\"", "addressing=\"invalid\"");
        var eds = CanOpenFile.Xdd.ReadString(source);
        eds.DeviceManager!.ModuleManagement!.Interfaces.Single().UnusedSlots.Should().BeTrue();
        eds.NetworkModuleManagement!.Interfaces.Single().Addressing.Should().Be(XddAddressing.Auto);
    }

    [Fact]
    public void Defaults_InvalidValuesAndEnumErrors_AreDeterministic()
    {
        var led = new XddLed { Colors = XddLedColors.Monocolor };
        led.Colors.Should().Be(XddLedColors.Monocolor);
        led.Type.Should().BeNull();
        var state = new XddLedState();
        state.State.Should().Be(XddLedVisualState.Off);
        state.Color.Should().Be(XddLedColor.Green);
        state.FlashingPeriod.Should().BeNull();
        state.ImpulseWidth.Should().Be(50);
        state.NumberOfImpulses.Should().Be(1);
        state.ImpulseWidth = 50;
        state.NumberOfImpulses = 1;
        state.FlashingPeriod = null;

        var device = new XddDeviceModuleReference();
        device.MaxCount.Should().Be(0);
        device.MaxCount = 0;
        var network = new XddNetworkModuleReference();
        network.MinimumAddress.Should().Be(1);
        network.MaximumAddress.Should().BeNull();
        network.Addressing.Should().Be(XddModuleAddressing.Inherit);
        network.Addressing = XddModuleAddressing.Inherit;
        network.MaximumAddress = null;
        new XddStandardCompliance().Range.Should().Be(XddStandardRange.International);
        new XddNetworkModuleInterface().Addressing.Should().Be(XddAddressing.Auto);
        new XddModuleRange().SortMode.Should().Be(XddModuleSortMode.Index);
        new XddModuleRange().SortNumber.Should().Be(XddModuleSortNumber.Continuous);
        new XddText().Kind.Should().Be(XddTextKind.Label);
        new XddTextGroup().Items.Count.Should().Be(0);
        new XddCharacteristicsList().Category.Should().BeNull();
        new XddDeviceModuleManagement().Module.Should().BeNull();
        new XddNetworkModuleManagement().Module.Should().BeNull();
        new ElectronicDataSheet().DeviceManager.Should().BeNull();
        new ElectronicDataSheet().NetworkModuleManagement.Should().BeNull();
        new ElectronicDataSheet().DeviceFunctions.Count.Should().Be(0);

        var invalidKind = () => new XddText((XddTextKind)99);
        invalidKind.Should().Throw<ArgumentOutOfRangeException>();
        var invalidColor = () => led.Colors = (XddLedColors)99;
        invalidColor.Should().Throw<ArgumentOutOfRangeException>();
        var invalidTextElement = () => XddXml.TextKind("other");
        invalidTextElement.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EmptyCollectionReads_DoNotChangeWrittenXml()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);
        var before = CanOpenFile.Xdd.WriteToString(eds);

        eds.DeviceManager!.Indicators.Count.Should().Be(0);
        eds.DeviceManager!.CombinedStates.Count.Should().Be(0);
        eds.DeviceFunctions.Single().Pictures.Count.Should().Be(0);
        eds.DeviceFunctions.Single().Dictionaries.Count.Should().Be(0);
        eds.DeviceFunctions.Single().Standards.Count.Should().Be(0);
        eds.DeviceManager!.ModuleManagement!.Interfaces.Single().ConnectedModuleIds.Count.Should().Be(0);

        CanOpenFile.Xdd.WriteToString(eds).Should().Be(before);
    }

    [Fact]
    public void ModuleManagement_UnknownDeviceManagerChild_RemainsAfterKnownContent()
    {
        var source = File.ReadAllText(Fixture).Replace("<q1:DeviceManager>",
            "<q1:DeviceManager><vendor:extension xmlns:vendor=\"urn:vendor\" />");
        var eds = CanOpenFile.Xdd.ReadString(source);

        eds.DeviceManager!.ModuleManagement = new XddDeviceModuleManagement
        {
            Module = new XddDeviceModuleReference { ChildId = "child1" }
        };

        var manager = XDocument.Parse(CanOpenFile.Xdd.WriteToString(eds))
            .Descendants(Co + "DeviceManager").Single();
        manager.Elements().Select(element => element.Name.LocalName).Should()
            .Equal("moduleManagement", "extension");
        manager.Elements().Last().Name.NamespaceName.Should().Be("urn:vendor");
    }

    [Fact]
    public void AssignedProfileModels_RemainConnectedToEdsAndDcf()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);
        var manager = new XddDeviceManager();
        var network = new XddNetworkModuleManagement();
        eds.DeviceManager = manager;
        eds.NetworkModuleManagement = network;

        var led = new XddLed { Colors = XddLedColors.Monocolor };
        led.Texts.Add("Run LED", "en");
        led.States.Add(new XddLedState { Id = "run", State = XddLedVisualState.On, Color = XddLedColor.Green });
        led.States.Single().Texts.Add("Running", "en");
        manager.Indicators.Add(led);
        network.Module = new XddNetworkModuleReference { ChildReference = "child1", MinimumAddress = 2 };

        eds.DeviceManager!.Indicators.Single().States.Single().Id.Should().Be("run");
        eds.NetworkModuleManagement!.Module!.ChildReference.Should().Be("child1");
        Cia311Schema.Validate(CanOpenFile.Xdd.WriteToString(eds)).Should().BeEmpty();

        var dcf = CanOpenFile.Eds.ConvertToDcf(eds, 3);
        var dcfManager = new XddDeviceManager();
        var dcfNetwork = new XddNetworkModuleManagement();
        dcf.DeviceManager = dcfManager;
        dcf.NetworkModuleManagement = dcfNetwork;
        var dcfLed = new XddLed { Colors = XddLedColors.Monocolor };
        dcfLed.Texts.Add("Status LED", "en");
        dcfLed.States.Add(new XddLedState { Id = "status", State = XddLedVisualState.On, Color = XddLedColor.Green });
        dcfLed.States.Single().Texts.Add("Active", "en");
        dcfManager.Indicators.Add(dcfLed);
        dcfNetwork.Module = new XddNetworkModuleReference { ChildReference = "child2", MinimumAddress = 2 };

        dcf.DeviceManager!.Indicators.Should().ContainSingle();
        dcf.NetworkModuleManagement!.Module!.ChildReference.Should().Be("child2");
        Cia311Schema.Validate(CanOpenFile.Xdc.WriteToString(dcf)).Should().BeEmpty();
    }

    [Fact]
    public void RetainedDeviceFunctions_UseCurrentPreservedContent()
    {
        var eds = new ElectronicDataSheet();
        var edsFunctions = eds.DeviceFunctions;
        eds.DeviceManager = new XddDeviceManager();
        eds.DeviceFunctions.Count.Should().Be(0);
        var edsFunction = new XddDeviceFunction();
        edsFunctions.Add(edsFunction);
        edsFunction.CharacteristicsLists.Single().Characteristics.Single().Name.Add("Current", "en");

        eds.DeviceFunctions.Single().CharacteristicsLists.Single().Characteristics.Single()
            .Name.Items.Single().Value.Should().Be("Current");

        var dcf = new DeviceConfigurationFile();
        var dcfFunctions = dcf.DeviceFunctions;
        dcf.NetworkModuleManagement = new XddNetworkModuleManagement();
        dcf.DeviceFunctions.Count.Should().Be(0);
        dcfFunctions.Add(new XddDeviceFunction());

        dcf.DeviceFunctions.Should().ContainSingle();
        dcfFunctions.Should().ContainSingle();
    }

    [Fact]
    public void RetainedCollections_ResolveTheirCurrentParentsForEveryOperation()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);
        var manager = eds.DeviceManager!;
        var indicators = manager.Indicators;
        var led = new XddLed { Colors = XddLedColors.Monocolor };
        led.Texts.Add("Run LED", "en");
        led.States.Add(new XddLedState { Id = "run", State = XddLedVisualState.On, Color = XddLedColor.Green });
        led.States.Single().Texts.Add("Running", "en");
        manager.Indicators.Add(led);
        indicators.Should().ContainSingle();

        var modules = manager.ModuleManagement!;
        var interfaces = modules.Interfaces;
        modules.Module = new XddDeviceModuleReference { ChildId = "single" };
        interfaces.Add(new XddDeviceModuleInterface
        {
            Id = "if_new", SearchPath = "modules/", MaxChildren = 1,
            UnusedSlots = false, MultipleChildren = true
        });
        interfaces.Single().Texts.Add("New interface", "en");
        interfaces.Single().ModuleTypes.Add("mt_io");

        interfaces.Should().ContainSingle();
        modules.Interfaces.Single().Id.Should().Be("if_new");
        modules.Module.Should().BeNull();
        eds.NetworkModuleManagement!.Interfaces.Single().InterfaceReference = "if_new";

        var combined = new XddCombinedState();
        combined.Texts.Add("Combined", "en");
        var references = combined.StateReferences;
        combined.StateReferences.Add("run");
        combined.StateReferences.Add("run");
        references.Should().Equal("run", "run");
        manager.CombinedStates.Add(combined);
        Cia311Schema.Validate(CanOpenFile.Xdd.WriteToString(eds)).Should().BeEmpty();
    }

    [Fact]
    public void TextAddedAfterChildren_PreservesSchemaOrderForAllTextBearingModels()
    {
        var eds = CanOpenFile.Xdd.ReadFile(Fixture);
        var led = new XddLed { Colors = XddLedColors.Monocolor };
        var state = new XddLedState { Id = "run", State = XddLedVisualState.On, Color = XddLedColor.Green };
        led.States.Add(state);
        led.Texts.Add("Run LED", "en");
        state.Texts.Add("Running", "en");
        eds.DeviceManager!.Indicators.Add(led);

        var combined = new XddCombinedState();
        combined.StateReferences.Add("run");
        combined.StateReferences.Add("run");
        combined.Texts.Add("Combined", "en");
        eds.DeviceManager.CombinedStates.Add(combined);

        var picture = new XddPicture { Uri = "run.png" };
        eds.DeviceFunctions.Single().Pictures.Add(picture);
        picture.Texts.Add("Picture", "en");
        var standard = new XddStandardCompliance { Name = "CiA 311" };
        eds.DeviceFunctions.Single().Standards.Add(standard);
        standard.Texts.Add("Standard", "en");

        var written = CanOpenFile.Xdd.WriteToString(eds);
        Cia311Schema.Validate(written).Should().BeEmpty(written);
        var xml = XDocument.Parse(written);
        xml.Descendants(Co + "LED").Single().Elements().Select(element => element.Name.LocalName)
            .Should().Equal("label", "LEDstate");
        xml.Descendants(Co + "combinedState").Single().Elements().Select(element => element.Name.LocalName)
            .Should().Equal("label", "LEDstateRef", "LEDstateRef");
    }
}
