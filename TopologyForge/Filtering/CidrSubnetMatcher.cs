using System.Globalization;
using System.Net;

namespace TopologyForge.Filtering;

/// <summary>
/// Évaluateur d'appartenance d'adresses IPv4 et IPv6 à des règles d'adresses exactes ou de sous-réseaux CIDR.
/// </summary>
public static class CidrSubnetMatcher
{
    /// <summary>
    /// Vérifie si une adresse IP correspond à au moins une règle de la liste fournie.
    /// </summary>
    /// <param name="ipAddressString">Adresse IP à tester.</param>
    /// <param name="cidrOrIpRules">Collection de règles (IP exactes ou notations CIDR ex: 192.168.1.0/24).</param>
    /// <returns><c>true</c> si une règle correspond ; sinon <c>false</c>.</returns>
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
    /// Vérifie si une adresse IP correspond à une adresse exacte ou à un bloc CIDR.
    /// </summary>
    /// <param name="ipAddressString">Adresse IP à évaluer.</param>
    /// <param name="cidrOrIpRule">Règle d'adresse IP exacte ou sous-réseau CIDR.</param>
    /// <returns><c>true</c> si l'adresse appartient au sous-réseau ; sinon <c>false</c>.</returns>
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
