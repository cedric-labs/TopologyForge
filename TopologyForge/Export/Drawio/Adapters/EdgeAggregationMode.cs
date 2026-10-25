namespace TopologyForge.Export.Drawio.Adapters;

/// <summary>
/// Stratégie de regroupement et d'agrégation des flux pour la cartographie Draw.io.
/// </summary>
public enum EdgeAggregationMode
{
    /// <summary>
    /// Flux individuel jusqu'à 3 ports (avec décalage géométrique), fusion automatique en câble unique au-delà.
    /// </summary>
    AdaptiveThreshold,

    /// <summary>
    /// Fusionne tous les flux partageant la même source, la même cible et la même direction en une seule arête.
    /// </summary>
    ByDirection,

    /// <summary>
    /// Conserve une arête distincte par port d'écoute avec répartition complète des ancres.
    /// </summary>
    None
}
