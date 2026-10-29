namespace TopologyForge.Rendering;

/// <summary>
/// Mode de tracé et de routage géométrique des flèches de flux entre les serveurs.
/// </summary>
public enum TopologyEdgeRoutingMode
{
    /// <summary>
    /// Courbes de Bézier cubiques avec déviation automatique autour des boîtes de serveurs intermédiaires et éventail sur liens multiples.
    /// </summary>
    BezierCurved = 0,

    /// <summary>
    /// Couloirs orthogonaux à angles droits (90°) avec pistes décalées anti-chevauchement.
    /// </summary>
    OrthogonalCorridors = 1,

    /// <summary>
    /// Segments directs de bordure à bordure avec décalage parallèle pour les liens multiples.
    /// </summary>
    DirectStraight = 2,

    /// <summary>
    /// Bus orthogonal : les flèches d'un même serveur partagent un tronc par sens (IN, OUT) jusqu'à une ligne de séparation,
    /// puis se divisent en une branche par cible. Pensé pour la mise en page en arbre, utilisable avec les autres.
    /// </summary>
    Bus = 3
}
