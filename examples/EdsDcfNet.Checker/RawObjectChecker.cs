namespace EdsDcfNet.Checker;

using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Utilities;

/// <summary>
/// Checks the object dictionary of an EDS/DCF file on the raw INI level: object types,
/// data types, value ranges, limits, <c>$NODEID</c> formulas, sub-index consistency,
/// object lists and PDO mappings.
/// </summary>
public sealed class RawObjectChecker
{
    private const int MaxParameterNameLength = 241;

    private static readonly Regex ObjectSectionPattern = new(
        "^(?<index>[0-9A-Fa-f]{4})$", RegexOptions.CultureInvariant);

    private static readonly Regex SubSectionPattern = new(
        "^(?<index>[0-9A-Fa-f]{4})sub(?<sub>[0-9A-Fa-f]{1,2})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AccessTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ro", "wo", "rw", "rwr", "rww", "const",
    };

    private static readonly byte[] ValidObjectTypes = { 0x0, 0x2, 0x5, 0x6, 0x7, 0x8, 0x9 };

    private static readonly string[] ObjectListSections = { "MandatoryObjects", "OptionalObjects", "ManufacturerObjects" };

    /// <summary>Complex data types predefined by CiA 301 (PDO/SDO parameter, identity).</summary>
    private static readonly HashSet<ushort> PredefinedComplexTypes = new() { 0x0020, 0x0021, 0x0022, 0x0023 };

    private readonly string _file;
    private readonly RawIniDocument _doc;
    private readonly bool _isDcf;
    private readonly List<Finding> _findings;
    private readonly byte[] _nodeIds;

    private readonly Dictionary<ushort, RawSection> _objects = new();
    private readonly Dictionary<ushort, SortedDictionary<byte, RawSection>> _subObjects = new();
    private readonly List<(ushort Index, byte SubIndex, RawSection Section)> _duplicateSubSections = new();

    public RawObjectChecker(string file, RawIniDocument doc, bool isDcf, List<Finding> findings)
    {
        _file = file;
        _doc = doc;
        _isDcf = isDcf;
        _findings = findings;
        _nodeIds = ResolveNodeIds();
    }

    public void Run()
    {
        CollectObjectSections();
        CheckObjectLists();

        foreach (var (index, section) in _objects.OrderBy(o => o.Key))
        {
            CheckObject(index, section);
        }

        foreach (var (index, subIndex, section) in _duplicateSubSections)
        {
            CheckObjectType(section);
            CheckParameterName(section);
            CheckEntryValues(section, index, subIndex);
        }

        foreach (var (index, subs) in _subObjects.OrderBy(o => o.Key))
        {
            if (!_objects.ContainsKey(index))
            {
                foreach (var sub in subs.Values)
                {
                    Add(Severity.Error, "OBJ008", sub, null, null,
                        "Sub-index section without a parent object section [" + Hex4(index) + "].");
                }
            }
        }

        CheckPdoMappings();
    }

    // ---------------------------------------------------------------- setup

    private byte[] ResolveNodeIds()
    {
        // EDS: formulas must hold for every possible node-ID, so evaluate both ends of 1..127.
        byte[] allNodeIds = { 1, 127 };
        if (!_isDcf)
        {
            return allNodeIds;
        }

        var commissioning = _doc.Get("DeviceComissioning") ?? _doc.Get("DeviceCommissioning");
        var entry = commissioning?.Get("NodeID");
        if (commissioning is null || entry is null || string.IsNullOrWhiteSpace(entry.Value))
        {
            _findings.Add(new Finding(Severity.Warning, "DCF001", _file, commissioning?.Line, commissioning?.Name ?? "DeviceComissioning", "NodeID", null,
                "DCF has no configured NodeID; $NODEID formulas are checked for the whole range 1..127."));
            return allNodeIds;
        }

        try
        {
            var nodeId = ValueConverter.ParseInteger(entry.Value);
            if (nodeId is >= 1 and <= 127)
            {
                return new[] { (byte)nodeId };
            }
        }
        catch (Exception ex) when (ex is EdsParseException or FormatException or OverflowException or NotSupportedException)
        {
            // Reported below.
        }

        _findings.Add(new Finding(Severity.Error, "DCF002", _file, entry.Line, commissioning.Name, entry.Key, entry.Value,
            "NodeID must be a number in the range 1..127."));
        return allNodeIds;
    }

