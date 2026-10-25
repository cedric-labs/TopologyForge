namespace TopologyForge.Export.Drawio.Model;

using System.Runtime.InteropServices;

/// <summary>
/// Représente un document de diagramme complet prêt pour la sérialisation Draw.io.
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
/// Calque Draw.io permettant d'afficher ou masquer sélectivement des familles d'éléments.
/// </summary>
public sealed record DiagramLayer(
    string Id,
    string Name,
    bool Visible = true,
    bool Locked = false);

/// <summary>
/// Conteneur hiérarchique (ex: Zone, Sous-réseau, VPC, Département).
/// Coordonnées TOUJOURS en repère absolu.
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
/// Nœud géométrique représentant une entité, un équipement ou une carte.
/// Coordonnées TOUJOURS en repère absolu.
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
/// Connexion orientée entre deux nœuds avec ancres relatives, waypoints absolus et échelonnement d'étiquettes.
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
/// Point d'ancrage relatif sur le périmètre de la boîte (valeurs normalisées de 0.0 à 1.0).
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DiagramAnchorPoint(double X, double Y, bool Perimeter = false);

/// <summary>
/// Point géométrique en coordonnées canevas absolues.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DiagramPoint(double X, double Y);

/// <summary>
/// Formes géométriques supportées pour un nœud de diagramme.
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
/// Direction de la relation représentée par l'arête.
/// </summary>
public enum DiagramEdgeDirection
{
    Directed,
    Bidirectional,
    Undirected
}

/// <summary>
/// Type de routage géométrique de l'arête.
/// </summary>
public enum DiagramEdgeRouting
{
    Orthogonal,
    Curved,
    Straight
}
