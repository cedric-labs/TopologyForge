namespace TopologyForge.Models;

/// <summary>
/// Protocole de transport réseau de couche 4.
/// </summary>
public enum TopologyTransportProtocol
{
    /// <summary>
    /// Protocole orienté connexion TCP.
    /// </summary>
    Tcp = 0,

    /// <summary>
    /// Protocole datagramme sans connexion UDP.
    /// </summary>
    Udp = 1
}
