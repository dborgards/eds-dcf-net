namespace EdsDcfNet.Benchmarks;

using BenchmarkDotNet.Attributes;
using EdsDcfNet.Models;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class WriterBenchmarks
{
    private ElectronicDataSheet _eds = null!;
    private DeviceConfigurationFile _dcf = null!;

    [GlobalSetup]
    public void Setup()
    {
        _eds = CanOpenFile.ReadEds("Fixtures/sample_device.eds");
        _dcf = CanOpenFile.ReadDcf("Fixtures/minimal.dcf");
    }

    [Benchmark(Description = "EDS write (string)")]
    public string EdsWriteToString() => CanOpenFile.WriteEdsToString(_eds);

    [Benchmark(Description = "DCF write (string)")]
    public string DcfWriteToString() => CanOpenFile.WriteDcfToString(_dcf);

    [Benchmark(Description = "XDD write (string)")]
    public string XddWriteToString() => CanOpenFile.WriteXddToString(_eds);
}
