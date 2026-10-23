using System.Globalization;
using TopologyForge.Filtering;
using TopologyForge.Models;

namespace TopologyForge.Builders;

/// <summary>
/// Constructeur de graphe topologique consolidant les serveurs, leurs ports de service non éphémères et leurs flux.
/// </summary>
/// <param name="flowEvaluator">Évaluateur des critères d'inclusion et d'exclusion.</param>
public sealed class TopologyGraphBuilder(ITopologyFlowEvaluator flowEvaluator) : ITopologyGraphBuilder
{
    private readonly ITopologyFlowEvaluator _flowEvaluator = flowEvaluator ?? throw new ArgumentNullException(nameof(flowEvaluator));

    /// <inheritdoc />
    public NetworkTopologyGraph BuildGraph(IEnumerable<TopologyFlowRecord> flows)
    {
        ArgumentNullException.ThrowIfNull(flows);

        var nodeAccumulators = new Dictionary<string, ServerNodeAccumulator>(StringComparer.OrdinalIgnoreCase);
        var edgeAccumulators = new Dictionary<string, ServiceEdgeAccumulator>(StringComparer.OrdinalIgnoreCase);
        var ipToNodeId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var totalProcessed = 0;
        var excludedCount = 0;

        foreach (var flow in flows)
        {
            totalProcessed++;
            if (!_flowEvaluator.ShouldInclude(flow))
            {
                excludedCount++;
                continue;
            }

            ProcessAcceptedFlow(flow, nodeAccumulators, edgeAccumulators, ipToNodeId);
        }

        var nodes = nodeAccumulators.Values
            .OrderByDescending(n => n.IsLocalAuditedServer)
            .ThenBy(n => n.HostName, StringComparer.OrdinalIgnoreCase)
            .Select(n => n.ToServerNode())
            .ToList();

        var edges = edgeAccumulators.Values
            .OrderBy(e => e.SourceNodeId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.TargetNodeId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.TargetPort)
            .Select(e => e.ToServiceEdge())
            .ToList();

        return new NetworkTopologyGraph(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Nodes: nodes,
            Edges: edges,
            TotalProcessedFlows: totalProcessed,
            ExcludedFlowsCount: excludedCount);
    }

