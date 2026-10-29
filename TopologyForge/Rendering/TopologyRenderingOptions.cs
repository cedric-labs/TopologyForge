namespace TopologyForge.Rendering;

/// <summary>
/// Visual, sort, layout, arrow routing, spacing and pagination options for the self-contained HTML map.
/// </summary>
public sealed record TopologyRenderingOptions
{
    /// <summary>
    /// Main title shown in the map header.
    /// </summary>
    public string Title { get; init; } = "Network Flow & Server Topology Map";

    /// <summary>
    /// Initial geometric layout of the nodes (hierarchical by default).
    /// </summary>
    public TopologyLayoutMode DefaultLayoutMode { get; init; } = TopologyLayoutMode.Layered;

    /// <summary>
    /// Initial server sort method (crossing-avoiding Sugiyama by default).
    /// </summary>
    public TopologyNodeSortMethod DefaultSortMethod { get; init; } = TopologyNodeSortMethod.BarycentricSugiyama;

    /// <summary>
    /// Initial arrow routing mode (Bézier curves that bend around servers by default).
    /// </summary>
    public TopologyEdgeRoutingMode DefaultRoutingMode { get; init; } = TopologyEdgeRoutingMode.BezierCurved;

    /// <summary>
    /// Initial spacing profile between servers and arrows.
    /// </summary>
    public TopologySpacingProfile DefaultSpacingProfile { get; init; } = TopologySpacingProfile.Balanced;

    /// <summary>
    /// Initial content of the arrow labels (ports shown by default).
    /// </summary>
    public TopologyEdgeLabelMode DefaultEdgeLabelMode { get; init; } = TopologyEdgeLabelMode.Ports;

    /// <summary>
    /// Initial number of servers shown per page (100 by default).
    /// </summary>
    public int DefaultPageSize { get; init; } = 100;

    /// <summary>
    /// Default port inclusion filters passed to the web page.
    /// </summary>
    public string InitialIncludePorts { get; init; } = string.Empty;

    /// <summary>
    /// Default port exclusion filters passed to the web page.
    /// </summary>
    public string InitialExcludePorts { get; init; } = string.Empty;

    /// <summary>
    /// Server or IP address exclusion filters passed to the web page.
    /// </summary>
    public string InitialExcludeHosts { get; init; } = string.Empty;

    /// <summary>
    /// Flow direction filter passed to the web page (all, OUT or IN).
    /// </summary>
    public string InitialDirection { get; init; } = "all";

    /// <summary>
    /// Syntax of the command offered for copying in the "CLI Command" section (NetFlowAudit 1.x syntax by default).
    /// </summary>
    public TopologyCliCommandFormat CliCommandFormat { get; init; } = new();
}
