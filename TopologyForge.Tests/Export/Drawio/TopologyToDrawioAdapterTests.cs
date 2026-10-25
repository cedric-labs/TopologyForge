namespace TopologyForge.Tests.Export.Drawio;

using FluentAssertions;
using TopologyForge.Export.Drawio.Adapters;
using TopologyForge.Models;
using Xunit;

public sealed class TopologyToDrawioAdapterTests
{
    [Fact]
    public void Convert_WhenProvidedAuditedServerAndTargets_GeneratesProperContainersLayersAndNodes()
    {
        // Arrange
        var rootServer = new TopologyServerNode(
            NodeId: "srv_root",
            HostName: "AUDIT-SRV",
            IpAddress: "10.0.1.5",
            IsLocalAuditedServer: true,
            ListeningPorts: [new ServerListenerDescriptor(80, TopologyTransportProtocol.Tcp, "nginx", "web")],
            InboundConnectionCount: 0,
            OutboundConnectionCount: 2);

        var target1 = new TopologyServerNode(
            NodeId: "srv_tgt1",
            HostName: "DB-PROD",
            IpAddress: "10.0.2.15",
            IsLocalAuditedServer: false,
            ListeningPorts: [new ServerListenerDescriptor(5432, TopologyTransportProtocol.Tcp, "postgres", "db")],
            InboundConnectionCount: 1,
            OutboundConnectionCount: 0);

        var edge1 = new TopologyServiceEdge(
            EdgeId: "e1",
            SourceNodeId: "srv_root",
            TargetNodeId: "srv_tgt1",
            TargetPort: 5432,
            Protocol: TopologyTransportProtocol.Tcp,
            ProcessName: "app",
            ServiceTarget: "db",
            ObservationCount: 1500,
            FlowDirection: TopologyFlowDirection.Outbound);

        var graph = new NetworkTopologyGraph(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Nodes: [rootServer, target1],
            Edges: [edge1],
            TotalProcessedFlows: 1,
            ExcludedFlowsCount: 0);

        var adapter = new TopologyToDrawioAdapter();

        // Act
        var doc = adapter.Convert(graph, "Production Network Flow");

        // Assert
        doc.Title.Should().Be("Production Network Flow");
        doc.Layers.Should().HaveCount(3);
        doc.Containers.Should().ContainSingle(c => c.Label.Contains("10.0.2.0/24"));
        doc.Nodes.Should().HaveCount(2);

        var rootNode = doc.Nodes.Single(n => n.Id == "srv_root");
        rootNode.RoleKey.Should().Be("AuditedRoot");
        rootNode.ContainerId.Should().BeNull();

        var targetNode = doc.Nodes.Single(n => n.Id == "srv_tgt1");
        targetNode.ContainerId.Should().NotBeNull();

        doc.Edges.Should().ContainSingle();
        var edge = doc.Edges.Single();
        edge.ExitAnchor.Should().NotBeNull();
        edge.EntryAnchor.Should().NotBeNull();
    }

    [Fact]
    public void Convert_WhenMoreThanThreePortsBetweenServers_AggregatesIntoSingleTrunkEdge()
    {
        // Arrange: 4 ports entre root et tgt
        var rootServer = new TopologyServerNode("srv_root", "ROOT", "10.0.1.5", true, [], 0, 4);
        var target = new TopologyServerNode("srv_tgt", "TARGET", "10.0.2.10", false, [], 4, 0);

        var edges = new List<TopologyServiceEdge>
        {
            new("e1", "srv_root", "srv_tgt", 80, TopologyTransportProtocol.Tcp, "p", "s", 100),
            new("e2", "srv_root", "srv_tgt", 443, TopologyTransportProtocol.Tcp, "p", "s", 200),
            new("e3", "srv_root", "srv_tgt", 8080, TopologyTransportProtocol.Tcp, "p", "s", 300),
            new("e4", "srv_root", "srv_tgt", 8443, TopologyTransportProtocol.Tcp, "p", "s", 400),
        };

        var graph = new NetworkTopologyGraph(DateTimeOffset.UtcNow, [rootServer, target], edges, 4, 0);
        var adapter = new TopologyToDrawioAdapter(aggregationMode: EdgeAggregationMode.AdaptiveThreshold);

        // Act
        var doc = adapter.Convert(graph);

        // Assert: Doit être agrégé en 1 seule arête consolidée
        doc.Edges.Should().ContainSingle();
        var aggEdge = doc.Edges.Single();
        aggEdge.TargetPortLabel.Should().Contain("(+2)");
        aggEdge.Metadata.Should().ContainKey("total_observations")
            .WhoseValue.Should().Be("1000");
    }
}
