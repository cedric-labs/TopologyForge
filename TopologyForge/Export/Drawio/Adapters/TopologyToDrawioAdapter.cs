namespace TopologyForge.Export.Drawio.Adapters;

using System.Globalization;
using TopologyForge.Export.Drawio.Model;
using TopologyForge.Export.Drawio.Styling;
using TopologyForge.Filtering;
using TopologyForge.Geometry;
using TopologyForge.Models;

/// <summary>
/// Converts a network topology graph <see cref="NetworkTopologyGraph"/>
/// into a complete, positioned and routed Draw.io diagram document.
/// </summary>
public sealed class TopologyToDrawioAdapter(
    int gridSize = 10,
    int fanSpacing = 20,
    EdgeAggregationMode aggregationMode = EdgeAggregationMode.AdaptiveThreshold)
{
    private const double NodeWidth = 200.0;
    private const double ZoneOriginX = 80.0;
    private const double MinimumRowHeight = 90.0;

    /// <summary>
    /// Placement of the target servers; the defaults group them by subnet and wrap rows at a bounded width.
    /// </summary>
    public TopologyDrawioLayoutOptions LayoutOptions { get; init; } = new();

    private const double ContainerPadding = 24.0;
    private const double ContainerHeaderOffset = 36.0;
    private const string MixedLayerId = "layer_mixed";
    private const string InternetZoneName = "Internet";

    /// <summary>
    /// Converts the network graph into a Draw.io document with a tree layout and collision-free routing.
    /// </summary>
    public DiagramDocument Convert(NetworkTopologyGraph graph, string title = "Network Flow Topology")
    {
        ArgumentNullException.ThrowIfNull(graph);

        var (containers, nodes) = LayoutNodesAndContainers(graph.Nodes);
        var edges = BuildAndAggregateEdges(graph.Edges);

        var router = new TopologyEdgeRouter(gridSize, fanSpacing);
        var routedEdges = router.RouteEdges(nodes, edges, forceVerticalTreeAnchors: true);

        var layers = new List<DiagramLayer>
        {
            new("layer_infra", "Infrastructure (Servers & Zones)", Visible: true),
            new("layer_inbound", "Inbound flows", Visible: true),
            new("layer_outbound", "Outbound flows", Visible: true)
        };
        if (edges.Any(e => e.LayerId == MixedLayerId))
        {
            layers.Add(new DiagramLayer(MixedLayerId, "Mixed flows (Inbound + Outbound)", Visible: true));
        }

        return new DiagramDocument(
            Title: title,
            Layers: layers,
            Containers: containers,
            Nodes: nodes,
            Edges: routedEdges,
            BackgroundColor: "#0f172a",
            GridSize: gridSize);
    }

    private (IReadOnlyList<DiagramContainer> Containers, IReadOnlyList<DiagramNode> Nodes) LayoutNodesAndContainers(
        IReadOnlyList<TopologyServerNode> serverNodes)
    {
        var roots = serverNodes.Where(n => n.IsLocalAuditedServer).ToList();
        if (roots.Count == 0 && serverNodes.Count > 0)
        {
            roots = [serverNodes[0]];
        }

        var rootIds = roots.Select(r => r.NodeId).ToHashSet(StringComparer.Ordinal);
        var targetNodes = serverNodes.Where(n => !rootIds.Contains(n.NodeId)).ToList();

        var diagramNodes = new List<DiagramNode>(serverNodes.Count);
        var diagramContainers = new List<DiagramContainer>();

        // 1. Roots (audited servers) at the top
        double rootStartX = 80.0;
        double rootY = 60.0;
        for (int i = 0; i < roots.Count; i++)
        {
            var root = roots[i];
            double rootX = rootStartX + (i * 260.0);
            var badges = root.ListeningPorts.Select(p => $":{p.Port}/{p.Protocol}").ToList();
            double nodeHeight = DrawioHtmlCardBuilder.ComputeCardHeight(badges.Count, gridSize);

            diagramNodes.Add(new DiagramNode(
                Id: root.NodeId,
                Label: root.HostName,
                Subtitle: root.IpAddress,
                X: GridSnapper.Snap(rootX, gridSize),
                Y: GridSnapper.Snap(rootY, gridSize),
                Width: NodeWidth,
                Height: nodeHeight,
                LayerId: "layer_infra",
                ContainerId: null,
                Shape: DiagramNodeShape.RichCardHtml,
                RoleKey: "AuditedRoot",
                Badges: badges,
                TooltipTemplate: "IP: %ip%\nName: %name%\nRole: Audited server",
                Metadata: new Dictionary<string, string>
                {
                    ["ip"] = root.IpAddress,
                    ["type"] = "AuditedRoot",
                    ["inbound_count"] = root.InboundConnectionCount.ToString(CultureInfo.InvariantCulture),
                    ["outbound_count"] = root.OutboundConnectionCount.ToString(CultureInfo.InvariantCulture)
                }));
        }

        // 2. Targets, grouped by subnet unless the options turn the zones off
        var subnetGroups = targetNodes
            .GroupBy(n => ExtractSubnetPrefix(n.IpAddress))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        double targetStartY = roots.Count > 0 ? 240.0 : 60.0;

        if (LayoutOptions.GroupBySubnets)
        {
            diagramContainers.AddRange(PlaceTargetsInSubnetZones(subnetGroups, targetStartY, diagramNodes));
        }
        else
        {
            PlaceTargetsAsGrid(subnetGroups, targetStartY, diagramNodes);
        }

        return (diagramContainers, diagramNodes);
    }

    private List<DiagramContainer> PlaceTargetsInSubnetZones(
        IReadOnlyList<IGrouping<string, TopologyServerNode>> subnetGroups,
        double startY,
        List<DiagramNode> diagramNodes)
    {
        var zones = subnetGroups.Select(group => PlanZone(group.Key, group.ToList())).ToList();
        var origins = RowPacker.Pack(
            zones.Select(z => (z.Width, z.Height)).ToList(),
            ZoneOriginX,
            startY,
            LayoutOptions.MaxRowWidth,
            horizontalGap: 40.0,
            verticalGap: 40.0);

        var containers = new List<DiagramContainer>(zones.Count);
        for (int zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
        {
            var zone = zones[zoneIndex];
            string zoneId = $"zone_{zoneIndex + 1}";
            var (zoneX, zoneY) = origins[zoneIndex];

            for (int idx = 0; idx < zone.Servers.Count; idx++)
            {
                double nodeX = zoneX + ContainerPadding + (idx % zone.Columns * (NodeWidth + 20.0));
                double nodeY = zoneY + ContainerHeaderOffset + (idx / zone.Columns * (zone.RowHeight + 20.0));
                diagramNodes.Add(CreateTargetNode(zone.Servers[idx], zone.Cidr, nodeX, nodeY, zoneId));
            }

            containers.Add(new DiagramContainer(
                Id: zoneId,
                Label: zone.Cidr == InternetZoneName ? InternetZoneName : $"Subnet {zone.Cidr}",
                X: GridSnapper.Snap(zoneX, gridSize),
                Y: GridSnapper.Snap(zoneY, gridSize),
                Width: zone.Width,
                Height: zone.Height,
                LayerId: "layer_infra",
                Metadata: new Dictionary<string, string> { ["cidr"] = zone.Cidr }));
        }

        return containers;
    }

    // Servers of one subnet stay next to each other, but no container surrounds them.
    private void PlaceTargetsAsGrid(
        IReadOnlyList<IGrouping<string, TopologyServerNode>> subnetGroups,
        double startY,
        List<DiagramNode> diagramNodes)
    {
        var servers = subnetGroups.SelectMany(group => group.Select(server => (Server: server, Cidr: group.Key))).ToList();
        var origins = RowPacker.Pack(
            servers.Select(s => (NodeWidth, ComputeNodeHeight(s.Server))).ToList(),
            ZoneOriginX,
            startY,
            LayoutOptions.MaxRowWidth,
            horizontalGap: 20.0,
            verticalGap: 20.0);

        for (int i = 0; i < servers.Count; i++)
        {
            diagramNodes.Add(CreateTargetNode(servers[i].Server, servers[i].Cidr, origins[i].X, origins[i].Y, containerId: null));
        }
    }

    private ZonePlan PlanZone(string cidr, List<TopologyServerNode> servers)
    {
        // Columns at most 2 nodes wide
        int columns = Math.Min(2, servers.Count);
        int rows = (int)Math.Ceiling(servers.Count / (double)columns);
        double rowHeight = Math.Max(MinimumRowHeight, servers.Max(ComputeNodeHeight));

        double width = (columns * NodeWidth) + ((columns - 1) * 20.0) + (ContainerPadding * 2);
        double height = ContainerHeaderOffset + (rows * (rowHeight + 20.0)) + ContainerPadding;

        return new ZonePlan(cidr, servers, columns, rowHeight, GridSnapper.Snap(width, gridSize), GridSnapper.Snap(height, gridSize));
    }

    private double ComputeNodeHeight(TopologyServerNode server) =>
        DrawioHtmlCardBuilder.ComputeCardHeight(server.ListeningPorts.Count, gridSize);

    private DiagramNode CreateTargetNode(TopologyServerNode server, string subnetCidr, double x, double y, string? containerId)
    {
        var badges = server.ListeningPorts.Select(p => $":{p.Port}/{p.Protocol}").ToList();

        return new DiagramNode(
            Id: server.NodeId,
            Label: server.HostName,
            Subtitle: server.IpAddress,
            X: GridSnapper.Snap(x, gridSize),
            Y: GridSnapper.Snap(y, gridSize),
            Width: NodeWidth,
            Height: DrawioHtmlCardBuilder.ComputeCardHeight(badges.Count, gridSize),
            LayerId: "layer_infra",
            ContainerId: containerId,
            Shape: DiagramNodeShape.RichCardHtml,
            RoleKey: "Standard",
            Badges: badges,
            TooltipTemplate: "IP: %ip%\nName: %name%\nSubnet: " + subnetCidr,
            Metadata: new Dictionary<string, string>
            {
                ["ip"] = server.IpAddress,
                ["subnet"] = subnetCidr,
                ["inbound_count"] = server.InboundConnectionCount.ToString(CultureInfo.InvariantCulture),
                ["outbound_count"] = server.OutboundConnectionCount.ToString(CultureInfo.InvariantCulture)
            });
    }

    private sealed record ZonePlan(
        string Cidr,
        IReadOnlyList<TopologyServerNode> Servers,
        int Columns,
        double RowHeight,
        double Width,
        double Height);

    private IReadOnlyList<DiagramEdge> BuildAndAggregateEdges(IReadOnlyList<TopologyServiceEdge> edges)
    {
        if (aggregationMode == EdgeAggregationMode.ByServerPair)
        {
            return BuildServerPairEdges(edges);
        }

        if (aggregationMode == EdgeAggregationMode.None)
        {
            return edges.Select(e =>
            {
                bool isInbound = e.FlowDirection == TopologyFlowDirection.Inbound;
                string layer = isInbound ? "layer_inbound" : "layer_outbound";
                string styleKey = isInbound ? "Inbound" : "Outbound";
                string portLabel = $":{e.TargetPort}/{e.Protocol}";

                return new DiagramEdge(
                    Id: e.EdgeId,
                    SourceNodeId: e.SourceNodeId,
                    TargetNodeId: e.TargetNodeId,
                    TargetPortLabel: portLabel,
                    LayerId: layer,
                    StyleKey: styleKey,
                    Metadata: new Dictionary<string, string>
                    {
                        ["port"] = e.TargetPort.ToString(CultureInfo.InvariantCulture),
                        ["protocol"] = e.Protocol.ToString(),
                        ["observations"] = e.ObservationCount.ToString(CultureInfo.InvariantCulture)
                    });
            }).ToList();
        }

        // Grouped by server pair and direction
        var groups = edges.GroupBy(e => $"{e.SourceNodeId}->{e.TargetNodeId}::{e.FlowDirection}").ToList();
        var aggregatedEdges = new List<DiagramEdge>();

        foreach (var group in groups)
        {
            var list = group.ToList();
            var first = list[0];
            bool isInbound = first.FlowDirection == TopologyFlowDirection.Inbound;
            string layer = isInbound ? "layer_inbound" : "layer_outbound";
            string styleKey = isInbound ? "Inbound" : "Outbound";

            if (aggregationMode == EdgeAggregationMode.AdaptiveThreshold && list.Count <= 3)
            {
                // One fanned-out edge per port up to 3 ports
                foreach (var e in list)
                {
                    aggregatedEdges.Add(new DiagramEdge(
                        Id: e.EdgeId,
                        SourceNodeId: e.SourceNodeId,
                        TargetNodeId: e.TargetNodeId,
                        TargetPortLabel: $":{e.TargetPort}/{e.Protocol}",
                        LayerId: layer,
                        StyleKey: styleKey,
                        Metadata: new Dictionary<string, string>
                        {
                            ["port"] = e.TargetPort.ToString(CultureInfo.InvariantCulture),
                            ["protocol"] = e.Protocol.ToString(),
                            ["observations"] = e.ObservationCount.ToString(CultureInfo.InvariantCulture)
                        }));
                }
            }
            else
            {
                // Beyond that, a single aggregated edge
                string label = FormatPortsLabel(list);

                long totalObservations = list.Sum(e => e.ObservationCount);

                aggregatedEdges.Add(new DiagramEdge(
                    Id: $"agg_{first.SourceNodeId}_{first.TargetNodeId}_{first.FlowDirection}",
                    SourceNodeId: first.SourceNodeId,
                    TargetNodeId: first.TargetNodeId,
                    TargetPortLabel: label,
                    LayerId: layer,
                    StyleKey: styleKey,
                    Metadata: new Dictionary<string, string>
                    {
                        ["ports"] = string.Join(",", list.Select(e => e.TargetPort)),
                        ["port_count"] = list.Count.ToString(CultureInfo.InvariantCulture),
                        ["total_observations"] = totalObservations.ToString(CultureInfo.InvariantCulture)
                    }));
            }
        }

        return aggregatedEdges;
    }

    // Same grouping as the HTML map when its ports are hidden: one edge per unordered pair, whose
    // colour keeps the IN/OUT direction only when every merged flow shares it.
    private static List<DiagramEdge> BuildServerPairEdges(IReadOnlyList<TopologyServiceEdge> edges)
    {
        return edges
            .GroupBy(e => string.CompareOrdinal(e.SourceNodeId, e.TargetNodeId) <= 0
                ? (First: e.SourceNodeId, Second: e.TargetNodeId)
                : (First: e.TargetNodeId, Second: e.SourceNodeId))
            .Select(group =>
            {
                var list = group.ToList();
                var first = list[0];
                bool flowsBothWays = list.Exists(e => e.SourceNodeId != first.SourceNodeId);
                var directions = list.Select(e => e.FlowDirection).Distinct().ToList();
                var (layer, styleKey) = directions.Count > 1
                    ? (MixedLayerId, "Mixed")
                    : directions[0] == TopologyFlowDirection.Inbound ? ("layer_inbound", "Inbound") : ("layer_outbound", "Outbound");

                return new DiagramEdge(
                    Id: $"pair_{group.Key.First}_{group.Key.Second}",
                    SourceNodeId: first.SourceNodeId,
                    TargetNodeId: first.TargetNodeId,
                    TargetPortLabel: FormatPortsLabel(list),
                    LayerId: layer,
                    StyleKey: styleKey,
                    Direction: flowsBothWays ? DiagramEdgeDirection.Bidirectional : DiagramEdgeDirection.Directed,
                    Metadata: new Dictionary<string, string>
                    {
                        ["ports"] = string.Join(",", list.Select(e => e.TargetPort).Distinct()),
                        ["flow_count"] = list.Count.ToString(CultureInfo.InvariantCulture),
                        ["total_observations"] = list.Sum(e => e.ObservationCount).ToString(CultureInfo.InvariantCulture)
                    });
            })
            .ToList();
    }

    private static string FormatPortsLabel(IEnumerable<TopologyServiceEdge> edges)
    {
        var ports = edges.Select(e => $":{e.TargetPort}/{e.Protocol}").Distinct().ToList();
        return ports.Count <= 2
            ? string.Join(", ", ports)
            : $"{ports[0]}, {ports[1]} (+{ports.Count - 2})";
    }

    private static string ExtractSubnetPrefix(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress)) return "Unknown";
        if (PublicAddressClassifier.IsPublic(ipAddress)) return InternetZoneName;
        int lastDot = ipAddress.LastIndexOf('.');
        return lastDot > 0 ? ipAddress[..lastDot] + ".0/24" : "Other";
    }
}
