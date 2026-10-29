namespace TopologyForge.Models;

/// <summary>
/// Directed edge representing a network dependency from a client server to a service port on a target server.
/// </summary>
/// <param name="EdgeId">Unique identifier of the edge in the graph.</param>
/// <param name="SourceNodeId">Identifier of the server that initiates the flow.</param>
/// <param name="TargetNodeId">Identifier of the server hosting the target service port.</param>
/// <param name="TargetPort">Target (non-ephemeral) service port number.</param>
/// <param name="Protocol">Transport protocol (TCP/UDP).</param>
/// <param name="ProcessName">Process associated with the flow.</param>
/// <param name="ServiceTarget">Execution target associated with the flow.</param>
/// <param name="ObservationCount">Cumulative number of observations on this edge.</param>
/// <param name="FlowDirection">Direction in which the flow was first observed (Outbound or Inbound).</param>
public sealed record TopologyServiceEdge(
    string EdgeId,
    string SourceNodeId,
    string TargetNodeId,
    int TargetPort,
    TopologyTransportProtocol Protocol,
    string ProcessName,
    string ServiceTarget,
    long ObservationCount,
    TopologyFlowDirection FlowDirection = TopologyFlowDirection.Outbound);
