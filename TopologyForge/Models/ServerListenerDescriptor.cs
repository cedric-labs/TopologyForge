namespace TopologyForge.Models;

/// <summary>
/// Describes a non-ephemeral service port hosted on a server.
/// </summary>
/// <param name="Port">Listening or service port number.</param>
/// <param name="Protocol">Transport protocol (TCP/UDP).</param>
/// <param name="ProcessName">Process exposing the port (if known).</param>
/// <param name="ServiceTarget">Application target or running module (if known).</param>
public sealed record ServerListenerDescriptor(
    int Port,
    TopologyTransportProtocol Protocol,
    string ProcessName,
    string ServiceTarget);
