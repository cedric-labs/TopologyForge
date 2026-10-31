namespace TopologyForge.Tests.Export.Drawio;

using AwesomeAssertions;
using TopologyForge.Export.Drawio.Adapters;
using TopologyForge.Models;
using TopologyForge.Rendering;
using Xunit;

public sealed class TopologyNodeOrdererTests
{
    private static TopologyServerNode Server(string name, string ip, int inbound = 0, int outbound = 0, params int[] ports) =>
        new(name, name, ip, false, [.. ports.Select(p => new ServerListenerDescriptor(p, TopologyTransportProtocol.Tcp, "svc", "svc"))], inbound, outbound);

    private static IEnumerable<string> Names(IEnumerable<TopologyServerNode> nodes) => nodes.Select(n => n.HostName);

    [Fact]
    public void Order_WhenMethodIsBarycentricSugiyama_KeepsTheInputOrder()
    {
        // Arrange
        var nodes = new[] { Server("zeta", "10.0.0.9"), Server("alpha", "10.0.0.1") };

        // Act
        var ordered = TopologyNodeOrderer.Order(nodes, [], TopologyNodeSortMethod.BarycentricSugiyama);

        // Assert
        Names(ordered).Should().Equal("zeta", "alpha");
    }

    [Fact]
    public void Order_WhenMethodIsAlphabetical_SortsByHostName()
    {
        // Arrange
        var nodes = new[] { Server("nas", "10.0.0.9"), Server("Backup", "10.0.0.1"), Server("pihole", "10.0.0.5") };

        // Act
        var ordered = TopologyNodeOrderer.Order(nodes, [], TopologyNodeSortMethod.Alphabetical);

        // Assert
        Names(ordered).Should().Equal("Backup", "nas", "pihole");
    }

    [Fact]
    public void Order_WhenMethodIsSubnetIp_SortsNumericallyByAddress()
    {
        // Arrange
        var nodes = new[] { Server("c", "192.168.1.20"), Server("a", "192.168.1.3"), Server("b", "10.0.0.200") };

        // Act
        var ordered = TopologyNodeOrderer.Order(nodes, [], TopologyNodeSortMethod.SubnetIp);

        // Assert
        Names(ordered).Should().Equal("b", "a", "c");
    }

    [Fact]
    public void Order_WhenMethodIsConnectionDegree_PutsTheBusiestServerFirstAndBreaksTiesByObservations()
    {
        // Arrange
        var nodes = new[]
        {
            Server("quiet", "10.0.0.1", inbound: 1),
            Server("busy", "10.0.0.2", inbound: 5, outbound: 2),
            Server("tie-low", "10.0.0.3", inbound: 3),
            Server("tie-high", "10.0.0.4", inbound: 3)
        };
        var edges = new[]
        {
            new TopologyServiceEdge("e1", "root", "tie-high", 80, TopologyTransportProtocol.Tcp, "p", "s", 50, TopologyFlowDirection.Outbound),
            new TopologyServiceEdge("e2", "root", "tie-low", 80, TopologyTransportProtocol.Tcp, "p", "s", 5, TopologyFlowDirection.Outbound)
        };

        // Act
        var ordered = TopologyNodeOrderer.Order(nodes, edges, TopologyNodeSortMethod.ConnectionDegree);

        // Assert
        Names(ordered).Should().Equal("busy", "tie-high", "tie-low", "quiet");
    }

    [Fact]
    public void Order_WhenMethodIsFunctionalTier_ListsClientsThenGatewaysThenApplicationsThenDatabases()
    {
        // Arrange
        var database = Server("db", "10.0.0.1", inbound: 2, outbound: 1, 5432);
        var application = Server("app", "10.0.0.2", inbound: 3, outbound: 1, 9000);
        var gateway = Server("proxy", "10.0.0.3", inbound: 4, outbound: 4, 443);
        var client = Server("laptop", "10.0.0.4", outbound: 6);

        // Act
        var ordered = TopologyNodeOrderer.Order([database, application, gateway, client], [], TopologyNodeSortMethod.FunctionalTier);

        // Assert
        Names(ordered).Should().Equal("laptop", "proxy", "app", "db");
    }

    [Fact]
    public void Order_WhenMethodIsSubnetCluster_GroupsBySubnetThenSortsByAddress()
    {
        // Arrange
        var nodes = new[] { Server("x", "192.168.1.9"), Server("y", "10.0.0.8"), Server("z", "192.168.1.2"), Server("w", "10.0.0.1") };

        // Act
        var ordered = TopologyNodeOrderer.Order(nodes, [], TopologyNodeSortMethod.SubnetCluster);

        // Assert
        Names(ordered).Should().Equal("w", "y", "z", "x");
    }
}
