namespace TopologyForge.Export.Drawio.Adapters;

using TopologyForge.Rendering;

/// <summary>
/// Controls how <see cref="TopologyToDrawioAdapter"/> places the target servers below the audited servers.
/// </summary>
public sealed record TopologyDrawioLayoutOptions
{
    /// <summary>
    /// Width, in diagram units, beyond which zones (or server cards when subnets are off) continue on a new row.
    /// </summary>
    public const double DefaultMaxRowWidth = 3200.0;

    /// <summary>
    /// Groups the target servers into one container per /24 subnet (<c>true</c> by default).
    /// When <c>false</c>, no container is written and the servers are laid out as a plain grid.
    /// </summary>
    public bool GroupBySubnets { get; init; } = true;

    /// <summary>
    /// Maximum width of a row of zones, or of a row of server cards when <see cref="GroupBySubnets"/> is <c>false</c>
    /// (<see cref="DefaultMaxRowWidth"/> by default). A single element wider than this still gets its own row.
    /// </summary>
    public double MaxRowWidth { get; init; } = DefaultMaxRowWidth;

    /// <summary>
    /// Order of the target servers, and of the zones for the subnet methods
    /// (<see cref="TopologyNodeSortMethod.BarycentricSugiyama"/> by default: zones by size, servers in graph order).
    /// </summary>
    public TopologyNodeSortMethod SortMethod { get; init; } = TopologyNodeSortMethod.BarycentricSugiyama;
}
