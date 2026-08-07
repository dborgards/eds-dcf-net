namespace EdsDcfNet.Tests.Infrastructure;

using System.Reflection;
using System.Runtime.Versioning;
using EdsDcfNet;

/// <summary>
/// Proves CI/local multi-TFM test hosts bind the intended library asset
/// (net10.0 vs netstandard2.0 via the net48 host), so #else runtime paths
/// are actually executed.
/// </summary>
public class TargetFrameworkBindingTests
{
    [Fact]
    public void CanOpenFileAssembly_BindsExpectedLibraryTargetFramework()
    {
        var attribute = typeof(CanOpenFile).Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>();

        attribute.Should().NotBeNull();
#if NET10_0_OR_GREATER
        attribute!.FrameworkName.Should().StartWith(".NETCoreApp,Version=v10.");
#else
        // net48 host must bind the library's netstandard2.0 asset.
        attribute!.FrameworkName.Should().Be(".NETStandard,Version=v2.0");
#endif
    }
}
