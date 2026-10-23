using TopologyForge.Models;

namespace TopologyForge.Rendering;

/// <summary>
/// Contrat de génération de cartes topologiques HTML interactives et 100 % autonomes (hors-ligne).
/// </summary>
public interface ITopologyHtmlRenderer
{
    /// <summary>
    /// Génère le document HTML complet et autonome représentant le graphe topologique.
    /// </summary>
    /// <param name="graph">Graphe topologique consolidé à représenter.</param>
    /// <param name="title">Titre affiché dans l'en-tête de la carte.</param>
    /// <returns>Chaîne contenant le code HTML5 complet (CSS et moteur JS embarqués).</returns>
    string RenderHtml(NetworkTopologyGraph graph, string title = "Cartographie des Flux & Serveurs");

    /// <summary>
    /// Écrit de manière asynchrone le document HTML autonome sur le disque.
    /// </summary>
    /// <param name="outputPath">Chemin du fichier HTML cible.</param>
    /// <param name="graph">Graphe topologique consolidé.</param>
    /// <param name="title">Titre de la carte.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopératif.</param>
    /// <returns>Tâche représentant l'opération d'écriture.</returns>
    Task WriteHtmlFileAsync(
        string outputPath,
        NetworkTopologyGraph graph,
        string title = "Cartographie des Flux & Serveurs",
        CancellationToken cancellationToken = default);
}
