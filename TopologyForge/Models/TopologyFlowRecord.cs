namespace TopologyForge.Models;

/// <summary>
/// A single network flow or listening port record used to build the topology.
/// </summary>
/// <param name="SourceServerName">Host name of the local or source server.</param>
/// <param name="SourceIp">Main or local IP address of the source server.</param>
/// <param name="LocalPort">Local port observed on the source server.</param>
/// <param name="RemoteServerName">Resolved host name of the remote server (empty when listening locally or unresolved).</param>
/// <param name="RemoteIp">IP address of the remote server.</param>
/// <param name="RemotePort">Remote port observed on the remote server.</param>
/// <param name="Protocol">Transport protocol (TCP or UDP).</param>
/// <param name="Direction">Flow direction (Listening, Inbound, Outbound).</param>
/// <param name="ProcessName">Name of the process associated with the flow.</param>
/// <param name="ServiceTarget">Resolved execution target or underlying service.</param>
/// <param name="ObservationCount">Cumulative number of observations of this flow.</param>
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
    /// Returns the persistent service port (local port for Listening/Inbound, remote port for Outbound).
    /// </summary>
    /// <returns>The service port number.</returns>
    public int GetServicePort() =>
        Direction == TopologyFlowDirection.Outbound ? RemotePort : LocalPort;
}
