namespace EdsDcfNet.Tests.Writers;

using System.Text;
using EdsDcfNet.Writers;

/// <summary>
/// Collision handling scans a generated document in 4096-character blocks and
/// retains only header lines. It must not materialize a second copy of a large
/// document merely to find <c>AdditionalSections</c> collisions.
/// </summary>
public class GeneratedSectionHeaderStreamingTests
{
    private sealed class HeaderProbeWriter : IniWriterBase
    {
        public static HashSet<string> Collect(StringBuilder output) => GetGeneratedSectionHeaders(output);
    }

    [Fact]
    public void GetGeneratedSectionHeaders_LargeDocument_KeepsOnlyHeadersAcrossBlockBoundaries()
    {
        // Arrange — long value lines force headers away from block boundaries and
        // a bracketed value must not be mistaken for a header.
        var output = new StringBuilder(capacity: 11 * 1024 * 1024);
        var value = new string('x', 1020);
        for (var i = 0; i < 10_000; i++)
        {
            output.Append("Key").Append(i).Append('=').Append(value).AppendLine();
        }

        output.AppendLine("Value=[NotAHeader]");
        output.AppendLine("[VendorHeader]");
        output.AppendLine("Key=value");
        output.AppendLine("[LastHeader]");

        // Act
        var headers = HeaderProbeWriter.Collect(output);

        // Assert
        headers.Should().BeEquivalentTo("[VendorHeader]", "[LastHeader]");
        IniWriterBase.IsGeneratedSection(headers, "vendorheader").Should().BeTrue();
        IniWriterBase.IsGeneratedSection(headers, "LastHeader").Should().BeTrue();
        IniWriterBase.IsGeneratedSection(headers, "NotAHeader").Should().BeFalse();
    }
}
