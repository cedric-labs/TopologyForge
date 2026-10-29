using TopologyForge.Models;

namespace TopologyForge.Builders;

/// <summary>
/// Builds a consolidated topology graph from network flow records.
/// </summary>
public interface ITopologyGraphBuilder
{
    /// <summary>
    /// Aggregates and deduplicates network flows into server nodes, non-ephemeral ports and dependency edges.
    /// </summary>
    /// <param name="flows">Raw observed flows.</param>
    /// <returns>The consolidated topology graph.</returns>
    NetworkTopologyGraph BuildGraph(IEnumerable<TopologyFlowRecord> flows);
}
