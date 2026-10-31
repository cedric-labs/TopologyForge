namespace TopologyForge.Export.Drawio.Adapters;

using System.Globalization;
using TopologyForge.Filtering;
using TopologyForge.Models;
using TopologyForge.Rendering;

/// <summary>
/// Orders the target servers of the Draw.io diagram with the same methods as the HTML map sort selector.
/// </summary>
public static class TopologyNodeOrderer
{
    private const string InternetZoneName = "Internet";

    private static readonly int[] DatabasePorts = [1433, 1521, 3306, 5432, 6379, 27017, 9200, 445, 2049, 389, 636];
    private static readonly int[] WebPorts = [80, 443, 8080, 8443, 53];

    /// <summary>
    /// Returns the servers in the order given by the sort method.
    /// </summary>
    /// <param name="nodes">The servers to order.</param>
    /// <param name="edges">The flows, used to break ties on traffic volume.</param>
    /// <param name="method">The sort method; <see cref="TopologyNodeSortMethod.BarycentricSugiyama"/> keeps the input order.</param>
    /// <returns>A new list; the input is not modified.</returns>
    public static IReadOnlyList<TopologyServerNode> Order(
        IReadOnlyList<TopologyServerNode> nodes,
        IReadOnlyList<TopologyServiceEdge> edges,
        TopologyNodeSortMethod method)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        return method switch
        {
            TopologyNodeSortMethod.Alphabetical => [.. nodes.OrderBy(n => n.HostName, StringComparer.OrdinalIgnoreCase)],
            TopologyNodeSortMethod.SubnetIp => [.. nodes.OrderBy(AddressRank).ThenBy(n => n.HostName, StringComparer.OrdinalIgnoreCase)],
            TopologyNodeSortMethod.ConnectionDegree => OrderByActivity(nodes, edges),
            TopologyNodeSortMethod.FunctionalTier => OrderByTier(nodes),
            TopologyNodeSortMethod.SubnetCluster => OrderBySubnet(nodes),
            _ => [.. nodes]
        };
    }

    /// <summary>
    /// Name of the zone a server belongs to: the /24 of a private address, <c>Internet</c> for a public one.
    /// </summary>
    internal static string SubnetPrefixOf(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress)) return "Unknown";
        if (PublicAddressClassifier.IsPublic(ipAddress)) return InternetZoneName;
        int lastDot = ipAddress.LastIndexOf('.');
        return lastDot > 0 ? ipAddress[..lastDot] + ".0/24" : "Other";
    }

    private static List<TopologyServerNode> OrderByActivity(IReadOnlyList<TopologyServerNode> nodes, IReadOnlyList<TopologyServiceEdge> edges)
    {
        var observations = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            long weight = Math.Max(edge.ObservationCount, 1);
            observations[edge.SourceNodeId] = observations.GetValueOrDefault(edge.SourceNodeId) + weight;
            observations[edge.TargetNodeId] = observations.GetValueOrDefault(edge.TargetNodeId) + weight;
        }

        return [.. nodes
            .OrderByDescending(n => n.InboundConnectionCount + n.OutboundConnectionCount + n.ListeningPorts.Count)
            .ThenByDescending(n => observations.GetValueOrDefault(n.NodeId))
            .ThenBy(n => n.HostName, StringComparer.OrdinalIgnoreCase)];
    }

    private static List<TopologyServerNode> OrderByTier(IReadOnlyList<TopologyServerNode> nodes) =>
        [.. nodes
            .OrderBy(ClassifyTier)
            .ThenByDescending(n => n.InboundConnectionCount + n.OutboundConnectionCount)
            .ThenBy(n => n.HostName, StringComparer.OrdinalIgnoreCase)];

    private static List<TopologyServerNode> OrderBySubnet(IReadOnlyList<TopologyServerNode> nodes) =>
        [.. nodes
            .GroupBy(n => SubnetPrefixOf(n.IpAddress), StringComparer.Ordinal)
            .OrderByDescending(g => g.Any(n => n.IsLocalAuditedServer))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .SelectMany(g => g.OrderBy(AddressRank).ThenBy(n => n.HostName, StringComparer.OrdinalIgnoreCase))];

    // 0 clients, 1 gateways and web entry points, 2 application services, 3 databases and sinks.
    private static int ClassifyTier(TopologyServerNode node)
    {
        int inbound = node.InboundConnectionCount;
        int outbound = node.OutboundConnectionCount;
        bool hasDatabasePort = node.ListeningPorts.Any(p => DatabasePorts.Contains(p.Port));
        bool hasWebPort = node.ListeningPorts.Any(p => WebPorts.Contains(p.Port));

        if (hasDatabasePort || (inbound > 0 && outbound == 0) || (inbound >= 4 && inbound > outbound * 2.5)) return 3;
        if ((outbound > 0 && inbound == 0) || (outbound >= 3 && outbound > inbound * 2.5 && !hasWebPort)) return 0;
        if (hasWebPort || (inbound > 0 && outbound > 0 && outbound >= inbound)) return 1;
        return 2;
    }

    private static long AddressRank(TopologyServerNode node)
    {
        var octets = node.IpAddress.Trim().Split('.');
        if (octets.Length != 4) return long.MaxValue;

        long rank = 0;
        foreach (var octet in octets)
        {
            if (!int.TryParse(octet, NumberStyles.None, CultureInfo.InvariantCulture, out int value)) return long.MaxValue;
            rank = (rank * 256) + value;
        }

        return rank;
    }
}
