namespace TopologyForge.Models;

/// <summary>
/// Nœud représentant un serveur (local audité ou distant) dans la cartographie réseau.
/// </summary>
/// <param name="NodeId">Identifiant canonique unique du nœud dans le graphe.</param>
/// <param name="HostName">Nom d'hôte affiché du serveur (ou adresse IP à défaut).</param>
/// <param name="IpAddress">Adresse IP principale associée au serveur.</param>
/// <param name="IsLocalAuditedServer">Indique si ce serveur est une source d'audit directe.</param>
/// <param name="ListeningPorts">Liste triée des ports de service non éphémères exposés par ce serveur.</param>
/// <param name="InboundConnectionCount">Nombre de flux entrants reçus par ce serveur.</param>
/// <param name="OutboundConnectionCount">Nombre de flux sortants émis depuis ce serveur.</param>
public sealed record TopologyServerNode(
    string NodeId,
    string HostName,
    string IpAddress,
    bool IsLocalAuditedServer,
    IReadOnlyList<ServerListenerDescriptor> ListeningPorts,
    int InboundConnectionCount,
    int OutboundConnectionCount);
