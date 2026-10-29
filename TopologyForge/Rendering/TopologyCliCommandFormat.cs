namespace TopologyForge.Rendering;

/// <summary>
/// Syntax of the command that the "CLI Command" section of the HTML map offers to copy to reproduce the current view.
/// The default values reproduce the historical NetFlowAudit 1.x syntax.
/// </summary>
public sealed record TopologyCliCommandFormat
{
    /// <summary>
    /// Start of the command, before the filter and rendering options (<c>NetFlowAudit.Cli --map-only</c> by default).
    /// </summary>
    public string Prefix { get; init; } = "NetFlowAudit.Cli --map-only";

    /// <summary>
    /// Option carrying the layout (<c>--map-layout</c> by default).
    /// </summary>
    public string LayoutOption { get; init; } = "--map-layout";

    /// <summary>
    /// Option carrying the sort method (<c>--map-sort</c> by default).
    /// </summary>
    public string SortOption { get; init; } = "--map-sort";

    /// <summary>
    /// Option carrying the arrow routing (<c>--map-routing</c> by default).
    /// </summary>
    public string RoutingOption { get; init; } = "--map-routing";

    /// <summary>
    /// Option carrying the spacing profile (<c>--map-spacing</c> by default).
    /// </summary>
    public string SpacingOption { get; init; } = "--map-spacing";

    /// <summary>
    /// Option carrying the arrow label content (<c>--map-edge-labels</c> by default), added with the value <c>none</c> when ports are hidden.
    /// </summary>
    public string EdgeLabelsOption { get; init; } = "--map-edge-labels";
}
