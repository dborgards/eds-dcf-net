namespace EdsDcfNet.Validation;

using System.Globalization;
using EdsDcfNet.Models;
using EdsDcfNet.Utilities;

/// <summary>
/// Format-family checks applied after shared model validation when an EDS, DCF, or CPJ file is
/// written with <see cref="CanOpenWriteOptions.ValidateBeforeWrite"/>.
/// </summary>
/// <remarks>
/// These rules stay out of <see cref="CanOpenModelValidator"/> because that validator runs for
/// every format, including XDD/XDC, where the same characters are legal XML text.
/// The writer calls <see cref="TryReject"/> for every section name, key, and value it emits,
/// independent of <see cref="CanOpenWriteOptions"/>.
/// </remarks>
internal static class IniWriteRules
{
    internal enum IniTextSlot
    {
        SectionName,
        Key,
        Value
    }

    internal static void Apply(object model, List<ValidationIssue> issues)
    {
        switch (model)
        {
            case DeviceConfigurationFile dcf:
                ApplyFile(dcf, includeDcfFields: true, issues);
                ApplyCommissioning(dcf.DeviceCommissioning, issues);
                ApplySectionRemainingEntries(dcf, issues);
                break;
            case ElectronicDataSheet eds:
                ApplyFile(eds, includeDcfFields: false, issues);
                ApplySectionRemainingEntries(eds, issues);
                break;
            case NodelistProject cpj:
                ApplyProject(cpj, issues);
                break;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="text"/> cannot be written as INI and
    /// read back unchanged. <paramref name="message"/> explains which rule failed.
    /// </summary>
    internal static bool TryReject(string? text, IniTextSlot slot, out string message)
    {
        if (text is null)
        {
            if (slot == IniTextSlot.Value)
            {
                message = string.Empty;
                return false;
            }

            message = EmptyMessage(slot);
            return true;
        }

        if (text.Length == 0)
        {
            if (slot == IniTextSlot.Key)
            {
                message = EmptyMessage(slot);
                return true;
            }

            message = string.Empty;
            return false;
        }

        if (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[text.Length - 1]))
        {
            message = string.Format(
                CultureInfo.InvariantCulture,
                "INI {0} must not have leading or trailing whitespace. The file would not round-trip.",
                SlotLabel(slot));
            return true;
        }

        if (slot == IniTextSlot.Key)
        {
            if (text.Contains('=') || text.Contains('[') || text.Contains(']'))
            {
                message = "INI key must not contain '=', '[' or ']'. The file would not round-trip.";
                return true;
            }

            if (text[0] == ';')
            {
                message = "INI key must not start with ';'. The file would not round-trip.";
                return true;
            }
        }
        else if (slot == IniTextSlot.SectionName && text.Contains(']'))
        {
            message = "INI section name must not contain ']'. The file would not round-trip.";
            return true;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character > '\u001F')
                continue;

            if (character == '\t' && slot == IniTextSlot.Value)
                continue;

            message = string.Format(
                CultureInfo.InvariantCulture,
                "INI {0} contains control character U+{1:X4}. A tab is allowed only inside a value; other C0 controls are not allowed. The file would not round-trip.",
                SlotLabel(slot),
                (int)character);
            return true;
        }

        message = string.Empty;
        return false;
    }

    private static void ApplyFile(ICanOpenFileModel model, bool includeDcfFields, List<ValidationIssue> issues)
    {
        ApplyFileInfo(model.FileInfo, includeDcfFields, issues);
        ApplyDeviceInfo(model.DeviceInfo, issues);
        ApplyDictionary(model.ObjectDictionary, includeDcfFields, issues);
        ApplyComments(model.Comments, issues);
        ApplyModules(model.SupportedModules, issues);
        ApplyDynamicChannels(model.DynamicChannels, issues);
        ApplyTools(model.Tools, issues);
        ApplyAdditionalSections(model.AdditionalSections, issues, model.ObjectDictionary);
    }

    private static void ApplyFileInfo(EdsFileInfo fileInfo, bool includeLastEds, List<ValidationIssue> issues)
    {
        Check(fileInfo.FileName, IniTextSlot.Value, "FileInfo.FileName", issues);
        Check(fileInfo.EdsVersion, IniTextSlot.Value, "FileInfo.EdsVersion", issues);
        Check(fileInfo.Description, IniTextSlot.Value, "FileInfo.Description", issues);
        Check(fileInfo.CreationTime, IniTextSlot.Value, "FileInfo.CreationTime", issues);
        Check(fileInfo.CreationDate, IniTextSlot.Value, "FileInfo.CreationDate", issues);
        Check(fileInfo.CreatedBy, IniTextSlot.Value, "FileInfo.CreatedBy", issues);
        Check(fileInfo.ModificationTime, IniTextSlot.Value, "FileInfo.ModificationTime", issues);
        Check(fileInfo.ModificationDate, IniTextSlot.Value, "FileInfo.ModificationDate", issues);
        Check(fileInfo.ModifiedBy, IniTextSlot.Value, "FileInfo.ModifiedBy", issues);
        if (includeLastEds)
            CheckIfPresent(fileInfo.LastEds, IniTextSlot.Value, "FileInfo.LastEds", issues);

        ApplyRemaining(
            fileInfo.RemainingEntries,
            includeLastEds ? SectionEntryKeys.IsDcfFileInfoKey : SectionEntryKeys.IsEdsFileInfoKey,
            "FileInfo",
            issues);
    }

