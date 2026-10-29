using TopologyForge.Models;

namespace TopologyForge.Rendering;

/// <summary>
/// Generates interactive, fully self-contained (offline) HTML topology maps.
/// </summary>
public interface ITopologyHtmlRenderer
{
    /// <summary>
    /// Generates the complete, self-contained HTML document representing the topology graph.
    /// </summary>
    /// <param name="graph">The consolidated topology graph to render.</param>
    /// <param name="title">Title shown in the map header.</param>
    /// <returns>The complete HTML5 code (CSS and JS engine embedded).</returns>
    string RenderHtml(NetworkTopologyGraph graph, string title = "Network Flow & Server Topology Map");

    /// <summary>
    /// Generates the complete, self-contained HTML document with the given sort, routing and spacing options.
    /// </summary>
    /// <param name="graph">The consolidated topology graph to render.</param>
    /// <param name="options">Rendering options (sort, arrow routing, spacing, pagination).</param>
    /// <returns>The complete HTML5 code.</returns>
    string RenderHtml(NetworkTopologyGraph graph, TopologyRenderingOptions options);

    /// <summary>
    /// Writes the self-contained HTML document to disk asynchronously.
    /// </summary>
    /// <param name="outputPath">Path of the target HTML file.</param>
    /// <param name="graph">The consolidated topology graph.</param>
    /// <param name="title">Title of the map.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A task representing the write operation.</returns>
    Task WriteHtmlFileAsync(
        string outputPath,
        NetworkTopologyGraph graph,
        string title = "Network Flow & Server Topology Map",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the self-contained HTML document to disk asynchronously with the given rendering options.
    /// </summary>
    /// <param name="outputPath">Path of the target HTML file.</param>
    /// <param name="graph">The consolidated topology graph.</param>
    /// <param name="options">Rendering options (sort, arrow routing, spacing, pagination).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A task representing the write operation.</returns>
    Task WriteHtmlFileAsync(
        string outputPath,
        NetworkTopologyGraph graph,
        TopologyRenderingOptions options,
        CancellationToken cancellationToken = default);
}
