namespace EdsDcfNet.Benchmarks;

using BenchmarkDotNet.Attributes;
using EdsDcfNet.Models;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WriterBenchmarks
{
    private ElectronicDataSheet _eds = null!;
    private DeviceConfigurationFile _dcf = null!;
    private NodelistProject _cpj = null!;

    [GlobalSetup]
    public void Setup()
    {
        _eds = CanOpenFile.Eds.ReadFile(GetFixturePath("sample_device.eds"));
        _dcf = CanOpenFile.Dcf.ReadFile(GetFixturePath("minimal.dcf"));
        _cpj = CanOpenFile.Cpj.ReadFile(GetFixturePath("minimal.cpj"));
    }

    [Benchmark(Baseline = true, Description = "EDS write (string)")]
    public string EdsWriteToString() => CanOpenFile.Eds.WriteToString(_eds);

    [Benchmark(Description = "DCF write (string)")]
    public string DcfWriteToString() => CanOpenFile.Dcf.WriteToString(_dcf);

    [Benchmark(Description = "CPJ write (string)")]
    public string CpjWriteToString() => CanOpenFile.Cpj.WriteToString(_cpj);

    [Benchmark(Description = "XDD write (string)")]
    public string XddWriteToString() => CanOpenFile.Xdd.WriteToString(_eds);

    private static string GetFixturePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
    }
}
