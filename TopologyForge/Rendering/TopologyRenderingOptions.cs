namespace TopologyForge.Rendering;

/// <summary>
/// Options de rendu visuel, de tri, de routage de flèches et d'écartement pour la cartographie HTML autonome.
/// </summary>
public sealed record TopologyRenderingOptions
{
    /// <summary>
    /// Titre principal affiché dans l'en-tête de la cartographie.
    /// </summary>
    public string Title { get; init; } = "Cartographie des Flux & Serveurs";

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
}
