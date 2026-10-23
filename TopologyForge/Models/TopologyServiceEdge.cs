namespace TopologyForge.Models;

/// <summary>
/// Lien orienté représentant une dépendance réseau d'un serveur client vers le port de service d'un serveur cible.
/// </summary>
/// <param name="EdgeId">Identifiant unique du lien dans le graphe.</param>
/// <param name="SourceNodeId">Identifiant du serveur initiateur du flux.</param>
/// <param name="TargetNodeId">Identifiant du serveur hébergeant le port de service cible.</param>
/// <param name="TargetPort">Numéro du port de service cible (non éphémère).</param>
/// <param name="Protocol">Protocole de transport (TCP/UDP).</param>
/// <param name="ProcessName">Processus associé au flux.</param>
/// <param name="ServiceTarget">Cible d'exécution associée au flux.</param>
/// <param name="ObservationCount">Volume cumulé d'observations sur ce lien.</param>
public sealed record TopologyServiceEdge(
    string EdgeId,
    string SourceNodeId,
    string TargetNodeId,
    int TargetPort,
    TopologyTransportProtocol Protocol,
    string ProcessName,
    string ServiceTarget,
    long ObservationCount);
