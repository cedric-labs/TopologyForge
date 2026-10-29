namespace TopologyForge.Export.Drawio.Model;

using System.Runtime.InteropServices;

/// <summary>
/// A complete diagram document ready for Draw.io serialization.
/// </summary>
public sealed record DiagramDocument(
    string Title,
    IReadOnlyList<DiagramLayer> Layers,
    IReadOnlyList<DiagramContainer> Containers,
    IReadOnlyList<DiagramNode> Nodes,
    IReadOnlyList<DiagramEdge> Edges,
    string? BackgroundColor = null,
    int GridSize = 10);

/// <summary>
/// Draw.io layer used to show or hide families of elements selectively.
/// </summary>
public sealed record DiagramLayer(
    string Id,
    string Name,
    bool Visible = true,
    bool Locked = false);

/// <summary>
/// Hierarchical container (e.g. zone, subnet, VPC, department).
/// Coordinates are ALWAYS absolute.
/// </summary>
public sealed record DiagramContainer(
    string Id,
    string Label,
    double X,
    double Y,
    double Width,
    double Height,
    string LayerId = "layer_infra",
    string? ParentContainerId = null,
    bool IsFoldable = true,
    IReadOnlyDictionary<string, string>? Metadata = null,
    Styling.DiagramContainerStyle? CustomStyle = null);

/// <summary>
/// Geometric node representing an entity, a device or a card.
/// Coordinates are ALWAYS absolute.
/// </summary>
public sealed record DiagramNode(
    string Id,
    string Label,
    string? Subtitle,
    double X,
    double Y,
    double Width,
    double Height,
    string LayerId = "layer_infra",
    string? ContainerId = null,
    DiagramNodeShape Shape = DiagramNodeShape.RoundedRectangle,
    string RoleKey = "Standard",
    IReadOnlyList<string>? Badges = null,
    string? TooltipTemplate = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    Styling.DiagramNodeStyle? CustomStyle = null);

/// <summary>
/// Directed connection between two nodes, with relative anchors, absolute waypoints and staggered labels.
/// </summary>
public sealed record DiagramEdge(
    string Id,
    string SourceNodeId,
    string TargetNodeId,
    string? CenterLabel = null,
    string? SourcePortLabel = null,
    string? TargetPortLabel = null,
    double TargetLabelPosition = 0.8,
    double CenterLabelPosition = 0.0,
    string LayerId = "layer_flows",
    string StyleKey = "Default",
    DiagramAnchorPoint? ExitAnchor = null,
    DiagramAnchorPoint? EntryAnchor = null,
    IReadOnlyList<DiagramPoint>? Waypoints = null,
    DiagramEdgeDirection Direction = DiagramEdgeDirection.Directed,
    DiagramEdgeRouting Routing = DiagramEdgeRouting.Orthogonal,
    IReadOnlyDictionary<string, string>? Metadata = null,
    Styling.DiagramEdgeStyle? CustomStyle = null);

/// <summary>
/// Relative anchor point on the box perimeter (normalised values from 0.0 to 1.0).
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DiagramAnchorPoint(double X, double Y, bool Perimeter = false);

/// <summary>
/// Geometric point in absolute canvas coordinates.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DiagramPoint(double X, double Y);

/// <summary>
/// Geometric shapes supported for a diagram node.
/// </summary>
public enum DiagramNodeShape
{
    Rectangle,
    RoundedRectangle,
    RichCardHtml,
    Cylinder,
    Cloud,
    Hexagon
}

/// <summary>
/// Direction of the relation the edge represents.
/// </summary>
public enum DiagramEdgeDirection
{
    Directed,
    Bidirectional,
    Undirected
}

/// <summary>
/// Geometric routing type of the edge.
/// </summary>
public enum DiagramEdgeRouting
{
    Orthogonal,
    Curved,
    Straight
}
