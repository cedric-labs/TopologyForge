namespace TopologyForge.Rendering;

/// <summary>
/// Geometric layout of the servers on the network map.
/// </summary>
public enum TopologyLayoutMode
{
    /// <summary>
    /// Tiered hierarchical layout in columns or concentric rings around the audited server.
    /// </summary>
    Layered = 0,

    /// <summary>
    /// Uniform circular layout on a ring.
    /// </summary>
    Circular = 1,

    /// <summary>
    /// Multi-column grid layout.
    /// </summary>
    Grid = 2,

    /// <summary>
    /// Organic force-directed layout (spring attraction and Coulomb repulsion).
    /// </summary>
    ForceDirectedOrganic = 3,

    /// <summary>
    /// Top-down multi-column tree layout (roots at the top, targets in compact columns below).
    /// </summary>
    Tree = 4,

    /// <summary>
    /// Horizontal tree layout (roots on the left, flows running from left to right).
    /// </summary>
    HorizontalTree = 5
}
