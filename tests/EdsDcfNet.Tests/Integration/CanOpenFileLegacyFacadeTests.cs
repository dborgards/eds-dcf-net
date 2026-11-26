namespace EdsDcfNet.Tests.Integration;

using System.Reflection;
using System.Text;
using EdsDcfNet;
using EdsDcfNet.Models;
using EdsDcfNet.Parsers;

/// <summary>
/// The one suite for the obsolete <see cref="CanOpenFile"/> facade overloads.
/// Every overload is called next to the <c>CanOpenFile.{Format}</c> member it forwards to,
/// with the same input, and both must end the same way (same output or same exception).
/// Each scenario also uses an input that only gives the expected result when the
/// size limit, options or cancellation token reach the canonical member.
/// Behaviour tests use the canonical API and live in the other suites.
/// </summary>
public class CanOpenFileLegacyFacadeTests
{
    private const long TooSmallMaxInputSize = 10;

    private const string MinimalCpjContent = "[Topology]\nNetName=Facade Network\nNodes=0";

    private static readonly IReadOnlyDictionary<string, IFacadeFormat> Formats = CreateFormats();

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var format in Formats.Values)
        {
            foreach (var shape in format.Shapes)
                data.Add(format.Token, shape);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public Task ObsoleteOverload_BehavesLikeCanonicalMember(string format, string shape)
        => Formats[format].RunAsync(shape);

    [Fact]
    public void EveryObsoleteFormatOverload_IsCoveredByThisSuite()
    {
        var obsolete = typeof(CanOpenFile)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<ObsoleteAttribute>() is not null)
            .ToList();

        var covered = Formats.Values
            .SelectMany(f => f.Shapes.Select(shape => f.Token + ":" + shape))
            .ToList();
        var actual = obsolete
            .Where(m => m.Name != nameof(CanOpenFile.EdsToDcf))
            .Select(ShapeKey)
            .ToList();

        actual.Should().BeEquivalentTo(covered);
        obsolete.Where(m => m.Name == nameof(CanOpenFile.EdsToDcf))
            .Select(m => string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name)))
            .Should().BeEquivalentTo(new[] { "ElectronicDataSheet,Byte,UInt16,String" });
    }

    [Theory]
    [InlineData((ushort)250, null)]
    [InlineData((ushort)500, "Facade Node")]
    public void EdsToDcf_ObsoleteOverload_BehavesLikeCanonicalMember(ushort baudrate, string? nodeName)
    {
        var eds = CanOpenFile.Eds.ReadFile("Fixtures/sample_device.eds");

        var legacy = CanOpenFile.EdsToDcf(eds, 5, baudrate, nodeName);
        var canonical = CanOpenFile.Eds.ConvertToDcf(eds, 5, baudrate, nodeName);

        // Both stamp the current time; align it so the rest of the output can be compared.
        legacy.FileInfo.CreationDate = canonical.FileInfo.CreationDate;
        legacy.FileInfo.CreationTime = canonical.FileInfo.CreationTime;
        legacy.FileInfo.ModificationDate = canonical.FileInfo.ModificationDate;
        legacy.FileInfo.ModificationTime = canonical.FileInfo.ModificationTime;

        CanOpenFile.Dcf.WriteToString(legacy).Should().Be(CanOpenFile.Dcf.WriteToString(canonical));
        legacy.DeviceCommissioning.Baudrate.Should().Be(baudrate);

        var invalidNodeId = () => CanOpenFile.EdsToDcf(eds, 0, baudrate, nodeName);
        invalidNodeId.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(nameof(CanOpenFile.ReadEds))]
    [InlineData(nameof(CanOpenFile.ReadDcf))]
    [InlineData(nameof(CanOpenFile.ReadCpj))]
    [InlineData(nameof(CanOpenFile.ReadXdd))]
    [InlineData(nameof(CanOpenFile.ReadXdc))]
    [InlineData(nameof(CanOpenFile.ReadEdsFromString))]
    [InlineData(nameof(CanOpenFile.ReadDcfFromString))]
    [InlineData(nameof(CanOpenFile.ReadCpjFromString))]
    [InlineData(nameof(CanOpenFile.ReadXddFromString))]
    [InlineData(nameof(CanOpenFile.ReadXdcFromString))]
    public void SyncReadMethods_KeepOptionalMaxInputSize(string methodName)
    {
        var method = typeof(CanOpenFile).GetMethod(methodName, new[] { typeof(string), typeof(long) });

        method.Should().NotBeNull();
        method!.GetParameters()[1].HasDefaultValue.Should().BeTrue();
    }

    private static string ShapeKey(MethodInfo method)
    {
        var token = Formats.Keys.Single(t => method.Name.Contains(t, StringComparison.Ordinal));
        var name = method.Name.Replace(token, string.Empty);
        var parameters = method.GetParameters().Select(p =>
            p.ParameterType == typeof(ElectronicDataSheet)
            || p.ParameterType == typeof(DeviceConfigurationFile)
            || p.ParameterType == typeof(NodelistProject)
                ? "Model"
                : p.ParameterType.Name);
        return token + ":" + name + "(" + string.Join(",", parameters) + ")";
    }

    private static IReadOnlyDictionary<string, IFacadeFormat> CreateFormats()
    {
        var eds = new FacadeFormat<ElectronicDataSheet>("Eds", CanOpenFile.Eds, ".eds", () => File.ReadAllText("Fixtures/sample_device.eds"))
        {
            ReadFileMax = (p, m) => CanOpenFile.ReadEds(p, m),
            ReadFileOptions = (p, o) => CanOpenFile.ReadEds(p, o),
            ReadFileAsyncToken = (p, t) => CanOpenFile.ReadEdsAsync(p, t),
            ReadFileAsyncMax = (p, m, t) => CanOpenFile.ReadEdsAsync(p, m, t),
            ReadFileAsyncOptions = (p, o, t) => CanOpenFile.ReadEdsAsync(p, o, t),
            ReadStringMax = (c, m) => CanOpenFile.ReadEdsFromString(c, m),
            ReadStringOptions = (c, o) => CanOpenFile.ReadEdsFromString(c, o),
            ReadStreamMax = (s, m) => CanOpenFile.ReadEds(s, m),
            ReadStreamOptions = (s, o) => CanOpenFile.ReadEds(s, o),
            ReadStreamAsyncToken = (s, t) => CanOpenFile.ReadEdsAsync(s, t),
            ReadStreamAsyncMax = (s, m, t) => CanOpenFile.ReadEdsAsync(s, m, t),
            ReadStreamAsyncOptions = (s, o, t) => CanOpenFile.ReadEdsAsync(s, o, t),
            WriteFile = (x, p) => CanOpenFile.WriteEds(x, p),
            WriteFileOptions = (x, p, o) => CanOpenFile.WriteEds(x, p, o),
            WriteStream = (x, s) => CanOpenFile.WriteEds(x, s),
            WriteStreamOptions = (x, s, o) => CanOpenFile.WriteEds(x, s, o),
            WriteFileAsyncToken = (x, p, t) => CanOpenFile.WriteEdsAsync(x, p, t),
            WriteFileAsyncOptions = (x, p, o, t) => CanOpenFile.WriteEdsAsync(x, p, o, t),
            WriteStreamAsyncToken = (x, s, t) => CanOpenFile.WriteEdsAsync(x, s, t),
            WriteStreamAsyncOptions = (x, s, o, t) => CanOpenFile.WriteEdsAsync(x, s, o, t),
            WriteToString = x => CanOpenFile.WriteEdsToString(x),
            WriteToStringOptions = (x, o) => CanOpenFile.WriteEdsToString(x, o),
        };

        var dcf = new FacadeFormat<DeviceConfigurationFile>("Dcf", CanOpenFile.Dcf, ".dcf", () => File.ReadAllText("Fixtures/minimal.dcf"))
        {
            ReadFileMax = (p, m) => CanOpenFile.ReadDcf(p, m),
            ReadFileOptions = (p, o) => CanOpenFile.ReadDcf(p, o),
            ReadFileAsyncToken = (p, t) => CanOpenFile.ReadDcfAsync(p, t),
            ReadFileAsyncMax = (p, m, t) => CanOpenFile.ReadDcfAsync(p, m, t),
            ReadFileAsyncOptions = (p, o, t) => CanOpenFile.ReadDcfAsync(p, o, t),
            ReadStringMax = (c, m) => CanOpenFile.ReadDcfFromString(c, m),
            ReadStringOptions = (c, o) => CanOpenFile.ReadDcfFromString(c, o),
            ReadStreamMax = (s, m) => CanOpenFile.ReadDcf(s, m),
            ReadStreamOptions = (s, o) => CanOpenFile.ReadDcf(s, o),
            ReadStreamAsyncToken = (s, t) => CanOpenFile.ReadDcfAsync(s, t),
            ReadStreamAsyncMax = (s, m, t) => CanOpenFile.ReadDcfAsync(s, m, t),
            ReadStreamAsyncOptions = (s, o, t) => CanOpenFile.ReadDcfAsync(s, o, t),
            WriteFile = (x, p) => CanOpenFile.WriteDcf(x, p),
            WriteFileOptions = (x, p, o) => CanOpenFile.WriteDcf(x, p, o),
            WriteStream = (x, s) => CanOpenFile.WriteDcf(x, s),
            WriteStreamOptions = (x, s, o) => CanOpenFile.WriteDcf(x, s, o),
            WriteFileAsyncToken = (x, p, t) => CanOpenFile.WriteDcfAsync(x, p, t),
            WriteFileAsyncOptions = (x, p, o, t) => CanOpenFile.WriteDcfAsync(x, p, o, t),
            WriteStreamAsyncToken = (x, s, t) => CanOpenFile.WriteDcfAsync(x, s, t),
            WriteStreamAsyncOptions = (x, s, o, t) => CanOpenFile.WriteDcfAsync(x, s, o, t),
            WriteToString = x => CanOpenFile.WriteDcfToString(x),
            WriteToStringOptions = (x, o) => CanOpenFile.WriteDcfToString(x, o),
        };

        var cpj = new FacadeFormat<NodelistProject>("Cpj", CanOpenFile.Cpj, ".cpj", () => MinimalCpjContent)
        {
            ReadFileMax = (p, m) => CanOpenFile.ReadCpj(p, m),
            ReadFileOptions = (p, o) => CanOpenFile.ReadCpj(p, o),
            ReadFileAsyncToken = (p, t) => CanOpenFile.ReadCpjAsync(p, t),
            ReadFileAsyncMax = (p, m, t) => CanOpenFile.ReadCpjAsync(p, m, t),
            ReadFileAsyncOptions = (p, o, t) => CanOpenFile.ReadCpjAsync(p, o, t),
            ReadStringMax = (c, m) => CanOpenFile.ReadCpjFromString(c, m),
            ReadStringOptions = (c, o) => CanOpenFile.ReadCpjFromString(c, o),
            ReadStreamMax = (s, m) => CanOpenFile.ReadCpj(s, m),
            ReadStreamOptions = (s, o) => CanOpenFile.ReadCpj(s, o),
            ReadStreamAsyncToken = (s, t) => CanOpenFile.ReadCpjAsync(s, t),
            ReadStreamAsyncMax = (s, m, t) => CanOpenFile.ReadCpjAsync(s, m, t),
            ReadStreamAsyncOptions = (s, o, t) => CanOpenFile.ReadCpjAsync(s, o, t),
            WriteFile = (x, p) => CanOpenFile.WriteCpj(x, p),
            WriteFileOptions = (x, p, o) => CanOpenFile.WriteCpj(x, p, o),
            WriteStream = (x, s) => CanOpenFile.WriteCpj(x, s),
            WriteStreamOptions = (x, s, o) => CanOpenFile.WriteCpj(x, s, o),
            WriteFileAsyncToken = (x, p, t) => CanOpenFile.WriteCpjAsync(x, p, t),
            WriteFileAsyncOptions = (x, p, o, t) => CanOpenFile.WriteCpjAsync(x, p, o, t),
            WriteStreamAsyncToken = (x, s, t) => CanOpenFile.WriteCpjAsync(x, s, t),
            WriteStreamAsyncOptions = (x, s, o, t) => CanOpenFile.WriteCpjAsync(x, s, o, t),
            WriteToString = x => CanOpenFile.WriteCpjToString(x),
            WriteToStringOptions = (x, o) => CanOpenFile.WriteCpjToString(x, o),
        };

        var xdd = new FacadeFormat<ElectronicDataSheet>("Xdd", CanOpenFile.Xdd, ".xdd", () => File.ReadAllText("Fixtures/sample_device.xdd"))
        {
            ReadFileMax = (p, m) => CanOpenFile.ReadXdd(p, m),
            ReadFileOptions = (p, o) => CanOpenFile.ReadXdd(p, o),
            ReadFileAsyncToken = (p, t) => CanOpenFile.ReadXddAsync(p, t),
            ReadFileAsyncMax = (p, m, t) => CanOpenFile.ReadXddAsync(p, m, t),
            ReadFileAsyncOptions = (p, o, t) => CanOpenFile.ReadXddAsync(p, o, t),
            ReadStringMax = (c, m) => CanOpenFile.ReadXddFromString(c, m),
            ReadStringOptions = (c, o) => CanOpenFile.ReadXddFromString(c, o),
            ReadStreamMax = (s, m) => CanOpenFile.ReadXdd(s, m),
            ReadStreamOptions = (s, o) => CanOpenFile.ReadXdd(s, o),
            ReadStreamAsyncToken = (s, t) => CanOpenFile.ReadXddAsync(s, t),
            ReadStreamAsyncMax = (s, m, t) => CanOpenFile.ReadXddAsync(s, m, t),
            ReadStreamAsyncOptions = (s, o, t) => CanOpenFile.ReadXddAsync(s, o, t),
            WriteFile = (x, p) => CanOpenFile.WriteXdd(x, p),
            WriteFileOptions = (x, p, o) => CanOpenFile.WriteXdd(x, p, o),
            WriteStream = (x, s) => CanOpenFile.WriteXdd(x, s),
            WriteStreamOptions = (x, s, o) => CanOpenFile.WriteXdd(x, s, o),
            WriteFileAsyncToken = (x, p, t) => CanOpenFile.WriteXddAsync(x, p, t),
            WriteFileAsyncOptions = (x, p, o, t) => CanOpenFile.WriteXddAsync(x, p, o, t),
            WriteStreamAsyncToken = (x, s, t) => CanOpenFile.WriteXddAsync(x, s, t),
            WriteStreamAsyncOptions = (x, s, o, t) => CanOpenFile.WriteXddAsync(x, s, o, t),
            WriteToString = x => CanOpenFile.WriteXddToString(x),
            WriteToStringOptions = (x, o) => CanOpenFile.WriteXddToString(x, o),
        };

        var xdc = new FacadeFormat<DeviceConfigurationFile>("Xdc", CanOpenFile.Xdc, ".xdc", () => File.ReadAllText("Fixtures/minimal.xdc"))
        {
            ReadFileMax = (p, m) => CanOpenFile.ReadXdc(p, m),
            ReadFileOptions = (p, o) => CanOpenFile.ReadXdc(p, o),
            ReadFileAsyncToken = (p, t) => CanOpenFile.ReadXdcAsync(p, t),
            ReadFileAsyncMax = (p, m, t) => CanOpenFile.ReadXdcAsync(p, m, t),
            ReadFileAsyncOptions = (p, o, t) => CanOpenFile.ReadXdcAsync(p, o, t),
            ReadStringMax = (c, m) => CanOpenFile.ReadXdcFromString(c, m),
            ReadStringOptions = (c, o) => CanOpenFile.ReadXdcFromString(c, o),
            ReadStreamMax = (s, m) => CanOpenFile.ReadXdc(s, m),
            ReadStreamOptions = (s, o) => CanOpenFile.ReadXdc(s, o),
            ReadStreamAsyncToken = (s, t) => CanOpenFile.ReadXdcAsync(s, t),
            ReadStreamAsyncMax = (s, m, t) => CanOpenFile.ReadXdcAsync(s, m, t),
            ReadStreamAsyncOptions = (s, o, t) => CanOpenFile.ReadXdcAsync(s, o, t),
            WriteFile = (x, p) => CanOpenFile.WriteXdc(x, p),
            WriteFileOptions = (x, p, o) => CanOpenFile.WriteXdc(x, p, o),
            WriteStream = (x, s) => CanOpenFile.WriteXdc(x, s),
            WriteStreamOptions = (x, s, o) => CanOpenFile.WriteXdc(x, s, o),
            WriteFileAsyncToken = (x, p, t) => CanOpenFile.WriteXdcAsync(x, p, t),
            WriteFileAsyncOptions = (x, p, o, t) => CanOpenFile.WriteXdcAsync(x, p, o, t),
            WriteStreamAsyncToken = (x, s, t) => CanOpenFile.WriteXdcAsync(x, s, t),
            WriteStreamAsyncOptions = (x, s, o, t) => CanOpenFile.WriteXdcAsync(x, s, o, t),
            WriteToString = x => CanOpenFile.WriteXdcToString(x),
            WriteToStringOptions = (x, o) => CanOpenFile.WriteXdcToString(x, o),
        };

        return new IFacadeFormat[] { eds, dcf, cpj, xdd, xdc }.ToDictionary(f => f.Token, StringComparer.Ordinal);
    }

    private interface IFacadeFormat
    {
        string Token { get; }

        IReadOnlyCollection<string> Shapes { get; }

        Task RunAsync(string shape);
    }

    /// <summary>
    /// The 22 obsolete overloads every format has, keyed by their shape
    /// (method name without the format token, model type shown as <c>Model</c>).
    /// </summary>
    private sealed class FacadeFormat<TModel> : IFacadeFormat
        where TModel : class
    {
        private readonly FormatCanOpenOperations<TModel> _canonical;
        private readonly string _extension;
        private readonly Func<string> _content;
        private Dictionary<string, Func<Task>>? _cases;

        internal FacadeFormat(string token, FormatCanOpenOperations<TModel> canonical, string extension, Func<string> content)
        {
            Token = token;
            _canonical = canonical;
            _extension = extension;
            _content = content;
        }

        public string Token { get; }

        public IReadOnlyCollection<string> Shapes => Cases.Keys;

        // Built on first use: the delegates are init-only properties set after the constructor.
        private Dictionary<string, Func<Task>> Cases => _cases ??= BuildCases();

        private Dictionary<string, Func<Task>> BuildCases() =>
            new(StringComparer.Ordinal)
            {
                ["Read(String,Int64)"] = () => ReadPathAsync(
                    (p, m) => Task.FromResult(ReadFileMax(p, m)),
                    (p, m) => Task.FromResult(_canonical.ReadFile(p, Limit(m)))),
                ["Read(String,CanOpenFileOptions)"] = () => ReadPathAsync(
                    (p, m) => Task.FromResult(ReadFileOptions(p, Limit(m))),
                    (p, m) => Task.FromResult(_canonical.ReadFile(p, Limit(m)))),
                ["ReadAsync(String,CancellationToken)"] = () => ReadPathWithTokenAsync(
                    (p, t) => ReadFileAsyncToken(p, t),
                    (p, t) => _canonical.ReadFileAsync(p, cancellationToken: t)),
                ["ReadAsync(String,Int64,CancellationToken)"] = () => ReadPathAsync(
                    (p, m) => ReadFileAsyncMax(p, m, CancellationToken.None),
                    (p, m) => _canonical.ReadFileAsync(p, Limit(m), CancellationToken.None),
                    (p, t) => ReadFileAsyncMax(p, IniParser.DefaultMaxInputSize, t),
                    (p, t) => _canonical.ReadFileAsync(p, Limit(IniParser.DefaultMaxInputSize), t)),
                ["ReadAsync(String,CanOpenFileOptions,CancellationToken)"] = () => ReadPathAsync(
                    (p, m) => ReadFileAsyncOptions(p, Limit(m), CancellationToken.None),
                    (p, m) => _canonical.ReadFileAsync(p, Limit(m), CancellationToken.None),
                    (p, t) => ReadFileAsyncOptions(p, CanOpenFileOptions.Default, t),
                    (p, t) => _canonical.ReadFileAsync(p, CanOpenFileOptions.Default, t)),
                ["ReadFromString(String,Int64)"] = () => ReadContentAsync(
                    (c, m) => ReadStringMax(c, m),
                    (c, m) => _canonical.ReadString(c, Limit(m))),
                ["ReadFromString(String,CanOpenFileOptions)"] = () => ReadContentAsync(
                    (c, m) => ReadStringOptions(c, Limit(m)),
                    (c, m) => _canonical.ReadString(c, Limit(m))),
                ["Read(Stream,Int64)"] = () => ReadStreamAsync(
                    (s, m) => Task.FromResult(ReadStreamMax(s, m)),
                    (s, m) => Task.FromResult(_canonical.ReadStream(s, Limit(m)))),
                ["Read(Stream,CanOpenFileOptions)"] = () => ReadStreamAsync(
                    (s, m) => Task.FromResult(ReadStreamOptions(s, Limit(m))),
                    (s, m) => Task.FromResult(_canonical.ReadStream(s, Limit(m)))),
                ["ReadAsync(Stream,CancellationToken)"] = () => ReadStreamWithTokenAsync(
                    (s, t) => ReadStreamAsyncToken(s, t),
                    (s, t) => _canonical.ReadStreamAsync(s, cancellationToken: t)),
                ["ReadAsync(Stream,Int64,CancellationToken)"] = () => ReadStreamAsync(
                    (s, m) => ReadStreamAsyncMax(s, m, CancellationToken.None),
                    (s, m) => _canonical.ReadStreamAsync(s, Limit(m), CancellationToken.None),
                    (s, t) => ReadStreamAsyncMax(s, IniParser.DefaultMaxInputSize, t),
                    (s, t) => _canonical.ReadStreamAsync(s, Limit(IniParser.DefaultMaxInputSize), t)),
                ["ReadAsync(Stream,CanOpenFileOptions,CancellationToken)"] = () => ReadStreamAsync(
                    (s, m) => ReadStreamAsyncOptions(s, Limit(m), CancellationToken.None),
                    (s, m) => _canonical.ReadStreamAsync(s, Limit(m), CancellationToken.None),
                    (s, t) => ReadStreamAsyncOptions(s, CanOpenFileOptions.Default, t),
                    (s, t) => _canonical.ReadStreamAsync(s, CanOpenFileOptions.Default, t)),
                ["Write(Model,String)"] = () => WriteFileAsync(
                    (x, p, _, _) => { WriteFile(x, p); return Task.CompletedTask; },
                    (x, p, _, _) => { _canonical.WriteFile(x, p); return Task.CompletedTask; },
                    usesOptions: false,
                    usesToken: false),
                ["Write(Model,String,CanOpenWriteOptions)"] = () => WriteFileAsync(
                    (x, p, o, _) => { WriteFileOptions(x, p, o); return Task.CompletedTask; },
                    (x, p, o, _) => { _canonical.WriteFile(x, p, o); return Task.CompletedTask; },
                    usesOptions: true,
                    usesToken: false),
                ["Write(Model,Stream)"] = () => WriteStreamAsync(
                    (x, s, _, _) => { WriteStream(x, s); return Task.CompletedTask; },
                    (x, s, _, _) => { _canonical.WriteStream(x, s); return Task.CompletedTask; },
                    usesOptions: false,
                    usesToken: false),
                ["Write(Model,Stream,CanOpenWriteOptions)"] = () => WriteStreamAsync(
                    (x, s, o, _) => { WriteStreamOptions(x, s, o); return Task.CompletedTask; },
                    (x, s, o, _) => { _canonical.WriteStream(x, s, o); return Task.CompletedTask; },
                    usesOptions: true,
                    usesToken: false),
                ["WriteAsync(Model,String,CancellationToken)"] = () => WriteFileAsync(
                    (x, p, _, t) => WriteFileAsyncToken(x, p, t),
                    (x, p, _, t) => _canonical.WriteFileAsync(x, p, t),
                    usesOptions: false,
                    usesToken: true),
                ["WriteAsync(Model,String,CanOpenWriteOptions,CancellationToken)"] = () => WriteFileAsync(
                    (x, p, o, t) => WriteFileAsyncOptions(x, p, o, t),
                    (x, p, o, t) => _canonical.WriteFileAsync(x, p, o, t),
                    usesOptions: true,
                    usesToken: true),
                ["WriteAsync(Model,Stream,CancellationToken)"] = () => WriteStreamAsync(
                    (x, s, _, t) => WriteStreamAsyncToken(x, s, t),
                    (x, s, _, t) => _canonical.WriteStreamAsync(x, s, t),
                    usesOptions: false,
                    usesToken: true),
                ["WriteAsync(Model,Stream,CanOpenWriteOptions,CancellationToken)"] = () => WriteStreamAsync(
                    (x, s, o, t) => WriteStreamAsyncOptions(x, s, o, t),
                    (x, s, o, t) => _canonical.WriteStreamAsync(x, s, o, t),
                    usesOptions: true,
                    usesToken: true),
                ["WriteToString(Model)"] = () => WriteTextAsync(
                    (x, _) => WriteToString(x),
                    (x, _) => _canonical.WriteToString(x),
                    usesOptions: false),
                ["WriteToString(Model,CanOpenWriteOptions)"] = () => WriteTextAsync(
                    (x, o) => WriteToStringOptions(x, o),
                    (x, o) => _canonical.WriteToString(x, o),
                    usesOptions: true),
            };

        public required Func<string, long, TModel> ReadFileMax { get; init; }
        public required Func<string, CanOpenFileOptions, TModel> ReadFileOptions { get; init; }
        public required Func<string, CancellationToken, Task<TModel>> ReadFileAsyncToken { get; init; }
        public required Func<string, long, CancellationToken, Task<TModel>> ReadFileAsyncMax { get; init; }
        public required Func<string, CanOpenFileOptions, CancellationToken, Task<TModel>> ReadFileAsyncOptions { get; init; }
        public required Func<string, long, TModel> ReadStringMax { get; init; }
        public required Func<string, CanOpenFileOptions, TModel> ReadStringOptions { get; init; }
        public required Func<Stream, long, TModel> ReadStreamMax { get; init; }
        public required Func<Stream, CanOpenFileOptions, TModel> ReadStreamOptions { get; init; }
        public required Func<Stream, CancellationToken, Task<TModel>> ReadStreamAsyncToken { get; init; }
        public required Func<Stream, long, CancellationToken, Task<TModel>> ReadStreamAsyncMax { get; init; }
        public required Func<Stream, CanOpenFileOptions, CancellationToken, Task<TModel>> ReadStreamAsyncOptions { get; init; }
        public required Action<TModel, string> WriteFile { get; init; }
        public required Action<TModel, string, CanOpenWriteOptions?> WriteFileOptions { get; init; }
        public required Action<TModel, Stream> WriteStream { get; init; }
        public required Action<TModel, Stream, CanOpenWriteOptions?> WriteStreamOptions { get; init; }
        public required Func<TModel, string, CancellationToken, Task> WriteFileAsyncToken { get; init; }
        public required Func<TModel, string, CanOpenWriteOptions?, CancellationToken, Task> WriteFileAsyncOptions { get; init; }
        public required Func<TModel, Stream, CancellationToken, Task> WriteStreamAsyncToken { get; init; }
        public required Func<TModel, Stream, CanOpenWriteOptions?, CancellationToken, Task> WriteStreamAsyncOptions { get; init; }
        public required Func<TModel, string> WriteToString { get; init; }
        public required Func<TModel, CanOpenWriteOptions?, string> WriteToStringOptions { get; init; }

        public Task RunAsync(string shape) => Cases[shape]();

        private static CanOpenFileOptions Limit(long maxInputSize) => new() { MaxInputSize = maxInputSize };

        // The opposite of the platform default, so the output shows whether the options arrived.
        private static CanOpenWriteOptions NonDefaultNewLine() =>
            new() { NewLine = Environment.NewLine == "\n" ? "\r\n" : "\n" };

        private TModel Sample() => _canonical.ReadString(_content());

        private string Serialize(TModel model) => _canonical.WriteToString(model);

        private async Task ReadPathAsync(
            Func<string, long, Task<TModel>> legacy,
            Func<string, long, Task<TModel>> canonical,
            Func<string, CancellationToken, Task<TModel>>? legacyWithToken = null,
            Func<string, CancellationToken, Task<TModel>>? canonicalWithToken = null)
        {
            using var file = new TempFile(_extension, _content());

            await ExpectSameAsync(
                () => legacy(file.Path, IniParser.DefaultMaxInputSize),
                () => canonical(file.Path, IniParser.DefaultMaxInputSize),
                expectFailure: false);
            await ExpectSameAsync(
                () => legacy(file.Path, TooSmallMaxInputSize),
                () => canonical(file.Path, TooSmallMaxInputSize),
                expectFailure: true);

            if (legacyWithToken is not null && canonicalWithToken is not null)
                await ReadPathWithTokenAsync(legacyWithToken, canonicalWithToken);
        }

        private async Task ReadPathWithTokenAsync(
            Func<string, CancellationToken, Task<TModel>> legacy,
            Func<string, CancellationToken, Task<TModel>> canonical)
        {
            using var file = new TempFile(_extension, _content());
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();

            await ExpectSameAsync(
                () => legacy(file.Path, CancellationToken.None),
                () => canonical(file.Path, CancellationToken.None),
                expectFailure: false);
            await ExpectSameAsync(
                () => legacy(file.Path, canceled.Token),
                () => canonical(file.Path, canceled.Token),
                expectFailure: true);
        }

        private async Task ReadContentAsync(
            Func<string, long, TModel> legacy,
            Func<string, long, TModel> canonical)
        {
            var content = _content();

            await ExpectSameAsync(
                () => Task.FromResult(legacy(content, IniParser.DefaultMaxInputSize)),
                () => Task.FromResult(canonical(content, IniParser.DefaultMaxInputSize)),
                expectFailure: false);
            await ExpectSameAsync(
                () => Task.FromResult(legacy(content, TooSmallMaxInputSize)),
                () => Task.FromResult(canonical(content, TooSmallMaxInputSize)),
                expectFailure: true);
        }

        private async Task ReadStreamAsync(
            Func<Stream, long, Task<TModel>> legacy,
            Func<Stream, long, Task<TModel>> canonical,
            Func<Stream, CancellationToken, Task<TModel>>? legacyWithToken = null,
            Func<Stream, CancellationToken, Task<TModel>>? canonicalWithToken = null)
        {
            var bytes = Encoding.UTF8.GetBytes(_content());

            await ExpectSameAsync(
                () => legacy(new MemoryStream(bytes), IniParser.DefaultMaxInputSize),
                () => canonical(new MemoryStream(bytes), IniParser.DefaultMaxInputSize),
                expectFailure: false);
            await ExpectSameAsync(
                () => legacy(new MemoryStream(bytes), TooSmallMaxInputSize),
                () => canonical(new MemoryStream(bytes), TooSmallMaxInputSize),
                expectFailure: true);

            if (legacyWithToken is not null && canonicalWithToken is not null)
                await ReadStreamWithTokenAsync(legacyWithToken, canonicalWithToken);
        }

        private async Task ReadStreamWithTokenAsync(
            Func<Stream, CancellationToken, Task<TModel>> legacy,
            Func<Stream, CancellationToken, Task<TModel>> canonical)
        {
            var bytes = Encoding.UTF8.GetBytes(_content());
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();

            await ExpectSameAsync(
                () => legacy(new MemoryStream(bytes), CancellationToken.None),
                () => canonical(new MemoryStream(bytes), CancellationToken.None),
                expectFailure: false);
            await ExpectSameAsync(
                () => legacy(new MemoryStream(bytes), canceled.Token),
                () => canonical(new MemoryStream(bytes), canceled.Token),
                expectFailure: true);
        }

        private async Task ExpectSameAsync(Func<Task<TModel>> legacy, Func<Task<TModel>> canonical, bool expectFailure)
        {
            var legacyOutcome = await OutcomeAsync(async () => Serialize(await legacy()));
            var canonicalOutcome = await OutcomeAsync(async () => Serialize(await canonical()));

            legacyOutcome.Should().Be(canonicalOutcome);
            legacyOutcome.StartsWith("throw:", StringComparison.Ordinal).Should().Be(expectFailure, legacyOutcome);
        }

        private async Task WriteFileAsync(
            Func<TModel, string, CanOpenWriteOptions?, CancellationToken, Task> legacy,
            Func<TModel, string, CanOpenWriteOptions?, CancellationToken, Task> canonical,
            bool usesOptions,
            bool usesToken)
        {
            async Task<string> RunAsync(
                Func<TModel, string, CanOpenWriteOptions?, CancellationToken, Task> write,
                CanOpenWriteOptions? options,
                CancellationToken cancellationToken)
            {
                using var file = new TempFile(_extension, string.Empty);
                await write(Sample(), file.Path, options, cancellationToken);
                return Convert.ToBase64String(File.ReadAllBytes(file.Path));
            }

            await ExpectSameWriteAsync(
                (o, t) => RunAsync(legacy, o, t),
                (o, t) => RunAsync(canonical, o, t),
                usesOptions,
                usesToken);
        }

        private async Task WriteStreamAsync(
            Func<TModel, Stream, CanOpenWriteOptions?, CancellationToken, Task> legacy,
            Func<TModel, Stream, CanOpenWriteOptions?, CancellationToken, Task> canonical,
            bool usesOptions,
            bool usesToken)
        {
            async Task<string> RunAsync(
                Func<TModel, Stream, CanOpenWriteOptions?, CancellationToken, Task> write,
                CanOpenWriteOptions? options,
                CancellationToken cancellationToken)
            {
                using var stream = new MemoryStream();
                await write(Sample(), stream, options, cancellationToken);
                return Convert.ToBase64String(stream.ToArray());
            }

            await ExpectSameWriteAsync(
                (o, t) => RunAsync(legacy, o, t),
                (o, t) => RunAsync(canonical, o, t),
                usesOptions,
                usesToken);
        }

        private Task WriteTextAsync(
            Func<TModel, CanOpenWriteOptions?, string> legacy,
            Func<TModel, CanOpenWriteOptions?, string> canonical,
            bool usesOptions)
            => ExpectSameWriteAsync(
                (o, _) => Task.FromResult(legacy(Sample(), o)),
                (o, _) => Task.FromResult(canonical(Sample(), o)),
                usesOptions,
                usesToken: false);

        private static async Task ExpectSameWriteAsync(
            Func<CanOpenWriteOptions?, CancellationToken, Task<string>> legacy,
            Func<CanOpenWriteOptions?, CancellationToken, Task<string>> canonical,
            bool usesOptions,
            bool usesToken)
        {
            var defaultOutcome = await OutcomeAsync(() => legacy(null, CancellationToken.None));
            defaultOutcome.Should().Be(await OutcomeAsync(() => canonical(null, CancellationToken.None)));
            defaultOutcome.Should().StartWith("ok:");

            if (usesOptions)
            {
                var options = NonDefaultNewLine();
                var optionsOutcome = await OutcomeAsync(() => legacy(options, CancellationToken.None));
                optionsOutcome.Should().Be(await OutcomeAsync(() => canonical(options, CancellationToken.None)));
                optionsOutcome.Should().StartWith("ok:").And.NotBe(defaultOutcome);
            }

            if (usesToken)
            {
                using var canceled = new CancellationTokenSource();
                canceled.Cancel();
                var canceledOutcome = await OutcomeAsync(() => legacy(null, canceled.Token));
                canceledOutcome.Should().Be(await OutcomeAsync(() => canonical(null, canceled.Token)));
                canceledOutcome.Should().StartWith("throw:");
            }
        }

        private static async Task<string> OutcomeAsync(Func<Task<string>> action)
        {
            try
            {
                return "ok:" + await action();
            }
            catch (Exception ex)
            {
                return "throw:" + ex.GetType().FullName;
            }
        }
    }

    private sealed class TempFile : IDisposable
    {
        internal TempFile(string extension, string content)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
            File.WriteAllText(Path, content);
        }

        internal string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
}
