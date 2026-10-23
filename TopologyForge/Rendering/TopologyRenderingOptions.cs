namespace TopologyForge.Rendering;

/// <summary>
/// Options de rendu visuel, de tri, de routage de flèches, d'écartement et de pagination pour la cartographie HTML autonome.
/// </summary>
public sealed record TopologyRenderingOptions
{
    /// <summary>
    /// Titre principal affiché dans l'en-tête de la cartographie.
    /// </summary>
    public string Title { get; init; } = "Network Flow & Server Topology Map";

    /// <summary>
    /// Méthode de tri initiale des serveurs (Sugiyama anti-croisement par défaut).
    /// </summary>
    public TopologyNodeSortMethod DefaultSortMethod { get; init; } = TopologyNodeSortMethod.BarycentricSugiyama;

    /// <summary>
    /// Mode de routage initial des flèches (Courbes de Bézier avec contournement par défaut).
    /// </summary>
    public TopologyEdgeRoutingMode DefaultRoutingMode { get; init; } = TopologyEdgeRoutingMode.BezierCurved;

    /// <summary>
    /// Profil d'écartement initial entre serveurs et flèches.
    /// </summary>
    public TopologySpacingProfile DefaultSpacingProfile { get; init; } = TopologySpacingProfile.Balanced;

    /// <summary>
    /// Nombre initial de serveurs affichés par page (100 par défaut).
    /// </summary>
    public int DefaultPageSize { get; init; } = 100;
}
