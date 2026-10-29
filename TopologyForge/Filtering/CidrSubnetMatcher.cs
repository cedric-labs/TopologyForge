using System.Globalization;
using System.Net;

namespace TopologyForge.Filtering;

/// <summary>
/// Checks whether IPv4 and IPv6 addresses match exact address rules or CIDR subnets.
/// </summary>
public static class CidrSubnetMatcher
{
    /// <summary>
    /// Checks whether an IP address matches at least one rule of the given list.
    /// </summary>
    /// <param name="ipAddressString">The IP address to test.</param>
    /// <param name="cidrOrIpRules">Rules (exact IPs or CIDR notations such as 192.168.1.0/24).</param>
    /// <returns><c>true</c> if a rule matches; otherwise <c>false</c>.</returns>
    public static bool IsMatchAny(string? ipAddressString, IEnumerable<string> cidrOrIpRules)
    {
        ArgumentNullException.ThrowIfNull(cidrOrIpRules);

        if (string.IsNullOrWhiteSpace(ipAddressString))
        {
            return false;
        }

        foreach (var rule in cidrOrIpRules)
        {
            if (IsMatch(ipAddressString, rule))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether an IP address matches an exact address or a CIDR block.
    /// </summary>
    /// <param name="ipAddressString">The IP address to evaluate.</param>
    /// <param name="cidrOrIpRule">Exact IP address or CIDR subnet rule.</param>
    /// <returns><c>true</c> if the address belongs to the subnet; otherwise <c>false</c>.</returns>
    public static bool IsMatch(string? ipAddressString, string? cidrOrIpRule)
    {
        if (string.IsNullOrWhiteSpace(ipAddressString) || string.IsNullOrWhiteSpace(cidrOrIpRule))
        {
            return false;
        }

        if (!IPAddress.TryParse(ipAddressString.Trim(), out var candidateIp))
        {
            return false;
        }

        var trimmedRule = cidrOrIpRule.Trim();
        var slashIndex = trimmedRule.IndexOf('/', StringComparison.Ordinal);

        if (slashIndex < 0)
        {
            return IPAddress.TryParse(trimmedRule, out var exactIp) && candidateIp.Equals(exactIp);
        }

        var networkPart = trimmedRule[..slashIndex];
        var prefixPart = trimmedRule[(slashIndex + 1)..];

        if (!IPAddress.TryParse(networkPart, out var networkIp) ||
            !int.TryParse(prefixPart, NumberStyles.None, CultureInfo.InvariantCulture, out var prefixLength))
        {
            return false;
        }

        if (candidateIp.AddressFamily != networkIp.AddressFamily)
        {
            return false;
        }

        var candidateBytes = candidateIp.GetAddressBytes();
        var networkBytes = networkIp.GetAddressBytes();
        var maxBits = candidateBytes.Length * 8;

        if (prefixLength < 0 || prefixLength > maxBits)
        {
            return false;
        }

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
        {
            if (candidateBytes[i] != networkBytes[i])
            {
                return false;
            }
        }

        if (remainingBits > 0)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            if ((candidateBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask))
            {
                return false;
            }
        }

        return true;
    }
}
