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
        _edsContent = File.ReadAllText(FixturePaths.Get("sample_device.eds"));
        _dcfContent = File.ReadAllText(FixturePaths.Get("minimal.dcf"));
        _cpjContent = File.ReadAllText(FixturePaths.Get("minimal.cpj"));
    }

    [Benchmark(Baseline = true, Description = "EDS round-trip (parse + write)")]
    public string EdsRoundTrip()
    {
        var eds = CanOpenFile.Eds.ReadString(_edsContent);
        return CanOpenFile.Eds.WriteToString(eds);
    }

    [Benchmark(Description = "DCF round-trip (parse + write)")]
    public string DcfRoundTrip()
    {
        var dcf = CanOpenFile.Dcf.ReadString(_dcfContent);
        return CanOpenFile.Dcf.WriteToString(dcf);
    }

    [Benchmark(Description = "CPJ round-trip (parse + write)")]
    public string CpjRoundTrip()
    {
        var cpj = CanOpenFile.Cpj.ReadString(_cpjContent);
        return CanOpenFile.Cpj.WriteToString(cpj);
    }

    [Benchmark(Description = "EDS → DCF conversion")]
    public object EdsToDcfConversion()
    {
        var eds = CanOpenFile.Eds.ReadString(_edsContent);
        return CanOpenFile.Eds.ConvertToDcf(eds, nodeId: 1, baudrate: 250);
    }
}
