using System.Globalization;
using System.Runtime.InteropServices;

namespace TopologyForge.Models;

/// <summary>
/// Plage de ports réseau TCP/UDP validée (0 à 65535).
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct NetworkPortRange
{
    /// <summary>
    /// Plage par défaut des ports dynamiques / éphémères selon le standard IANA (49152 à 65535).
    /// </summary>
    public static readonly NetworkPortRange DefaultEphemeralRange = new(49152, 65535);

    /// <summary>
    /// Borne inférieure incluse de la plage de ports.
    /// </summary>
    public int StartPort { get; }

    /// <summary>
    /// Borne supérieure incluse de la plage de ports.
    /// </summary>
    public int EndPort { get; }

    /// <summary>
    /// Initialise une plage de ports validée.
    /// </summary>
    /// <param name="startPort">Port de début (0 à 65535).</param>
    /// <param name="endPort">Port de fin (0 à 65535, supérieur ou égal à <paramref name="startPort"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException">Si un port est hors des limites 0..65535.</exception>
    /// <exception cref="ArgumentException">Si le port de début est supérieur au port de fin.</exception>
    public NetworkPortRange(int startPort, int endPort)
    {
        if (startPort is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(startPort), "Le port de début doit être compris entre 0 et 65535.");
        }

        if (endPort is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(endPort), "Le port de fin doit être compris entre 0 et 65535.");
        }

        if (startPort > endPort)
        {
            throw new ArgumentException("Le port de début ne peut pas être supérieur au port de fin.", nameof(startPort));
        }

        StartPort = startPort;
        EndPort = endPort;
    }

    /// <summary>
    /// Crée une plage ciblant un port unique.
    /// </summary>
    /// <param name="port">Numéro du port unique.</param>
    /// <returns>Instance de <see cref="NetworkPortRange"/> pour ce port.</returns>
    public static NetworkPortRange Single(int port) => new(port, port);

    /// <summary>
    /// Vérifie si un port appartient à cette plage (bornes incluses).
    /// </summary>
    /// <param name="port">Numéro du port à tester.</param>
    /// <returns><c>true</c> si le port est dans l'intervalle ; sinon <c>false</c>.</returns>
    public bool Contains(int port) => port >= StartPort && port <= EndPort;

    /// <summary>
    /// Analyse une représentation textuelle de port ou de plage (ex: "443" ou "8000-8080").
    /// </summary>
    /// <param name="specification">Spécification textuelle.</param>
    /// <returns>Plage de ports correspondante.</returns>
    /// <exception cref="FormatException">Si le format est invalide.</exception>
    public static NetworkPortRange Parse(string specification)
    {
        if (string.IsNullOrWhiteSpace(specification))
        {
            throw new FormatException("La spécification de plage de ports ne peut pas être vide.");
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

        throw new FormatException($"Format de plage de ports invalide : '{specification}'. Attendu : '80' ou '8000-8080'.");
    }

    /// <summary>
    /// Analyse une liste de ports ou plages séparés par des virgules ou points-virgules (ex: "80, 443, 8000-8080").
    /// </summary>
    /// <param name="specifications">Chaîne contenant une ou plusieurs spécifications.</param>
    /// <returns>Liste en lecture seule des plages analysées.</returns>
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
    /// Formate la plage sous forme canonique ("443" ou "8000-8080").
    /// </summary>
    /// <returns>Représentation textuelle.</returns>
    public override string ToString() =>
        StartPort == EndPort
            ? StartPort.ToString(CultureInfo.InvariantCulture)
            : $"{StartPort.ToString(CultureInfo.InvariantCulture)}-{EndPort.ToString(CultureInfo.InvariantCulture)}";
}