    private void CollectObjectSections()
    {
        foreach (var section in _doc.Sections.Values)
        {
            var objectMatch = ObjectSectionPattern.Match(section.Name);
            if (objectMatch.Success)
            {
                _objects[ushort.Parse(objectMatch.Groups["index"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)] = section;
                continue;
            }

            var subMatch = SubSectionPattern.Match(section.Name);
            if (subMatch.Success)
            {
                var index = ushort.Parse(subMatch.Groups["index"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var sub = byte.Parse(subMatch.Groups["sub"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (!_subObjects.TryGetValue(index, out var subs))
                {
                    subs = new SortedDictionary<byte, RawSection>();
                    _subObjects[index] = subs;
                }

                if (subs.TryGetValue(sub, out var existing))
                {
                    // e.g. [1018sub1] and [1018sub01]: different INI sections, same sub-index.
                    Add(Severity.Error, "INI002", section, null, null, string.Format(CultureInfo.InvariantCulture,
                        "Sub-index 0x{0:X2} of {1} is already described by [{2}] (line {3}); readers keep only one of them.",
                        sub, Hex4(index), existing.Name, existing.Line));

                    // Keep the first section; the duplicate is still checked on its own in Run().
                    _duplicateSubSections.Add((index, sub, section));
                    continue;
                }

                subs[sub] = section;
            }
        }
    }

    // ---------------------------------------------------------------- object lists

    private void CheckObjectLists()
    {
        var listed = new Dictionary<ushort, string>();

        foreach (var listName in ObjectListSections)
        {
            var list = _doc.Get(listName);
            if (list is null)
            {
                continue;
            }

            var countEntry = list.Get("SupportedObjects");
            var count = 0;
            if (countEntry is null)
            {
                Add(Severity.Error, "LST001", list, null, null, "SupportedObjects is missing.");
            }
            else if (!TryParseInt(countEntry.Value, out count))
            {
                Add(Severity.Error, "LST001", list, countEntry, "SupportedObjects is not a valid number.");
            }

            var entryCount = list.Entries.Keys.Count(k => !k.Equals("SupportedObjects", StringComparison.OrdinalIgnoreCase));
            if (countEntry is not null && entryCount != count)
            {
                Add(Severity.Error, "LST001", list, countEntry, string.Format(CultureInfo.InvariantCulture,
                    "SupportedObjects={0} but the section contains {1} entries.", count, entryCount));
            }

            foreach (var entry in list.Entries.Values)
            {
                if (entry.Key.Equals("SupportedObjects", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryParseInt(entry.Key, out var position) || position < 1 || position > count)
                {
                    Add(Severity.Error, "LST006", list, entry, string.Format(CultureInfo.InvariantCulture,
                        "List position '{0}' is outside 1..{1}.", entry.Key, count));
                }

                ushort index;
                try
                {
                    index = ValueConverter.ParseUInt16(entry.Value);
                }
                catch (EdsParseException)
                {
                    Add(Severity.Error, "LST006", list, entry, "Entry is not a valid object index.");
                    continue;
                }

                if (listed.TryGetValue(index, out var otherList))
                {
                    Add(Severity.Error, "LST004", list, entry,
                        "Object " + Hex4(index) + " is already listed in [" + otherList + "].");
                    continue;
                }

                listed[index] = listName;

                var expectedList = index is >= 0x2000 and <= 0x5FFF ? "ManufacturerObjects" : "OptionalObjects";
                if (listName != "MandatoryObjects" && listName != expectedList)
                {
                    Add(Severity.Warning, "LST007", list, entry,
                        "Object " + Hex4(index) + " belongs in [" + expectedList + "] (CiA 306-1 Table 4: 2000h-5FFFh manufacturer, others optional).");
                }

                if (!_objects.ContainsKey(index))
                {
                    Add(Severity.Error, "LST002", list, entry,
                        "Listed object " + Hex4(index) + " has no [" + Hex4(index) + "] section.");
                }
            }
        }

        foreach (var (index, section) in _objects)
        {
            if (!listed.ContainsKey(index))
            {
                Add(Severity.Error, "LST003", section, null, null,
                    "Object is not listed in [MandatoryObjects], [OptionalObjects] or [ManufacturerObjects]; EdsDcfNet and other list-driven tools ignore it.");
            }
        }

        foreach (ushort mandatory in new ushort[] { 0x1000, 0x1001, 0x1018 })
        {
            if (!_objects.ContainsKey(mandatory))
            {
                _findings.Add(new Finding(Severity.Error, "LST005", _file, null, Hex4(mandatory), null, null,
                    "Mandatory CiA 301 object " + Hex4(mandatory) + " is missing."));
            }
            else if (listed.TryGetValue(mandatory, out var listName) && listName != "MandatoryObjects")
            {
                Add(Severity.Warning, "LST005", _objects[mandatory], null, null,
                    "Mandatory object " + Hex4(mandatory) + " is listed in [" + listName + "] instead of [MandatoryObjects].");
            }
        }
    }

    // ---------------------------------------------------------------- objects

    private void CheckObject(ushort index, RawSection section)
    {
        var objectType = CheckObjectType(section);
        CheckParameterName(section);

        _subObjects.TryGetValue(index, out var subs);
        // SubNumber does not count the reserved sub-index FFh (CiA 306-1 Table 6).
        var subCount = subs?.Keys.Count(k => k != 0xFF) ?? 0;
        var compactSubObj = ParseOptionalByte(section, "CompactSubObj");
        var subNumber = ParseOptionalByte(section, "SubNumber");

        var isComposite = objectType is CanOpenObjectType.Array or CanOpenObjectType.Record or CanOpenObjectType.DefStruct;
        CheckedValues? compactTemplate = null;
        // CompactSubObj still generates sub-indices when ObjectType is omitted (it defaults to VAR).
        if (isComposite || compactSubObj is > 0)
        {
            if (compactSubObj is > 0 &&
                objectType is CanOpenObjectType.Var or CanOpenObjectType.Domain or CanOpenObjectType.DefType)
            {
                Add(Severity.Error, "OBJ006", section, section.Get("CompactSubObj"),
                    "Object with ObjectType VAR/DOMAIN/DEFTYPE must not have a non-zero CompactSubObj (ObjectType defaults to VAR when omitted). Compact arrays use ARRAY (0x8).");
            }

            if (compactSubObj is > 0)
            {
                // A nonzero SubNumber is valid when it covers explicit sub-indices above
                // the compact range. IniWriterBase.ResolveSubNumberForWrite emits that
                // form (CompactSubObj=2, SubNumber=255, [2100subFF]). Without such
                // entries SubNumber must stay 0, empty or absent. 0/absent is still
                // accepted: the reader scans every sub-index regardless of SubNumber.
                if (subNumber is > 0)
                {
                    var compactMax = Math.Min(compactSubObj.Value, (byte)254);
                    byte? highestBeyond = null;
                    if (subs is not null)
                    {
                        foreach (var subIndex in subs.Keys)
                        {
                            if (subIndex <= compactMax)
                            {
                                continue;
                            }

                            if (highestBeyond is null || subIndex > highestBeyond.Value)
                            {
                                highestBeyond = subIndex;
                            }
                        }
                    }

                    if (highestBeyond is null)
                    {
                        Add(Severity.Error, "OBJ006", section, section.Get("SubNumber"),
                            "SubNumber is not supported together with a non-zero CompactSubObj; it shall be 0, empty or absent.");
                    }
                    else if (subNumber.Value < highestBeyond.Value)
                    {
                        Add(Severity.Error, "OBJ006", section, section.Get("SubNumber"), string.Format(CultureInfo.InvariantCulture,
                            "SubNumber={0} does not cover expanded sub-index {1} above CompactSubObj range 1..{2}.",
                            subNumber.Value, highestBeyond.Value, compactMax));
                    }
                }

                // Compact array: sub-indices are generated from the parent template.
                // DCF stores commissioned sub-object values in [XXXXValue] (checked below).
                compactTemplate = CheckEntryValues(section, index, null);
            }
            else if (subNumber is null)
            {
                Add(Severity.Error, "OBJ006", section, null, null,
                    "ARRAY/RECORD object has neither SubNumber nor CompactSubObj.");
            }
            else if (subNumber.Value != subCount &&
                     !(subNumber.Value == 0 && subCount == 1 && subs!.ContainsKey(0)))
            {
                // SubNumber=0 with only [XXXXsub0] is accepted like in CanOpenModelValidator.
                Add(Severity.Error, "OBJ006", section, section.Get("SubNumber"), string.Format(CultureInfo.InvariantCulture,
                    "SubNumber={0} but {1} sub-index section(s) exist.", subNumber.Value, subCount));
            }

            // [XXXXValue] overrides are applied to explicit sub-objects with their own data type
            // and limits (DcfReader); on compact lists the remaining ones use the parent template.
            var explicitValues = new Dictionary<byte, CheckedValues>();
            if (subs is not null)
            {
                CheckSubIndexZero(index, subs, compactSubObj);
                foreach (var (subIndex, subSection) in subs)
                {
                    CheckObjectType(subSection);
                    CheckParameterName(subSection);

                    // DEFSTRUCT sub-indices describe member types, not values.
                    if (objectType != CanOpenObjectType.DefStruct)
                    {
                        var checkedSub = CheckEntryValues(subSection, index, subIndex);
                        if (checkedSub is not null)
                        {
                            explicitValues[subIndex] = checkedSub.Value;
                        }
                    }
                }
            }

            if (compactSubObj is > 0)
            {
                if (compactTemplate is not null)
                {
                    CheckCompactValueEntries(section, index, compactSubObj.Value, compactTemplate.Value, explicitValues);
                }
            }
            else
            {
                CheckExpandedValueOverrides(index, explicitValues);
            }
        }
        else if (objectType is CanOpenObjectType.Var or CanOpenObjectType.Domain or CanOpenObjectType.DefType)
        {
            if (subCount > 0 || subNumber is > 0)
            {
                Add(Severity.Error, "OBJ006", section, section.Get("SubNumber"),
                    "Object with ObjectType VAR/DOMAIN/DEFTYPE must not have sub-indices.");
            }

            if (objectType == CanOpenObjectType.Var)
            {
                CheckEntryValues(section, index, null);
            }
        }
        else if (objectType is null && subCount == 0)
        {
            // Invalid ObjectType (already reported): still check the values as if it were a VAR.
            CheckEntryValues(section, index, null);
        }
    }

    private byte? CheckObjectType(RawSection section)
    {
        var entry = section.Get("ObjectType");
        if (entry is null || string.IsNullOrWhiteSpace(entry.Value))
        {
            // CiA 306: ObjectType defaults to VAR.
            return CanOpenObjectType.Var;
        }

        byte objectType;
        try
        {
            objectType = ValueConverter.ParseByte(entry.Value);
        }
        catch (EdsParseException)
        {
            Add(Severity.Error, "OBJ001", section, entry, "ObjectType is not a valid number.");
            return null;
        }

        if (Array.IndexOf(ValidObjectTypes, objectType) < 0)
        {
            Add(Severity.Error, "OBJ001", section, entry, string.Format(CultureInfo.InvariantCulture,
                "ObjectType 0x{0:X} is not a valid CiA 306 object code (0x0, 0x2, 0x5-0x9).", objectType));
            return null;
        }

        return objectType;
    }

    private void CheckParameterName(RawSection section)
    {
        var entry = section.Get("ParameterName");
        if (entry is null || string.IsNullOrWhiteSpace(entry.Value))
        {
            Add(Severity.Error, "OBJ010", section, entry, "Mandatory entry ParameterName is missing or empty (CiA 306-1 Table 7).");
        }
        else if (entry.Value.Length > MaxParameterNameLength)
        {
            Add(Severity.Error, "OBJ010", section, entry, string.Format(CultureInfo.InvariantCulture,
                "ParameterName has {0} characters; CiA 306 allows at most {1}.", entry.Value.Length, MaxParameterNameLength));
        }
    }

    private void CheckSubIndexZero(ushort index, SortedDictionary<byte, RawSection> subs, byte? compactSubObj)
    {
        if (!subs.TryGetValue(0, out var sub0))
        {
            if (compactSubObj is > 0)
            {
                // The reader synthesizes sub-index 0 from CompactSubObj, and the writer omits
                // [XXXXsub0] when that entry still matches the synthesized template.
                return;
            }

            _findings.Add(new Finding(Severity.Error, "OBJ007", _file, _objects[index].Line, _objects[index].Name, null, null,
                "ARRAY/RECORD object has no sub-index 0 section ([" + Hex4(index) + "sub0])."));
            return;
        }

        var value = sub0.Get(_isDcf && sub0.GetValue("ParameterValue") is not null ? "ParameterValue" : "DefaultValue");
        if (value is null || compactSubObj is > 0 || value.Value.Contains('$'))
        {
            return;
        }

        // For writable sub-index 0 (PDO mappings, error history, ...) the value is the number of
        // entries currently in use, not the highest supported sub-index.
        var access = sub0.GetValue("AccessType")?.ToLowerInvariant();
        if (access is "rw" or "wo" or "rww" or "rwr" || index == 0x1003 || IsPdoMapping(index))
        {
            return;
        }

        if (!TryParseInt(value.Value, out var highest))
        {
            return; // reported by the value checks
        }

        // Sub-index 0 gives the highest sub-index not counting FFh (CiA 301).
        var maxSub = subs.Keys.Where(k => k != 0xFF).DefaultIfEmpty((byte)0).Max();
        if (highest < maxSub)
        {
            Add(Severity.Error, "OBJ007", sub0, value, string.Format(CultureInfo.InvariantCulture,
                "Sub-index 0 announces highest sub-index {0}, but sub-index {1} is defined.", highest, maxSub));
        }
        else if (highest > maxSub)
        {
            Add(Severity.Warning, "OBJ007", sub0, value, string.Format(CultureInfo.InvariantCulture,
                "Sub-index 0 announces highest sub-index {0}, but the highest defined sub-index is {1}.", highest, maxSub));
        }
    }

    // ---------------------------------------------------------------- values

    private readonly record struct CheckedValues(ushort DataType, Dictionary<string, ValueEvaluation> Evaluations);

    private CheckedValues? CheckEntryValues(RawSection section, ushort index, byte? subIndex)
    {
        CheckAccessType(section);
        CheckPdoMappingFlag(section);

        var dataTypeEntry = section.Get("DataType");
        if (dataTypeEntry is null || string.IsNullOrWhiteSpace(dataTypeEntry.Value))
        {
            Add(Severity.Error, "OBJ004", section, dataTypeEntry, "DataType is missing.");
            return null;
        }

        ushort dataType;
        try
        {
            dataType = ValueConverter.ParseUInt16(dataTypeEntry.Value);
        }
        catch (EdsParseException)
        {
            Add(Severity.Error, "OBJ002", section, dataTypeEntry, "DataType is not a valid number.");
            return null;
        }

        if (!CanOpenDataType.IsStandardType(dataType))
        {
            if (dataType < 0x0020)
            {
                Add(Severity.Error, "OBJ003", section, dataTypeEntry, string.Format(CultureInfo.InvariantCulture,
                    "DataType 0x{0:X4} is not a defined CANopen basic data type.", dataType));
            }
            else if (!PredefinedComplexTypes.Contains(dataType) && !_objects.ContainsKey(dataType))
            {
                Add(Severity.Warning, "OBJ003", section, dataTypeEntry, string.Format(CultureInfo.InvariantCulture,
                    "DataType 0x{0:X4} is neither a standard type nor defined by a [{0:X4}] DEFTYPE/DEFSTRUCT section; values are not checked.", dataType));
            }

            return null;
        }

        CheckWellKnownDataType(section, index, subIndex, dataType, dataTypeEntry);

        var evaluations = new Dictionary<string, ValueEvaluation>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "DefaultValue", "LowLimit", "HighLimit", "ParameterValue" })
        {
            var entry = section.Get(key);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Value))
            {
                continue;
            }

            var evaluation = EvaluateValue(section, entry, dataType);
            if (evaluation is not null)
            {
                evaluations[key] = evaluation;
            }
        }

        var hasLimits = section.GetValue("LowLimit") is not null || section.GetValue("HighLimit") is not null;
        if (hasLimits && !ValueSupport.IsNumeric(dataType))
        {
            Add(Severity.Warning, "VAL006", section, section.Get("LowLimit") ?? section.Get("HighLimit"),
                "LowLimit/HighLimit are only meaningful for numeric data types, not " + ValueSupport.TypeName(dataType) + ".");
            return new CheckedValues(dataType, evaluations);
        }

        evaluations.TryGetValue("LowLimit", out var low);
        evaluations.TryGetValue("HighLimit", out var high);

        foreach (var nodeId in _nodeIds)
        {
            NumericValue lowValue = default, highValue = default;
            var hasLow = low is not null && low.TryGet(nodeId, out lowValue);
            var hasHigh = high is not null && high.TryGet(nodeId, out highValue);

            if (hasLow && hasHigh && lowValue.CompareTo(highValue) > 0)
            {
                Add(Severity.Error, "VAL002", section, section.Get("LowLimit"),
                    "LowLimit " + lowValue + " is greater than HighLimit " + highValue + NodeSuffix(low!, high!, nodeId) + ".");
            }

            foreach (var key in new[] { "DefaultValue", "ParameterValue" })
            {
                if (!evaluations.TryGetValue(key, out var evaluation) || !evaluation.TryGet(nodeId, out var value))
                {
                    continue;
                }

                var code = key == "DefaultValue" ? "VAL003" : "VAL004";
                if (hasLow && value.CompareTo(lowValue) < 0)
                {
                    Add(Severity.Error, code, section, section.Get(key),
                        key + " " + value + " is below LowLimit " + lowValue + NodeSuffix(evaluation, low!, nodeId) + ".");
                }

                if (hasHigh && value.CompareTo(highValue) > 0)
                {
                    Add(Severity.Error, code, section, section.Get(key),
                        key + " " + value + " is above HighLimit " + highValue + NodeSuffix(evaluation, high!, nodeId) + ".");
                }
            }

            if (!evaluations.Values.Any(e => e.IsFormula))
            {
                break; // no node-ID dependency: one pass is enough
            }
        }

        return new CheckedValues(dataType, evaluations);
    }

    /// <summary>
    /// Validates DCF <c>[xxxxValue]</c> entries (CiA 306 §5.2.3.2). Each decimal key in
    /// <c>1..min(CompactSubObj, 254)</c> is applied as that sub-object's <c>ParameterValue</c>
    /// and must fit the parent template's data type and limits. An explicit
    /// <c>[XXXXsubN]</c> above that range still receives the override
    /// (<c>DcfReader.ApplyCompactListSection</c>) and is checked against its own type and limits.
    /// </summary>
    private void CheckCompactValueEntries(
        RawSection template,
        ushort index,
        byte compactSubObj,
        CheckedValues templateValues,
        Dictionary<byte, CheckedValues> explicitSubs)
    {
        var dataType = templateValues.DataType;
        var templateEvaluations = templateValues.Evaluations;
        if (!_isDcf)
        {
            return;
        }

        var valueSection = _doc.Get(Hex4(index) + "Value");
        if (valueSection is null)
        {
            return;
        }

        var compactMax = Math.Min(compactSubObj, (byte)254);
        var hasLimits = template.GetValue("LowLimit") is not null || template.GetValue("HighLimit") is not null;
        templateEvaluations.TryGetValue("LowLimit", out var low);
        templateEvaluations.TryGetValue("HighLimit", out var high);
        if (!hasLimits || !ValueSupport.IsNumeric(dataType))
        {
            // Non-numeric limits are already reported as VAL006 on the template.
            low = null;
            high = null;
        }

        foreach (var entry in valueSection.Entries.Values.OrderBy(e => e.Line))
        {
            if (!TryParseCompactListSubIndex(entry.Key, out var subIndex) ||
                string.IsNullOrWhiteSpace(entry.Value))
            {
                continue;
            }

            if (explicitSubs.TryGetValue(subIndex, out var explicitSub))
            {
                // An explicit [XXXXsubN] keeps its own data type and limits, including
                // sub-indices above CompactSubObj (DcfReader still applies the value).
                ValueEvaluation? subLow = null;
                ValueEvaluation? subHigh = null;
                if (ValueSupport.IsNumeric(explicitSub.DataType))
                {
                    explicitSub.Evaluations.TryGetValue("LowLimit", out subLow);
                    explicitSub.Evaluations.TryGetValue("HighLimit", out subHigh);
                }

                CheckAppliedListValue(valueSection, entry, explicitSub.DataType, subLow, subHigh);
                continue;
            }

            if (subIndex > compactMax)
            {
                // No synthesized sub-object and no explicit section: the reader ignores the key.
                continue;
            }

            CheckAppliedListValue(valueSection, entry, dataType, low, high);
        }
    }

    /// <summary>
    /// Validates DCF <c>[xxxxValue]</c> overrides on an expanded object (no nonzero
    /// <c>CompactSubObj</c>). <c>DcfReader</c> still applies those entries to each matching
    /// explicit sub-object, using that sub-object's own data type and limits.
    /// </summary>
    private void CheckExpandedValueOverrides(ushort index, Dictionary<byte, CheckedValues> subs)
    {
        if (!_isDcf || subs.Count == 0)
        {
            return;
        }

        var valueSection = _doc.Get(Hex4(index) + "Value");
        if (valueSection is null)
        {
            return;
        }

        foreach (var entry in valueSection.Entries.Values.OrderBy(e => e.Line))
        {
            if (!TryParseCompactListSubIndex(entry.Key, out var subIndex) ||
                string.IsNullOrWhiteSpace(entry.Value) ||
                !subs.TryGetValue(subIndex, out var checkedSub))
            {
                continue;
            }

            ValueEvaluation? low = null;
            ValueEvaluation? high = null;
            if (ValueSupport.IsNumeric(checkedSub.DataType))
            {
                checkedSub.Evaluations.TryGetValue("LowLimit", out low);
                checkedSub.Evaluations.TryGetValue("HighLimit", out high);
            }

            CheckAppliedListValue(valueSection, entry, checkedSub.DataType, low, high);
        }
    }

    private void CheckAppliedListValue(
        RawSection valueSection,
        RawEntry entry,
        ushort dataType,
        ValueEvaluation? low,
        ValueEvaluation? high)
    {
        var evaluation = EvaluateValue(valueSection, entry, dataType);
        if (evaluation is null)
        {
            return;
        }

        foreach (var nodeId in _nodeIds)
        {
            NumericValue lowValue = default, highValue = default;
            var hasLow = low is not null && low.TryGet(nodeId, out lowValue);
            var hasHigh = high is not null && high.TryGet(nodeId, out highValue);
            if (evaluation.TryGet(nodeId, out var value))
            {
                if (hasLow && value.CompareTo(lowValue) < 0)
                {
                    Add(Severity.Error, "VAL004", valueSection, entry,
                        "ParameterValue " + value + " is below LowLimit " + lowValue + NodeSuffix(evaluation, low!, nodeId) + ".");
                }

                if (hasHigh && value.CompareTo(highValue) > 0)
                {
                    Add(Severity.Error, "VAL004", valueSection, entry,
                        "ParameterValue " + value + " is above HighLimit " + highValue + NodeSuffix(evaluation, high!, nodeId) + ".");
                }
            }

            if (!evaluation.IsFormula && low?.IsFormula != true && high?.IsFormula != true)
            {
                break;
            }
        }
    }

    private ValueEvaluation? EvaluateValue(RawSection section, RawEntry entry, ushort dataType)
    {
        var value = entry.Value.Trim();

        if (value.Contains("$NODEID", StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateFormula(section, entry, value, dataType);
        }

        try
        {
            // Convert.ToUInt64 throws OverflowException for an all-octal literal that does not fit
            // in 64 bits. Keep it inside this handler so the checker reports VAL001 and continues.
            if (ValueSupport.IsInteger(dataType) && ValueSupport.IsOctalLiteral(value) && value.All(c => c is >= '0' and <= '7'))
            {
                var octal = Convert.ToUInt64(value, 8);
                Add(Severity.Warning, "VAL005", section, entry, string.Format(CultureInfo.InvariantCulture,
                    "Leading zero makes this an octal literal (= {0} decimal). Use '{1}' or '0x{0:X}' if decimal was intended.",
                    octal, value.TrimStart('0').Length == 0 ? "0" : value.TrimStart('0')));
            }

            if (ValueSupport.IsReal(dataType) && value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                Add(Severity.Warning, "VAL007", section, entry,
                    "Hexadecimal literal for " + ValueSupport.TypeName(dataType) + "; REAL values should be written as decimal floating point numbers.");
                return null;
            }

            var parsed = ValueSupport.ParseLiteral(value, dataType);
            if (parsed is null)
            {
                return null;
            }

            var evaluation = new ValueEvaluation();
            evaluation.Values[0] = parsed.Value;
            return evaluation;
        }
        catch (NotSupportedException)
        {
            return null; // DOMAIN, TIME_OF_DAY, ... have no typed representation
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or EdsParseException or ArgumentException)
        {
            ReportInvalidValue(section, entry, dataType, value, ex);
            return null;
        }
    }

    private ValueEvaluation? EvaluateFormula(RawSection section, RawEntry entry, string value, ushort dataType)
    {
        if (!ValueSupport.IsInteger(dataType))
        {
            Add(Severity.Error, "FRM003", section, entry,
                "$NODEID formulas are only allowed for integer data types, not " + ValueSupport.TypeName(dataType) + ".");
            return null;
        }

        if (!ValueSupport.TrySplitFormula(value, out var terms, out var prefixForm))
        {
            Add(Severity.Error, "FRM001", section, entry,
                "Invalid $NODEID formula. CiA 306 allows only '$NODEID' followed by '+<number>' offsets.");
            return null;
        }

        if (prefixForm)
        {
            Add(Severity.Error, "FRM002", section, entry,
                "$NODEID must appear at the beginning (CiA 306-1 clause 6.3); otherwise the entry is not a formula and not a valid number. Write '$NODEID+" + terms[0].Operand + "'.");
            return null;
        }

        if (terms.Count > 1 && terms.Any(t => t.Sign < 0))
        {
            Add(Severity.Error, "FRM001", section, entry,
                "Mixed or repeated '-' offsets are not supported; use '$NODEID' with '+<number>' offsets (CiA 306) or a single '$NODEID-<number>'.");
            return null;
        }

        var offset = BigInteger.Zero;
        foreach (var (termSign, operandText) in terms)
        {
            try
            {
                offset += termSign * new BigInteger(ValueSupport.ParseOperand(operandText));
            }
            catch (Exception ex) when (ex is EdsParseException or FormatException or OverflowException or NotSupportedException)
            {
                Add(Severity.Error, "FRM001", section, entry, "Formula operand '" + operandText + "' is not a valid number.");
                return null;
            }
        }

        if (terms.Any(t => t.Sign < 0))
        {
            Add(Severity.Warning, "FRM005", section, entry,
                "Subtraction is not part of the CiA 306 formula syntax ($NODEID {\"+\" number}); other tools may reject it.");
        }

        var evaluation = new ValueEvaluation { IsFormula = true };
        foreach (var nodeId in _nodeIds)
        {
            var result = nodeId + offset;
            try
            {
                var parsed = ValueSupport.ParseLiteral(result.ToString(CultureInfo.InvariantCulture), dataType);
                if (parsed is not null)
                {
                    evaluation.Values[nodeId] = parsed.Value;
                }
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or EdsParseException or ArgumentException)
            {
                Add(Severity.Error, "FRM004", section, entry, string.Format(CultureInfo.InvariantCulture,
                    "Formula evaluates to {0} for node-ID {1}, which does not fit {2}.",
                    result, nodeId, ValueSupport.RangeDescription(dataType)));
                return null;
            }
        }

        return evaluation;
    }

    private void ReportInvalidValue(RawSection section, RawEntry entry, ushort dataType, string value, Exception ex)
    {
        string reason;
        if (ValueSupport.IsInteger(dataType) || dataType == CanOpenDataType.Boolean)
        {
            reason = ex is OverflowException
                ? "Value does not fit " + ValueSupport.RangeDescription(dataType) + "."
                : "Value is not a valid " + ValueSupport.RangeDescription(dataType) + " literal.";

            if (ValueSupport.IsOctalLiteral(value) && value.Any(c => c is '8' or '9'))
            {
                reason += " A leading zero marks an octal literal, so digits 8/9 are invalid.";
            }
        }
        else
        {
            reason = "Value is not valid for " + ValueSupport.TypeName(dataType) + ": " + ex.Message;
        }

        Add(Severity.Error, "VAL001", section, entry, reason);
    }

    private void CheckAccessType(RawSection section)
    {
        var entry = section.Get("AccessType");
        if (entry is null || string.IsNullOrWhiteSpace(entry.Value))
        {
            Add(Severity.Error, "OBJ005", section, entry, "AccessType is missing.");
            return;
        }

        if (!AccessTypes.Contains(entry.Value.Trim()))
        {
            Add(Severity.Error, "OBJ005", section, entry,
                "AccessType must be one of ro, wo, rw, rwr, rww, const.");
        }
    }

    private void CheckPdoMappingFlag(RawSection section)
    {
        var entry = section.Get("PDOMapping");
        if (entry is null || string.IsNullOrWhiteSpace(entry.Value))
        {
            return;
        }

        if (ParseFlag(entry.Value) is null)
        {
            Add(Severity.Warning, "OBJ009", section, entry, "PDOMapping should be 0 or 1.");
        }
    }

    // ---------------------------------------------------------------- well-known objects

    private void CheckWellKnownDataType(RawSection section, ushort index, byte? subIndex, ushort dataType, RawEntry entry)
    {
        var expected = ExpectedDataType(index, subIndex);
        if (expected.HasValue && expected.Value != dataType)
        {
            Add(Severity.Error, "STD001", section, entry, string.Format(CultureInfo.InvariantCulture,
                "CiA 301 defines this entry as {0}, but DataType is {1}.",
                ValueSupport.TypeName(expected.Value), ValueSupport.TypeName(dataType)));
        }
    }

    private static ushort? ExpectedDataType(ushort index, byte? subIndex)
    {
        const ushort u8 = CanOpenDataType.Unsigned8, u16 = CanOpenDataType.Unsigned16, u32 = CanOpenDataType.Unsigned32;

        if (subIndex is null)
        {
            return index switch
            {
                0x1000 or 0x1002 or 0x1005 or 0x1006 or 0x1007 or 0x1012 or 0x1014 => u32,
                0x1001 or 0x100D or 0x1019 => u8,
                0x100C or 0x1015 or 0x1017 => u16,
                _ => null,
            };
        }

        var sub = subIndex.Value;
        if (sub == 0 && index is 0x1003 or 0x1016 or 0x1018 or (>= 0x1200 and <= 0x12FF) or (>= 0x1400 and <= 0x1BFF))
        {
            return u8;
        }

        return index switch
        {
            0x1003 or 0x1016 => u32,
            0x1018 when sub <= 4 => u32,
            >= 0x1200 and <= 0x12FF when sub is 1 or 2 => u32,
            >= 0x1200 and <= 0x12FF when sub == 3 => u8,
            >= 0x1400 and <= 0x15FF or >= 0x1800 and <= 0x19FF when sub == 1 => u32,
            >= 0x1400 and <= 0x15FF or >= 0x1800 and <= 0x19FF when sub is 2 or 4 or 6 => u8,
            >= 0x1400 and <= 0x15FF or >= 0x1800 and <= 0x19FF when sub is 3 or 5 => u16,
            >= 0x1600 and <= 0x17FF or >= 0x1A00 and <= 0x1BFF => u32,
            _ => null,
        };
    }

    // ---------------------------------------------------------------- PDO mapping

    private void CheckPdoMappings()
    {
        var indexes = new SortedSet<ushort>();
        foreach (var index in _subObjects.Keys)
        {
            if (IsPdoMapping(index))
            {
                indexes.Add(index);
            }
        }

        foreach (var (index, section) in _objects)
        {
            if (IsPdoMapping(index) && ParseOptionalByte(section, "CompactSubObj") is > 0)
            {
                indexes.Add(index);
            }
        }

        foreach (var index in indexes)
        {
            CheckPdoMapping(index);
        }
    }

    private void CheckPdoMapping(ushort index)
    {
        _subObjects.TryGetValue(index, out var subs);
        _objects.TryGetValue(index, out var parent);
        var compact = parent is null ? null : ParseOptionalByte(parent, "CompactSubObj", report: false);
        var compactMax = compact is > 0 ? Math.Min((int)compact.Value, 254) : 0;
        var valueSection = _isDcf ? _doc.Get(Hex4(index) + "Value") : null;

        var numberOfEntries = int.MaxValue;
        if (subs is not null && subs.TryGetValue(0, out var sub0) && TryParseInt(MappingValue(sub0) ?? string.Empty, out var count))
        {
            numberOfEntries = count;
        }
        else if (compact is > 0)
        {
            // No explicit sub-index 0: the reader synthesizes it with DefaultValue = CompactSubObj.
            numberOfEntries = compact.Value;
        }

        var slots = new SortedSet<byte>();
        if (subs is not null)
        {
            foreach (var subIndex in subs.Keys)
            {
                slots.Add(subIndex);
            }
        }

        if (compactMax > 0)
        {
            var last = numberOfEntries == int.MaxValue ? compactMax : Math.Min(compactMax, numberOfEntries);
            for (var subIndex = 1; subIndex <= last; subIndex++)
            {
                slots.Add((byte)subIndex);
            }
        }

        var isTx = index >= 0x1A00;
        var totalBits = 0;
        foreach (var subIndex in slots)
        {
            if (subIndex == 0 || subIndex > numberOfEntries)
            {
                continue;
            }

            if (!TryResolveMappingEntry(subIndex, subs, parent, compactMax, valueSection, out var section, out var entry, out var raw) ||
                raw.Contains('$'))
            {
                continue;
            }

            uint mapping;
            try
            {
                mapping = ValueConverter.ParseInteger(raw);
            }
            catch (Exception ex) when (ex is EdsParseException or FormatException or OverflowException)
            {
                continue; // reported by value checks
            }

            if (mapping == 0)
            {
                continue;
            }

            var mappedIndex = (ushort)(mapping >> 16);
            var mappedSub = (byte)((mapping >> 8) & 0xFF);
            var length = (int)(mapping & 0xFF);
            totalBits += length;

            CheckMappedObject(section, entry, mappedIndex, mappedSub, length, isTx);
        }

        if (totalBits > 64)
        {
            RawSection? anchor = null;
            if (subs is not null && subs.TryGetValue(0, out var anchorSub))
            {
                anchor = anchorSub;
            }
            else if (valueSection is not null)
            {
                anchor = valueSection;
            }
            else if (parent is not null)
            {
                anchor = parent;
            }
            else if (subs is not null)
            {
                anchor = subs.Values.First();
            }

            if (anchor is not null)
            {
                Add(Severity.Error, "PDO004", anchor, null, null, string.Format(CultureInfo.InvariantCulture,
                    "PDO mapping [{0}] maps {1} bits; a CAN frame carries at most 64.", Hex4(index), totalBits));
            }
        }
    }

    /// <summary>
    /// Resolves one PDO mapping slot. DCF <c>[xxxxValue]</c> overwrites <c>ParameterValue</c>;
    /// otherwise an explicit <c>[XXXXsubY]</c> is used, then the compact parent template.
    /// </summary>
    private bool TryResolveMappingEntry(
        byte subIndex,
        SortedDictionary<byte, RawSection>? subs,
        RawSection? parent,
        int compactMax,
        RawSection? valueSection,
        out RawSection section,
        out RawEntry entry,
        out string raw)
    {
        section = null!;
        entry = null!;
        raw = string.Empty;

        if (valueSection is not null)
        {
            var applies = (subs is not null && subs.ContainsKey(subIndex)) || (subIndex >= 1 && subIndex <= compactMax);
            if (applies)
            {
                foreach (var candidate in valueSection.Entries.Values)
                {
                    if (!TryParseCompactListSubIndex(candidate.Key, out var keySub) || keySub != subIndex ||
                        string.IsNullOrWhiteSpace(candidate.Value))
                    {
                        continue;
                    }

                    section = valueSection;
                    entry = candidate;
                    raw = candidate.Value.Trim();
                    return true;
                }
            }
        }

        if (subs is not null && subs.TryGetValue(subIndex, out var explicitSub))
        {
            var key = _isDcf && explicitSub.GetValue("ParameterValue") is not null ? "ParameterValue" : "DefaultValue";
            var explicitEntry = explicitSub.Get(key);
            var value = explicitSub.GetValue(key);
            if (explicitEntry is null || value is null)
            {
                return false;
            }

            section = explicitSub;
            entry = explicitEntry;
            raw = value;
            return true;
        }

        if (parent is not null && subIndex >= 1 && subIndex <= compactMax)
        {
            var templateEntry = parent.Get("DefaultValue");
            var value = parent.GetValue("DefaultValue");
            if (templateEntry is null || value is null)
            {
                return false;
            }

            section = parent;
            entry = templateEntry;
            raw = value;
            return true;
        }

        return false;
    }

    private string? MappingValue(RawSection section) =>
        (_isDcf ? section.GetValue("ParameterValue") : null) ?? section.GetValue("DefaultValue");

    private void CheckMappedObject(RawSection section, RawEntry entry, ushort index, byte sub, int length, bool isTx)
    {
        var target = string.Format(CultureInfo.InvariantCulture, "0x{0:X4}sub{1:X}", index, sub);

        if (index < 0x0020)
        {
            // Dummy mapping: index is the data type.
            if (!CanOpenDataType.IsStandardType(index) || sub != 0)
            {
                Add(Severity.Error, "PDO001", section, entry, string.Format(CultureInfo.InvariantCulture,
                    "Mapping 0x{0:X8} references {1}, which is neither an object nor a valid dummy data type (index 0x0001-0x001F, sub-index 0).",
                    ((uint)index << 16) | ((uint)sub << 8) | (uint)length, target));
                return;
            }

            var dummyBits = CanOpenDataType.TryGetBitLength(index);
            if (isTx)
            {
                Add(Severity.Warning, "PDO002", section, entry, "Dummy entry " + target + " mapped into a TPDO.");
            }

            if (dummyBits.HasValue && dummyBits.Value != length)
            {
                Add(Severity.Error, "PDO003", section, entry, string.Format(CultureInfo.InvariantCulture,
                    "Dummy entry {0} has length {1} bits, but {2} is {3} bits.", target, length, ValueSupport.TypeName(index), dummyBits.Value));
            }

            return;
        }

        RawSection? mapped = null;
        _objects.TryGetValue(index, out var parent);
        var compact = parent is null ? null : ParseOptionalByte(parent, "CompactSubObj", report: false);
        var compactActive = compact is > 0;
        if (_subObjects.TryGetValue(index, out var subs))
        {
            subs.TryGetValue(sub, out mapped);
        }
        else if (sub == 0 && parent is not null && !compactActive)
        {
            // No sub-sections: sub-index 0 is the object itself.
            // Compact storage is different — sub-index 0 is synthesized and is not the template.
            mapped = parent;
        }

        if (mapped is null && compact is > 0 and var compactCount && parent is not null)
        {
            var compactMax = Math.Min((int)compactCount, 254);
            if (sub >= 1 && sub <= compactMax)
            {
                // Synthesized element: type, access and PDOMapping come from the parent template.
                mapped = parent;
            }
            else if (sub == 0)
            {
                // Synthesized sub-index 0 is UNSIGNED8, read-only, and not PDO-mappable,
                // even when the file has no explicit [XXXXsubN] sections at all.
                Add(Severity.Error, "PDO002", section, entry, "Mapped object " + target + " is not PDO-mappable (PDOMapping is not 1).");
                if (!isTx)
                {
                    Add(Severity.Error, "PDO002", section, entry, "Mapped object " + target + " is ro and cannot be written by an RPDO.");
                }

                if (length != 8)
                {
                    Add(Severity.Error, "PDO003", section, entry, string.Format(CultureInfo.InvariantCulture,
                        "Mapping length {0} bits does not match UNSIGNED8 (8 bits) of {1}.", length, target));
                }

                return;
            }
        }

        if (mapped is null)
        {
            Add(Severity.Error, "PDO001", section, entry, "Mapped object " + target + " does not exist.");
            return;
        }

        if (ParseFlag(mapped.GetValue("PDOMapping")) != true)
        {
            Add(Severity.Error, "PDO002", section, entry, "Mapped object " + target + " is not PDO-mappable (PDOMapping is not 1).");
        }

        var access = mapped.GetValue("AccessType")?.ToLowerInvariant();
        if (!isTx && access is "ro" or "const")
        {
            Add(Severity.Error, "PDO002", section, entry, "Mapped object " + target + " is " + access + " and cannot be written by an RPDO.");
        }
        else if (!isTx && access is "rwr")
        {
            // rwr is ReadWriteInput: process input, mapped in a TPDO only.
            Add(Severity.Error, "PDO002", section, entry, "Mapped object " + target + " is rwr (process input) and cannot be written by an RPDO.");
        }
        else if (isTx && access is "wo")
        {
            Add(Severity.Error, "PDO002", section, entry, "Mapped object " + target + " is write-only and cannot be sent in a TPDO.");
        }
        else if (isTx && access is "rww")
        {
            // rww is ReadWriteOutput: process output, mapped in an RPDO only.
            Add(Severity.Error, "PDO002", section, entry, "Mapped object " + target + " is rww (process output) and cannot be sent in a TPDO.");
        }

        var dataTypeText = mapped.GetValue("DataType");
        if (dataTypeText is null)
        {
            return;
        }

        try
        {
            var dataType = ValueConverter.ParseUInt16(dataTypeText);
            var bits = CanOpenDataType.TryGetBitLength(dataType);
            if (bits.HasValue && bits.Value != length)
            {
                Add(Severity.Error, "PDO003", section, entry, string.Format(CultureInfo.InvariantCulture,
                    "Mapping length {0} bits does not match {1} ({2} bits) of {3}.",
                    length, ValueSupport.TypeName(dataType), bits.Value, target));
            }
        }
        catch (EdsParseException)
        {
            // reported by value checks
        }
    }

    // ---------------------------------------------------------------- helpers

    private byte? ParseOptionalByte(RawSection section, string key, bool report = true)
    {
        var entry = section.Get(key);
        if (entry is null || string.IsNullOrWhiteSpace(entry.Value))
        {
            return null;
        }

        try
        {
            return ValueConverter.ParseByte(entry.Value);
        }
        catch (EdsParseException)
        {
            if (report)
            {
                Add(Severity.Error, "OBJ009", section, entry, key + " is not a valid UNSIGNED8 number (0..255).");
            }

            return null;
        }
    }

    /// <summary>Parses a 0/1 flag (decimal or hex such as <c>0x1</c>); <see langword="null"/> when invalid.</summary>
    private static bool? ParseFlag(string? value) =>
        value is not null && TryParseInt(value, out var flag) && flag is 0 or 1 ? flag == 1 : null;

    /// <summary>
    /// Parses a compact-list key as a decimal sub-index in the CiA 306 range 1..254.
    /// Matches the reader: <c>NrOfEntries</c>, hex keys, and sub-index 0xFF are ignored.
    /// </summary>
    private static bool TryParseCompactListSubIndex(string key, out byte subIndex) =>
        byte.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out subIndex)
        && subIndex >= 1
        && subIndex <= 254;

    private static bool TryParseInt(string value, out int result)
    {
        try
        {
            var parsed = ValueConverter.ParseInteger(value);
            if (parsed <= int.MaxValue && !string.IsNullOrWhiteSpace(value))
            {
                result = (int)parsed;
                return true;
            }
        }
        catch (Exception ex) when (ex is EdsParseException or FormatException or OverflowException or NotSupportedException)
        {
            // fall through
        }

        result = 0;
        return false;
    }

    private static bool IsPdoMapping(ushort index) => index is (>= 0x1600 and <= 0x17FF) or (>= 0x1A00 and <= 0x1BFF);

    private static string NodeSuffix(ValueEvaluation a, ValueEvaluation b, byte nodeId) =>
        a.IsFormula || b.IsFormula
            ? string.Format(CultureInfo.InvariantCulture, " (node-ID {0})", nodeId)
            : string.Empty;

    private static string Hex4(ushort index) => index.ToString("X4", CultureInfo.InvariantCulture);

    private void Add(Severity severity, string code, RawSection section, RawEntry? entry, string message) =>
        _findings.Add(new Finding(severity, code, _file, entry?.Line ?? section.Line, section.Name, entry?.Key, entry?.Value, message));

    private void Add(Severity severity, string code, RawSection section, string? key, string? value, string message) =>
        _findings.Add(new Finding(severity, code, _file, section.Line, section.Name, key, value, message));
}
