using TopologyForge.Models;

namespace TopologyForge.Filtering;

/// <summary>
/// Options déclaratives de filtrage (inclusions et exclusions) pour la génération de la cartographie réseau.
/// </summary>
public sealed record TopologyFilterOptions
{
    /// <summary>
    /// Exclut automatiquement les flux dont le port de service appartient à la plage éphémère (<c>true</c> par défaut).
    /// </summary>
    public bool ExcludeEphemeralPorts { get; init; } = true;

    /// <summary>
    /// Plage de ports considérée comme éphémère (49152-65535 par défaut).
    /// </summary>
    public NetworkPortRange EphemeralPortRange { get; init; } = NetworkPortRange.DefaultEphemeralRange;

    /// <summary>
    /// Plages de ports de service explicitement incluses (si non vide, seuls ces ports sont retenus).
    /// </summary>
    public IReadOnlyList<NetworkPortRange> IncludedPorts { get; init; } = [];

    /// <summary>
    /// Plages de ports de service explicitement exclues.
    /// </summary>
    public IReadOnlyList<NetworkPortRange> ExcludedPorts { get; init; } = [];

    /// <summary>
    /// Motifs de noms d'hôtes inclus (accepte le caractère générique <c>*</c>, insensible à la casse).
    /// </summary>
    public IReadOnlyList<string> IncludedHostPatterns { get; init; } = [];

    /// <summary>
    /// Motifs de noms d'hôtes exclus (accepte le caractère générique <c>*</c>, insensible à la casse).
    /// </summary>
    public IReadOnlyList<string> ExcludedHostPatterns { get; init; } = [];

    /// <summary>
    /// Adresses IP ou sous-réseaux CIDR explicitement inclus.
    /// </summary>
    public IReadOnlyList<string> IncludedIpRanges { get; init; } = [];

    /// <summary>
    /// Adresses IP ou sous-réseaux CIDR explicitement exclus.
    /// </summary>
    public IReadOnlyList<string> ExcludedIpRanges { get; init; } = [];

    /// <summary>
    /// Motifs de noms de processus ou cibles applicatives inclus.
    /// </summary>
    public IReadOnlyList<string> IncludedProcesses { get; init; } = [];

    /// <summary>
    /// Motifs de noms de processus ou cibles applicatives exclus.
    /// </summary>
    public IReadOnlyList<string> ExcludedProcesses { get; init; } = [];

    /// <summary>
    /// Inclut les ports en écoute passive (<see cref="TopologyFlowDirection.Listening"/>).
    /// </summary>
    public bool IncludeListeners { get; init; } = true;

    /// <summary>
    /// Inclut les connexions entrantes (<see cref="TopologyFlowDirection.Inbound"/>).
    /// </summary>
    public bool IncludeInboundFlows { get; init; } = true;

    /// <summary>
    /// Inclut les connexions sortantes (<see cref="TopologyFlowDirection.Outbound"/>).
    /// </summary>
    public bool IncludeOutboundFlows { get; init; } = true;
}
