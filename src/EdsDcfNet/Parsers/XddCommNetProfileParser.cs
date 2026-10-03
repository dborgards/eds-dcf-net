namespace EdsDcfNet.Parsers;

using System.Globalization;
using System.Xml.Linq;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Models;
using static EdsDcfNet.Parsers.XddParsingPrimitives;

internal static class XddCommNetProfileParser
{
    private static readonly Dictionary<ushort, Action<BaudRates>> BaudRateSetters =
        new Dictionary<ushort, Action<BaudRates>>
        {
            [10] = br => br.BaudRate10 = true,
            [20] = br => br.BaudRate20 = true,
            [50] = br => br.BaudRate50 = true,
            [100] = br => br.BaudRate100 = true,
            [125] = br => br.BaudRate125 = true,
            [250] = br => br.BaudRate250 = true,
            [500] = br => br.BaudRate500 = true,
            [800] = br => br.BaudRate800 = true,
            [1000] = br => br.BaudRate1000 = true
        };
    internal static void ParseCommNetProfile(XElement profileBody, ElectronicDataSheet eds, bool includeActualValues)
    {
        var appLayers = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "ApplicationLayers");

        if (appLayers != null)
        {
            // Object dictionary
            var objList = appLayers.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "CANopenObjectList");
            if (objList != null)
                ParseObjectDictionary(objList, eds, includeActualValues);