    private static void ProcessAcceptedFlow(
        TopologyFlowRecord flow,
        Dictionary<string, ServerNodeAccumulator> nodeAccumulators,
        Dictionary<string, ServiceEdgeAccumulator> edgeAccumulators,
        Dictionary<string, string> ipToNodeId)
    {
        var localNode = ResolveOrCreateNode(
            hostName: flow.SourceServerName,
            ipAddress: flow.SourceIp,
            isLocalAudited: true,
            nodeAccumulators: nodeAccumulators,
            ipToNodeId: ipToNodeId);

        if (flow.Direction == TopologyFlowDirection.Listening)
        {
            localNode.AddListeningPort(flow.LocalPort, flow.Protocol, flow.ProcessName, flow.ServiceTarget);
            return;
        }

        var remoteNode = ResolveOrCreateNode(
            hostName: flow.RemoteServerName,
            ipAddress: flow.RemoteIp,
            isLocalAudited: false,
            nodeAccumulators: nodeAccumulators,
            ipToNodeId: ipToNodeId);

        if (string.Equals(localNode.NodeId, remoteNode.NodeId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (flow.Direction == TopologyFlowDirection.Outbound)
        {
            // En flux sortant, le port de service (non éphémère) est hébergé par le serveur distant (RemotePort)
            remoteNode.AddListeningPort(flow.RemotePort, flow.Protocol, string.Empty, string.Empty);

            RegisterOrIncrementEdge(
                sourceNode: localNode,
                targetNode: remoteNode,
                targetPort: flow.RemotePort,
                protocol: flow.Protocol,
                processName: flow.ProcessName,
                serviceTarget: flow.ServiceTarget,
                observationCount: flow.ObservationCount,
                edgeAccumulators: edgeAccumulators);
        }
        else if (flow.Direction == TopologyFlowDirection.Inbound)
        {
            // En flux entrant, le port de service (non éphémère) est hébergé par le serveur local (LocalPort)
            localNode.AddListeningPort(flow.LocalPort, flow.Protocol, flow.ProcessName, flow.ServiceTarget);

            RegisterOrIncrementEdge(
                sourceNode: remoteNode,
                targetNode: localNode,
                targetPort: flow.LocalPort,
                protocol: flow.Protocol,
                processName: flow.ProcessName,
                serviceTarget: flow.ServiceTarget,
                observationCount: flow.ObservationCount,
                edgeAccumulators: edgeAccumulators);
        }
    }

    private static ServerNodeAccumulator ResolveOrCreateNode(
        string hostName,
        string ipAddress,
        bool isLocalAudited,
        Dictionary<string, ServerNodeAccumulator> nodeAccumulators,
        Dictionary<string, string> ipToNodeId)
    {
        var normalizedIp = NormalizeIpAddress(ipAddress);
        var normalizedHost = string.IsNullOrWhiteSpace(hostName) ? normalizedIp : hostName.Trim();
        var canonicalKey = !string.IsNullOrWhiteSpace(hostName)
            ? normalizedHost.ToUpperInvariant()
            : normalizedIp.ToUpperInvariant();

        if (!string.IsNullOrEmpty(normalizedIp) &&
            ipToNodeId.TryGetValue(normalizedIp, out var existingIdByIp) &&
            nodeAccumulators.TryGetValue(existingIdByIp, out var nodeByIp))
        {
            nodeByIp.MergeMetadata(normalizedHost, normalizedIp, isLocalAudited);
            return nodeByIp;
        }

        if (nodeAccumulators.TryGetValue(canonicalKey, out var existingNode))
        {
            existingNode.MergeMetadata(normalizedHost, normalizedIp, isLocalAudited);
            if (!string.IsNullOrEmpty(normalizedIp))
            {
                ipToNodeId[normalizedIp] = existingNode.NodeId;
            }

            return existingNode;
        }

        var created = new ServerNodeAccumulator(canonicalKey, normalizedHost, normalizedIp, isLocalAudited);
        nodeAccumulators[canonicalKey] = created;
        if (!string.IsNullOrEmpty(normalizedIp))
        {
            ipToNodeId[normalizedIp] = canonicalKey;
        }

        return created;
    }

    private static void RegisterOrIncrementEdge(
        ServerNodeAccumulator sourceNode,
        ServerNodeAccumulator targetNode,
        int targetPort,
        TopologyTransportProtocol protocol,
        string processName,
        string serviceTarget,
        long observationCount,
        Dictionary<string, ServiceEdgeAccumulator> edgeAccumulators)
    {
        var edgeKey = $"{sourceNode.NodeId}->{targetNode.NodeId}:{targetPort.ToString(CultureInfo.InvariantCulture)}/{protocol}";
        var countToAdd = observationCount > 0 ? observationCount : 1;

        if (edgeAccumulators.TryGetValue(edgeKey, out var existingEdge))
        {
            existingEdge.AddObservations(countToAdd, processName, serviceTarget);
            return;
        }

        var edge = new ServiceEdgeAccumulator(
            edgeKey,
            sourceNode.NodeId,
            targetNode.NodeId,
            targetPort,
            protocol,
            processName,
            serviceTarget,
            countToAdd);

        edgeAccumulators[edgeKey] = edge;
        sourceNode.OutboundConnectionCount++;
        targetNode.InboundConnectionCount++;
    }

    private static string NormalizeIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return string.Empty;
        }

        var trimmed = ipAddress.Trim();
        return trimmed is "0.0.0.0" or "::" or "*" ? string.Empty : trimmed;
    }

