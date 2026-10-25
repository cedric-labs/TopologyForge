namespace TopologyForge.Rendering;

/// <summary>
/// Disposition géométrique des serveurs sur la cartographie réseau.
/// </summary>
public enum TopologyLayoutMode
{
    /// <summary>
    /// Disposition hiérarchique étagée en colonnes ou anneaux concentriques autour du serveur audité.
    /// </summary>
    Layered = 0,

    /// <summary>
    /// Disposition circulaire uniforme en anneau.
    /// </summary>
    Circular = 1,

    /// <summary>
    /// Disposition matricielle en grille multi-colonnes.
    /// </summary>
    Grid = 2,

    /// <summary>
    /// Disposition organique dirigée par les forces physiques (attraction par ressorts et répulsion coulombienne).
    /// </summary>
    ForceDirectedOrganic = 3
}
