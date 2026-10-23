using TopologyForge.Models;

namespace TopologyForge.Builders;

/// <summary>
/// Contrat de construction d'un graphe topologique consolidé à partir d'enregistrements de flux réseau.
/// </summary>
public interface ITopologyGraphBuilder
{
    /// <summary>
    /// Agrège et déduplique les flux réseau en nœuds serveurs, ports non éphémères et liens de dépendance.
    /// </summary>
    /// <param name="flows">Collection des flux bruts observés.</param>
    /// <returns>Graphe topologique consolidé.</returns>
    NetworkTopologyGraph BuildGraph(IEnumerable<TopologyFlowRecord> flows);
}
