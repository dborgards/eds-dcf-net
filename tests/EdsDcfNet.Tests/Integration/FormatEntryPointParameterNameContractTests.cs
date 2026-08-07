namespace EdsDcfNet.Tests.Integration;

using System.Reflection;
using System.Text;
using EdsDcfNet;
using EdsDcfNet.Models;
using EdsDcfNet.Validation;

/// <summary>
/// Golden contract for public format entry-point parameter names (named-argument source ABI).
/// Incidents: #314 / #321 — shared generic bases silently renamed parameters on
/// <c>CanOpenFile.{Eds,Dcf,Cpj,Xdd,Xdc}</c>.
/// </summary>
public class FormatEntryPointParameterNameContractTests
{
    private static readonly string BaselinePath = Path.Combine(
        AppContext.BaseDirectory,
        "Baselines",
        "format-entry-point-parameter-names.txt");

    [Fact]
    public void FormatEntryPoints_PreservePublicParameterNames()
    {
        var actual = BuildContractSnapshot();
        var sourceBaselinePath = FindSourceBaselinePath();

        if (string.Equals(
                Environment.GetEnvironmentVariable("UPDATE_PUBLIC_API_BASELINE"),
                "1",
                StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sourceBaselinePath)!);
            File.WriteAllText(sourceBaselinePath, actual);
            return;
        }

        File.Exists(BaselinePath).Should().BeTrue(
            $"missing baseline at {BaselinePath}; regenerate with UPDATE_PUBLIC_API_BASELINE=1");

        var expected = NormalizeNewlines(File.ReadAllText(BaselinePath));
        actual.Should().Be(expected,
            "public format entry-point parameter names are a source contract. " +
            "If the rename is intentional, update Baselines/format-entry-point-parameter-names.txt " +
            "(UPDATE_PUBLIC_API_BASELINE=1) and note the breaking change in the PR " +
            "(see CONTRIBUTING.md Public API checklist).");
    }

    private static string FindSourceBaselinePath()
    {
        // Walk up from bin/... to the test project root. Do not stop on a
        // Baselines file alone: CopyToOutputDirectory places a copy under
        // AppContext.BaseDirectory, which must not be treated as the source.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Integration")))
                return Path.Combine(dir.FullName, "Baselines", "format-entry-point-parameter-names.txt");
            dir = dir.Parent;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..",
            "Baselines",
            "format-entry-point-parameter-names.txt"));
    }

    internal static string BuildContractSnapshot()
    {
        var lines = new SortedSet<string>(StringComparer.Ordinal);

        AddTypeMethods(lines, typeof(FormatCanOpenOperations<ElectronicDataSheet>), "FormatCanOpenOperations");
        AddTypeMethods(lines, typeof(EdsCanOpenOperations), "CanOpenFile.Eds");
        AddTypeMethods(lines, typeof(DcfCanOpenOperations), "CanOpenFile.Dcf");
        AddTypeMethods(lines, typeof(CpjCanOpenOperations), "CanOpenFile.Cpj");
        AddTypeMethods(lines, typeof(XddCanOpenOperations), "CanOpenFile.Xdd");
        AddTypeMethods(lines, typeof(XdcCanOpenOperations), "CanOpenFile.Xdc");
        AddCanOpenFileValidationMethods(lines);

        var sb = new StringBuilder();
        sb.AppendLine("# Public format entry-point parameter-name contract");
        sb.AppendLine("# Named arguments on these methods are a source ABI. Update only with intentional breaks.");
        sb.AppendLine("# See CONTRIBUTING.md § Public API compatibility checklist and issue #425.");
        sb.AppendLine();
        foreach (var line in lines)
            sb.AppendLine(line);

        return NormalizeNewlines(sb.ToString());
    }

    private static void AddTypeMethods(ISet<string> lines, Type type, string prefix)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .Where(m => !m.IsSpecialName)
                     .Where(m => m.DeclaringType != typeof(object))
                     .Where(IsFormatSurfaceMethod)
                     .OrderBy(m => m.Name, StringComparer.Ordinal)
                     .ThenBy(m => m.GetParameters().Length)
                     .ThenBy(m => FormatParameterList(m.GetParameters()), StringComparer.Ordinal))
        {
            lines.Add($"{prefix}.{method.Name}({FormatParameterList(method.GetParameters())})");
        }
    }

    private static bool IsFormatSurfaceMethod(MethodInfo method)
    {
        var declaring = method.DeclaringType;
        if (declaring is null)
            return false;

        if (declaring.IsGenericType &&
            declaring.GetGenericTypeDefinition() == typeof(FormatCanOpenOperations<>))
            return true;

        return declaring.Namespace == "EdsDcfNet" &&
               declaring.Name.EndsWith("CanOpenOperations", StringComparison.Ordinal);
    }

    private static void AddCanOpenFileValidationMethods(ISet<string> lines)
    {
        foreach (var method in typeof(CanOpenFile).GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name is "Validate" or "ValidateAsync" or "EnsureValid" or "EnsureValidAsync")
                     .OrderBy(m => m.Name, StringComparer.Ordinal)
                     .ThenBy(m => FormatTypeName(m.GetParameters()[0].ParameterType), StringComparer.Ordinal)
                     .ThenBy(m => m.GetParameters().Length))
        {
            lines.Add($"CanOpenFile.{method.Name}({FormatParameterList(method.GetParameters())})");
        }
    }

    private static string FormatParameterList(ParameterInfo[] parameters)
        => string.Join(", ", parameters.Select(p => $"{FormatTypeName(p.ParameterType)} {p.Name}"));

    private static string FormatTypeName(Type type)
    {
        if (type.IsByRef)
            return FormatTypeName(type.GetElementType()!) + "&";

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return FormatTypeName(underlying) + "?";

        if (type.IsArray)
            return FormatTypeName(type.GetElementType()!) + "[]";

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            if (def == typeof(Task<>))
                return $"Task<{args}>";
            if (def == typeof(IReadOnlyList<>))
                return $"IReadOnlyList<{args}>";

            var name = type.Name;
            var tick = name.IndexOf('`', StringComparison.Ordinal);
            if (tick >= 0)
                name = name[..tick];
            return $"{name}<{args}>";
        }

        return type switch
        {
            _ when type == typeof(void) => "void",
            _ when type == typeof(string) => "string",
            _ when type == typeof(byte) => "byte",
            _ when type == typeof(ushort) => "ushort",
            _ when type == typeof(long) => "long",
            _ when type == typeof(int) => "int",
            _ when type == typeof(bool) => "bool",
            _ when type == typeof(DateTime) => "DateTime",
            _ when type == typeof(Stream) => "Stream",
            _ when type == typeof(Task) => "Task",
            _ when type == typeof(CancellationToken) => "CancellationToken",
            _ when type == typeof(ElectronicDataSheet) => "ElectronicDataSheet",
            _ when type == typeof(DeviceConfigurationFile) => "DeviceConfigurationFile",
            _ when type == typeof(NodelistProject) => "NodelistProject",
            _ when type == typeof(CanOpenFileOptions) => "CanOpenFileOptions",
            _ when type == typeof(CanOpenWriteOptions) => "CanOpenWriteOptions",
            _ when type == typeof(ValidationIssue) => "ValidationIssue",
            _ => type.Name
        };
    }

    private static string NormalizeNewlines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";
}