            // Dummy usage
            var dummyUsage = appLayers.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "dummyUsage");
            if (dummyUsage != null)
                ParseDummyUsage(dummyUsage, eds.ObjectDictionary);

            // Dynamic channels
            var dynChannels = appLayers.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "dynamicChannels");
            if (dynChannels != null)
            {
                var dc = ParseDynamicChannels(dynChannels);
                if (dc != null && dc.Segments.Count > 0)
                    eds.DynamicChannels = dc;
            }
        }

        var transportLayers = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "TransportLayers");
        if (transportLayers != null)
            ParseBaudRates(transportLayers, eds.DeviceInfo);

        var networkMgmt = profileBody.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "NetworkManagement");
        if (networkMgmt != null)
            ParseNetworkManagement(networkMgmt, eds.DeviceInfo);

        // Mirror unknown CommunicationNetwork ProfileBody children as INI-shaped
        // AdditionalSections entries (attributes only) for callers. The XDD/XDC writers do not
        // emit these entries; they write the element itself, which XddPreservedContentReader
        // keeps whole — see architecture docs §8.4.
        var knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ApplicationLayers",
            "TransportLayers",
            "NetworkManagement"
        };
        foreach (var child in profileBody.Elements())
        {
            if (knownNames.Contains(child.Name.LocalName))
                continue;
            var section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attr in child.Attributes())
                section[attr.Name.LocalName] = attr.Value;
            eds.AdditionalSections[child.Name.LocalName] = section;
        }
    }

    private static void ParseObjectDictionary(XElement objList, ElectronicDataSheet eds, bool includeActualValues)
    {
        var dict = eds.ObjectDictionary;
        var resolver = XddUniqueIdResolver.Create(eds.ApplicationProcess);

        // HashSets provide O(1) duplicate detection without the O(n) List.Contains cost.
        var seenMandatory = new HashSet<ushort>();
        var seenOptional = new HashSet<ushort>();
        var seenManufacturer = new HashSet<ushort>();

        foreach (var objElem in objList.Elements().Where(e => e.Name.LocalName == "CANopenObject"))
        {
            var obj = ParseCanOpenObject(objElem, includeActualValues, resolver);
            dict.Objects[obj.Index] = obj;

            // Classify object into the right list based on index range
            ClassifyObject(dict, obj.Index, seenMandatory, seenOptional, seenManufacturer);
        }
    }

    private static CanOpenObject ParseCanOpenObject(
        XElement elem,
        bool includeActualValues,
        XddUniqueIdResolver resolver)
    {
        var obj = new CanOpenObject();

        obj.Index = ParseRequiredHexIndexAttribute(elem, "CANopenObject");
        obj.ParameterName = elem.Attribute("name")?.Value ?? string.Empty;
        obj.UniqueIdRef = ReadUniqueIdRef(elem);
        obj.ObjectType = ParseObjectTypeAttribute(elem, "CANopenObject");

        if (elem.Attribute("dataType")?.Value is string dataTypeStr)
            obj.DataType = ParseHexDataType(dataTypeStr);

        if (elem.Attribute("accessType")?.Value is string accessStr)
            obj.AccessType = ParseXddAccessType(accessStr);

        obj.DefaultValue = elem.Attribute("defaultValue")?.Value;
        obj.LowLimit = elem.Attribute("lowLimit")?.Value;
        obj.HighLimit = elem.Attribute("highLimit")?.Value;

        var pdoMappingStr = elem.Attribute("PDOmapping")?.Value;
        obj.PdoMappingMode = ParseXddPdoMapping(pdoMappingStr);

        if (GetTrimmedAttributeValue(elem, "objFlags") is { Length: > 0 } objFlagsStr)
            ReadObjFlags(obj, objFlagsStr);

        var subNumberStr = GetTrimmedAttributeValue(elem, "subNumber");
        if (!string.IsNullOrEmpty(subNumberStr))
        {
            var subNumberParsed = byte.TryParse(subNumberStr, UnsignedXsdIntegerStyles, CultureInfo.InvariantCulture, out var subNum);
            if (subNumberParsed)
                obj.SubNumber = subNum;
            RejectFailedNumericAttribute(subNumberStr, subNumberParsed, "subNumber");
        }

        if (includeActualValues)
        {
            if (elem.Attribute("actualValue")?.Value is string actualValue)
                obj.ParameterValue = actualValue;

            if (elem.Attribute("denotation")?.Value is string denotation)
                obj.Denotation = denotation;
        }

        obj.XddPreservedAttributes = XddPreservedContentReader.ObjectAttributes(elem, includeActualValues);

        // Parse sub-objects
        foreach (var subElem in elem.Elements().Where(e => e.Name.LocalName == "CANopenSubObject"))
        {
            var subObj = ParseCanOpenSubObject(subElem, includeActualValues);
            subObj.XddPreservedAttributes = XddPreservedContentReader.SubObjectAttributes(subElem, includeActualValues);
            subObj.UniqueIdRef = ReadUniqueIdRef(subElem);
            resolver.ApplySubObject(obj.Index, subObj, ExplicitAttributes.From(subElem));
            obj.SubObjects[subObj.SubIndex] = subObj;
        }

        resolver.ApplyObject(obj, ExplicitAttributes.From(elem));
        return obj;
    }

    /// <summary>
    /// CiA 311 Annex A.1.4: <c>objFlags</c> is <c>xsd:hexBinary</c> (four hex digits in the
    /// annotation). Bits 0, 1 and 2 are defined; bits 3..31 are reserved. There is no
    /// decimal fallback. A single hex digit matches the old decimal spelling of values 0..7.
    /// </summary>
    private static void ReadObjFlags(CanOpenObject obj, string raw)
    {
        // Defined bits are 0..2. Bit 2 means the change takes effect after reset.
        const uint definedMask = 0x7;

        if (!IsAsciiHex(raw))
        {
            RejectObjFlags(obj.Index, raw, overflow: false);
            return;
        }

        var oddLength = (raw.Length & 1) != 0;
        var parsed = uint.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var flags);
        if (!parsed)
        {
            if (oddLength)
            {
                // Odd length is not schema-valid hexBinary, so an overflowing token stays a parse error.
                RejectObjFlags(obj.Index, raw, overflow: true);
                return;
            }

            // Even length is schema-valid hexBinary that does not fit in uint. Keep the text.
            ReportObjFlags(
                Diagnostics.ParseDiagnosticCodes.XddObjFlagsExceedsUInt32,
                obj.Index,
                raw,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "objFlags '{0}' is valid xsd:hexBinary and does not fit in 32 bits. ObjFlags was left at 0 and the original text is preserved for writing.",
                    raw));
            obj.ObjFlags = 0;
            obj.ObjFlagsLexical = raw;
            obj.ObjFlagsLexicalBaseline = 0;
            return;
        }

        if (oddLength)
        {
            ReportObjFlags(
                Diagnostics.ParseDiagnosticCodes.XddObjFlagsOddHexLength,
                obj.Index,
                raw,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "objFlags '{0}' has an odd number of hexadecimal digits. xsd:hexBinary requires an even number. The value was accepted as hexadecimal in lenient mode.",
                    raw));

            if (StrictParsingScope.IsEnabled)
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "objFlags '{0}' has an odd number of hexadecimal digits. xsd:hexBinary requires an even number.",
                        raw))
                {
                    Code = Diagnostics.ParseDiagnosticCodes.XddObjFlagsOddHexLength
                };
            }
        }

        if ((flags & ~definedMask) != 0)
        {
            ReportObjFlags(
                Diagnostics.ParseDiagnosticCodes.XddObjFlagsReservedBits,
                obj.Index,
                raw,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "objFlags '{0}' sets reserved bits 3..31 after hexadecimal interpretation. CiA 311 defines bits 0, 1, and 2 and reserves bits 3..31. A multi-digit decimal value written by a library version before this fix can have the same spelling and a different meaning.",
                    raw));
        }

        obj.ObjFlags = flags;
        obj.ObjFlagsLexical = null;
    }

    private static bool IsAsciiHex(string raw)
    {
        for (var i = 0; i < raw.Length; i++)
        {
            var character = raw[i];
            var digit = (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')
                || (character >= 'A' && character <= 'F');
            if (!digit)
                return false;
        }

        return raw.Length > 0;
    }

    private static void RejectObjFlags(ushort index, string raw, bool overflow)
    {
        var diagnosticMessage = overflow
            ? string.Format(
                CultureInfo.InvariantCulture,
                "Invalid objFlags '{0}'. The hexadecimal value does not fit in 32 bits. The attribute is ignored.",
                raw)
            : string.Format(
                CultureInfo.InvariantCulture,
                "Invalid objFlags '{0}'. Value is not an xsd:hexBinary hexadecimal value. The attribute is ignored.",
                raw);
        var exceptionMessage = overflow
            ? string.Format(
                CultureInfo.InvariantCulture,
                "Invalid objFlags '{0}'. The hexadecimal value does not fit in 32 bits.",
                raw)
            : string.Format(
                CultureInfo.InvariantCulture,
                "Invalid objFlags '{0}'. Value is not an xsd:hexBinary hexadecimal value.",
                raw);

        ReportObjFlags(
            Diagnostics.ParseDiagnosticCodes.XddInvalidNumericAttribute,
            index,
            raw,
            diagnosticMessage);

        if (!StrictParsingScope.IsEnabled)
            return;

        throw new EdsParseException(exceptionMessage)
        {
            Code = Diagnostics.ParseDiagnosticCodes.XddInvalidNumericAttribute
        };
    }

    private static void ReportObjFlags(string code, ushort index, string raw, string message)
    {
        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            code,
            path: string.Format(
                CultureInfo.InvariantCulture,
                "CANopenObject[@index='{0:X4}']/objFlags",
                index),
            message: message,
            rawValue: raw));
    }

    private static string? ReadUniqueIdRef(XElement elem)
    {
        var value = elem.Attribute("uniqueIDRef")?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value!.Trim();
    }

    private static CanOpenSubObject ParseCanOpenSubObject(XElement elem, bool includeActualValues)
    {
        var subObj = new CanOpenSubObject();

        var subIndexStr = elem.Attribute("subIndex")?.Value ?? "00";
        subObj.SubIndex = ParseHexSubIndex(subIndexStr);
        subObj.ParameterName = elem.Attribute("name")?.Value ?? string.Empty;
        subObj.ObjectType = ParseObjectTypeAttribute(elem, "CANopenSubObject");

        if (elem.Attribute("dataType")?.Value is string dataTypeStr)
            subObj.DataType = ParseHexDataType(dataTypeStr);

        if (elem.Attribute("accessType")?.Value is string accessStr)
            subObj.AccessType = ParseXddAccessType(accessStr);

        subObj.DefaultValue = elem.Attribute("defaultValue")?.Value;
        subObj.LowLimit = elem.Attribute("lowLimit")?.Value;
        subObj.HighLimit = elem.Attribute("highLimit")?.Value;

        var pdoMappingStr = elem.Attribute("PDOmapping")?.Value;
        subObj.PdoMappingMode = ParseXddPdoMapping(pdoMappingStr);

        if (includeActualValues)
        {
            if (elem.Attribute("actualValue")?.Value is string actualValue)
                subObj.ParameterValue = actualValue;

            if (elem.Attribute("denotation")?.Value is string denotation)
                subObj.Denotation = denotation;
        }

        return subObj;
    }

    /// <summary>
    /// Reads <c>index</c> from a <c>CANopenObject</c>. Lenient: missing → <c>0000</c>.
    /// Strict: missing attribute throws <see cref="EdsParseException"/>.
    /// </summary>
    private static ushort ParseRequiredHexIndexAttribute(XElement elem, string elementName)
    {
        var attr = elem.Attribute("index");
        if (attr == null)
        {
            Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                Diagnostics.ParseSeverity.Warning,
                Diagnostics.ParseDiagnosticCodes.XddMissingIndex,
                path: elementName,
                coercedTo: "0x0000",
                message: string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} is missing required attribute 'index'; treated as 0x0000.",
                    elementName)));

            if (StrictParsingScope.IsEnabled)
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} is missing required attribute 'index'.",
                        elementName))
                {
                    Code = Diagnostics.ParseDiagnosticCodes.XddMissingIndex
                };
            }

            return ParseHexIndex("0000");
        }

        return ParseHexIndex(attr.Value);
    }

    /// <summary>
    /// Reads <c>objectType</c>. Lenient: missing/invalid → <c>0x7</c> (VAR).
    /// Strict: missing or non-parsable values throw <see cref="EdsParseException"/>.
    /// Surrounding whitespace is trimmed (XML Schema integer whitespace collapse).
    /// Optional leading sign is accepted (<c>+9</c>, <c>-0</c>); out-of-range
    /// negatives such as <c>-1</c> remain invalid for <c>xsd:unsignedByte</c>.
    /// </summary>
    private static byte ParseObjectTypeAttribute(XElement elem, string elementName)
    {
        var attr = elem.Attribute("objectType");
        if (attr == null)
        {
            Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                Diagnostics.ParseSeverity.Warning,
                Diagnostics.ParseDiagnosticCodes.XddMissingObjectType,
                path: elementName,
                coercedTo: "0x7",
                message: string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} is missing required attribute 'objectType'; treated as VAR (0x7).",
                    elementName)));

            if (StrictParsingScope.IsEnabled)
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} is missing required attribute 'objectType'.",
                        elementName))
                {
                    Code = Diagnostics.ParseDiagnosticCodes.XddMissingObjectType
                };
            }

            return 0x7;
        }

        var objTypeStr = attr.Value.Trim();
        // AllowLeadingSign matches xsd:unsignedByte lexical forms (+9, -0) after trim.
        if (byte.TryParse(objTypeStr, UnsignedXsdIntegerStyles, CultureInfo.InvariantCulture, out var objType))
            return objType;

        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            Diagnostics.ParseDiagnosticCodes.XddInvalidObjectType,
            path: elementName,
            rawValue: objTypeStr,
            coercedTo: "0x7",
            message: string.Format(
                CultureInfo.InvariantCulture,
                "Invalid objectType '{0}' on {1}; treated as VAR (0x7).",
                objTypeStr,
                elementName)));

        if (StrictParsingScope.IsEnabled)
        {
            throw new EdsParseException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Invalid objectType '{0}' on {1}. Expected an Unsigned8 decimal integer.",
                    objTypeStr,
                    elementName))
            {
                Code = Diagnostics.ParseDiagnosticCodes.XddInvalidObjectType
            };
        }

        return 0x7;
    }

    private static void ClassifyObject(
        ObjectDictionary dict, ushort index,
        HashSet<ushort> seenMandatory, HashSet<ushort> seenOptional, HashSet<ushort> seenManufacturer)
    {
        // Mandatory objects: 1000h, 1001h and 1018h (CiA 306-1 Table 4, as CanOpenModelValidator requires)
        if (index == 0x1000 || index == 0x1001 || index == 0x1018)
        {
            if (seenMandatory.Add(index))
                dict.MandatoryObjects.Add(index);
        }
        // Manufacturer-specific objects: 2000h-5FFFh
        else if (index >= 0x2000 && index <= 0x5FFF)
        {
            if (seenManufacturer.Add(index))
                dict.ManufacturerObjects.Add(index);
        }
        // Everything else goes to optional
        else
        {
            if (seenOptional.Add(index))
                dict.OptionalObjects.Add(index);
        }
    }

    private static void ParseDummyUsage(XElement dummyUsage, ObjectDictionary dict)
    {
        foreach (var dummy in dummyUsage.Elements().Where(e => e.Name.LocalName == "dummy"))
        {
            var entry = dummy.Attribute("entry")?.Value ?? string.Empty;
            // Format: "DummyXXXX=0" or "DummyXXXX=1"
            var eqIdx = entry.IndexOf('=');
            if (eqIdx < 0)
            {
                RejectMalformedDummyUsageEntry(entry);
                continue;
            }

            var keyPart = entry[..eqIdx].Trim();
            var valPart = entry[(eqIdx + 1)..].Trim();

            // keyPart must start with "Dummy" followed by at least 4 hex digits.
            // StrictParsing additionally requires exactly DummyXXXX (length 9).
            if (!keyPart.StartsWith("Dummy", StringComparison.OrdinalIgnoreCase) || keyPart.Length < 9)
            {
                RejectMalformedDummyUsageEntry(entry);
                continue;
            }

            if (keyPart.Length != 9)
            {
                if (StrictParsingScope.IsEnabled)
                {
                    // Throws: RejectMalformedDummyUsageEntry always throws in strict mode.
                    RejectMalformedDummyUsageEntry(entry);
                }
                else
                {
                    // Lenient mode accepts the overlong key — report the deviation so
                    // Read*WithDiagnostics surfaces what strict mode would reject.
                    Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                        Diagnostics.ParseSeverity.Warning,
                        Diagnostics.ParseDiagnosticCodes.XddInvalidDummyUsage,
                        path: "dummyUsage/dummy",
                        rawValue: entry,
                        message: string.Format(
                            CultureInfo.InvariantCulture,
                            "Overlong dummyUsage key '{0}'. Expected exactly DummyXXXX with four hexadecimal digits; accepted in lenient mode.",
                            entry)));
                }
            }

            var hexPart = keyPart[5..];
            if (!ushort.TryParse(hexPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var dummyIndex))
            {
                RejectMalformedDummyUsageEntry(entry);
                continue;
            }

            if (valPart != "0" && valPart != "1")
                RejectMalformedDummyUsageEntry(entry, coercedTo: "false");

            dict.DummyUsage[dummyIndex] = valPart == "1";
        }
    }

    private static void RejectMalformedDummyUsageEntry(string entry, string? coercedTo = null)
    {
        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            Diagnostics.ParseDiagnosticCodes.XddInvalidDummyUsage,
            path: "dummyUsage/dummy",
            rawValue: entry,
            coercedTo: coercedTo,
            message: coercedTo == null
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "Invalid dummyUsage entry '{0}'. Expected DummyXXXX=0|1 with a hexadecimal index.",
                    entry)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "Invalid dummyUsage entry '{0}'. Expected DummyXXXX=0|1 with a hexadecimal index; treated as {1} in lenient mode.",
                    entry,
                    coercedTo)));

        if (!StrictParsingScope.IsEnabled)
            return;

        throw new EdsParseException(
            string.Format(
                CultureInfo.InvariantCulture,
                "Invalid dummyUsage entry '{0}'. Expected DummyXXXX=0|1 with a hexadecimal index.",
                entry))
        {
            Code = Diagnostics.ParseDiagnosticCodes.XddInvalidDummyUsage
        };
    }

    private static DynamicChannels? ParseDynamicChannels(XElement dynChannels)
    {
        var result = new DynamicChannels();

        foreach (var chanElem in dynChannels.Elements().Where(e => e.Name.LocalName == "dynamicChannel"))
        {
            var seg = new DynamicChannelSegment();

            if (chanElem.Attribute("dataType")?.Value is string typeStr)
                seg.Type = ParseHexDataType(typeStr);

            if (chanElem.Attribute("accessType")?.Value is string dirStr)
                seg.Dir = ParseDynamicChannelAccessType(dirStr);

            seg.Range = chanElem.Attribute("startIndex")?.Value ?? string.Empty;
            var endIdx = chanElem.Attribute("endIndex")?.Value;
            if (!string.IsNullOrEmpty(endIdx) && !string.IsNullOrEmpty(seg.Range))
                seg.Range = seg.Range + "-" + endIdx;

            var maxNumber = GetTrimmedAttributeValue(chanElem, "maxNumber");
            if (!string.IsNullOrEmpty(maxNumber))
            {
                var maxParsed = uint.TryParse(maxNumber, UnsignedXsdIntegerStyles, CultureInfo.InvariantCulture, out var maxValue);
                if (maxParsed)
                    seg.MaxNumber = maxValue;
                RejectFailedNumericAttribute(maxNumber, maxParsed, "maxNumber");
            }

            var bitAlignment = GetTrimmedAttributeValue(chanElem, "bitAlignment");
            if (!string.IsNullOrEmpty(bitAlignment))
            {
                var bitParsed = byte.TryParse(bitAlignment, UnsignedXsdIntegerStyles, CultureInfo.InvariantCulture, out var bitValue);
                if (bitParsed)
                    seg.BitAlignment = bitValue;
                RejectFailedNumericAttribute(bitAlignment, bitParsed, "bitAlignment");
            }

            var addressOffset = chanElem.Attribute("addressOffset");
            if (addressOffset != null)
                ReadAddressOffset(seg, addressOffset.Value);
            else
                ReadLegacyMappingIndex(seg, GetTrimmedAttributeValue(chanElem, "pDOmappingIndex"));

            result.Segments.Add(seg);
        }

        return result.Segments.Count > 0 ? result : null;
    }

    private static readonly char[] XsdWhitespace = { ' ', '\t', '\n', '\r' };

    /// <summary>
    /// <c>addressOffset</c> is <c>xsd:hexBinary</c> with no fixed length. The spelling
    /// is kept so a later write can emit the same digits. A value that does not fit
    /// in <see cref="uint"/> is reported and kept in both parsing modes. Only
    /// surrounding whitespace is allowed (whiteSpace=collapse); interior whitespace
    /// is not a valid lexical form and is rejected.
    /// </summary>
    private static void ReadAddressOffset(DynamicChannelSegment segment, string raw)
    {
        var collapsed = raw.Trim(XsdWhitespace);
        if (!IsEvenAsciiHex(collapsed))
        {
            RejectAddressOffset(raw);
            return;
        }

        uint value;
        if (collapsed.Length == 0)
        {
            value = 0;
        }
        else if (!uint.TryParse(collapsed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
        {
            Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                Diagnostics.ParseSeverity.Warning,
                Diagnostics.ParseDiagnosticCodes.XddAddressOffsetExceedsUInt32,
                path: "dynamicChannel/addressOffset",
                message: string.Format(
                    CultureInfo.InvariantCulture,
                    "addressOffset '{0}' is valid xsd:hexBinary and does not fit in 32 bits. PPOffset was left at 0 and the original text is preserved for writing.",
                    raw),
                rawValue: raw));
            segment.PPOffset = 0;
            segment.AddressOffsetLexical = raw;
            segment.AddressOffsetLexicalBaseline = 0;
            return;
        }

        segment.PPOffset = value;
        segment.AddressOffsetLexical = raw;
        segment.AddressOffsetLexicalBaseline = value;
    }

    private static void RejectAddressOffset(string raw)
    {
        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            Diagnostics.ParseDiagnosticCodes.XddInvalidNumericAttribute,
            path: "dynamicChannel/addressOffset",
            message: string.Format(
                CultureInfo.InvariantCulture,
                "Invalid addressOffset '{0}'. Value is not an xsd:hexBinary value. The attribute is ignored.",
                raw),
            rawValue: raw));

        if (!StrictParsingScope.IsEnabled)
            return;

        throw new EdsParseException(
            string.Format(
                CultureInfo.InvariantCulture,
                "Invalid addressOffset '{0}'. Value is not an xsd:hexBinary value.",
                raw))
        {
            Code = Diagnostics.ParseDiagnosticCodes.XddInvalidNumericAttribute
        };
    }

    /// <summary>
    /// Older versions of this library wrote <see cref="DynamicChannelSegment.PPOffset"/>
    /// as <c>pDOmappingIndex</c>. The schema has no such attribute.
    /// </summary>
    private static void ReadLegacyMappingIndex(DynamicChannelSegment segment, string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return;

        var parsed = uint.TryParse(raw, UnsignedXsdIntegerStyles, CultureInfo.InvariantCulture, out var offset);
        var message = string.Format(
            CultureInfo.InvariantCulture,
            "dynamicChannel uses legacy attribute pDOmappingIndex '{0}', which is not defined by the CiA 311 schema. Expected addressOffset.",
            raw);

        Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
            Diagnostics.ParseSeverity.Warning,
            Diagnostics.ParseDiagnosticCodes.XddLegacyAttribute,
            path: "dynamicChannel/pDOmappingIndex",
            message: message,
            rawValue: raw,
            coercedTo: parsed ? offset.ToString(CultureInfo.InvariantCulture) : null));

        if (StrictParsingScope.IsEnabled)
        {
            throw new EdsParseException(message)
            {
                Code = Diagnostics.ParseDiagnosticCodes.XddLegacyAttribute
            };
        }

        if (parsed)
            segment.PPOffset = offset;
        else
            RejectFailedNumericAttribute(raw, parsed: false, "pDOmappingIndex");
    }

    private static bool IsEvenAsciiHex(string text)
    {
        if ((text.Length & 1) != 0)
            return false;

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            var hex = (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')
                || (character >= 'A' && character <= 'F');
            if (!hex)
                return false;
        }

        return true;
    }

    private static void ParseBaudRates(XElement transportLayers, DeviceInfo deviceInfo)
    {
        var physLayer = transportLayers.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "PhysicalLayer");
        if (physLayer == null)
            return;

        var baudRate = physLayer.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "baudRate");
        if (baudRate == null)
            return;

        // defaultValue must match the CiA 311 baud vocabulary under StrictParsing. A known value is
        // kept in its canonical spelling so the writer can emit it again (it is not derivable from
        // the flags); an empty or unknown one leaves the default to be derived.
        var defaultValue = baudRate.Attribute("defaultValue")?.Value ?? string.Empty;
        string? keptDefault = null;
        if (defaultValue.Length > 0)
        {
            var defaultKbps = ParseBaudRateString(defaultValue, out var defaultIsAuto);
            if (defaultIsAuto)
                keptDefault = "auto-baudRate";
            else if (defaultKbps != 0)
                keptDefault = string.Format(CultureInfo.InvariantCulture, "{0} Kbps", defaultKbps);
        }

        var baudRates = deviceInfo.SupportedBaudRates;
        foreach (var supported in baudRate.Elements()
            .Where(e => e.Name.LocalName == "supportedBaudRate"))
        {
            var val = supported.Attribute("value")?.Value ?? string.Empty;
            var kbps = ParseBaudRateString(val, out var isAuto);
            if (isAuto)
                baudRates.AutoBaudRate = true;
            else
                SetBaudRate(baudRates, kbps);
        }

        baudRates.DefaultValueLexical = keptDefault;
        baudRates.DefaultValueFlagsBaseline = baudRates.FlagMask();
    }

    private static void ParseNetworkManagement(XElement networkMgmt, DeviceInfo deviceInfo)
    {
        var generalFeatures = networkMgmt.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "CANopenGeneralFeatures");
        if (generalFeatures != null)
        {
            var granStr = GetTrimmedAttributeValue(generalFeatures, "granularity");
            if (!string.IsNullOrEmpty(granStr))
            {
                var granParsed = byte.TryParse(granStr, UnsignedXsdIntegerStyles, CultureInfo.InvariantCulture, out var gran);
                if (granParsed)
                    deviceInfo.Granularity = gran;
                RejectFailedNumericAttribute(granStr, granParsed, "granularity");
            }

            var rxPdoStr = GetTrimmedAttributeValue(generalFeatures, "nrOfRxPDO");
            if (!string.IsNullOrEmpty(rxPdoStr))
            {
                var rxPdoParsed = ushort.TryParse(rxPdoStr, UnsignedXsdIntegerStyles, CultureInfo.InvariantCulture, out var rxPdo);
                if (rxPdoParsed)
                    deviceInfo.NrOfRxPdo = rxPdo;
                RejectFailedNumericAttribute(rxPdoStr, rxPdoParsed, "nrOfRxPDO");
            }

            var txPdoStr = GetTrimmedAttributeValue(generalFeatures, "nrOfTxPDO");
            if (!string.IsNullOrEmpty(txPdoStr))
            {
                var txPdoParsed = ushort.TryParse(txPdoStr, UnsignedXsdIntegerStyles, CultureInfo.InvariantCulture, out var txPdo);
                if (txPdoParsed)
                    deviceInfo.NrOfTxPdo = txPdo;
                RejectFailedNumericAttribute(txPdoStr, txPdoParsed, "nrOfTxPDO");
            }

            if (generalFeatures.Attribute("bootUpSlave")?.Value is string bootUpSlaveStr)
                deviceInfo.SimpleBootUpSlave = ParseXmlBool(bootUpSlaveStr);

            if (generalFeatures.Attribute("groupMessaging")?.Value is string groupMsgStr)
                deviceInfo.GroupMessaging = ParseXmlBool(groupMsgStr);

            if (generalFeatures.Attribute("layerSettingServiceSlave")?.Value is string lssStr)
                deviceInfo.LssSupported = ParseXmlBool(lssStr);

            if (generalFeatures.Attribute("selfStartingDevice")?.Value is string selfStartingStr)
                deviceInfo.SelfStartingDevice = ParseXmlBool(selfStartingStr);

            if (generalFeatures.Attribute("SDORequestingDevice")?.Value is string sdoRequestingStr)
                deviceInfo.SdoRequestingDevice = ParseXmlBool(sdoRequestingStr);

            var dynChanStr = GetTrimmedAttributeValue(generalFeatures, "dynamicChannels");
            if (!string.IsNullOrEmpty(dynChanStr))
            {
                var dynChanParsed = byte.TryParse(dynChanStr, UnsignedXsdIntegerStyles, CultureInfo.InvariantCulture, out var dynChan);
                if (dynChanParsed)
                    deviceInfo.DynamicChannelsSupported = dynChan;
                RejectFailedNumericAttribute(dynChanStr, dynChanParsed, "dynamicChannels");
            }
        }

        var masterFeatures = networkMgmt.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "CANopenMasterFeatures");
        if (masterFeatures != null)
        {
            if (masterFeatures.Attribute("bootUpMaster")?.Value is string bootUpMasterStr)
                deviceInfo.SimpleBootUpMaster = ParseXmlBool(bootUpMasterStr);

            if (masterFeatures.Attribute("flyingMaster")?.Value is string flyingMasterStr)
                deviceInfo.FlyingMaster = ParseXmlBool(flyingMasterStr);

            if (masterFeatures.Attribute("SDOManager")?.Value is string sdoManagerStr)
                deviceInfo.SdoManager = ParseXmlBool(sdoManagerStr);

            if (masterFeatures.Attribute("configurationManager")?.Value is string configManagerStr)
                deviceInfo.ConfigurationManager = ParseXmlBool(configManagerStr);

            if (masterFeatures.Attribute("layerSettingServiceMaster")?.Value is string lssMasterStr)
                deviceInfo.LayerSettingServiceMaster = ParseXmlBool(lssMasterStr);
        }
    }

    internal static DeviceCommissioning? ParseDeviceCommissioning(XElement networkMgmt)
    {
        var dcElem = networkMgmt.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "deviceCommissioning");
        if (dcElem == null)
            return null;

        var dc = new DeviceCommissioning();

        var nodeIdStr = dcElem.Attribute("nodeID")?.Value ?? string.Empty;
        if (!string.IsNullOrEmpty(nodeIdStr))
        {
            byte nodeIdValue;
            bool parsed;
            if (nodeIdStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                parsed = byte.TryParse(nodeIdStr[2..], NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out nodeIdValue);
            else
                parsed = byte.TryParse(nodeIdStr, NumberStyles.None,
                    CultureInfo.InvariantCulture, out nodeIdValue);

            if (parsed)
            {
                if (!CanOpenNodeId.IsInRange(nodeIdValue))
                    throw new EdsParseException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Invalid nodeID '{0}' (parsed value {1}). CANopen Node-ID must be in range " + CanOpenNodeId.RangeDescription + ".",
                            nodeIdStr,
                            nodeIdValue));
                dc.NodeId = nodeIdValue;
            }
            else
            {
                throw new EdsParseException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Invalid nodeID '{0}'. Value cannot be parsed as a CANopen Node-ID (byte).",
                        nodeIdStr));
            }
        }

        dc.NodeName = dcElem.Attribute("nodeName")?.Value ?? string.Empty;

        // actualBaudRate is a free xsd:string (CiA 311 Annex A.1.4): a text such as
        // "auto-baudRate" is valid but not a rate in kbps. Keep the spelling, report it, and never
        // throw for it (valid input the model only partly represents).
        if (dcElem.Attribute("actualBaudRate")?.Value is string baudrateStr)
        {
            dc.Baudrate = ParseActualBaudRate(baudrateStr);
            dc.ActualBaudRateLexical = baudrateStr;
            dc.ActualBaudRateLexicalBaseline = dc.Baudrate;
        }

        var netNumberStr = GetTrimmedAttributeValue(dcElem, "networkNumber") ?? string.Empty;
        if (!string.IsNullOrEmpty(netNumberStr))
        {
            // xsd:unsignedLong: a value above uint.MaxValue is valid, the model property is a uint.
            var netNumberParsed = ulong.TryParse(netNumberStr, UnsignedXsdIntegerStyles, CultureInfo.InvariantCulture, out var netNum);
            if (netNumberParsed && netNum <= uint.MaxValue)
            {
                dc.NetNumber = (uint)netNum;
                dc.NetworkNumberLexical = netNumberStr;
                dc.NetworkNumberLexicalBaseline = dc.NetNumber;
            }
            else if (netNumberParsed)
            {
                Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                    Diagnostics.ParseSeverity.Warning,
                    Diagnostics.ParseDiagnosticCodes.XddNetworkNumberExceedsUInt32,
                    path: "networkNumber",
                    message: string.Format(
                        CultureInfo.InvariantCulture,
                        "networkNumber '{0}' is a valid xsd:unsignedLong and does not fit in 32 bits. NetNumber was left at 0 and the original text is preserved for writing.",
                        netNumberStr),
                    rawValue: netNumberStr));
                dc.NetNumber = 0;
                dc.NetworkNumberLexical = netNumberStr;
                dc.NetworkNumberLexicalBaseline = 0;
            }

            RejectFailedNumericAttribute(netNumberStr, netNumberParsed, "networkNumber");
        }

        dc.NetworkName = dcElem.Attribute("networkName")?.Value ?? string.Empty;

        if (dcElem.Attribute("CANopenManager")?.Value is string managerStr)
            dc.CANopenManager = ParseXmlBool(managerStr);

        return dc;
    }

    private static ushort ParseActualBaudRate(string value)
    {
        if (TryParseKnownBaudRate(value, out var kbps))
            return kbps;

        if (value.Trim().Length > 0)
        {
            Diagnostics.ParseDiagnosticScope.Report(new Diagnostics.ParseDiagnostic(
                Diagnostics.ParseSeverity.Warning,
                Diagnostics.ParseDiagnosticCodes.XddUnknownBaudRate,
                path: "actualBaudRate",
                rawValue: value,
                coercedTo: "0",
                message: string.Format(
                    CultureInfo.InvariantCulture,
                    "actualBaudRate '{0}' is not a rate in kbps. Baudrate was left at 0 and the original text is preserved for writing.",
                    value)));
        }

        return 0;
    }

    private static void SetBaudRate(BaudRates baudRates, ushort kbps)
    {
        if (BaudRateSetters.TryGetValue(kbps, out var setter))
            setter(baudRates);
    }
}
