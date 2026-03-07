namespace EdsDcfNet.Utilities;

using EdsDcfNet.Models;

/// <summary>
/// Provides deep-clone operations for CANopen model objects.
/// Used by conversion flows (e.g. EDS → DCF) to produce isolated copies.
/// </summary>
internal static class ModelCloner
{
    /// <summary>
    /// Creates a deep copy of a <see cref="DeviceInfo"/> instance.
    /// </summary>
    internal static DeviceInfo CloneDeviceInfo(DeviceInfo source)
    {
        return new DeviceInfo
        {
            VendorName = source.VendorName,
            VendorNumber = source.VendorNumber,
            ProductName = source.ProductName,
            ProductNumber = source.ProductNumber,
            RevisionNumber = source.RevisionNumber,
            OrderCode = source.OrderCode,
            SupportedBaudRates = new BaudRates
            {
                BaudRate10 = source.SupportedBaudRates.BaudRate10,
                BaudRate20 = source.SupportedBaudRates.BaudRate20,
                BaudRate50 = source.SupportedBaudRates.BaudRate50,
                BaudRate125 = source.SupportedBaudRates.BaudRate125,
                BaudRate250 = source.SupportedBaudRates.BaudRate250,
                BaudRate500 = source.SupportedBaudRates.BaudRate500,
                BaudRate800 = source.SupportedBaudRates.BaudRate800,
                BaudRate1000 = source.SupportedBaudRates.BaudRate1000
            },
            SimpleBootUpMaster = source.SimpleBootUpMaster,
            SimpleBootUpSlave = source.SimpleBootUpSlave,
            Granularity = source.Granularity,
            DynamicChannelsSupported = source.DynamicChannelsSupported,
            GroupMessaging = source.GroupMessaging,
            NrOfRxPdo = source.NrOfRxPdo,
            NrOfTxPdo = source.NrOfTxPdo,
            LssSupported = source.LssSupported,
            CompactPdo = source.CompactPdo,
            CANopenSafetySupported = source.CANopenSafetySupported
        };
    }

    /// <summary>
    /// Creates a deep copy of an <see cref="ObjectDictionary"/>, including all objects and sub-objects.
    /// </summary>
    internal static ObjectDictionary CloneObjectDictionary(ObjectDictionary source)
    {
        var clone = new ObjectDictionary();
        clone.MandatoryObjects.AddRange(source.MandatoryObjects);
        clone.OptionalObjects.AddRange(source.OptionalObjects);
        clone.ManufacturerObjects.AddRange(source.ManufacturerObjects);
        foreach (var kvp in source.DummyUsage)
            clone.DummyUsage[kvp.Key] = kvp.Value;

        foreach (var kvp in source.Objects)
        {
            clone.Objects[kvp.Key] = CloneObject(kvp.Value);
        }

        return clone;
    }

    /// <summary>
    /// Creates a deep copy of a <see cref="CanOpenObject"/>, including all sub-objects and object links.
    /// </summary>
    internal static CanOpenObject CloneObject(CanOpenObject source)
    {
        var clone = new CanOpenObject
        {
            Index = source.Index,
            ParameterName = source.ParameterName,
            ObjectType = source.ObjectType,
            DataType = source.DataType,
            AccessType = source.AccessType,
            DefaultValue = source.DefaultValue,
            LowLimit = source.LowLimit,
            HighLimit = source.HighLimit,
            PdoMapping = source.PdoMapping,
            ObjFlags = source.ObjFlags,
            SubNumber = source.SubNumber,
            CompactSubObj = source.CompactSubObj,
            ParameterValue = source.ParameterValue,
            Denotation = source.Denotation,
            UploadFile = source.UploadFile,
            DownloadFile = source.DownloadFile,
            SrdoMapping = source.SrdoMapping,
            InvertedSrad = source.InvertedSrad,
            ParamRefd = source.ParamRefd
        };

        clone.ObjectLinks.AddRange(source.ObjectLinks);

        foreach (var kvp in source.SubObjects)
        {
            clone.SubObjects[kvp.Key] = CloneSubObject(kvp.Value);
        }

        return clone;
    }

