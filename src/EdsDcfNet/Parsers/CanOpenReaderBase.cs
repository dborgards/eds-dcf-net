namespace EdsDcfNet.Parsers;

using System.Globalization;

using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Abstract base class for EDS and DCF readers.
/// Contains the polymorphic CANopen INI parsing extension points that vary
/// per format; shared stateless section parsing lives in
/// <see cref="CanOpenSectionParsers"/> and <see cref="IniParser"/>.
/// </summary>
public abstract class CanOpenReaderBase
{
    /// <summary>
    /// Section names that are considered "known" for this file format.
    /// Unknown sections are preserved in AdditionalSections for round-trip fidelity.
    /// </summary>
    protected abstract string[] KnownSectionNames { get; }

    /// <summary>
    /// Template method that parses all sections shared between EDS and DCF files into
    /// <paramref name="model"/>. The parse order (FileInfo, DeviceInfo, format-specific
    /// pre-object sections, ObjectDictionary, Comments, format-specific post-object
    /// sections, SupportedModules, DynamicChannels, Tools, additional sections) is part
    /// of the observable behavior: it determines which parse exception surfaces first
    /// for files with multiple defects and must not change.
    /// </summary>
    /// <remarks>
    /// The shared section parsers in <see cref="CanOpenSectionParsers"/> self-guard
    /// against absent sections (they default counts to 0 and return empty results, or
    /// <see langword="null"/> for <c>[DynamicChannels]</c>), so no
    /// <c>HasSection</c> pre-checks are needed here.
    /// </remarks>
    private protected void ParseCommonSections(
        ICanOpenFileModel model,
        Dictionary<string, Dictionary<string, string>> sections)
    {
        model.FileInfo = ParseFileInfo(sections);
        model.DeviceInfo = CanOpenSectionParsers.ParseDeviceInfo(sections);
        ParsePreObjectDictionarySections(model, sections);
        model.ObjectDictionary = ParseObjectDictionary(sections);
        model.Comments = CanOpenSectionParsers.ParseComments(sections);
        ParsePostObjectDictionarySections(model, sections);

        model.SupportedModules.AddRange(CanOpenSectionParsers.ParseSupportedModules(sections));
        model.DynamicChannels = CanOpenSectionParsers.ParseDynamicChannels(sections);
        model.Tools.AddRange(CanOpenSectionParsers.ParseTools(sections));

        // Preserve any unknown sections for round-trip fidelity.
        foreach (var sectionName in sections.Keys)
        {
            if (!IsKnownSection(sectionName) &&
                !IsToolSectionForParsedTools(sectionName, model.Tools.Count) &&
                !IsSectionHandledByFormat(sectionName, model))
            {
                model.AdditionalSections[sectionName] =
                    new Dictionary<string, string>(sections[sectionName], StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Extension point for format-specific sections that must be parsed after
    /// <c>[DeviceInfo]</c> but before the object dictionary (DCF: <c>[DeviceCommissioning]</c>).
    /// </summary>
    private protected virtual void ParsePreObjectDictionarySections(
        ICanOpenFileModel model,
        Dictionary<string, Dictionary<string, string>> sections)
    {
    }

    /// <summary>
    /// Extension point for format-specific sections that must be parsed after
    /// <c>[Comments]</c> but before the shared module/tool sections (DCF: <c>[ConnectedModules]</c>).
    /// </summary>
    private protected virtual void ParsePostObjectDictionarySections(
        ICanOpenFileModel model,
        Dictionary<string, Dictionary<string, string>> sections)
    {
    }

    /// <summary>
    /// Extension point that reports whether a section was already captured by
    /// format-specific parsing and therefore must not be preserved in
    /// <c>AdditionalSections</c> (DCF: <c>ObjectLinks</c> of existing objects;
    /// shared: consumed <c>[xxxxName]</c> for compact objects).
    /// </summary>
    private protected virtual bool IsSectionHandledByFormat(string sectionName, ICanOpenFileModel model)
        => IsCompactNameSectionForExistingObject(sectionName, model.ObjectDictionary);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="sectionName"/> is a
    /// <c>[xxxxName]</c> section for an object that uses CompactSubObj storage
    /// (and was therefore consumed by <see cref="ApplyCompactNameOverrides"/>).
    /// Orphan name sections remain in <c>AdditionalSections</c> for round-trip.
    /// </summary>
    private protected static bool IsCompactNameSectionForExistingObject(
        string sectionName,
        ObjectDictionary objectDictionary)
    {
        if (!IsHexPrefixedSection(sectionName, "Name"))
            return false;

        var prefix = sectionName[..^"Name".Length];
        if (!ushort.TryParse(prefix, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var index))
            return false;

        return objectDictionary.Objects.TryGetValue(index, out var obj)
               && obj.CompactSubObj.HasValue
               && obj.CompactSubObj.Value > 0;
    }

    /// <summary>
    /// Parses the <c>[FileInfo]</c> section into an <see cref="EdsFileInfo"/> object.
    /// Derived classes may override this to read additional format-specific fields.
    /// </summary>
    /// <remarks>
    /// <c>[FileInfo]</c> is treated as <b>optional</b>: CiA 306 recommends the section but
    /// does not make it mandatory, and many real-world EDS/DCF files omit individual fields
    /// or the section entirely. When the section is absent an empty <see cref="EdsFileInfo"/>
    /// with default values is returned so that the rest of the file can still be parsed.
    /// </remarks>
    protected virtual EdsFileInfo ParseFileInfo(Dictionary<string, Dictionary<string, string>> sections)
    {
        var fileInfo = new EdsFileInfo();

        // [FileInfo] is optional — return defaults when the section is absent.
        if (!IniParser.HasSection(sections, "FileInfo"))
            return fileInfo;

        fileInfo.FileName = IniParser.GetValue(sections, "FileInfo", "FileName");
        fileInfo.FileVersion = ValueConverter.ParseByte(IniParser.GetValue(sections, "FileInfo", "FileVersion", "1"));
        fileInfo.FileRevision = ValueConverter.ParseByte(IniParser.GetValue(sections, "FileInfo", "FileRevision", "0"));
        fileInfo.EdsVersion = IniParser.GetValue(sections, "FileInfo", "EDSVersion", "4.0");
        fileInfo.Description = IniParser.GetValue(sections, "FileInfo", "Description");
        fileInfo.CreationTime = IniParser.GetValue(sections, "FileInfo", "CreationTime");
        fileInfo.CreationDate = IniParser.GetValue(sections, "FileInfo", "CreationDate");
        fileInfo.CreatedBy = IniParser.GetValue(sections, "FileInfo", "CreatedBy");
        fileInfo.ModificationTime = IniParser.GetValue(sections, "FileInfo", "ModificationTime");
        fileInfo.ModificationDate = IniParser.GetValue(sections, "FileInfo", "ModificationDate");
        fileInfo.ModifiedBy = IniParser.GetValue(sections, "FileInfo", "ModifiedBy");

        return fileInfo;
    }

    /// <summary>
    /// Parses the mandatory, optional, and manufacturer object sections into an
    /// <see cref="ObjectDictionary"/>, including all sub-objects and dummy usage entries.
    /// </summary>
    protected ObjectDictionary ParseObjectDictionary(Dictionary<string, Dictionary<string, string>> sections)
    {
        var objDict = new ObjectDictionary();

        ParseObjectListSection(sections, "MandatoryObjects", objDict.MandatoryObjects);
        ParseObjectListSection(sections, "OptionalObjects", objDict.OptionalObjects);
        ParseObjectListSection(sections, "ManufacturerObjects", objDict.ManufacturerObjects);

        // Parse all object definitions
        var allObjects = objDict.MandatoryObjects
            .Concat(objDict.OptionalObjects)
            .Concat(objDict.ManufacturerObjects)
            .Distinct();

        foreach (var index in allObjects)
        {
            var obj = ParseObject(sections, index);
            if (obj != null)
            {
                objDict.Objects[index] = obj;
            }
        }

        // Parse dummy usage
        if (IniParser.HasSection(sections, "DummyUsage"))
        {
            foreach (var key in IniParser.GetKeys(sections, "DummyUsage"))
            {
                if (key.StartsWith("Dummy", StringComparison.OrdinalIgnoreCase) && key.Length > 5)
                {
                    var indexStr = key[5..];
                    if (ushort.TryParse(indexStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var index))
                    {
                        objDict.DummyUsage[index] = ValueConverter.ParseBoolean(
                            IniParser.GetValue(sections, "DummyUsage", key));
                    }
                }
            }
        }

        return objDict;
    }

    private static void ParseObjectListSection(
        Dictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        List<ushort> targetList)
    {
        if (!IniParser.HasSection(sections, sectionName))
            return;

        var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, sectionName, "SupportedObjects", "0"));
        for (int i = 1; i <= count; i++)
        {
            var indexStr = IniParser.GetValue(sections, sectionName, i.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(indexStr))
            {
                targetList.Add(ValueConverter.ParseUInt16(indexStr));
            }
        }
    }

    /// <summary>
    /// Parses a single CANopen object at the given <paramref name="index"/> from the INI sections.
    /// Returns <see langword="null"/> if no section exists for that index.
    /// Derived classes may override this to read additional format-specific fields.
    /// </summary>
    protected virtual CanOpenObject? ParseObject(Dictionary<string, Dictionary<string, string>> sections, ushort index)
    {
        var sectionName = ToHexInvariant(index);
        if (!IniParser.HasSection(sections, sectionName))
            return null;

        var obj = new CanOpenObject
        {
            Index = index,
            ParameterName = IniParser.GetValue(sections, sectionName, "ParameterName"),
            ObjectType = ValueConverter.ParseByte(IniParser.GetValue(sections, sectionName, "ObjectType", CanOpenObjectType.VarLiteral))
        };

        var dataTypeStr = IniParser.GetValue(sections, sectionName, "DataType");
        if (!string.IsNullOrEmpty(dataTypeStr))
        {
            obj.DataType = ValueConverter.ParseUInt16(dataTypeStr);
        }

        var accessTypeStr = IniParser.GetValue(sections, sectionName, "AccessType");
        if (!string.IsNullOrEmpty(accessTypeStr))
        {
            obj.AccessType = ValueConverter.ParseAccessType(accessTypeStr);
        }

        obj.DefaultValue = IniParser.GetValue(sections, sectionName, "DefaultValue");
        obj.LowLimit = IniParser.GetValue(sections, sectionName, "LowLimit");
        obj.HighLimit = IniParser.GetValue(sections, sectionName, "HighLimit");
        obj.PdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "PDOMapping"));
        obj.SrdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "SRDOMapping"));
        obj.InvertedSrad = IniParser.GetValue(sections, sectionName, "InvertedSRAD");
        obj.ObjFlags = ValueConverter.ParseInteger(IniParser.GetValue(sections, sectionName, "ObjFlags", "0"));

        var subNumberStr = IniParser.GetValue(sections, sectionName, "SubNumber");
        if (!string.IsNullOrEmpty(subNumberStr))
        {
            obj.SubNumber = ValueConverter.ParseByte(subNumberStr);
        }

        var compactSubObjStr = IniParser.GetValue(sections, sectionName, "CompactSubObj");
        if (!string.IsNullOrEmpty(compactSubObjStr))
        {
            obj.CompactSubObj = ValueConverter.ParseByte(compactSubObjStr);
        }

        // Parse sub-objects for composite types, CompactSubObj templates (CiA 306 §4.5.2.4.2),
        // or an explicit SubNumber. CompactSubObj may be non-zero while SubNumber is 0/absent.
        if (obj.SubNumber > 0 ||
            (obj.CompactSubObj.HasValue && obj.CompactSubObj.Value > 0) ||
            CanOpenObjectType.HasSubObjects(obj.ObjectType))
        {
            ParseSubObjects(sections, index, obj);
        }

        // Parse object links
        var linksSectionName = string.Concat(ToHexInvariant(index), "ObjectLinks");
        if (IniParser.HasSection(sections, linksSectionName))
        {
            var count = ValueConverter.ParseUInt16(IniParser.GetValue(sections, linksSectionName, "ObjectLinks", "0"));
            for (int i = 1; i <= count; i++)
            {
                var linkStr = IniParser.GetValue(sections, linksSectionName, i.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(linkStr))
                {
                    obj.ObjectLinks.Add(ValueConverter.ParseUInt16(linkStr));
                }
            }
        }

        return obj;
    }

    /// <summary>
    /// Parses all sub-objects for the given <paramref name="obj"/> and populates
    /// <see cref="CanOpenObject.SubObjects"/>.
    /// When <see cref="CanOpenObject.CompactSubObj"/> is non-zero, missing
    /// <c>[xxxsubN]</c> sections are synthesized from the parent object template
    /// per CiA 306 §4.5.2.4.2 (sub-indexes <c>0..min(CompactSubObj, 254)</c>;
    /// sub-index <c>0xFF</c> is never synthesized), then optional <c>[xxxxName]</c>
    /// overrides are applied.
    /// Derived classes may override this to handle additional compact storage formats.
    /// </summary>
    protected virtual void ParseSubObjects(Dictionary<string, Dictionary<string, string>> sections, ushort index, CanOpenObject obj)
    {
        // Determine the number of sub-objects to parse
        var maxSubIndex = (int)(obj.SubNumber ?? 0);
        var compactSubObj = obj.CompactSubObj.GetValueOrDefault();
        // CiA 306 compact lists use sub-indexes 1..254; never synthesize 0xFF.
        var compactMax = compactSubObj == 0
            ? 0
            : Math.Min((int)compactSubObj, MaxCompactListableSubIndex);
        if (compactMax > 0)
        {
            maxSubIndex = Math.Max(maxSubIndex, compactMax);
        }

        // Use an int loop counter so a max sub-index of 0xFF does not wrap back to 0.
        for (var subIndex = 0; subIndex <= maxSubIndex; subIndex++)
        {
            var subIndexValue = (byte)subIndex;
            var subObj = ParseSubObject(sections, index, subIndexValue);
            if (subObj == null && compactMax > 0 && subIndex <= compactMax)
            {
                subObj = SynthesizeCompactSubObject(obj, subIndexValue);
            }

            if (subObj != null)
            {
                obj.SubObjects[subIndexValue] = subObj;
            }
        }

        if (compactMax > 0)
        {
            ApplyCompactNameOverrides(sections, index, obj);
        }
    }

    /// <summary>
    /// Builds a sub-object from the parent object template when CompactSubObj storage
    /// omits an individual <c>[xxxsubN]</c> section (CiA 306 §4.5.2.4.2).
    /// </summary>
    private static CanOpenSubObject SynthesizeCompactSubObject(CanOpenObject parent, byte subIndex)
    {
        if (subIndex == 0)
        {
            return new CanOpenSubObject
            {
                SubIndex = 0,
                ParameterName = "NrOfObjects",
                ObjectType = CanOpenObjectType.Var,
                DataType = Unsigned8DataType,
                AccessType = AccessType.ReadOnly,
                DefaultValue = parent.CompactSubObj!.Value.ToString(CultureInfo.InvariantCulture),
                PdoMapping = false
            };
        }

        return new CanOpenSubObject
        {
            SubIndex = subIndex,
            ParameterName = string.Concat(
                parent.ParameterName,
                subIndex.ToString(CultureInfo.InvariantCulture)),
            ObjectType = CanOpenObjectType.Var,
            DataType = parent.DataType ?? 0,
            AccessType = parent.AccessType,
            DefaultValue = parent.DefaultValue,
            PdoMapping = parent.PdoMapping
        };
    }

    /// <summary>
    /// Applies optional <c>[xxxxName]</c> parameter-name overrides for compact sub-objects
    /// (CiA 306 §4.5.2.4.2). Keys are decimal sub-indexes (1..254) and may be sparse;
    /// <c>NrOfEntries</c> is the list length, not a loop bound.
    /// </summary>
    private static void ApplyCompactNameOverrides(
        Dictionary<string, Dictionary<string, string>> sections,
        ushort index,
        CanOpenObject obj)
    {
        var nameSection = string.Concat(ToHexInvariant(index), "Name");
        if (!IniParser.HasSection(sections, nameSection))
            return;

        foreach (var entry in sections[nameSection])
        {
            if (entry.Key.Equals("NrOfEntries", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.IsNullOrEmpty(entry.Value))
                continue;

            if (!byte.TryParse(entry.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var subIndex))
                continue;

            if (subIndex < 1 || subIndex > MaxCompactListableSubIndex)
                continue;

            if (obj.SubObjects.TryGetValue(subIndex, out var subObj))
            {
                subObj.ParameterName = entry.Value;
            }
        }
    }

    /// <summary>CiA 301 / CiA 306 UNSIGNED8 data-type index.</summary>
    private const ushort Unsigned8DataType = 0x0005;

    /// <summary>
    /// Highest sub-index covered by CompactSubObj value/name/denotation lists (CiA 306).
    /// Sub-index <c>0xFF</c> is reserved and is never synthesized from the template.
    /// </summary>
    private protected const int MaxCompactListableSubIndex = 254;

    /// <summary>
    /// Parses a single sub-object at the given <paramref name="index"/> and <paramref name="subIndex"/>.
    /// Returns <see langword="null"/> if no section exists for that sub-object.
    /// Derived classes may override this to read additional format-specific fields.
    /// </summary>
    protected virtual CanOpenSubObject? ParseSubObject(Dictionary<string, Dictionary<string, string>> sections, ushort index, byte subIndex)
    {
        var sectionName = string.Concat(ToHexInvariant(index), "sub", ToHexInvariant(subIndex));
        if (!IniParser.HasSection(sections, sectionName))
            return null;

        var subObj = new CanOpenSubObject
        {
            SubIndex = subIndex,
            ParameterName = IniParser.GetValue(sections, sectionName, "ParameterName"),
            ObjectType = ValueConverter.ParseByte(IniParser.GetValue(sections, sectionName, "ObjectType", CanOpenObjectType.VarLiteral)),
            DataType = ValueConverter.ParseUInt16(IniParser.GetValue(sections, sectionName, "DataType", "0")),
            AccessType = ValueConverter.ParseAccessType(IniParser.GetValue(sections, sectionName, "AccessType")),
            DefaultValue = IniParser.GetValue(sections, sectionName, "DefaultValue"),
            LowLimit = IniParser.GetValue(sections, sectionName, "LowLimit"),
            HighLimit = IniParser.GetValue(sections, sectionName, "HighLimit"),
            PdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "PDOMapping")),
            SrdoMapping = ValueConverter.ParseBoolean(IniParser.GetValue(sections, sectionName, "SRDOMapping")),
            InvertedSrad = IniParser.GetValue(sections, sectionName, "InvertedSRAD")
        };

        return subObj;
    }

    /// <summary>
    /// Determines whether <paramref name="sectionName"/> is a known section for this file format.
    /// Unknown sections are preserved in <c>AdditionalSections</c> for round-trip fidelity.
    /// Derived classes may override this to recognise additional format-specific sections.
    /// </summary>
    protected virtual bool IsKnownSection(string sectionName)
    {
        if (KnownSectionNames.Contains(sectionName, StringComparer.OrdinalIgnoreCase))
            return true;

        // Check for object sections (hex index)
        if (ushort.TryParse(sectionName, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            return true;

        // Check for sub-object sections (hex index + "sub" + hex subindex)
        if (IsSubObjectSection(sectionName))
            return true;

        // Check for module sections (M + digits + known suffix)
        if (IsModuleSection(sectionName))
            return true;

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="sectionName"/> matches a
    /// <c>[Tool{n}]</c> section for one of the already-parsed tools (1 ≤ n ≤ <paramref name="parsedToolCount"/>).
    /// Used to avoid treating tool data sections as unknown additional sections.
    /// </summary>
    protected static bool IsToolSectionForParsedTools(string sectionName, int parsedToolCount)
    {
        if (!sectionName.StartsWith("Tool", StringComparison.OrdinalIgnoreCase) || sectionName.Length <= 4)
            return false;

        if (!int.TryParse(sectionName[4..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var toolNumber))
            return false;

        return toolNumber >= 1 && toolNumber <= parsedToolCount;
    }

    /// <summary>
    /// Checks if a section name matches the sub-object pattern: {HexIndex}sub{HexSubIndex}.
    /// </summary>
    protected static bool IsSubObjectSection(string sectionName)
    {
        var subPos = sectionName.IndexOf("sub", StringComparison.OrdinalIgnoreCase);
        if (subPos < 1)
            return false;

        var prefix = sectionName[..subPos];
        return ushort.TryParse(prefix, NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    /// Checks if a section name has a valid hex object index prefix followed by the given suffix.
    /// </summary>
    protected static bool IsHexPrefixedSection(string sectionName, string suffix)
    {
        if (!sectionName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var prefix = sectionName[..^suffix.Length];
        return prefix.Length > 0 && ushort.TryParse(prefix,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    /// Formats an object index as uppercase hexadecimal using invariant culture.
    /// </summary>
    protected static string ToHexInvariant(ushort value)
        => value.ToString("X", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a sub-index as uppercase hexadecimal using invariant culture.
    /// </summary>
    protected static string ToHexInvariant(byte value)
        => value.ToString("X", CultureInfo.InvariantCulture);

    /// <summary>
    /// Checks if a section name matches a module section pattern: M{Digits}{KnownSuffix}.
    /// </summary>
    protected static bool IsModuleSection(string sectionName)
    {
        if (sectionName.Length < 2 ||
            !sectionName.StartsWith("M", StringComparison.OrdinalIgnoreCase))
            return false;

        // Must have at least one digit after "M"
        var i = 1;
        while (i < sectionName.Length && char.IsDigit(sectionName[i]))
            i++;

        if (i == 1)
            return false;

        // The suffix after "M{digits}" must be a known module suffix
        var suffix = sectionName[i..];
        return suffix.Equals("ModuleInfo", StringComparison.OrdinalIgnoreCase) ||
               suffix.Equals("FixedObjects", StringComparison.OrdinalIgnoreCase) ||
               suffix.StartsWith("SubExtend", StringComparison.OrdinalIgnoreCase) ||
               suffix.StartsWith("SubExt", StringComparison.OrdinalIgnoreCase) ||
               suffix.Equals("Comments", StringComparison.OrdinalIgnoreCase);
    }

    #region Obsolete compatibility shims (kept for external subclasses; removal requires a major release)

    // These forwarding shims preserve the previous protected surface of this public
    // base class. They are instance methods for binary compatibility, hence the
    // CA1822 pragma; the real implementations live in IniParser and
    // CanOpenSectionParsers and carry no suppressions.
#pragma warning disable CA1822 // Mark members as static — obsolete compat shims must stay instance members.

    /// <summary>
    /// Parses INI sections from a file path.
    /// </summary>
    [Obsolete("Use IniParser.ParseFile instead.")]
    protected Dictionary<string, Dictionary<string, string>> ParseSectionsFromFile(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => IniParser.ParseFile(filePath, maxInputSize);

    /// <summary>
    /// Parses INI sections from a file path asynchronously.
    /// </summary>
    [Obsolete("Use IniParser.ParseFileAsync instead.")]
    protected Task<Dictionary<string, Dictionary<string, string>>> ParseSectionsFromFileAsync(
        string filePath,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize,
        CancellationToken cancellationToken = default)
        => IniParser.ParseFileAsync(filePath, maxInputSize, cancellationToken);

    /// <summary>
    /// Parses INI sections from a string.
    /// </summary>
    [Obsolete("Use IniParser.ParseString instead.")]
    protected Dictionary<string, Dictionary<string, string>> ParseSectionsFromString(
        string content,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => IniParser.ParseString(content, maxInputSize);

    /// <summary>
    /// Parses INI sections from a stream.
    /// </summary>
    [Obsolete("Use IniParser.ParseStream instead.")]
    protected Dictionary<string, Dictionary<string, string>> ParseSectionsFromStream(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize)
        => IniParser.ParseStream(stream, maxInputSize);

    /// <summary>
    /// Parses INI sections from a stream asynchronously.
    /// </summary>
    [Obsolete("Use IniParser.ParseStreamAsync instead.")]
    protected Task<Dictionary<string, Dictionary<string, string>>> ParseSectionsFromStreamAsync(
        Stream stream,
        long maxInputSize = ReaderDefaults.DefaultMaxInputSize,
        CancellationToken cancellationToken = default)
        => IniParser.ParseStreamAsync(stream, maxInputSize, cancellationToken);

    /// <summary>
    /// Parses the <c>[DeviceInfo]</c> section into a <see cref="DeviceInfo"/> object.
    /// </summary>
    /// <exception cref="EdsDcfNet.Exceptions.EdsParseException">Thrown when the <c>[DeviceInfo]</c> section is absent.</exception>
    protected DeviceInfo ParseDeviceInfo(Dictionary<string, Dictionary<string, string>> sections)
        => CanOpenSectionParsers.ParseDeviceInfo(sections);

    /// <summary>
    /// Parses the <c>[Comments]</c> section into a <see cref="Comments"/> object,
    /// or returns <see langword="null"/> if the section is absent.
    /// </summary>
    protected Comments? ParseComments(Dictionary<string, Dictionary<string, string>> sections)
        => CanOpenSectionParsers.ParseComments(sections);

    /// <summary>
    /// Parses the <c>[SupportedModules]</c> section and each module's <c>ModuleInfo</c>
    /// section into a list of <see cref="ModuleInfo"/> objects.
    /// </summary>
    protected List<ModuleInfo> ParseSupportedModules(Dictionary<string, Dictionary<string, string>> sections)
        => CanOpenSectionParsers.ParseSupportedModules(sections);

    /// <summary>
    /// Parses the <c>[M{moduleNumber}ModuleInfo]</c> section for the given module number.
    /// Returns <see langword="null"/> if the section does not exist.
    /// </summary>
    protected ModuleInfo? ParseModuleInfo(Dictionary<string, Dictionary<string, string>> sections, int moduleNumber)
        => CanOpenSectionParsers.ParseModuleInfo(sections, moduleNumber);

    /// <summary>
    /// Parses the <c>[DynamicChannels]</c> section into a <see cref="DynamicChannels"/> object,
    /// or returns <see langword="null"/> if the section has no segments.
    /// </summary>
    protected DynamicChannels? ParseDynamicChannels(Dictionary<string, Dictionary<string, string>> sections)
        => CanOpenSectionParsers.ParseDynamicChannels(sections);

    /// <summary>
    /// Parses the <c>[Tools]</c> section and each individual <c>[Tool{n}]</c> section
    /// into a list of <see cref="ToolInfo"/> objects.
    /// </summary>
    protected List<ToolInfo> ParseTools(Dictionary<string, Dictionary<string, string>> sections)
        => CanOpenSectionParsers.ParseTools(sections);

#pragma warning restore CA1822

    #endregion
}
