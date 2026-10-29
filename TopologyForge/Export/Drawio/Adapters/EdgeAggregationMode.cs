namespace TopologyForge.Export.Drawio.Adapters;

/// <summary>
/// How flows are grouped and aggregated on the Draw.io map.
/// </summary>
public enum EdgeAggregationMode
{
    /// <summary>
    /// One edge per flow up to 3 ports (with a geometric offset), merged into a single trunk beyond that.
    /// </summary>
    AdaptiveThreshold,

    /// <summary>
    /// Merges every flow sharing the same source, target and direction into a single edge.
    /// </summary>
    ByDirection,

    /// <summary>
    /// Keeps one edge per listening port, with anchors spread over the whole side.
    /// </summary>
    None,

    /// <summary>
    /// Merges every flow of a server pair, whatever their orientation and direction,
    /// into a single edge: double-headed when flows go both ways, with the <c>Mixed</c> style
    /// (<c>layer_mixed</c> layer) when the merged flows do not share the same direction.
    /// </summary>
    ByServerPair
}
