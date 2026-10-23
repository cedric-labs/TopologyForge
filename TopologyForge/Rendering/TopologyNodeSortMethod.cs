namespace TopologyForge.Rendering;

/// <summary>
/// Algorithme de tri et d'ordonnancement des serveurs sur la cartographie.
/// </summary>
public enum TopologyNodeSortMethod
{
    /// <summary>
    /// Tri barycentrique multicouche (heuristique de Sugiyama) plaçant face à face les serveurs interconnectés pour minimiser les croisements de flèches.
    /// </summary>
    BarycentricSugiyama = 0,

    /// <summary>
    /// Tri par densité de trafic (nombre total de connexions entrantes et sortantes décroissant).
    /// </summary>
    ConnectionDegree = 1,

    /// <summary>
    /// Tri alphabétique strict par nom d'hôte du serveur.
    /// </summary>
    Alphabetical = 2,

    /// <summary>
    /// Tri logique par sous-réseau et octets d'adresse IP.
    /// </summary>
    SubnetIp = 3
}
