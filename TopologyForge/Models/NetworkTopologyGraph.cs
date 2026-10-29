namespace TopologyForge.Models;

/// <summary>
/// Complete graph consolidating servers, their non-ephemeral ports and their interconnections.
/// </summary>
/// <param name="GeneratedAtUtc">UTC timestamp at which the graph was generated.</param>
/// <param name="Nodes">Mapped servers.</param>
/// <param name="Edges">Directed dependency edges between servers.</param>
/// <param name="TotalProcessedFlows">Total number of flow entries examined.</param>
/// <param name="ExcludedFlowsCount">Number of entries discarded by the inclusion/exclusion filters.</param>
public sealed record NetworkTopologyGraph(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<TopologyServerNode> Nodes,
    IReadOnlyList<TopologyServiceEdge> Edges,
    int TotalProcessedFlows,
    int ExcludedFlowsCount);
