namespace EdsDcfNet.Tests.Exceptions;

using EdsDcfNet.Exceptions;

public class EdsParseExceptionTests
{
    [Fact]
    public void DefaultConstructor_CreatesException()
    {
        // Act
        var ex = new EdsParseException();

        // Assert
        ex.Should().NotBeNull();
        ex.LineNumber.Should().BeNull();
        ex.SectionName.Should().BeNull();
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        // Act
        var ex = new EdsParseException("Parse error occurred");

        // Assert
        ex.Message.Should().Be("Parse error occurred");
        ex.LineNumber.Should().BeNull();
        ex.SectionName.Should().BeNull();
    }

    [Fact]
    public void MessageAndInnerException_BothSet()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var ex = new EdsParseException("Outer error", inner);

        // Assert
        ex.Message.Should().Be("Outer error");
        ex.InnerException.Should().BeSameAs(inner);
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void MessageAndLineNumber_BothSet()
    {
        // Act
        var ex = new EdsParseException("Line error", 42);

        // Assert
        ex.Message.Should().Be("Line error");
        ex.LineNumber.Should().Be(42);
        ex.SectionName.Should().BeNull();
    }

    [Fact]
    public void MessageSectionAndLineNumber_AllSet()
    {
        // Act
        var ex = new EdsParseException("Section error", "DeviceInfo", 99);

        // Assert
        ex.Message.Should().Be("Section error");
        ex.SectionName.Should().Be("DeviceInfo");
        ex.LineNumber.Should().Be(99);
    }

    [Fact]
    public void IsException_InheritsFromException()
    {
        // Act
        var ex = new EdsParseException("test");

        // Assert
        ex.Should().BeAssignableTo<Exception>();
    }
}
