using TopologyForge.Models;

namespace TopologyForge.Filtering;

/// <summary>
/// Contrat d'évaluation des règles d'inclusion et d'exclusion sur les enregistrements de flux topologiques.
/// </summary>
public interface ITopologyFlowEvaluator
{
    /// <summary>
    /// Détermine si un enregistrement de flux doit être inclus dans la cartographie finale.
    /// </summary>
    /// <param name="flow">Enregistrement de flux à évaluer.</param>
    /// <returns><c>true</c> si le flux respecte l'ensemble des critères d'inclusion et d'exclusion ; sinon <c>false</c>.</returns>
    bool ShouldInclude(TopologyFlowRecord flow);
}
