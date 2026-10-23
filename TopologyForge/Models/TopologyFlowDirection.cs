namespace TopologyForge.Models;

/// <summary>
/// Sens directionnel d'un flux réseau observé.
/// </summary>
public enum TopologyFlowDirection
{
    /// <summary>
    /// Port local en écoute passive (serveur hébergeant un service).
    /// </summary>
    Listening = 0,

    /// <summary>
    /// Connexion entrante d'un client distant vers un port de service local.
    /// </summary>
    Inbound = 1,

    /// <summary>
    /// Connexion sortante du serveur local vers un port de service d'un serveur distant.
    /// </summary>
    Outbound = 2
}
