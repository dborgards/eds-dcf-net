namespace EdsDcfNet.Benchmarks;

using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class RoundTripBenchmarks
{
    private string _edsContent = null!;
    private string _dcfContent = null!;
    private string _cpjContent = null!;

    [GlobalSetup]
    public void Setup()
    {
        _edsContent = File.ReadAllText("Fixtures/sample_device.eds");
        _dcfContent = File.ReadAllText("Fixtures/minimal.dcf");
        _cpjContent = File.ReadAllText("Fixtures/minimal.cpj");
    }

    [Benchmark(Baseline = true, Description = "EDS round-trip (parse + write)")]
    public string EdsRoundTrip()
    {
        var eds = CanOpenFile.ReadEdsFromString(_edsContent);
        return CanOpenFile.WriteEdsToString(eds);
    }

    [Benchmark(Description = "DCF round-trip (parse + write)")]
    public string DcfRoundTrip()
    {
        var dcf = CanOpenFile.ReadDcfFromString(_dcfContent);
        return CanOpenFile.WriteDcfToString(dcf);
    }

    [Benchmark(Description = "CPJ round-trip (parse + write)")]
    public string CpjRoundTrip()
    {
        var cpj = CanOpenFile.ReadCpjFromString(_cpjContent);
        return CanOpenFile.WriteCpjToString(cpj);
    }

    [Benchmark(Description = "EDS → DCF conversion")]
    public object EdsToDcfConversion()
    {
        var eds = CanOpenFile.ReadEdsFromString(_edsContent);
        return CanOpenFile.EdsToDcf(eds, nodeId: 1, baudrate: 250);
    }
}
