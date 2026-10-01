namespace EdsDcfNet.Tests.Parsers;

using System.Text;
using AwesomeAssertions;
using EdsDcfNet.Diagnostics;
using EdsDcfNet.Exceptions;
using EdsDcfNet.Parsers;
using Xunit;

/// <summary>
/// WP-07 (L4/L5): malformed section headers, lines without '=', duplicate section headers
/// and string-vs-stream line-number parity in <see cref="IniParser"/>.
/// </summary>
public class IniParserMalformedHeaderTests
{
    private static (Dictionary<string, Dictionary<string, string>> Sections, IReadOnlyList<ParseDiagnostic> Diagnostics)
        Lenient(string content)
    {
        using var scope = ParseDiagnosticScope.Enter();
        var sections = IniParser.ParseString(content);
        return (sections, scope.Diagnostics);
    }

    private static IReadOnlyList<ParseDiagnostic> LenientStream(string content)
    {
        using var scope = ParseDiagnosticScope.Enter();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        IniParser.ParseStream(stream);
        return scope.Diagnostics;
    }

    private static EdsParseException Strict(string content)
    {
        var act = () => IniParser.ParseString(content, IniParser.DefaultMaxInputSize, strictParsing: true);
        return act.Should().Throw<EdsParseException>().Which;
    }

    // --- Trailing comment on a section header ------------------------------

    [Theory]
    [InlineData("[2000] ; comment")]
    [InlineData("[2000]; comment")]
    [InlineData("  [2000]   ;comment  ")]
    public void ParseString_HeaderWithTrailingComment_AcceptsHeaderInStrictAndLenient(string header)
    {
        var content = header + "\nParameterName=X\n";

        var (lenient, diagnostics) = Lenient(content);
        var strict = IniParser.ParseString(content, IniParser.DefaultMaxInputSize, strictParsing: true);

        lenient.Should().ContainKey("2000");
        lenient["2000"]["ParameterName"].Should().Be("X");
        diagnostics.Should().BeEmpty();
        strict["2000"]["ParameterName"].Should().Be("X");
    }

    [Fact]
    public void ParseString_HeaderWithTrailingComment_DoesNotChangeValueCommentSemantics()
    {
        var (sections, _) = Lenient("[S] ; c\nKey=a ; b\n");

        sections["S"]["Key"].Should().Be("a ; b");
    }

    // --- Unterminated / garbage-after-header --------------------------------

    [Fact]
    public void ParseString_UnterminatedHeader_Lenient_ReportsDiagnosticAndDoesNotLeakKeysIntoPreviousSection()
    {
        var content = "[Good]\nA=1\n[2000\nB=2\n[Next]\nC=3\n";

        var (sections, diagnostics) = Lenient(content);

        sections["Good"].Should().ContainKey("A").And.NotContainKey("B");
        sections.Keys.Should().BeEquivalentTo(new[] { "Good", "Next" });
        sections["Next"]["C"].Should().Be("3");
        var d = diagnostics.Should().ContainSingle().Subject;
        d.Code.Should().Be(ParseDiagnosticCodes.IniMalformedSectionHeader);
        d.Line.Should().Be(3);
        d.Severity.Should().Be(ParseSeverity.Warning);
    }

    [Fact]
    public void ParseString_UnterminatedHeader_Strict_ThrowsWithCodeAndLine()
    {
        var ex = Strict("[Good]\nA=1\n[2000\nB=2\n");

        ex.Code.Should().Be(ParseDiagnosticCodes.IniMalformedSectionHeader);
        ex.LineNumber.Should().Be(3);
    }

