using TopologyForge.Models;

namespace TopologyForge.Parsers;

/// <summary>
/// Contrat d'analyse de journaux CSV de flux réseau vers des enregistrements topologiques normalisés.
/// </summary>
public interface ITopologyCsvParser
{
    /// <summary>
    /// Analyse une séquence de lignes CSV (en-tête inclus) en enregistrements topologiques.
    /// </summary>
    /// <param name="lines">Séquence des lignes du fichier CSV.</param>
    /// <param name="delimiter">Caractère délimiteur de colonnes (virgule par défaut).</param>
    /// <returns>Liste en lecture seule des enregistrements analysés.</returns>
    IReadOnlyList<TopologyFlowRecord> ParseLines(IEnumerable<string> lines, char delimiter = ',');

    /// <summary>
    /// Lit et analyse de manière asynchrone un fichier CSV de flux réseau.
    /// </summary>
    /// <param name="filePath">Chemin du fichier CSV à lire.</param>
    /// <param name="delimiter">Caractère délimiteur de colonnes (virgule par défaut).</param>
    /// <param name="cancellationToken">Jeton d'annulation coopératif.</param>
    /// <returns>Liste en lecture seule des enregistrements analysés.</returns>
    Task<IReadOnlyList<TopologyFlowRecord>> ParseFileAsync(
        string filePath,
        char delimiter = ',',
        CancellationToken cancellationToken = default);
}