    /// <summary>
    /// Creates a deep copy of a <see cref="CanOpenSubObject"/>.
    /// </summary>
    internal static CanOpenSubObject CloneSubObject(CanOpenSubObject source)
    {
        return new CanOpenSubObject
        {
            SubIndex = source.SubIndex,
            ParameterName = source.ParameterName,
            ObjectType = source.ObjectType,
            DataType = source.DataType,
            AccessType = source.AccessType,
            DefaultValue = source.DefaultValue,
            LowLimit = source.LowLimit,
            HighLimit = source.HighLimit,
            PdoMapping = source.PdoMapping,
            ParameterValue = source.ParameterValue,
            Denotation = source.Denotation,
            SrdoMapping = source.SrdoMapping,
            InvertedSrad = source.InvertedSrad,
            ParamRefd = source.ParamRefd
        };
    }

    /// <summary>
    /// Creates a deep copy of a <see cref="Comments"/> instance.
    /// Returns <see langword="null"/> when the source is <see langword="null"/>.
    /// </summary>
    internal static Comments? CloneComments(Comments? source)
    {
        if (source == null) return null;
        var clone = new Comments { Lines = source.Lines };
        foreach (var kvp in source.CommentLines)
            clone.CommentLines[kvp.Key] = kvp.Value;
        return clone;
    }

    /// <summary>
    /// Creates a deep copy of a list of <see cref="ModuleInfo"/> instances.
    /// </summary>
    internal static List<ModuleInfo> CloneSupportedModules(List<ModuleInfo> source)
    {
        var clone = new List<ModuleInfo>(source.Count);
        foreach (var module in source)
        {
            var clonedModule = new ModuleInfo
            {
                ModuleNumber = module.ModuleNumber,
                ProductName = module.ProductName,
                ProductVersion = module.ProductVersion,
                ProductRevision = module.ProductRevision,
                OrderCode = module.OrderCode,
                Comments = CloneComments(module.Comments)
            };

            clonedModule.FixedObjects.AddRange(module.FixedObjects);
            clonedModule.SubExtends.AddRange(module.SubExtends);

            foreach (var kvp in module.FixedObjectDefinitions)
            {
                clonedModule.FixedObjectDefinitions[kvp.Key] = CloneObject(kvp.Value);
            }

            foreach (var kvp in module.SubExtensionDefinitions)
            {
                clonedModule.SubExtensionDefinitions[kvp.Key] = new ModuleSubExtension
                {
                    Index = kvp.Value.Index,
                    ParameterName = kvp.Value.ParameterName,
                    DataType = kvp.Value.DataType,
                    AccessType = kvp.Value.AccessType,
                    DefaultValue = kvp.Value.DefaultValue,
                    PdoMapping = kvp.Value.PdoMapping,
                    Count = kvp.Value.Count,
                    ObjExtend = kvp.Value.ObjExtend
                };
            }

            clone.Add(clonedModule);
        }
        return clone;
    }

    /// <summary>
    /// Creates a deep copy of a <see cref="DynamicChannels"/> instance.
    /// Returns <see langword="null"/> when the source is <see langword="null"/>.
    /// </summary>
    internal static DynamicChannels? CloneDynamicChannels(DynamicChannels? source)
    {
        if (source == null)
            return null;

        var clone = new DynamicChannels();
        foreach (var segment in source.Segments)
        {
            clone.Segments.Add(new DynamicChannelSegment
            {
                Type = segment.Type,
                Dir = segment.Dir,
                Range = segment.Range,
                PPOffset = segment.PPOffset
            });
        }

        return clone;
    }

    /// <summary>
    /// Creates a deep copy of a list of <see cref="ToolInfo"/> instances.
    /// </summary>
    internal static List<ToolInfo> CloneTools(List<ToolInfo> source)
    {
        var clone = new List<ToolInfo>(source.Count);
        foreach (var tool in source)
        {
            clone.Add(new ToolInfo
            {
                Name = tool.Name,
                Command = tool.Command
            });
        }

        return clone;
    }

    /// <summary>
    /// Creates a deep copy of additional sections (string-keyed dictionaries)
    /// preserving case-insensitive key comparison.
    /// </summary>
    internal static Dictionary<string, Dictionary<string, string>> CloneAdditionalSections(
        Dictionary<string, Dictionary<string, string>> source)
    {
        var clone = new Dictionary<string, Dictionary<string, string>>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in source)
        {
            clone[kvp.Key] = AdditionalSectionsCloner.CloneSectionEntriesCaseInsensitive(kvp.Value);
        }
        return clone;
    }
}
