namespace TopologyForge.Rendering;

/// <summary>
/// How flow arrows between servers are drawn and routed.
/// </summary>
public enum TopologyEdgeRoutingMode
{
    /// <summary>
    /// Cubic Bézier curves that bend around intermediate server boxes and fan out on multiple links.
    /// </summary>
    BezierCurved = 0,

    /// <summary>
    /// Right-angled (90°) orthogonal corridors with offset lanes so that arrows do not overlap.
    /// </summary>
    OrthogonalCorridors = 1,

    /// <summary>
    /// Straight border-to-border segments with a parallel offset for multiple links.
    /// </summary>
    DirectStraight = 2,

    /// <summary>
    /// Orthogonal bus: the arrows of a server share one trunk per direction (IN, OUT) up to a separation line,
    /// then split into one branch per target. Designed for the tree layout, usable with the others.
    /// </summary>
    Bus = 3
}
