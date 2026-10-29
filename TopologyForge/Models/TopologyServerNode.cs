namespace TopologyForge.Models;

/// <summary>
/// Node representing a server (audited locally or remote) on the network map.
/// </summary>
/// <param name="NodeId">Unique canonical identifier of the node in the graph.</param>
/// <param name="HostName">Displayed host name of the server (or its IP address when unknown).</param>
/// <param name="IpAddress">Main IP address of the server.</param>
/// <param name="IsLocalAuditedServer">Whether this server is a direct audit source.</param>
/// <param name="ListeningPorts">Sorted non-ephemeral service ports exposed by this server.</param>
/// <param name="InboundConnectionCount">Number of inbound flows received by this server.</param>
/// <param name="OutboundConnectionCount">Number of outbound flows sent from this server.</param>
public sealed record TopologyServerNode(
    string NodeId,
    string HostName,
    string IpAddress,
    bool IsLocalAuditedServer,
    IReadOnlyList<ServerListenerDescriptor> ListeningPorts,
    int InboundConnectionCount,
    int OutboundConnectionCount);
