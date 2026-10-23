using FluentAssertions;
using TopologyForge.Filtering;
using TopologyForge.Models;

namespace TopologyForge.Tests.Filtering;

public sealed class TopologyFlowEvaluatorTests
{
    private static TopologyFlowRecord CreateSampleFlow(
        string sourceHost = "SRV-WEB-01",
        string sourceIp = "10.0.1.10",
        int localPort = 54321,
        string remoteHost = "SRV-SQL-01",
        string remoteIp = "10.0.2.20",
        int remotePort = 1433,
        TopologyTransportProtocol protocol = TopologyTransportProtocol.Tcp,
        TopologyFlowDirection direction = TopologyFlowDirection.Outbound,
        string processName = "w3wp") =>
        new(
            SourceServerName: sourceHost,
            SourceIp: sourceIp,
            LocalPort: localPort,
            RemoteServerName: remoteHost,
            RemoteIp: remoteIp,
            RemotePort: remotePort,
            Protocol: protocol,
            Direction: direction,
            ProcessName: processName,
            ServiceTarget: processName,
            ObservationCount: 5);

    [Fact]
    public void ShouldInclude_WhenDefaultOptionsAndEphemeralServicePort_ReturnsFalse()
    {
        // Arrange
        var evaluator = new TopologyFlowEvaluator(new TopologyFilterOptions());
        var ephemeralOutbound = CreateSampleFlow(localPort: 443, remotePort: 55123, direction: TopologyFlowDirection.Outbound);
        var ephemeralListener = CreateSampleFlow(localPort: 50000, remotePort: 0, direction: TopologyFlowDirection.Listening);

        // Act & Assert
        evaluator.ShouldInclude(ephemeralOutbound).Should().BeFalse();
        evaluator.ShouldInclude(ephemeralListener).Should().BeFalse();
    }

    [Fact]
    public void ShouldInclude_WhenDefaultOptionsAndNonEphemeralServicePort_ReturnsTrue()
    {
        // Arrange
        var evaluator = new TopologyFlowEvaluator(new TopologyFilterOptions());
        var validOutbound = CreateSampleFlow(localPort: 55123, remotePort: 1433, direction: TopologyFlowDirection.Outbound);
        var validInbound = CreateSampleFlow(localPort: 443, remotePort: 55123, direction: TopologyFlowDirection.Inbound);
        var validListener = CreateSampleFlow(localPort: 443, remotePort: 0, direction: TopologyFlowDirection.Listening);

        // Act & Assert
        evaluator.ShouldInclude(validOutbound).Should().BeTrue();
        evaluator.ShouldInclude(validInbound).Should().BeTrue();
        evaluator.ShouldInclude(validListener).Should().BeTrue();
    }

