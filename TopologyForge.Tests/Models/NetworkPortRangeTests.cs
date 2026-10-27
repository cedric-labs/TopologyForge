using AwesomeAssertions;
using TopologyForge.Models;

namespace TopologyForge.Tests.Models;

public sealed class NetworkPortRangeTests
{
    [Theory]
    [InlineData(-1, 80)]
    [InlineData(-100, 100)]
    public void Constructor_WhenStartPortIsNegative_ThrowsArgumentOutOfRangeException(int startPort, int endPort)
    {
        // Arrange & Act
        var act = () => new NetworkPortRange(startPort, endPort);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(80, 65536)]
    [InlineData(0, 70000)]
    public void Constructor_WhenEndPortExceedsMax_ThrowsArgumentOutOfRangeException(int startPort, int endPort)
    {
        // Arrange & Act
        var act = () => new NetworkPortRange(startPort, endPort);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WhenStartPortExceedsEndPort_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new NetworkPortRange(8080, 80);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(80, 80, 80)]
    [InlineData(8000, 8080, 8000)]
    [InlineData(8000, 8080, 8040)]
    [InlineData(8000, 8080, 8080)]
    public void Contains_WhenPortIsWithinBounds_ReturnsTrue(int start, int end, int port)
    {
        // Arrange
        var range = new NetworkPortRange(start, end);

        // Act
        var result = range.Contains(port);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(80, 80, 443)]
    [InlineData(8000, 8080, 7999)]
    [InlineData(8000, 8080, 8081)]
    public void Contains_WhenPortIsOutsideBounds_ReturnsFalse(int start, int end, int port)
    {
        // Arrange
        var range = new NetworkPortRange(start, end);

        // Act
        var result = range.Contains(port);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Parse_WhenSinglePortProvided_ReturnsSinglePortRange()
    {
        // Arrange & Act
        var range = NetworkPortRange.Parse("443");

        // Assert
        range.StartPort.Should().Be(443);
        range.EndPort.Should().Be(443);
        range.ToString().Should().Be("443");
    }

    [Fact]
    public void Parse_WhenPortIntervalProvided_ReturnsExpectedRange()
    {
        // Arrange & Act
        var range = NetworkPortRange.Parse("8000-8080");

        // Assert
        range.StartPort.Should().Be(8000);
        range.EndPort.Should().Be(8080);
        range.ToString().Should().Be("8000-8080");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("80-abc")]
    [InlineData("80-90-100")]
    public void Parse_WhenInputIsInvalid_ThrowsFormatException(string rawSpec)
    {
        // Arrange & Act
        var act = () => NetworkPortRange.Parse(rawSpec);

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseList_WhenCommaSeparatedSpecProvided_ReturnsParsedRanges()
    {
        // Arrange & Act
        var ranges = NetworkPortRange.ParseList("80, 443, 8000-8080");

        // Assert
        ranges.Should().HaveCount(3);
        ranges[0].Should().Be(NetworkPortRange.Single(80));
        ranges[1].Should().Be(NetworkPortRange.Single(443));
        ranges[2].Should().Be(new NetworkPortRange(8000, 8080));
    }
}
