namespace TopologyForge.Models;

/// <summary>
/// Layer 4 network transport protocol.
/// </summary>
public enum TopologyTransportProtocol
{
    /// <summary>
    /// Connection-oriented TCP protocol.
    /// </summary>
    Tcp = 0,

    /// <summary>
    /// Connectionless UDP datagram protocol.
    /// </summary>
    Udp = 1
}
