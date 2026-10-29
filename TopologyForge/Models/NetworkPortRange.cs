using System.Globalization;
using System.Runtime.InteropServices;

namespace TopologyForge.Models;

/// <summary>
/// Validated TCP/UDP network port range (0 to 65535).
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetworkPortRange
{
    /// <summary>
    /// Default IANA range of dynamic / ephemeral ports (49152 to 65535).
    /// </summary>
    public static readonly NetworkPortRange DefaultEphemeralRange = new(49152, 65535);

    /// <summary>
    /// Inclusive lower bound of the range.
    /// </summary>
    public int StartPort { get; }

    /// <summary>
    /// Inclusive upper bound of the range.
    /// </summary>
    public int EndPort { get; }

    /// <summary>
    /// Creates a validated port range.
    /// </summary>
    /// <param name="startPort">First port (0 to 65535).</param>
    /// <param name="endPort">Last port (0 to 65535, greater than or equal to <paramref name="startPort"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException">A port is outside 0..65535.</exception>
    /// <exception cref="ArgumentException">The first port is greater than the last port.</exception>
    public NetworkPortRange(int startPort, int endPort)
    {
        if (startPort is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(startPort), "The start port must be between 0 and 65535.");
        }

        if (endPort is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(endPort), "The end port must be between 0 and 65535.");
        }

        if (startPort > endPort)
        {
            throw new ArgumentException("The start port cannot be greater than the end port.", nameof(startPort));
        }

        StartPort = startPort;
        EndPort = endPort;
    }

    /// <summary>
    /// Creates a range that targets a single port.
    /// </summary>
    /// <param name="port">The single port number.</param>
    /// <returns>A <see cref="NetworkPortRange"/> for this port.</returns>
    public static NetworkPortRange Single(int port) => new(port, port);

    /// <summary>
    /// Checks whether a port belongs to this range (bounds included).
    /// </summary>
    /// <param name="port">The port number to test.</param>
    /// <returns><c>true</c> if the port is within the range; otherwise <c>false</c>.</returns>
    public bool Contains(int port) => port >= StartPort && port <= EndPort;

    /// <summary>
    /// Parses a port or range specification (e.g. "443" or "8000-8080").
    /// </summary>
    /// <param name="specification">The text specification.</param>
    /// <returns>The matching port range.</returns>
    /// <exception cref="FormatException">The format is invalid.</exception>
    public static NetworkPortRange Parse(string specification)
    {
        if (string.IsNullOrWhiteSpace(specification))
        {
            throw new FormatException("The port range specification cannot be empty.");
        }

        var trimmed = specification.Trim();
        var parts = trimmed.Split('-', StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
        {
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var singlePort))
            {
                throw new FormatException($"Port invalide : '{specification}'.");
            }

            return new NetworkPortRange(singlePort, singlePort);
        }

        if (parts.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var startPort) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var endPort))
        {
            return new NetworkPortRange(startPort, endPort);
        }

        throw new FormatException($"Invalid port range format: '{specification}'. Expected '80' or '8000-8080'.");
    }

    /// <summary>
    /// Parses a list of ports or ranges separated by commas or semicolons (e.g. "80, 443, 8000-8080").
    /// </summary>
    /// <param name="specifications">A string holding one or more specifications.</param>
    /// <returns>A read-only list of the parsed ranges.</returns>
    public static IReadOnlyList<NetworkPortRange> ParseList(string? specifications)
    {
        if (string.IsNullOrWhiteSpace(specifications))
        {
            return [];
        }

        var tokens = specifications.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<NetworkPortRange>(tokens.Length);
        foreach (var token in tokens)
        {
            result.Add(Parse(token));
        }

        return result;
    }

    /// <summary>
    /// Formats the range in canonical form ("443" or "8000-8080").
    /// </summary>
    /// <returns>The text representation.</returns>
    public override string ToString() =>
        StartPort == EndPort
            ? StartPort.ToString(CultureInfo.InvariantCulture)
            : $"{StartPort.ToString(CultureInfo.InvariantCulture)}-{EndPort.ToString(CultureInfo.InvariantCulture)}";
}
