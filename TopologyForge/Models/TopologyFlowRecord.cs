namespace TopologyForge.Models;

/// <summary>
/// Enregistrement unitaire d'un flux ou d'un port d'écoute réseau destiné à la construction topologique.
/// </summary>
/// <param name="SourceServerName">Nom d'hôte du serveur local ou source.</param>
/// <param name="SourceIp">Adresse IP principale ou locale du serveur source.</param>
/// <param name="LocalPort">Port local observé sur le serveur source.</param>
/// <param name="RemoteServerName">Nom d'hôte résolu du serveur distant (vide si écoute locale ou non résolu).</param>
/// <param name="RemoteIp">Adresse IP du serveur distant.</param>
/// <param name="RemotePort">Port distant observé sur le serveur distant.</param>
/// <param name="Protocol">Protocole de transport (TCP ou UDP).</param>
/// <param name="Direction">Direction du flux (Listening, Inbound, Outbound).</param>
/// <param name="ProcessName">Nom du processus associé au flux.</param>
/// <param name="ServiceTarget">Cible d'exécution ou service sous-jacent résolu.</param>
/// <param name="ObservationCount">Nombre d'observations cumulées de ce flux.</param>
public sealed record TopologyFlowRecord(
    string SourceServerName,
    string SourceIp,
    int LocalPort,
    string RemoteServerName,
    string RemoteIp,
    int RemotePort,
    TopologyTransportProtocol Protocol,
    TopologyFlowDirection Direction,
    string ProcessName,
    string ServiceTarget,
    long ObservationCount = 1)
{
    /// <summary>
    /// Détermine le port de service persistant (port local pour Listening/Inbound, port distant pour Outbound).
    /// </summary>
    /// <returns>Numéro du port de service.</returns>
    public int GetServicePort() =>
        Direction == TopologyFlowDirection.Outbound ? RemotePort : LocalPort;
}