    private static void ApplyDeviceInfo(DeviceInfo deviceInfo, List<ValidationIssue> issues)
    {
        Check(deviceInfo.VendorName, IniTextSlot.Value, "DeviceInfo.VendorName", issues);
        Check(deviceInfo.ProductName, IniTextSlot.Value, "DeviceInfo.ProductName", issues);
        Check(deviceInfo.OrderCode, IniTextSlot.Value, "DeviceInfo.OrderCode", issues);
        ApplyRemaining(deviceInfo.RemainingEntries, SectionEntryKeys.IsDeviceInfoKey, "DeviceInfo", issues);
    }

    private static void ApplyCommissioning(DeviceCommissioning commissioning, List<ValidationIssue> issues)
    {
        if (!DeviceCommissioningSemantics.IsWrittenToDcf(commissioning))
            return;

        Check(commissioning.NodeName, IniTextSlot.Value, "DeviceCommissioning.NodeName", issues);
        CheckIfPresent(commissioning.NodeRefd, IniTextSlot.Value, "DeviceCommissioning.NodeRefd", issues);
        Check(commissioning.NetworkName, IniTextSlot.Value, "DeviceCommissioning.NetworkName", issues);
        CheckIfPresent(commissioning.NetRefd, IniTextSlot.Value, "DeviceCommissioning.NetRefd", issues);
        ApplyRemaining(
            commissioning.RemainingEntries,
            SectionEntryKeys.IsDeviceCommissioningKey,
            "DeviceCommissioning",
            issues);
    }

    private static void ApplyDictionary(ObjectDictionary dictionary, bool includeDcfFields, List<ValidationIssue> issues)
    {
        foreach (var entry in dictionary.Objects)
        {
            var objectPath = string.Format(
                CultureInfo.InvariantCulture,
                "ObjectDictionary.Objects[0x{0:X4}]",
                entry.Key);
            ApplyObject(entry.Value, includeDcfFields, objectPath, issues);
        }
    }

