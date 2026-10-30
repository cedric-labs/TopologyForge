namespace TopologyForge.Tests.Export.Drawio;

using AwesomeAssertions;
using TopologyForge.Export.Drawio.Adapters;
using TopologyForge.Export.Drawio.Model;
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
    public void Convert_WhenServersAreLaidOut_WritesTheTooltipsInEnglish()
    {
        // Arrange
        var rootServer = new TopologyServerNode("srv_root", "pve01", "192.168.1.10", true, [], 0, 1);
        var target = new TopologyServerNode("srv_nas", "nas", "192.168.1.20", false, [], 1, 0);
        var edge = new TopologyServiceEdge(
            "e1", "srv_root", "srv_nas", 445, TopologyTransportProtocol.Tcp, "smb", "nas", 1, TopologyFlowDirection.Outbound);
        var graph = new NetworkTopologyGraph(DateTimeOffset.UtcNow, [rootServer, target], [edge], 1, 0);

        // Act
        var doc = new TopologyToDrawioAdapter().Convert(graph, "Homelab");

        // Assert
        doc.Nodes.Single(n => n.Id == "srv_root").TooltipTemplate.Should().Be("IP: %ip%\nName: %name%\nRole: Audited server");
        doc.Nodes.Single(n => n.Id == "srv_nas").TooltipTemplate.Should().Be("IP: %ip%\nName: %name%\nSubnet: 192.168.1.0/24");
    }

    [Fact]
    public void Convert_WhenMoreThanThreePortsBetweenServers_AggregatesIntoSingleTrunkEdge()
    {
        // Arrange: 4 ports between root and tgt
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

        // Assert: aggregated into a single consolidated edge
        doc.Edges.Should().ContainSingle();
        var aggEdge = doc.Edges.Single();
        aggEdge.TargetPortLabel.Should().Contain("(+2)");
        aggEdge.Metadata.Should().ContainKey("total_observations")
            .WhoseValue.Should().Be("1000");
    }

    [Fact]
    public void Convert_WhenByServerPairAndFlowsGoBothWays_MergesThemIntoOneBidirectionalMixedEdge()
    {
        // Arrange
        var desktop = new TopologyServerNode("desktop", "desktop", "192.168.1.10", true, [], 1, 1);
        var proxy = new TopologyServerNode("proxy", "proxy", "192.168.1.30", false, [], 1, 1);
        var edges = new List<TopologyServiceEdge>
        {
            new("e1", "desktop", "proxy", 443, TopologyTransportProtocol.Tcp, "curl", "nginx", 25, TopologyFlowDirection.Outbound),
            new("e2", "proxy", "desktop", 8080, TopologyTransportProtocol.Tcp, "nginx", "kestrel", 40, TopologyFlowDirection.Inbound)
        };
        var graph = new NetworkTopologyGraph(DateTimeOffset.UtcNow, [desktop, proxy], edges, 65, 0);
        var adapter = new TopologyToDrawioAdapter(aggregationMode: EdgeAggregationMode.ByServerPair);

        // Act
        var doc = adapter.Convert(graph);

        // Assert
        var pairEdge = doc.Edges.Should().ContainSingle().Subject;
        pairEdge.Direction.Should().Be(DiagramEdgeDirection.Bidirectional);
        pairEdge.StyleKey.Should().Be("Mixed");
        pairEdge.LayerId.Should().Be("layer_mixed");
        pairEdge.TargetPortLabel.Should().Be(":443/Tcp, :8080/Tcp");
        pairEdge.Metadata.Should().ContainKey("flow_count").WhoseValue.Should().Be("2");
        pairEdge.Metadata.Should().ContainKey("total_observations").WhoseValue.Should().Be("65");
        doc.Layers.Select(l => l.Id).Should().Contain("layer_mixed");
    }

    [Fact]
    public void Convert_WhenByServerPairAndFlowsShareOneDirection_KeepsTheDirectionStyleAndOneHead()
    {
        // Arrange
        var desktop = new TopologyServerNode("desktop", "desktop", "192.168.1.10", true, [], 0, 2);
        var nas = new TopologyServerNode("nas", "nas", "192.168.1.50", false, [], 2, 0);
        var edges = new List<TopologyServiceEdge>
        {
            new("e1", "desktop", "nas", 443, TopologyTransportProtocol.Tcp, "curl", "nginx", 5, TopologyFlowDirection.Outbound),
            new("e2", "desktop", "nas", 873, TopologyTransportProtocol.Tcp, "agent", "rsync", 5, TopologyFlowDirection.Outbound)
        };
        var graph = new NetworkTopologyGraph(DateTimeOffset.UtcNow, [desktop, nas], edges, 10, 0);
        var adapter = new TopologyToDrawioAdapter(aggregationMode: EdgeAggregationMode.ByServerPair);

        // Act
        var doc = adapter.Convert(graph);

        // Assert
        var pairEdge = doc.Edges.Should().ContainSingle().Subject;
        pairEdge.Direction.Should().Be(DiagramEdgeDirection.Directed);
        pairEdge.SourceNodeId.Should().Be("desktop");
        pairEdge.StyleKey.Should().Be("Outbound");
        pairEdge.LayerId.Should().Be("layer_outbound");
        doc.Layers.Select(l => l.Id).Should().NotContain("layer_mixed");
    }
    private static NetworkTopologyGraph CreateGraphWithOneServerPerSubnet(int subnetCount)
    {
        var nodes = new List<TopologyServerNode>
        {
            new("root", "pve01", "192.168.1.10", true, [], 0, subnetCount)
        };
        nodes.AddRange(Enumerable.Range(1, subnetCount)
            .Select(i => new TopologyServerNode($"srv_{i}", $"host-{i}", $"10.{i}.0.5", false, [], 1, 0)));
        return new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, [], 0, 0);
    }

    [Fact]
    public void Convert_WhenManySubnets_WrapsTheZonesWithinTheMaximumRowWidth()
    {
        // Arrange
        var graph = CreateGraphWithOneServerPerSubnet(56);
        var adapter = new TopologyToDrawioAdapter { LayoutOptions = new TopologyDrawioLayoutOptions { MaxRowWidth = 2000 } };

        // Act
        var doc = adapter.Convert(graph);

        // Assert
        doc.Containers.Should().HaveCount(56);
        doc.Containers.Max(c => c.X + c.Width).Should().BeLessThanOrEqualTo(80 + 2000);
        doc.Containers.Select(c => c.Y).Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Convert_WhenManySubnetsAndDefaultOptions_KeepsTheDiagramWithinTheDefaultRowWidth()
    {
        // Arrange
        var graph = CreateGraphWithOneServerPerSubnet(56);

        // Act
        var doc = new TopologyToDrawioAdapter().Convert(graph);

        // Assert
        doc.Containers.Max(c => c.X + c.Width).Should().BeLessThanOrEqualTo(80 + TopologyDrawioLayoutOptions.DefaultMaxRowWidth);
    }

    [Fact]
    public void Convert_WhenZonesWrapOntoSeveralRows_DoesNotOverlapZonesOrServers()
    {
        // Arrange
        var graph = CreateGraphWithOneServerPerSubnet(20);
        var adapter = new TopologyToDrawioAdapter { LayoutOptions = new TopologyDrawioLayoutOptions { MaxRowWidth = 1000 } };

        // Act
        var doc = adapter.Convert(graph);

        // Assert
        var zones = doc.Containers.ToList();
        for (int i = 0; i < zones.Count; i++)
        {
            for (int j = i + 1; j < zones.Count; j++)
            {
                var a = zones[i];
                var b = zones[j];
                bool overlap = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                overlap.Should().BeFalse($"{a.Label} and {b.Label} must not overlap");
            }
        }
    }

    [Fact]
    public void Convert_WhenSubnetGroupingIsOff_WritesNoContainerAndKeepsEveryNode()
    {
        // Arrange
        var graph = CreateGraphWithOneServerPerSubnet(30);
        var adapter = new TopologyToDrawioAdapter
        {
            LayoutOptions = new TopologyDrawioLayoutOptions { GroupBySubnets = false, MaxRowWidth = 1000 }
        };

        // Act
        var doc = adapter.Convert(graph);

        // Assert
        doc.Containers.Should().BeEmpty();
        doc.Nodes.Should().HaveCount(31);
        doc.Nodes.Should().OnlyContain(n => n.ContainerId == null);
        doc.Nodes.Where(n => n.RoleKey != "AuditedRoot").Max(n => n.X + n.Width).Should().BeLessThanOrEqualTo(80 + 1000);
        doc.Nodes.Select(n => (n.X, n.Y)).Distinct().Should().HaveCount(31);
    }
    [Fact]
    public void Convert_WhenZonesWrapOntoSeveralRows_RoutesEdgesToEveryRowWithoutWaypointInsideAnotherServer()
    {
        // Arrange
        var graph = CreateGraphWithOneServerPerSubnet(12);
        var edges = Enumerable.Range(1, 12)
            .Select(i => new TopologyServiceEdge($"e{i}", "root", $"srv_{i}", 443, TopologyTransportProtocol.Tcp, "p", "s", 1, TopologyFlowDirection.Outbound))
            .ToList();
        graph = graph with { Edges = edges };
        var adapter = new TopologyToDrawioAdapter { LayoutOptions = new TopologyDrawioLayoutOptions { MaxRowWidth = 800 } };

        // Act
        var doc = adapter.Convert(graph);

        // Assert
        doc.Containers.Select(c => c.Y).Distinct().Should().HaveCountGreaterThan(1);
        doc.Edges.Should().HaveCount(12).And.OnlyContain(e => e.ExitAnchor != null && e.EntryAnchor != null);
        foreach (var edge in doc.Edges)
        {
            var obstacles = doc.Nodes.Where(n => n.Id != edge.SourceNodeId && n.Id != edge.TargetNodeId);
            foreach (var point in edge.Waypoints ?? [])
            {
                obstacles.Should().NotContain(
                    n => point.X > n.X && point.X < n.X + n.Width && point.Y > n.Y && point.Y < n.Y + n.Height,
                    $"edge {edge.Id} must go around servers");
            }
        }
    }

    [Fact]
    public void Convert_WhenTargetsHavePublicAddresses_GroupsThemInOneInternetZone()
    {
        // Arrange
        var nodes = new List<TopologyServerNode>
        {
            new("root", "pve01", "192.168.1.10", true, [], 0, 3),
            new("lan", "nas", "192.168.1.20", false, [], 1, 0),
            new("pub1", "dns", "1.1.1.1", false, [], 1, 0),
            new("pub2", "cdn", "93.184.216.34", false, [], 1, 0),
            new("pub3", "api", "151.101.1.69", false, [], 1, 0)
        };
        var graph = new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, [], 0, 0);
        var adapter = new TopologyToDrawioAdapter();

        // Act
        var doc = adapter.Convert(graph);

        // Assert
        doc.Containers.Select(c => c.Label).Should().BeEquivalentTo("Subnet 192.168.1.0/24", "Internet");
        var internet = doc.Containers.Single(c => c.Label == "Internet");
        doc.Nodes.Where(n => n.ContainerId == internet.Id).Select(n => n.Id).Should().BeEquivalentTo("pub1", "pub2", "pub3");
    }
}
