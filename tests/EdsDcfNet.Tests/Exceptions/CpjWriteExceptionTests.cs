namespace EdsDcfNet.Tests.Exceptions;

using EdsDcfNet.Exceptions;

public class CpjWriteExceptionTests
{
    [Fact]
    public void DefaultConstructor_CreatesException()
    {
        // Act
        var ex = new CpjWriteException();

        // Assert
        ex.Should().NotBeNull();
        ex.SectionName.Should().BeNull();
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        // Act
        var ex = new CpjWriteException("Write failed");

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
        var ex = new CpjWriteException("Write error", inner);

        // Assert
        ex.Message.Should().Be("Write error");
        ex.InnerException.Should().BeSameAs(inner);
        ex.InnerException.Should().BeOfType<IOException>();
    }

    [Fact]
    public void MessageAndSectionName_BothSet()
    {
        // Act
        var ex = new CpjWriteException("Section error", "Topology");

        // Assert
        ex.Message.Should().Be("Section error");
        ex.SectionName.Should().Be("Topology");
    }

    [Fact]
    public void IsException_InheritsFromException()
    {
        // Act
        var ex = new CpjWriteException("test");

        // Assert
        ex.Should().BeAssignableTo<Exception>();
    }
}
