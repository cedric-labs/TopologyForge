namespace TopologyForge.Rendering;

/// <summary>
/// Contenu des étiquettes affichées sur les flèches de la cartographie HTML.
/// </summary>
public enum TopologyEdgeLabelMode
{
    /// <summary>
    /// Chaque flèche porte son sens, son port et son protocole (par exemple <c>OUT :443/TCP</c>).
    /// </summary>
    Ports = 0,

    /// <summary>
    /// Aucune étiquette sur les flèches ; les ports restent listés dans l'inspecteur de serveur.
    /// </summary>
    None = 1
}
