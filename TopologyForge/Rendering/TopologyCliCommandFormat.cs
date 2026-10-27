namespace TopologyForge.Rendering;

/// <summary>
/// Syntaxe de la commande que la section « CLI Command » de la carte HTML propose de copier pour reproduire la vue affichée.
/// Les valeurs par défaut reproduisent la syntaxe historique de NetFlowAudit 1.x.
/// </summary>
public sealed record TopologyCliCommandFormat
{
    /// <summary>
    /// Début de la commande, avant les options de filtre et de rendu (par défaut <c>NetFlowAudit.Cli --map-only</c>).
    /// </summary>
    public string Prefix { get; init; } = "NetFlowAudit.Cli --map-only";

    /// <summary>
    /// Option portant la disposition (par défaut <c>--map-layout</c>).
    /// </summary>
    public string LayoutOption { get; init; } = "--map-layout";

    /// <summary>
    /// Option portant la méthode de tri (par défaut <c>--map-sort</c>).
    /// </summary>
    public string SortOption { get; init; } = "--map-sort";

    /// <summary>
    /// Option portant le routage des flèches (par défaut <c>--map-routing</c>).
    /// </summary>
    public string RoutingOption { get; init; } = "--map-routing";

    /// <summary>
    /// Option portant le profil d'écartement (par défaut <c>--map-spacing</c>).
    /// </summary>
    public string SpacingOption { get; init; } = "--map-spacing";
}