    [Fact]
    public void ShouldInclude_WhenExcludeEphemeralPortsDisabled_ReturnsTrueForHighPort()
    {
        // Arrange
        var options = new TopologyFilterOptions { ExcludeEphemeralPorts = false };
        var evaluator = new TopologyFlowEvaluator(options);
        var highPortFlow = CreateSampleFlow(localPort: 50000, remotePort: 0, direction: TopologyFlowDirection.Listening);

        // Act
        var result = evaluator.ShouldInclude(highPortFlow);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldInclude_WhenPortInExcludedPorts_ReturnsFalse()
    {
        // Arrange
        var options = new TopologyFilterOptions
        {
            ExcludedPorts = [NetworkPortRange.Single(53), new NetworkPortRange(135, 139)]
        };
        var evaluator = new TopologyFlowEvaluator(options);

        // Act & Assert
        evaluator.ShouldInclude(CreateSampleFlow(remotePort: 53)).Should().BeFalse();
        evaluator.ShouldInclude(CreateSampleFlow(remotePort: 137)).Should().BeFalse();
        evaluator.ShouldInclude(CreateSampleFlow(remotePort: 1433)).Should().BeTrue();
    }

    [Fact]
    public void ShouldInclude_WhenIncludedPortsConfigured_AllowsOnlyMatchingServicePorts()
    {
        // Arrange
        var options = new TopologyFilterOptions
        {
            IncludedPorts = [NetworkPortRange.Single(443), NetworkPortRange.Single(1433)]
        };
        var evaluator = new TopologyFlowEvaluator(options);

        // Act & Assert
        evaluator.ShouldInclude(CreateSampleFlow(remotePort: 1433)).Should().BeTrue();
        evaluator.ShouldInclude(CreateSampleFlow(remotePort: 80)).Should().BeFalse();
    }

    [Fact]
    public void ShouldInclude_WhenHostMatchesExcludedPattern_ReturnsFalse()
    {
        // Arrange
        var options = new TopologyFilterOptions
        {
            ExcludedHostPatterns = ["*.telemetry.*", "SRV-MON-*"]
        };
        var evaluator = new TopologyFlowEvaluator(options);

        // Act & Assert
        evaluator.ShouldInclude(CreateSampleFlow(remoteHost: "api.telemetry.microsoft.com")).Should().BeFalse();
        evaluator.ShouldInclude(CreateSampleFlow(sourceHost: "SRV-MON-02")).Should().BeFalse();
        evaluator.ShouldInclude(CreateSampleFlow(sourceHost: "SRV-WEB-01", remoteHost: "SRV-SQL-01")).Should().BeTrue();
    }

    [Fact]
    public void ShouldInclude_WhenIncludedHostPatternsConfigured_RequiresHostMatch()
    {
        // Arrange
        var options = new TopologyFilterOptions
        {
            IncludedHostPatterns = ["*SQL*", "SRV-DB-*"]
        };
        var evaluator = new TopologyFlowEvaluator(options);

        // Act & Assert
        evaluator.ShouldInclude(CreateSampleFlow(sourceHost: "SRV-WEB-01", remoteHost: "SRV-SQL-01")).Should().BeTrue();
        evaluator.ShouldInclude(CreateSampleFlow(sourceHost: "SRV-WEB-01", remoteHost: "SRV-REDIS-01")).Should().BeFalse();
    }

    [Fact]
    public void ShouldInclude_WhenIpFiltersConfigured_EnforcesIncludedAndExcludedCidrs()
    {
        // Arrange
        var options = new TopologyFilterOptions
        {
            IncludedIpRanges = ["10.0.0.0/16"],
            ExcludedIpRanges = ["10.0.99.0/24"]
        };
        var evaluator = new TopologyFlowEvaluator(options);

        // Act & Assert
        evaluator.ShouldInclude(CreateSampleFlow(sourceIp: "10.0.1.10", remoteIp: "10.0.2.20")).Should().BeTrue();
        evaluator.ShouldInclude(CreateSampleFlow(sourceIp: "10.0.1.10", remoteIp: "10.0.99.5")).Should().BeFalse();
        evaluator.ShouldInclude(CreateSampleFlow(sourceIp: "192.168.1.10", remoteIp: "192.168.1.20")).Should().BeFalse();
    }

    [Fact]
    public void ShouldInclude_WhenProcessFiltersConfigured_EnforcesInclusionAndExclusion()
    {
        // Arrange
        var options = new TopologyFilterOptions
        {
            IncludedProcesses = ["sql*", "w3wp"],
            ExcludedProcesses = ["sqlwriter"]
        };
        var evaluator = new TopologyFlowEvaluator(options);

        // Act & Assert
        evaluator.ShouldInclude(CreateSampleFlow(processName: "sqlservr")).Should().BeTrue();
        evaluator.ShouldInclude(CreateSampleFlow(processName: "sqlwriter")).Should().BeFalse();
        evaluator.ShouldInclude(CreateSampleFlow(processName: "chrome")).Should().BeFalse();
    }

    [Fact]
    public void ShouldInclude_WhenDirectionFlagsConfigured_FiltersByDirection()
    {
        // Arrange
        var options = new TopologyFilterOptions
        {
            IncludeListeners = true,
            IncludeInboundFlows = false,
            IncludeOutboundFlows = false
        };
        var evaluator = new TopologyFlowEvaluator(options);

        // Act & Assert
        evaluator.ShouldInclude(CreateSampleFlow(localPort: 443, direction: TopologyFlowDirection.Listening)).Should().BeTrue();
        evaluator.ShouldInclude(CreateSampleFlow(localPort: 443, direction: TopologyFlowDirection.Inbound)).Should().BeFalse();
        evaluator.ShouldInclude(CreateSampleFlow(remotePort: 443, direction: TopologyFlowDirection.Outbound)).Should().BeFalse();
    }
}
