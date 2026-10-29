using TopologyForge.Models;

namespace TopologyForge.Parsers;

/// <summary>
/// Parses network flow CSV logs into normalised topology records.
/// </summary>
public interface ITopologyCsvParser
{
    /// <summary>
    /// Parses a sequence of CSV lines (header included) into topology records.
    /// </summary>
    /// <param name="lines">The lines of the CSV file.</param>
    /// <param name="delimiter">Column delimiter (comma by default).</param>
    /// <returns>A read-only list of the parsed records.</returns>
    IReadOnlyList<TopologyFlowRecord> ParseLines(IEnumerable<string> lines, char delimiter = ',');

    /// <summary>
    /// Reads and parses a network flow CSV file asynchronously.
    /// </summary>
    /// <param name="filePath">Path of the CSV file to read.</param>
    /// <param name="delimiter">Column delimiter (comma by default).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A read-only list of the parsed records.</returns>
    Task<IReadOnlyList<TopologyFlowRecord>> ParseFileAsync(
        string filePath,
        char delimiter = ',',
        CancellationToken cancellationToken = default);
}
