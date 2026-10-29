namespace TopologyForge.Export.Drawio.Styling;

/// <summary>
/// Style visuel d'un nœud dans le diagramme Draw.io.
/// </summary>
public sealed record DiagramNodeStyle(
    string FillColor,
    string StrokeColor,
    int StrokeWidth = 2,
    bool Rounded = true,
    int ArcSize = 10,
    bool Shadow = true,
    string FontColor = "#ffffff",
    string? FontFamily = "'Segoe UI', Arial, sans-serif",
    int FontSize = 12,
    bool Dashed = false,
    double Opacity = 100.0);

/// <summary>
/// Style visuel d'un conteneur (zone / swimlane) dans le diagramme Draw.io.
/// </summary>
public sealed record DiagramContainerStyle(
    string FillColor,
    string StrokeColor,
    int StrokeWidth = 2,
    int HeaderHeight = 28,
    bool Rounded = true,
    int ArcSize = 8,
    bool Dashed = true,
    string FontColor = "#cbd5e1",
    int FontSize = 12);

/// <summary>
/// Style visuel d'une arête (lien / flux) dans le diagramme Draw.io.
/// </summary>
public sealed record DiagramEdgeStyle(
    string StrokeColor,
    int StrokeWidth = 2,
    string? FontColor = "#94a3b8",
    int FontSize = 10,
    bool Dashed = false,
    string ArrowEnd = "classic",
    string ArrowStart = "none");

/// <summary>
/// Thème visuel complet pour le canevas, les conteneurs, les nœuds par rôle et les flux par clé de style.
/// </summary>
public sealed record DiagramTheme(
    string Name,
    string CanvasBackgroundColor,
    DiagramContainerStyle ContainerStyle,
    IReadOnlyDictionary<string, DiagramNodeStyle> RoleStyles,
    IReadOnlyDictionary<string, DiagramEdgeStyle> FlowStyles)
{
    /// <summary>
    /// Thème sombre moderne optimisé pour les cartes d'infrastructure.
    /// </summary>
    public static DiagramTheme DarkModern => new(
        Name: "DarkModern",
        CanvasBackgroundColor: "#0f172a",
        ContainerStyle: new DiagramContainerStyle(
            FillColor: "#1e293b",
            StrokeColor: "#475569",
            StrokeWidth: 2,
            HeaderHeight: 28,
            Rounded: true,
            ArcSize: 8,
            Dashed: true,
            FontColor: "#f8fafc",
            FontSize: 12),
        RoleStyles: new Dictionary<string, DiagramNodeStyle>(StringComparer.OrdinalIgnoreCase)
        {
            ["Standard"] = new(
                FillColor: "#1e293b",
                StrokeColor: "#38bdf8",
                StrokeWidth: 2,
                Rounded: true,
                ArcSize: 10,
                Shadow: true,
                FontColor: "#f8fafc"),
            ["AuditedRoot"] = new(
                FillColor: "#1e293b",
                StrokeColor: "#f59e0b",
                StrokeWidth: 3,
                Rounded: true,
                ArcSize: 10,
                Shadow: true,
                FontColor: "#f8fafc"),
            ["Database"] = new(
                FillColor: "#1e293b",
                StrokeColor: "#10b981",
                StrokeWidth: 2,
                Rounded: true,
                ArcSize: 10,
                Shadow: true,
                FontColor: "#f8fafc"),
            ["ExternalGateway"] = new(
                FillColor: "#1e293b",
                StrokeColor: "#ec4899",
                StrokeWidth: 2,
                Rounded: true,
                ArcSize: 10,
                Shadow: true,
                FontColor: "#f8fafc")
        },
        FlowStyles: new Dictionary<string, DiagramEdgeStyle>(StringComparer.OrdinalIgnoreCase)
        {
            ["Default"] = new(StrokeColor: "#38bdf8", StrokeWidth: 2, FontColor: "#94a3b8"),
            ["Inbound"] = new(StrokeColor: "#10b981", StrokeWidth: 2, FontColor: "#6ee7b7"),
            ["Outbound"] = new(StrokeColor: "#38bdf8", StrokeWidth: 2, FontColor: "#93c5fd"),
            ["Mixed"] = new(StrokeColor: "#8b9dc3", StrokeWidth: 2, FontColor: "#cbd5e1"),
            ["Highlighted"] = new(StrokeColor: "#f59e0b", StrokeWidth: 3, FontColor: "#fbbf24")
        });

    /// <summary>
    /// Thème clair d'entreprise adapté aux impressions et documents institutionnels.
    /// </summary>
    public static DiagramTheme EnterpriseLight => new(
        Name: "EnterpriseLight",
        CanvasBackgroundColor: "#ffffff",
        ContainerStyle: new DiagramContainerStyle(
            FillColor: "#f8fafc",
            StrokeColor: "#cbd5e1",
            StrokeWidth: 2,
            HeaderHeight: 28,
            Rounded: true,
            ArcSize: 8,
            Dashed: true,
            FontColor: "#334155",
            FontSize: 12),
        RoleStyles: new Dictionary<string, DiagramNodeStyle>(StringComparer.OrdinalIgnoreCase)
        {
            ["Standard"] = new(
                FillColor: "#ffffff",
                StrokeColor: "#0284c7",
                StrokeWidth: 2,
                Rounded: true,
                ArcSize: 10,
                Shadow: true,
                FontColor: "#0f172a"),
            ["AuditedRoot"] = new(
                FillColor: "#ffffff",
                StrokeColor: "#d97706",
                StrokeWidth: 3,
                Rounded: true,
                ArcSize: 10,
                Shadow: true,
                FontColor: "#0f172a"),
            ["Database"] = new(
                FillColor: "#ffffff",
                StrokeColor: "#059669",
                StrokeWidth: 2,
                Rounded: true,
                ArcSize: 10,
                Shadow: true,
                FontColor: "#0f172a"),
            ["ExternalGateway"] = new(
                FillColor: "#ffffff",
                StrokeColor: "#db2777",
                StrokeWidth: 2,
                Rounded: true,
                ArcSize: 10,
                Shadow: true,
                FontColor: "#0f172a")
        },
        FlowStyles: new Dictionary<string, DiagramEdgeStyle>(StringComparer.OrdinalIgnoreCase)
        {
            ["Default"] = new(StrokeColor: "#0284c7", StrokeWidth: 2, FontColor: "#475569"),
            ["Inbound"] = new(StrokeColor: "#059669", StrokeWidth: 2, FontColor: "#047857"),
            ["Outbound"] = new(StrokeColor: "#0284c7", StrokeWidth: 2, FontColor: "#0369a1"),
            ["Mixed"] = new(StrokeColor: "#64748b", StrokeWidth: 2, FontColor: "#475569"),
            ["Highlighted"] = new(StrokeColor: "#d97706", StrokeWidth: 3, FontColor: "#b45309")
        });
}
