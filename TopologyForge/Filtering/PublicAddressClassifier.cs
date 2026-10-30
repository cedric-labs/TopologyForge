using System.Net;
using System.Net.Sockets;

namespace TopologyForge.Filtering;

/// <summary>
/// Tells whether an IPv4 address is routable on the public Internet.
/// </summary>
public static class PublicAddressClassifier
{
    private static readonly string[] NonPublicRanges =
    [
        "0.0.0.0/8",
        "10.0.0.0/8",
        "100.64.0.0/10",
        "127.0.0.0/8",
        "169.254.0.0/16",
        "172.16.0.0/12",
        "192.0.0.0/24",
        "192.168.0.0/16",
        "198.18.0.0/15",
        "224.0.0.0/3"
    ];

    /// <summary>
    /// Checks whether an address lies outside the private, loopback, link-local, shared, multicast and reserved IPv4 ranges.
    /// </summary>
    /// <param name="ipAddress">The address to test.</param>
    /// <returns><c>true</c> for a public IPv4 address; <c>false</c> for any other range, an IPv6 address or an unparsable text.</returns>
    public static bool IsPublic(string? ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        return !CidrSubnetMatcher.IsMatchAny(ipAddress, NonPublicRanges);
    }
}
