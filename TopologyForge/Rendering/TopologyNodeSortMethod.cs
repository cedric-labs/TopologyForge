namespace TopologyForge.Rendering;

/// <summary>
/// Algorithm used to sort and order the servers on the map.
/// </summary>
public enum TopologyNodeSortMethod
{
    /// <summary>
    /// Multi-layer barycentric sort (Sugiyama heuristic) that places interconnected servers face to face to minimise arrow crossings.
    /// </summary>
    BarycentricSugiyama = 0,

    /// <summary>
    /// Sort by traffic density (total inbound and outbound connections, descending).
    /// </summary>
    ConnectionDegree = 1,

    /// <summary>
    /// Strict alphabetical sort by server host name.
    /// </summary>
    Alphabetical = 2,

    /// <summary>
    /// Logical sort by subnet and IP address octets.
    /// </summary>
    SubnetIp = 3,

    /// <summary>
    /// Sort by functional tier and architecture layer (Clients ➔ Gateways/Ingress ➔ Applications ➔ Databases/Infra).
    /// </summary>
    FunctionalTier = 4,

    /// <summary>
    /// Grouping by IP subnet and contiguous network security zones.
    /// </summary>
    SubnetCluster = 5
}
