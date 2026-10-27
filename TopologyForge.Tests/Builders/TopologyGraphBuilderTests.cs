using AwesomeAssertions;
using TopologyForge.Builders;
using TopologyForge.Filtering;
using TopologyForge.Models;

namespace TopologyForge.Tests.Builders;

public sealed class TopologyGraphBuilderTests
{
    [Fact]
    public void BuildGraph_WhenFlowsContainListenersAndOutbound_AggregatesNodesAndNonEphemeralPorts()
    {
        // Arrange
        var evaluator = new TopologyFlowEvaluator(new TopologyFilterOptions());
        var builder = new TopologyGraphBuilder(evaluator);
        var flows = new[]
        {
            new TopologyFlowRecord("SRV-WEB-01", "10.0.1.10", 443, "", "0.0.0.0", 0, TopologyTransportProtocol.Tcp, TopologyFlowDirection.Listening, "nginx", "nginx.conf", 10),
            new TopologyFlowRecord("SRV-WEB-01", "10.0.1.10", 52100, "SRV-SQL-01", "10.0.2.20", 1433, TopologyTransportProtocol.Tcp, TopologyFlowDirection.Outbound, "dotnet", "App.dll", 25)
        };

        // Act
        var graph = builder.BuildGraph(flows);

        // Assert
        graph.Nodes.Should().HaveCount(2);
        var webNode = graph.Nodes.Single(n => n.HostName == "SRV-WEB-01");
        webNode.IsLocalAuditedServer.Should().BeTrue();
        webNode.ListeningPorts.Should().ContainSingle(p => p.Port == 443 && p.ProcessName == "nginx");
        webNode.OutboundConnectionCount.Should().Be(1);

        var sqlNode = graph.Nodes.Single(n => n.HostName == "SRV-SQL-01");
        sqlNode.IpAddress.Should().Be("10.0.2.20");
        sqlNode.ListeningPorts.Should().ContainSingle(p => p.Port == 1433);
        sqlNode.InboundConnectionCount.Should().Be(1);

        graph.Edges.Should().ContainSingle();
        var edge = graph.Edges[0];
        edge.SourceNodeId.Should().Be(webNode.NodeId);
        edge.TargetNodeId.Should().Be(sqlNode.NodeId);
        edge.TargetPort.Should().Be(1433);
        edge.ObservationCount.Should().Be(25);
        edge.FlowDirection.Should().Be(TopologyFlowDirection.Outbound);
    }

    [Fact]
    public void BuildGraph_WhenInboundFlowObserved_CreatesDirectedEdgeFromRemoteToLocalServicePort()
    {
        // Arrange
        var evaluator = new TopologyFlowEvaluator(new TopologyFilterOptions());
        var builder = new TopologyGraphBuilder(evaluator);
        var flows = new[]
        {
            new TopologyFlowRecord("SRV-SQL-01", "10.0.2.20", 1433, "SRV-APP-02", "10.0.1.15", 60123, TopologyTransportProtocol.Tcp, TopologyFlowDirection.Inbound, "sqlservr", "sqlservr.exe", 12)
        };

        // Act
        var graph = builder.BuildGraph(flows);

        // Assert
        graph.Nodes.Should().HaveCount(2);
        var appNode = graph.Nodes.Single(n => n.HostName == "SRV-APP-02");
        var sqlNode = graph.Nodes.Single(n => n.HostName == "SRV-SQL-01");

        // The ephemeral remote port 60123 must NOT be added to SRV-APP-02's listening ports
        appNode.ListeningPorts.Should().BeEmpty();
        sqlNode.ListeningPorts.Should().ContainSingle(p => p.Port == 1433);

        graph.Edges.Should().ContainSingle();
        graph.Edges[0].SourceNodeId.Should().Be(appNode.NodeId);
        graph.Edges[0].TargetNodeId.Should().Be(sqlNode.NodeId);
        graph.Edges[0].TargetPort.Should().Be(1433);
        graph.Edges[0].FlowDirection.Should().Be(TopologyFlowDirection.Inbound);
    }

    [Fact]
    public void BuildGraph_WhenDuplicateFlowsAndExcludedFlowsExist_DeduplicatesAndCountsAccurately()
    {
        // Arrange
        var evaluator = new TopologyFlowEvaluator(new TopologyFilterOptions());
        var builder = new TopologyGraphBuilder(evaluator);
        var flows = new[]
        {
            new TopologyFlowRecord("SRV-WEB-01", "10.0.1.10", 51001, "SRV-SQL-01", "10.0.2.20", 1433, TopologyTransportProtocol.Tcp, TopologyFlowDirection.Outbound, "dotnet", "App.dll", 10),
            new TopologyFlowRecord("SRV-WEB-01", "10.0.1.10", 51002, "SRV-SQL-01", "10.0.2.20", 1433, TopologyTransportProtocol.Tcp, TopologyFlowDirection.Outbound, "dotnet", "App.dll", 15),
            new TopologyFlowRecord("SRV-WEB-01", "10.0.1.10", 51003, "SRV-SQL-01", "10.0.2.20", 55999, TopologyTransportProtocol.Tcp, TopologyFlowDirection.Outbound, "dotnet", "App.dll", 5)
        };

        // Act
        var graph = builder.BuildGraph(flows);

        // Assert
        graph.TotalProcessedFlows.Should().Be(3);
        graph.ExcludedFlowsCount.Should().Be(1);
        graph.Edges.Should().ContainSingle();
        graph.Edges[0].ObservationCount.Should().Be(25);
    }
}
