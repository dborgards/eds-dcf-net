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
        _eds = CanOpenFile.ReadEds("Fixtures/sample_device.eds");
        _dcf = CanOpenFile.ReadDcf("Fixtures/minimal.dcf");
        _cpj = CanOpenFile.ReadCpj("Fixtures/minimal.cpj");
    }

    [Benchmark(Baseline = true, Description = "EDS write (string)")]
    public string EdsWriteToString() => CanOpenFile.WriteEdsToString(_eds);

    [Benchmark(Description = "DCF write (string)")]
    public string DcfWriteToString() => CanOpenFile.WriteDcfToString(_dcf);

    [Benchmark(Description = "CPJ write (string)")]
    public string CpjWriteToString() => CanOpenFile.WriteCpjToString(_cpj);

    [Benchmark(Description = "XDD write (string)")]
    public string XddWriteToString() => CanOpenFile.WriteXddToString(_eds);
}