    [Fact]
    public void ParseString_HeaderFollowedByGarbage_Lenient_ReportsDiagnosticAndDiscardsKeys()
    {
        var (sections, diagnostics) = Lenient("[Good]\nA=1\n[2000] junk\nB=2\n");

        sections.Keys.Should().BeEquivalentTo(new[] { "Good" });
        sections["Good"].Should().NotContainKey("B");
        diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniMalformedSectionHeader && d.Line == 3);
    }

    [Fact]
    public void ParseString_HeaderFollowedByGarbage_Strict_Throws()
    {
        Strict("[Good]\nA=1\n[2000] junk\nB=2\n")
            .Code.Should().Be(ParseDiagnosticCodes.IniMalformedSectionHeader);
    }

    [Fact]
    public void ParseString_UnterminatedHeaderBeforeAnySection_Lenient_DiscardsKeysInsteadOfThrowing()
    {
        var (sections, diagnostics) = Lenient("[2000\nB=2\n");

        sections.Should().BeEmpty();
        diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniMalformedSectionHeader);
    }

    // --- Line without '=' ------------------------------------------------------

    [Fact]
    public void ParseString_LineWithoutEquals_Lenient_ReportsDiagnosticAndKeepsSection()
    {
        var (sections, diagnostics) = Lenient("[S]\nA=1\nnot a key value pair\nB=2\n");

        sections["S"].Keys.Should().BeEquivalentTo(new[] { "A", "B" });
        var d = diagnostics.Should().ContainSingle().Subject;
        d.Code.Should().Be(ParseDiagnosticCodes.IniMissingEquals);
        d.Line.Should().Be(3);
        d.Path.Should().Be("S");
    }

    [Fact]
    public void ParseString_LineWithoutEquals_Strict_Throws()
    {
        var ex = Strict("[S]\nA=1\nnot a key value pair\n");

        ex.Code.Should().Be(ParseDiagnosticCodes.IniMissingEquals);
        ex.LineNumber.Should().Be(3);
        ex.SectionName.Should().Be("S");
    }

    [Fact]
    public void ParseString_LineWithEmptyKey_Lenient_ReportsDiagnostic()
    {
        var (sections, diagnostics) = Lenient("[S]\n=value\nA=1\n");

        sections["S"].Keys.Should().BeEquivalentTo(new[] { "A" });
        diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniMissingEquals && d.Line == 2);
    }

    [Fact]
    public void ParseString_LineWithEmptyKey_Strict_Throws()
    {
        Strict("[S]\n=value\n").Code.Should().Be(ParseDiagnosticCodes.IniMissingEquals);
    }

    [Fact]
    public void ParseString_LineWithoutEqualsBeforeAnySection_Lenient_ReportsDiagnostic()
    {
        var (sections, diagnostics) = Lenient("garbage\n[S]\nA=1\n");

        sections["S"]["A"].Should().Be("1");
        diagnostics.Should().ContainSingle(d => d.Code == ParseDiagnosticCodes.IniMissingEquals && d.Line == 1);
    }

    [Fact]
    public void ParseString_LineWithoutEqualsBeforeAnySection_Strict_Throws()
    {
        Strict("garbage\n[S]\nA=1\n").Code.Should().Be(ParseDiagnosticCodes.IniMissingEquals);
    }

    // --- Duplicate section header ----------------------------------------------

    [Fact]
    public void ParseString_DuplicateSection_Lenient_MergesAndReports()
    {
        var (sections, diagnostics) = Lenient("[S]\nA=1\n[T]\nX=1\n[s]\nB=2\n");

        sections["S"].Keys.Should().BeEquivalentTo(new[] { "A", "B" });
        var d = diagnostics.Should().ContainSingle().Subject;
        d.Code.Should().Be(ParseDiagnosticCodes.IniDuplicateSection);
        d.Line.Should().Be(5);
        d.Path.Should().Be("s");
    }

    [Fact]
    public void ParseString_DuplicateSection_Strict_Throws()
    {
        var ex = Strict("[S]\nA=1\n[S]\nB=2\n");

        ex.Code.Should().Be(ParseDiagnosticCodes.IniDuplicateSection);
        ex.LineNumber.Should().Be(3);
        ex.SectionName.Should().Be("S");
    }

    // --- String / stream line-number parity --------------------------------------

    [Theory]
    [InlineData("\n\n[S]\n\nA=1\n\nA=2\n")]
    [InlineData("\r\n\r\n[S]\r\n\r\nA=1\r\n\r\nA=2\r\n")]
    [InlineData("\r\r[S]\r\rA=1\r\rA=2\r")]
    [InlineData("\n\r[S]\n\rA=1\n\rA=2")]
    public void ParseString_BlankLines_ReportsSameLineNumbersAsStream(string content)
    {
        var fromString = Lenient(content).Diagnostics;
        var fromStream = LenientStream(content);

        fromString.Should().ContainSingle();
        fromStream.Should().ContainSingle();
        fromString[0].Line.Should().Be(fromStream[0].Line);
    }

    [Fact]
    public void ParseString_BlankLinesBeforeDuplicateKey_StrictLineNumberMatchesStream()
    {
        var content = "\n\n[S]\n\nA=1\n\nA=2\n";

        var fromString = Strict(content);
        var act = () =>
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            IniParser.ParseStream(stream, IniParser.DefaultMaxInputSize, strictParsing: true);
        };

        fromString.LineNumber.Should().Be(7);
        act.Should().Throw<EdsParseException>().Which.LineNumber.Should().Be(7);
    }

    [Fact]
    public void ParseString_KeyOutsideSection_AfterBlankLines_ReportsPhysicalLine()
    {
        var act = () => IniParser.ParseString("\n\nA=1\n");

        act.Should().Throw<EdsParseException>().Which.LineNumber.Should().Be(3);
    }

    [Fact]
    public void ParseString_MalformedHeaderAfterBlankLines_StreamAndStringAgree()
    {
        var content = "\n\n[S]\n\n[2000\n";

        Lenient(content).Diagnostics.Single().Line.Should().Be(5);
        LenientStream(content).Single().Line.Should().Be(5);
    }
}
