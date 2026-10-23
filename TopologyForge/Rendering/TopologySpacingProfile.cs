namespace TopologyForge.Rendering;

/// <summary>
/// Profil d'espacement entre les boîtes de serveurs et d'écartement des flèches parallèles.
/// </summary>
public enum TopologySpacingProfile
{
    /// <summary>
    /// Disposition dense minimisant l'espace entre les serveurs.
    /// </summary>
    Compact = 0,

    /// <summary>
    /// Disposition équilibrée par défaut.
    /// </summary>
    Balanced = 1,

    /// <summary>
    /// Disposition aérée avec larges couloirs de routage adaptée aux grands parcs (> 100 serveurs).
    /// </summary>
    Spacious = 2
}
