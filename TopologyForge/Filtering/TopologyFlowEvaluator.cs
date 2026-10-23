using TopologyForge.Models;

namespace TopologyForge.Filtering;

/// <summary>
/// Évaluateur de filtrage multicritère (ports éphémères, plages de ports, hôtes, CIDR, processus et directions).
/// </summary>
/// <param name="options">Options de filtrage configurées.</param>
public sealed class TopologyFlowEvaluator(TopologyFilterOptions options) : ITopologyFlowEvaluator
{
    private readonly TopologyFilterOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public bool ShouldInclude(TopologyFlowRecord flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (!IsDirectionAllowed(flow.Direction))
        {
            return false;
        }

        if (!IsServicePortAllowed(flow.GetServicePort()))
        {
            return false;
        }

        if (!IsHostAllowed(flow.SourceServerName, flow.RemoteServerName))
        {
            return false;
        }

        if (!IsIpAllowed(flow.SourceIp, flow.RemoteIp, flow.Direction))
        {
            return false;
        }

        return IsProcessAllowed(flow.ProcessName, flow.ServiceTarget);
    }

    private bool IsDirectionAllowed(TopologyFlowDirection direction) =>
        direction switch
        {
            TopologyFlowDirection.Listening => _options.IncludeListeners,
            TopologyFlowDirection.Inbound => _options.IncludeInboundFlows,
            TopologyFlowDirection.Outbound => _options.IncludeOutboundFlows,
            _ => false
        };

    private bool IsServicePortAllowed(int servicePort)
    {
        if (servicePort is <= 0 or > 65535)
        {
            return false;
        }

        if (_options.ExcludeEphemeralPorts && _options.EphemeralPortRange.Contains(servicePort))
        {
            return false;
        }

        foreach (var excludedRange in _options.ExcludedPorts)
        {
            if (excludedRange.Contains(servicePort))
            {
                return false;
            }
        }

        if (_options.IncludedPorts.Count == 0)
        {
            return true;
        }

        foreach (var includedRange in _options.IncludedPorts)
        {
            if (includedRange.Contains(servicePort))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsHostAllowed(string sourceHost, string remoteHost)
    {
        if (_options.ExcludedHostPatterns.Count > 0 &&
            (MatchesAnyPattern(sourceHost, _options.ExcludedHostPatterns) ||
             MatchesAnyPattern(remoteHost, _options.ExcludedHostPatterns)))
        {
            return false;
        }

        if (_options.IncludedHostPatterns.Count == 0)
        {
            return true;
        }

        return MatchesAnyPattern(sourceHost, _options.IncludedHostPatterns) ||
               MatchesAnyPattern(remoteHost, _options.IncludedHostPatterns);
    }

    private bool IsIpAllowed(string sourceIp, string remoteIp, TopologyFlowDirection direction)
    {
        if (_options.ExcludedIpRanges.Count > 0)
        {
            if (CidrSubnetMatcher.IsMatchAny(sourceIp, _options.ExcludedIpRanges))
            {
                return false;
            }

            if (direction != TopologyFlowDirection.Listening &&
                CidrSubnetMatcher.IsMatchAny(remoteIp, _options.ExcludedIpRanges))
            {
                return false;
            }
        }

        if (_options.IncludedIpRanges.Count == 0)
        {
            return true;
        }

        if (CidrSubnetMatcher.IsMatchAny(sourceIp, _options.IncludedIpRanges))
        {
            return true;
        }

        return direction != TopologyFlowDirection.Listening &&
               CidrSubnetMatcher.IsMatchAny(remoteIp, _options.IncludedIpRanges);
    }

    private bool IsProcessAllowed(string processName, string serviceTarget)
    {
        if (_options.ExcludedProcesses.Count > 0 &&
            (MatchesAnyPattern(processName, _options.ExcludedProcesses) ||
             MatchesAnyPattern(serviceTarget, _options.ExcludedProcesses)))
        {
            return false;
        }

        if (_options.IncludedProcesses.Count == 0)
        {
            return true;
        }

        return MatchesAnyPattern(processName, _options.IncludedProcesses) ||
               MatchesAnyPattern(serviceTarget, _options.IncludedProcesses);
    }

    private static bool MatchesAnyPattern(string? candidate, IReadOnlyList<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        foreach (var pattern in patterns)
        {
            if (!string.IsNullOrWhiteSpace(pattern) && MatchesWildcard(candidate.Trim(), pattern.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesWildcard(string input, string pattern)
    {
        if (pattern == "*")
        {
            return true;
        }

        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return input.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        var parts = pattern.Split('*', StringSplitOptions.None);
        var currentIndex = 0;

        for (var i = 0; i < parts.Length; i++)
        {
            var segment = parts[i];
            if (segment.Length == 0)
            {
                continue;
            }

            if (i == 0 && !pattern.StartsWith('*'))
            {
                if (!input.StartsWith(segment, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                currentIndex = segment.Length;
                continue;
            }

            if (i == parts.Length - 1 && !pattern.EndsWith('*'))
            {
                return input.Length >= currentIndex + segment.Length &&
                       input.EndsWith(segment, StringComparison.OrdinalIgnoreCase);
            }

            var foundIndex = input.IndexOf(segment, currentIndex, StringComparison.OrdinalIgnoreCase);
            if (foundIndex < 0)
            {
                return false;
            }

            currentIndex = foundIndex + segment.Length;
        }

        return true;
    }
}
