namespace TopologyForge.Models;

/// <summary>
/// Descripteur d'un port de service non éphémère hébergé sur un serveur.
/// </summary>
/// <param name="Port">Numéro du port d'écoute ou de service.</param>
/// <param name="Protocol">Protocole de transport (TCP/UDP).</param>
/// <param name="ProcessName">Processus exposant le port (si connu).</param>
/// <param name="ServiceTarget">Cible applicative ou module exécuté (si connu).</param>
public sealed record ServerListenerDescriptor(
    int Port,
    TopologyTransportProtocol Protocol,
    string ProcessName,
    string ServiceTarget);
