using TopologyForge.Models;

namespace TopologyForge.Filtering;

/// <summary>
/// Declarative filtering options (inclusions and exclusions) for generating the network map.
/// </summary>
public sealed record TopologyFilterOptions
{
    /// <summary>
    /// Automatically excludes flows whose service port is in the ephemeral range (<c>true</c> by default).
    /// </summary>
    public bool ExcludeEphemeralPorts { get; init; } = true;

    /// <summary>
    /// Port range considered ephemeral (49152-65535 by default).
    /// </summary>
    public NetworkPortRange EphemeralPortRange { get; init; } = NetworkPortRange.DefaultEphemeralRange;

    /// <summary>
    /// Explicitly included service port ranges (when not empty, only these ports are kept).
    /// </summary>
    public IReadOnlyList<NetworkPortRange> IncludedPorts { get; init; } = [];

    /// <summary>
    /// Explicitly excluded service port ranges.
    /// </summary>
    public IReadOnlyList<NetworkPortRange> ExcludedPorts { get; init; } = [];

    /// <summary>
    /// Included host name patterns (accepts the <c>*</c> wildcard, case-insensitive).
    /// </summary>
    public IReadOnlyList<string> IncludedHostPatterns { get; init; } = [];

    /// <summary>
    /// Excluded host name patterns (accepts the <c>*</c> wildcard, case-insensitive).
    /// </summary>
    public IReadOnlyList<string> ExcludedHostPatterns { get; init; } = [];

    /// <summary>
    /// Explicitly included IP addresses or CIDR subnets.
    /// </summary>
    public IReadOnlyList<string> IncludedIpRanges { get; init; } = [];

    /// <summary>
    /// Explicitly excluded IP addresses or CIDR subnets.
    /// </summary>
    public IReadOnlyList<string> ExcludedIpRanges { get; init; } = [];

    /// <summary>
    /// Included process name or application target patterns.
    /// </summary>
    public IReadOnlyList<string> IncludedProcesses { get; init; } = [];

    /// <summary>
    /// Excluded process name or application target patterns.
    /// </summary>
    public IReadOnlyList<string> ExcludedProcesses { get; init; } = [];

    /// <summary>
    /// Includes passively listening ports (<see cref="TopologyFlowDirection.Listening"/>).
    /// </summary>
    public bool IncludeListeners { get; init; } = true;

    /// <summary>
    /// Includes inbound connections (<see cref="TopologyFlowDirection.Inbound"/>).
    /// </summary>
    public bool IncludeInboundFlows { get; init; } = true;

    /// <summary>
    /// Includes outbound connections (<see cref="TopologyFlowDirection.Outbound"/>).
    /// </summary>
    public bool IncludeOutboundFlows { get; init; } = true;
}
