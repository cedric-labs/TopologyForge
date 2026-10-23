using FluentAssertions;
using TopologyForge.Filtering;

namespace TopologyForge.Tests.Filtering;

public sealed class CidrSubnetMatcherTests
{
    [Fact]
    public void IsMatch_WhenExactIpMatches_ReturnsTrue()
    {
        // Arrange & Act
        var result = CidrSubnetMatcher.IsMatch("10.20.30.40", "10.20.30.40");

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("192.168.1.50", "192.168.1.0/24")]
    [InlineData("10.15.200.1", "10.0.0.0/8")]
    [InlineData("172.16.5.4", "172.16.0.0/12")]
    [InlineData("fe80::1", "fe80::/10")]
    public void IsMatch_WhenIpIsInsideCidrRange_ReturnsTrue(string ipAddress, string cidrRule)
    {
        // Arrange & Act
        var result = CidrSubnetMatcher.IsMatch(ipAddress, cidrRule);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("192.168.2.50", "192.168.1.0/24")]
    [InlineData("11.0.0.1", "10.0.0.0/8")]
    [InlineData("192.168.1.1", "fe80::/10")]
    public void IsMatch_WhenIpIsOutsideCidrRange_ReturnsFalse(string ipAddress, string cidrRule)
    {
        // Arrange & Act
        var result = CidrSubnetMatcher.IsMatch(ipAddress, cidrRule);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "192.168.1.0/24")]
    [InlineData("192.168.1.1", "")]
    [InlineData("not-an-ip", "192.168.1.0/24")]
    [InlineData("192.168.1.1", "invalid-cidr/24")]
    [InlineData("192.168.1.1", "192.168.1.0/99")]
    [InlineData("192.168.1.1", "192.168.1.0/-1")]
    [InlineData("192.168.1.1", "192.168.1.0/abc")]
    public void IsMatch_WhenIpOrRuleIsInvalid_ReturnsFalse(string ipAddress, string cidrRule)
    {
        // Arrange & Act
        var result = CidrSubnetMatcher.IsMatch(ipAddress, cidrRule);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsMatchAny_WhenListContainsMatchingSubnet_ReturnsTrue()
    {
        // Arrange
        var rules = new[] { "10.0.0.0/8", "192.168.10.0/24" };

        // Act
        var match = CidrSubnetMatcher.IsMatchAny("192.168.10.99", rules);
        var noMatch = CidrSubnetMatcher.IsMatchAny("172.16.0.1", rules);

        // Assert
        match.Should().BeTrue();
        noMatch.Should().BeFalse();
    }
}
