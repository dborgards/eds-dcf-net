namespace EdsDcfNet.Benchmarks;

using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ParserBenchmarks
{
    private string _edsContent = null!;
    private string _dcfContent = null!;
    private string _xddContent = null!;

    [GlobalSetup]
    public void Setup()
    {
        _edsContent = File.ReadAllText("Fixtures/sample_device.eds");
        _dcfContent = File.ReadAllText("Fixtures/minimal.dcf");
        _xddContent = File.ReadAllText("Fixtures/sample_device.xdd");
    }

    [Benchmark(Description = "EDS parse (string)")]
    public object EdsParseFromString() => CanOpenFile.ReadEdsFromString(_edsContent);

    [Benchmark(Description = "DCF parse (string)")]
    public object DcfParseFromString() => CanOpenFile.ReadDcfFromString(_dcfContent);

    [Benchmark(Description = "XDD parse (string)")]
    public object XddParseFromString() => CanOpenFile.ReadXddFromString(_xddContent);
}
