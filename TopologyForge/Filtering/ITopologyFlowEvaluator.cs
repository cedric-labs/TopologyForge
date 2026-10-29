using TopologyForge.Models;

namespace TopologyForge.Filtering;

/// <summary>
/// Evaluates the inclusion and exclusion rules on topology flow records.
/// </summary>
public interface ITopologyFlowEvaluator
{
    /// <summary>
    /// Determines whether a flow record must be included in the final map.
    /// </summary>
    /// <param name="flow">The flow record to evaluate.</param>
    /// <returns><c>true</c> if the flow meets every inclusion and exclusion criterion; otherwise <c>false</c>.</returns>
    bool ShouldInclude(TopologyFlowRecord flow);
}
