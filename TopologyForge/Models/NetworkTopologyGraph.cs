namespace TopologyForge.Models;

/// <summary>
/// Graphe complet consolidant les serveurs, leurs ports non éphémères et leurs interconnexions.
/// </summary>
/// <param name="GeneratedAtUtc">Horodatage UTC de génération du graphe.</param>
/// <param name="Nodes">Liste des serveurs cartographiés.</param>
/// <param name="Edges">Liste des liens orientés de dépendance entre serveurs.</param>
/// <param name="TotalProcessedFlows">Nombre total d'entrées de flux examinées.</param>
/// <param name="ExcludedFlowsCount">Nombre d'entrées écartées par les filtres d'inclusion/exclusion.</param>
public sealed record NetworkTopologyGraph(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<TopologyServerNode> Nodes,
    IReadOnlyList<TopologyServiceEdge> Edges,
    int TotalProcessedFlows,
    int ExcludedFlowsCount);
