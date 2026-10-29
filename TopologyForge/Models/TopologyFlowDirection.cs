namespace TopologyForge.Models;

/// <summary>
/// Direction of an observed network flow.
/// </summary>
public enum TopologyFlowDirection
{
    /// <summary>
    /// Local port listening passively (server hosting a service).
    /// </summary>
    Listening = 0,

    /// <summary>
    /// Inbound connection from a remote client to a local service port.
    /// </summary>
    Inbound = 1,

    /// <summary>
    /// Outbound connection from the local server to a service port on a remote server.
    /// </summary>
    Outbound = 2
}
