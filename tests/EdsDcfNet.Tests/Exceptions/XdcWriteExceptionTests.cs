namespace EdsDcfNet.Tests.Exceptions;

using EdsDcfNet.Exceptions;

public class XdcWriteExceptionTests
{
    [Fact]
    public void DefaultConstructor_CreatesException()
    {
        // Act
        var ex = new XdcWriteException();

        // Assert
        ex.Should().NotBeNull();
        ex.SectionName.Should().BeNull();
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        // Act
        var ex = new XdcWriteException("Write failed");

        // Assert
        ex.Message.Should().Be("Write failed");
        ex.SectionName.Should().BeNull();
    }

    [Fact]
    public void MessageAndInnerException_BothSet()
    {
        // Arrange
        var inner = new IOException("disk full");

        // Act
        var ex = new XdcWriteException("Write error", inner);

        // Assert
        ex.Message.Should().Be("Write error");
        ex.InnerException.Should().BeSameAs(inner);
        ex.InnerException.Should().BeOfType<IOException>();
    }

    [Fact]
    public void MessageAndSectionName_BothSet()
    {
        // Act
        var ex = new XdcWriteException("Section error", "deviceCommissioning");

        // Assert
        ex.Message.Should().Be("Section error");
        ex.SectionName.Should().Be("deviceCommissioning");
    }

    [Fact]
    public void IsException_InheritsFromWriteException()
    {
        // Act
        var ex = new XdcWriteException("test");

        // Assert
        ex.Should().BeAssignableTo<WriteException>();
        ex.Should().BeAssignableTo<Exception>();
    }
}