    private static void ApplyObject(
        CanOpenObject obj,
        bool includeDcfFields,
        string objectPath,
        List<ValidationIssue> issues)
    {
        Check(obj.ParameterName, IniTextSlot.Value, objectPath + ".ParameterName", issues);
        CheckIfPresent(obj.DefaultValue, IniTextSlot.Value, objectPath + ".DefaultValue", issues);
        CheckIfPresent(obj.LowLimit, IniTextSlot.Value, objectPath + ".LowLimit", issues);
        CheckIfPresent(obj.HighLimit, IniTextSlot.Value, objectPath + ".HighLimit", issues);
        CheckIfPresent(obj.InvertedSrad, IniTextSlot.Value, objectPath + ".InvertedSrad", issues);
        if (includeDcfFields)
        {
            CheckIfPresent(obj.ParameterValue, IniTextSlot.Value, objectPath + ".ParameterValue", issues);
            CheckIfPresent(obj.Denotation, IniTextSlot.Value, objectPath + ".Denotation", issues);
            CheckIfPresent(obj.ParamRefd, IniTextSlot.Value, objectPath + ".ParamRefd", issues);
            CheckIfPresent(obj.UploadFile, IniTextSlot.Value, objectPath + ".UploadFile", issues);
            CheckIfPresent(obj.DownloadFile, IniTextSlot.Value, objectPath + ".DownloadFile", issues);
        }

        ApplyRemaining(
            obj.RemainingEntries,
            includeDcfFields ? SectionEntryKeys.IsDcfObjectKey : SectionEntryKeys.IsEdsObjectKey,
            objectPath,
            issues);

        foreach (var subEntry in obj.SubObjects)
        {
            var subPath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.SubObjects[0x{1:X2}]",
                objectPath,
                subEntry.Key);
            ApplySubObject(subEntry.Value, includeDcfFields, subPath, issues);
        }
    }

    private static void ApplySubObject(
        CanOpenSubObject subObj,
        bool includeDcfFields,
        string subPath,
        List<ValidationIssue> issues)
    {
        Check(subObj.ParameterName, IniTextSlot.Value, subPath + ".ParameterName", issues);
        CheckIfPresent(subObj.DefaultValue, IniTextSlot.Value, subPath + ".DefaultValue", issues);
        CheckIfPresent(subObj.LowLimit, IniTextSlot.Value, subPath + ".LowLimit", issues);
        CheckIfPresent(subObj.HighLimit, IniTextSlot.Value, subPath + ".HighLimit", issues);
        CheckIfPresent(subObj.InvertedSrad, IniTextSlot.Value, subPath + ".InvertedSrad", issues);
        if (includeDcfFields)
        {
            CheckIfPresent(subObj.ParameterValue, IniTextSlot.Value, subPath + ".ParameterValue", issues);
            CheckIfPresent(subObj.Denotation, IniTextSlot.Value, subPath + ".Denotation", issues);
            CheckIfPresent(subObj.ParamRefd, IniTextSlot.Value, subPath + ".ParamRefd", issues);
        }

        ApplyRemaining(
            subObj.RemainingEntries,
            includeDcfFields ? SectionEntryKeys.IsDcfSubObjectKey : SectionEntryKeys.IsEdsSubObjectKey,
            subPath,
            issues);
    }

    private static void ApplyRemaining(
        OrderedStringDictionary entries,
        Func<string, bool> isDedicatedKey,
        string ownerPath,
        List<ValidationIssue> issues)
    {
        foreach (var entry in entries)
        {
            if (isDedicatedKey(entry.Key))
                continue;

            var path = ownerPath + ".RemainingEntries[" + entry.Key + "]";
            Check(entry.Key, IniTextSlot.Key, path, issues);
            Check(entry.Value, IniTextSlot.Value, path, issues);
        }
    }

    private static void ApplyComments(Comments? comments, List<ValidationIssue> issues)
        => ApplyCommentLines(comments, "Comments", issues);

    private static void ApplyCommentLines(Comments? comments, string path, List<ValidationIssue> issues)
    {
        if (comments == null)
            return;

        ApplyRemaining(
            comments.RemainingEntries,
            key => SectionEntryKeys.IsGeneratedCommentsKey(key, comments.CommentLines.Keys),
            path,
            issues);

        foreach (var line in comments.CommentLines)
        {
            Check(
                line.Value,
                IniTextSlot.Value,
                string.Format(CultureInfo.InvariantCulture, "{0}.CommentLines[{1}]", path, line.Key),
                issues);
        }
    }

    private static void ApplyModules(List<ModuleInfo> modules, List<ValidationIssue> issues)
    {
        for (var i = 0; i < modules.Count; i++)
        {
            var module = modules[i];
            var path = string.Format(CultureInfo.InvariantCulture, "SupportedModules[{0}]", i);
            Check(module.ProductName, IniTextSlot.Value, path + ".ProductName", issues);
            Check(module.OrderCode, IniTextSlot.Value, path + ".OrderCode", issues);
            ApplyCommentLines(module.Comments, path + ".Comments", issues);

            foreach (var entry in module.FixedObjectDefinitions)
            {
                var objectPath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.FixedObjectDefinitions[0x{1:X4}]",
                    path,
                    entry.Key);
                // WriteModuleFixedObject emits DCF value fields and filters remaining
                // entries with the DCF key set for both EDS and DCF.
                ApplyObject(entry.Value, includeDcfFields: true, objectPath, issues);
            }

            foreach (var entry in module.SubExtensionDefinitions)
            {
                var extensionPath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.SubExtensionDefinitions[0x{1:X4}]",
                    path,
                    entry.Key);
                var extension = entry.Value;
                Check(extension.ParameterName, IniTextSlot.Value, extensionPath + ".ParameterName", issues);
                CheckIfPresent(extension.DefaultValue, IniTextSlot.Value, extensionPath + ".DefaultValue", issues);
                CheckIfPresent(extension.LowLimit, IniTextSlot.Value, extensionPath + ".LowLimit", issues);
                CheckIfPresent(extension.HighLimit, IniTextSlot.Value, extensionPath + ".HighLimit", issues);
                Check(extension.Count, IniTextSlot.Value, extensionPath + ".Count", issues);
            }
        }
    }

    private static void ApplyDynamicChannels(DynamicChannels? dynamicChannels, List<ValidationIssue> issues)
    {
        if (dynamicChannels == null)
            return;

        ApplyRemaining(
            dynamicChannels.RemainingEntries,
            key => SectionEntryKeys.IsDynamicChannelsKey(key, dynamicChannels.Segments.Count),
            "DynamicChannels",
            issues);

        for (var i = 0; i < dynamicChannels.Segments.Count; i++)
        {
            Check(
                dynamicChannels.Segments[i].Range,
                IniTextSlot.Value,
                string.Format(CultureInfo.InvariantCulture, "DynamicChannels.Segments[{0}].Range", i),
                issues);
        }
    }

    private static void ApplyTools(List<ToolInfo> tools, List<ValidationIssue> issues)
    {
        for (var i = 0; i < tools.Count; i++)
        {
            var path = string.Format(CultureInfo.InvariantCulture, "Tools[{0}]", i);
            Check(tools[i].Name, IniTextSlot.Value, path + ".Name", issues);
            Check(tools[i].Command, IniTextSlot.Value, path + ".Command", issues);
            ApplyRemaining(tools[i].RemainingEntries, SectionEntryKeys.IsToolKey, path, issues);
        }
    }

    /// <summary>
    /// Checks the kept entries of <c>SectionRemainingEntries</c> that the writer outputs. The
    /// filter comes from <see cref="Writers.IniWriterBase.TryGetWrittenSectionFilter"/>: a
    /// section the writer does not emit (for example of a removed module) and a kept key the
    /// writer generates itself (for example slot <c>1</c> of a non-empty object list) are not
    /// written and therefore not checked. Only keys and values are checked; the writer emits
    /// the canonical section name.
    /// </summary>
    private static void ApplySectionRemainingEntries(ICanOpenFileModel model, List<ValidationIssue> issues)
    {
        foreach (var section in model.SectionRemainingEntries)
        {
            if (section.Value == null
                || !Writers.IniWriterBase.TryGetWrittenSectionFilter(model, section.Key, out var isSuppressedKey))
            {
                continue;
            }

            foreach (var entry in section.Value)
            {
                if (isSuppressedKey(entry.Key))
                    continue;

                var path = "SectionRemainingEntries[" + section.Key + "][" + entry.Key + "]";
                Check(entry.Key, IniTextSlot.Key, path, issues);
                Check(entry.Value, IniTextSlot.Value, path, issues);
            }
        }
    }

    private static void ApplyAdditionalSections(
        Dictionary<string, Dictionary<string, string>> sections,
        List<ValidationIssue> issues,
        ObjectDictionary? objectDictionary = null)
    {
        foreach (var section in sections)
        {
            // EdsWriter and DcfWriter drop a stale [xxxxObjectLinks] section when that
            // object already exists and emit ObjectLinks from the object instead.
            if (objectDictionary != null
                && ObjectLinksSectionHelper.IsObjectLinksSectionForExistingObject(section.Key, objectDictionary))
            {
                continue;
            }

            var sectionPath = "AdditionalSections[" + section.Key + "]";
            Check(section.Key, IniTextSlot.SectionName, sectionPath, issues);
            if (section.Value == null)
                continue;

            foreach (var entry in section.Value)
            {
                var path = sectionPath + "." + entry.Key;
                Check(entry.Key, IniTextSlot.Key, path, issues);
                Check(entry.Value, IniTextSlot.Value, path, issues);
            }
        }
    }

    private static void ApplyProject(NodelistProject project, List<ValidationIssue> issues)
    {
        for (var i = 0; i < project.Networks.Count; i++)
        {
            var network = project.Networks[i];
            var path = string.Format(CultureInfo.InvariantCulture, "Networks[{0}]", i);
            CheckIfPresent(network.NetName, IniTextSlot.Value, path + ".NetName", issues);
            CheckIfPresent(network.NetRefd, IniTextSlot.Value, path + ".NetRefd", issues);
            CheckIfPresent(network.EdsBaseName, IniTextSlot.Value, path + ".EdsBaseName", issues);

            foreach (var nodeEntry in network.Nodes)
            {
                var nodePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Nodes[{1}]",
                    path,
                    nodeEntry.Key);
                var node = nodeEntry.Value;
                CheckIfPresent(node.Name, IniTextSlot.Value, nodePath + ".Name", issues);
                CheckIfPresent(node.Refd, IniTextSlot.Value, nodePath + ".Refd", issues);
                CheckIfPresent(node.DcfFileName, IniTextSlot.Value, nodePath + ".DcfFileName", issues);
            }
        }

        ApplyAdditionalSections(project.AdditionalSections, issues);
    }

    private static void Check(string? text, IniTextSlot slot, string path, List<ValidationIssue> issues)
    {
        if (!TryReject(text, slot, out var message))
            return;

        issues.Add(new ValidationIssue(path, message, ValidationIssueCodes.IniTextNotRoundTrippable));
    }

    private static void CheckIfPresent(string? text, IniTextSlot slot, string path, List<ValidationIssue> issues)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Check(text, slot, path, issues);
    }

    private static string EmptyMessage(IniTextSlot slot)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "INI {0} must not be empty. The file would not round-trip.",
            SlotLabel(slot));
    }

    private static string SlotLabel(IniTextSlot slot) => slot switch
    {
        IniTextSlot.SectionName => "section name",
        IniTextSlot.Key => "key",
        _ => "value"
    };
}
