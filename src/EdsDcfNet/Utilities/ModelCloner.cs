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
    /// Creates a deep copy of an <see cref="ApplicationProcess"/> instance.
    /// Returns <see langword="null"/> when the source is <see langword="null"/>.
    /// </summary>
    internal static ApplicationProcess? CloneApplicationProcess(ApplicationProcess? source)
    {
        if (source == null)
            return null;

        var clone = new ApplicationProcess
        {
            DataTypeList = CloneDataTypeList(source.DataTypeList),
            FunctionInstanceList = CloneFunctionInstanceList(source.FunctionInstanceList),
            TemplateList = CloneTemplateList(source.TemplateList)
        };

        foreach (var functionType in source.FunctionTypeList)
            clone.FunctionTypeList.Add(CloneFunctionType(functionType));

        foreach (var parameter in source.ParameterList)
            clone.ParameterList.Add(CloneParameter(parameter));

        foreach (var parameterGroup in source.ParameterGroupList)
            clone.ParameterGroupList.Add(CloneParameterGroup(parameterGroup));

        return clone;
    }

    private static ApDataTypeList? CloneDataTypeList(ApDataTypeList? source)
    {
        if (source == null)
            return null;

        var clone = new ApDataTypeList();
        foreach (var item in source.Arrays)
            clone.Arrays.Add(CloneArrayType(item));
        foreach (var item in source.Structs)
            clone.Structs.Add(CloneStructType(item));
        foreach (var item in source.Enums)
            clone.Enums.Add(CloneEnumType(item));
        foreach (var item in source.Derived)
            clone.Derived.Add(CloneDerivedType(item));
        return clone;
    }

    private static ApArrayType CloneArrayType(ApArrayType source)
    {
        var clone = new ApArrayType
        {
            Name = source.Name,
            UniqueId = source.UniqueId,
            ElementType = CloneTypeRef(source.ElementType)
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        foreach (var subrange in source.Subranges)
        {
            clone.Subranges.Add(new ApSubrange
            {
                LowerLimit = subrange.LowerLimit,
                UpperLimit = subrange.UpperLimit
            });
        }
        return clone;
    }

    private static ApStructType CloneStructType(ApStructType source)
    {
        var clone = new ApStructType
        {
            Name = source.Name,
            UniqueId = source.UniqueId
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        foreach (var declaration in source.VarDeclarations)
            clone.VarDeclarations.Add(CloneVarDeclaration(declaration));
        return clone;
    }

    private static ApVarDeclaration CloneVarDeclaration(ApVarDeclaration source)
    {
        var clone = new ApVarDeclaration
        {
            Name = source.Name,
            UniqueId = source.UniqueId,
            Start = source.Start,
            Size = source.Size,
            IsSigned = source.IsSigned,
            Offset = source.Offset,
            Multiplier = source.Multiplier,
            InitialValue = source.InitialValue,
            Type = CloneTypeRef(source.Type),
            DefaultValue = CloneParameterValue(source.DefaultValue),
            AllowedValues = CloneAllowedValues(source.AllowedValues),
            Unit = CloneUnit(source.Unit)
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        clone.ConditionalSupports.AddRange(source.ConditionalSupports);
        return clone;
    }

    private static ApEnumType CloneEnumType(ApEnumType source)
    {
        var clone = new ApEnumType
        {
            Name = source.Name,
            UniqueId = source.UniqueId,
            Size = source.Size,
            SimpleTypeName = source.SimpleTypeName
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        foreach (var value in source.EnumValues)
            clone.EnumValues.Add(CloneEnumValue(value));
        return clone;
    }

    private static ApEnumValue CloneEnumValue(ApEnumValue source)
    {
        var clone = new ApEnumValue
        {
            Value = source.Value
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        return clone;
    }

    private static ApDerivedType CloneDerivedType(ApDerivedType source)
    {
        var clone = new ApDerivedType
        {
            Name = source.Name,
            UniqueId = source.UniqueId,
            Count = CloneDerivedCount(source.Count),
            BaseType = CloneTypeRef(source.BaseType)
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        return clone;
    }

    private static ApDerivedCount? CloneDerivedCount(ApDerivedCount? source)
    {
        if (source == null)
            return null;

        var clone = new ApDerivedCount
        {
            UniqueId = source.UniqueId,
            Access = source.Access,
            DefaultValue = CloneParameterValue(source.DefaultValue),
            AllowedValues = CloneAllowedValues(source.AllowedValues)
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        return clone;
    }

    private static ApTypeRef? CloneTypeRef(ApTypeRef? source)
    {
        if (source == null)
            return null;

        return new ApTypeRef
        {
            SimpleTypeName = source.SimpleTypeName,
            DataTypeIdRef = source.DataTypeIdRef
        };
    }

    private static ApFunctionType CloneFunctionType(ApFunctionType source)
    {
        var clone = new ApFunctionType
        {
            Name = source.Name,
            UniqueId = source.UniqueId,
            Package = source.Package,
            InterfaceList = CloneInterfaceList(source.InterfaceList),
            FunctionInstanceList = CloneFunctionInstanceList(source.FunctionInstanceList)
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        foreach (var versionInfo in source.VersionInfos)
            clone.VersionInfos.Add(CloneVersionInfo(versionInfo));
        return clone;
    }

    private static ApVersionInfo CloneVersionInfo(ApVersionInfo source)
    {
        var clone = new ApVersionInfo
        {
            Organization = source.Organization,
            Version = source.Version,
            Author = source.Author,
            Date = source.Date
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        return clone;
    }

    private static ApInterfaceList? CloneInterfaceList(ApInterfaceList? source)
    {
        if (source == null)
            return null;

        var clone = new ApInterfaceList();
        foreach (var input in source.InputVars)
            clone.InputVars.Add(CloneVarDeclaration(input));
        foreach (var output in source.OutputVars)
            clone.OutputVars.Add(CloneVarDeclaration(output));
        foreach (var config in source.ConfigVars)
            clone.ConfigVars.Add(CloneVarDeclaration(config));
        return clone;
    }

    private static ApFunctionInstanceList? CloneFunctionInstanceList(ApFunctionInstanceList? source)
    {
        if (source == null)
            return null;

        var clone = new ApFunctionInstanceList();
        foreach (var instance in source.FunctionInstances)
            clone.FunctionInstances.Add(CloneFunctionInstance(instance));
        foreach (var connection in source.Connections)
            clone.Connections.Add(new ApConnection
            {
                Source = connection.Source,
                Destination = connection.Destination,
                Description = connection.Description
            });
        return clone;
    }

    private static ApFunctionInstance CloneFunctionInstance(ApFunctionInstance source)
    {
        var clone = new ApFunctionInstance
        {
            Name = source.Name,
            UniqueId = source.UniqueId,
            TypeIdRef = source.TypeIdRef
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        return clone;
    }

    private static ApTemplateList? CloneTemplateList(ApTemplateList? source)
    {
        if (source == null)
            return null;

        var clone = new ApTemplateList();
        foreach (var template in source.ParameterTemplates)
            clone.ParameterTemplates.Add(CloneParameterTemplate(template));
        foreach (var template in source.AllowedValuesTemplates)
            clone.AllowedValuesTemplates.Add(CloneAllowedValuesTemplate(template));
        return clone;
    }

    private static ApParameterTemplate CloneParameterTemplate(ApParameterTemplate source)
    {
        var clone = new ApParameterTemplate
        {
            UniqueId = source.UniqueId,
            Access = source.Access,
            AccessList = source.AccessList,
            Support = source.Support,
            Persistent = source.Persistent,
            Offset = source.Offset,
            Multiplier = source.Multiplier,
            TypeRef = CloneTypeRef(source.TypeRef),
            ActualValue = CloneParameterValue(source.ActualValue),
            DefaultValue = CloneParameterValue(source.DefaultValue),
            SubstituteValue = CloneParameterValue(source.SubstituteValue),
            AllowedValues = CloneAllowedValues(source.AllowedValues),
            Unit = CloneUnit(source.Unit)
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        clone.ConditionalSupports.AddRange(source.ConditionalSupports);
        foreach (var property in source.Properties)
        {
            clone.Properties.Add(new ApProperty
            {
                Name = property.Name,
                Value = property.Value
            });
        }
        return clone;
    }

    private static ApAllowedValuesTemplate CloneAllowedValuesTemplate(ApAllowedValuesTemplate source)
    {
        var clone = new ApAllowedValuesTemplate
        {
            UniqueId = source.UniqueId
        };
        foreach (var value in source.Values)
            clone.Values.Add(CloneParameterValue(value)!);
        foreach (var range in source.Ranges)
            clone.Ranges.Add(CloneAllowedRange(range));
        return clone;
    }

    private static ApParameter CloneParameter(ApParameter source)
    {
        var clone = new ApParameter
        {
            UniqueId = source.UniqueId,
            Access = source.Access,
            AccessList = source.AccessList,
            Support = source.Support,
            Persistent = source.Persistent,
            Offset = source.Offset,
            Multiplier = source.Multiplier,
            TemplateIdRef = source.TemplateIdRef,
            TypeRef = CloneTypeRef(source.TypeRef),
            Denotation = CloneLabelGroup(source.Denotation),
            ActualValue = CloneParameterValue(source.ActualValue),
            DefaultValue = CloneParameterValue(source.DefaultValue),
            SubstituteValue = CloneParameterValue(source.SubstituteValue),
            AllowedValues = CloneAllowedValues(source.AllowedValues),
            Unit = CloneUnit(source.Unit)
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        clone.ConditionalSupports.AddRange(source.ConditionalSupports);
        foreach (var variableRef in source.VariableRefs)
            clone.VariableRefs.Add(CloneVariableRef(variableRef));
        foreach (var property in source.Properties)
        {
            clone.Properties.Add(new ApProperty
            {
                Name = property.Name,
                Value = property.Value
            });
        }
        return clone;
    }

    private static ApVariableRef CloneVariableRef(ApVariableRef source)
    {
        var clone = new ApVariableRef
        {
            Position = source.Position,
            VariableIdRef = source.VariableIdRef,
            MemberRef = source.MemberRef == null
                ? null
                : new ApMemberRef
                {
                    UniqueIdRef = source.MemberRef.UniqueIdRef,
                    Index = source.MemberRef.Index
                }
        };
        clone.InstanceIdRefs.AddRange(source.InstanceIdRefs);
        return clone;
    }

    private static ApAllowedValues? CloneAllowedValues(ApAllowedValues? source)
    {
        if (source == null)
            return null;

        var clone = new ApAllowedValues
        {
            TemplateIdRef = source.TemplateIdRef
        };
        foreach (var value in source.Values)
            clone.Values.Add(CloneParameterValue(value)!);
        foreach (var range in source.Ranges)
            clone.Ranges.Add(CloneAllowedRange(range));
        return clone;
    }

    private static ApAllowedRange CloneAllowedRange(ApAllowedRange source)
    {
        return new ApAllowedRange
        {
            MinValue = CloneParameterValue(source.MinValue),
            MaxValue = CloneParameterValue(source.MaxValue),
            Step = CloneParameterValue(source.Step)
        };
    }

    private static ApParameterValue? CloneParameterValue(ApParameterValue? source)
    {
        if (source == null)
            return null;

        var clone = new ApParameterValue
        {
            Value = source.Value,
            Offset = source.Offset,
            Multiplier = source.Multiplier
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        return clone;
    }

    private static ApUnit? CloneUnit(ApUnit? source)
    {
        if (source == null)
            return null;

        var clone = new ApUnit
        {
            Multiplier = source.Multiplier,
            UnitUri = source.UnitUri
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        return clone;
    }

    private static ApParameterGroup CloneParameterGroup(ApParameterGroup source)
    {
        var clone = new ApParameterGroup
        {
            UniqueId = source.UniqueId,
            KindOfAccess = source.KindOfAccess
        };
        CopyLabelGroup(source.LabelGroup, clone.LabelGroup);
        clone.ParameterRefs.AddRange(source.ParameterRefs);
        foreach (var subGroup in source.SubGroups)
            clone.SubGroups.Add(CloneParameterGroup(subGroup));
        return clone;
    }

    private static ApLabelGroup? CloneLabelGroup(ApLabelGroup? source)
    {
        if (source == null)
            return null;
        var clone = new ApLabelGroup();
        CopyLabelGroup(source, clone);
        return clone;
    }

    private static void CopyLabelGroup(ApLabelGroup source, ApLabelGroup target)
    {
        foreach (var label in source.Labels)
        {
            target.Labels.Add(new ApLabel
            {
                Lang = label.Lang,
                Text = label.Text
            });
        }

        foreach (var description in source.Descriptions)
        {
            target.Descriptions.Add(new ApDescription
            {
                Lang = description.Lang,
                Text = description.Text,
                Uri = description.Uri
            });
        }

        foreach (var textRef in source.TextRefs)
        {
            target.TextRefs.Add(new ApTextRef
            {
                DictId = textRef.DictId,
                TextId = textRef.TextId,
                Uri = textRef.Uri,
                IsDescriptionRef = textRef.IsDescriptionRef
            });
        }
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
