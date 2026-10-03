namespace EdsDcfNet.Tests.Writers;

using System.Text;
using EdsDcfNet.Models;
using EdsDcfNet.Writers;

/// <summary>
/// The <c>protected virtual</c> compact-list hook of <see cref="IniWriterBase"/> is part of the
/// public extension contract: a subclass that overrides it must still be called when it writes
/// a compact object.
/// </summary>
public class CompactListOverrideDispatchTests
{
    [Fact]
    public void WriteObject_CompactObject_CallsProtectedCompactListOverride()
    {
        // Arrange
        var writer = new RecordingDcfWriter();
        var obj = new CanOpenObject
        {
            Index = 0x2000,
            ParameterName = "Compact",
            ObjectType = 0x8,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "0",
            CompactSubObj = 1
        };
        obj.SubObjects[1] = new CanOpenSubObject
        {
            SubIndex = 1,
            ParameterName = "Compact1",
            ObjectType = 0x7,
            DataType = 0x0007,
            AccessType = AccessType.ReadWrite,
            DefaultValue = "0",
            ParameterValue = "5"
        };

        // Act
        var text = writer.Write(obj);

        // Assert
        writer.Calls.Should().Be(1);
        text.Should().Contain("[2000Value]", "the base DcfWriter behaviour still runs");
        text.Should().Contain("; custom compact list");
    }

    private sealed class RecordingDcfWriter : DcfWriter
    {
        public int Calls { get; private set; }

        public string Write(CanOpenObject obj)
        {
            var sb = new StringBuilder();
            WriteObject(sb, obj, static (_, write) => write());
            return sb.ToString();
        }

        protected override void WriteCompactValueAndDenotationSections(
            StringBuilder sb,
            CanOpenObject obj,
            int compactMax,
            HashSet<byte> expandedSubIndexes,
            Action<string, Action> writeSection)
        {
            Calls++;
            base.WriteCompactValueAndDenotationSections(sb, obj, compactMax, expandedSubIndexes, writeSection);
            sb.AppendLine("; custom compact list");
        }
    }
}
