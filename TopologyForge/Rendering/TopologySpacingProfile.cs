namespace TopologyForge.Rendering;

/// <summary>
/// Spacing between server boxes and between parallel arrows.
/// </summary>
public enum TopologySpacingProfile
{
    /// <summary>
    /// Dense layout that minimises the space between servers.
    /// </summary>
    Compact = 0,

    /// <summary>
    /// Balanced layout (default).
    /// </summary>
    Balanced = 1,

    /// <summary>
    /// Airy layout with wide routing corridors, suited to large estates (> 100 servers).
    /// </summary>
    Spacious = 2
}
