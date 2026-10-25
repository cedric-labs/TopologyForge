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
    SubnetIp = 3,

    /// <summary>
    /// Tri par tiers fonctionnels et couches d'architecture (Clients ➔ Passerelles/Ingress ➔ Applications ➔ Bases de données/Infra).
    /// </summary>
    FunctionalTier = 4,

    /// <summary>
    /// Regroupement par sous-réseau IP et zones de sécurité réseau contiguës.
    /// </summary>
    SubnetCluster = 5
}