    private sealed class ServerNodeAccumulator(string nodeId, string hostName, string ipAddress, bool isLocalAudited)
    {
        private readonly Dictionary<(int Port, TopologyTransportProtocol Protocol), ServerListenerDescriptor> _listeners = [];

        public string NodeId { get; } = nodeId;
        public string HostName { get; private set; } = string.IsNullOrWhiteSpace(hostName) ? "Unknown" : hostName;
        public string IpAddress { get; private set; } = ipAddress;
        public bool IsLocalAuditedServer { get; private set; } = isLocalAudited;
        public int InboundConnectionCount { get; set; }
        public int OutboundConnectionCount { get; set; }

        public void MergeMetadata(string hostName, string ipAddress, bool isLocalAudited)
        {
            if (isLocalAudited)
            {
                IsLocalAuditedServer = true;
            }

            if (string.IsNullOrEmpty(IpAddress) && !string.IsNullOrEmpty(ipAddress))
            {
                IpAddress = ipAddress;
            }

            if (!string.IsNullOrWhiteSpace(hostName) &&
                !string.Equals(hostName, IpAddress, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(HostName, IpAddress, StringComparison.OrdinalIgnoreCase) || string.Equals(HostName, "Unknown", StringComparison.OrdinalIgnoreCase)))
            {
                HostName = hostName;
            }
        }

        public void AddListeningPort(int port, TopologyTransportProtocol protocol, string processName, string serviceTarget)
        {
            if (port is <= 0 or > 65535)
            {
                return;
            }

            var key = (port, protocol);
            if (_listeners.TryGetValue(key, out var existing))
            {
                var mergedProcess = string.IsNullOrWhiteSpace(existing.ProcessName) ? processName : existing.ProcessName;
                var mergedTarget = string.IsNullOrWhiteSpace(existing.ServiceTarget) ? serviceTarget : existing.ServiceTarget;
                _listeners[key] = new ServerListenerDescriptor(port, protocol, mergedProcess, mergedTarget);
            }
            else
            {
                _listeners[key] = new ServerListenerDescriptor(port, protocol, processName, serviceTarget);
            }
        }

        public TopologyServerNode ToServerNode() =>
            new(
                NodeId: NodeId,
                HostName: HostName,
                IpAddress: IpAddress,
                IsLocalAuditedServer: IsLocalAuditedServer,
                ListeningPorts: _listeners.Values.OrderBy(p => p.Port).ThenBy(p => p.Protocol).ToList(),
                InboundConnectionCount: InboundConnectionCount,
                OutboundConnectionCount: OutboundConnectionCount);
    }

    private sealed class ServiceEdgeAccumulator(
        string edgeId,
        string sourceNodeId,
        string targetNodeId,
        int targetPort,
        TopologyTransportProtocol protocol,
        string processName,
        string serviceTarget,
        long observationCount)
    {
        public string EdgeId { get; } = edgeId;
        public string SourceNodeId { get; } = sourceNodeId;
        public string TargetNodeId { get; } = targetNodeId;
        public int TargetPort { get; } = targetPort;
        public TopologyTransportProtocol Protocol { get; } = protocol;
        public string ProcessName { get; private set; } = processName;
        public string ServiceTarget { get; private set; } = serviceTarget;
        public long ObservationCount { get; private set; } = observationCount;

        public void AddObservations(long count, string processName, string serviceTarget)
        {
            ObservationCount += count;
            if (string.IsNullOrWhiteSpace(ProcessName) && !string.IsNullOrWhiteSpace(processName))
            {
                ProcessName = processName;
            }

            if (string.IsNullOrWhiteSpace(ServiceTarget) && !string.IsNullOrWhiteSpace(serviceTarget))
            {
                ServiceTarget = serviceTarget;
            }
        }

        public TopologyServiceEdge ToServiceEdge() =>
            new(EdgeId, SourceNodeId, TargetNodeId, TargetPort, Protocol, ProcessName, ServiceTarget, ObservationCount);
    }
}
