namespace TopologyForge.Tests.Filtering;

using AwesomeAssertions;
using TopologyForge.Filtering;
using Xunit;

public sealed class PublicAddressClassifierTests
{
    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    public void IsPublic_WhenAddressIsRoutable_ReturnsTrue(string address) =>
        PublicAddressClassifier.IsPublic(address).Should().BeTrue();

    [Theory]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.10.1")]
    [InlineData("100.64.0.1")]
    [InlineData("224.0.0.251")]
    [InlineData("::1")]
    [InlineData("not an address")]
    [InlineData("")]
    [InlineData(null)]
    public void IsPublic_WhenAddressIsNotPublicIpv4_ReturnsFalse(string? address) =>
        PublicAddressClassifier.IsPublic(address).Should().BeFalse();
}
