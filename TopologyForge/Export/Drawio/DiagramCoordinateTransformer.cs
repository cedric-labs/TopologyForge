namespace TopologyForge.Export.Drawio;

using TopologyForge.Export.Drawio.Model;

/// <summary>
/// Transformateur géométrique résolvant les coordonnées relatives récursives pour les conteneurs et nœuds enfants mxGraph.
/// </summary>
public static class DiagramCoordinateTransformer
{
    /// <summary>
    /// Nœud résolu avec coordonnées relatives au conteneur parent direct.
    /// </summary>
    public sealed record ResolvedNode(DiagramNode Original, double RelativeX, double RelativeY, string ParentCellId);

    /// <summary>
    /// Conteneur résolu avec coordonnées relatives au conteneur parent direct.
    /// </summary>
    public sealed record ResolvedContainer(DiagramContainer Original, double RelativeX, double RelativeY, string ParentCellId);

    /// <summary>
    /// Calcule les coordonnées relatives requises par mxGraph à partir des coordonnées absolues du modèle.
    /// </summary>
    public static (IReadOnlyList<ResolvedContainer> Containers, IReadOnlyList<ResolvedNode> Nodes) ResolveHierarchicalCoordinates(
        DiagramDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var containerMap = document.Containers.ToDictionary(c => c.Id, StringComparer.Ordinal);

        var resolvedContainers = new List<ResolvedContainer>(document.Containers.Count);
        foreach (var container in document.Containers)
        {
            double relX = container.X;
            double relY = container.Y;
            string parentCellId = container.LayerId;

            if (!string.IsNullOrEmpty(container.ParentContainerId) &&
                containerMap.TryGetValue(container.ParentContainerId, out var parentContainer))
            {
                parentCellId = parentContainer.Id;
                relX = container.X - parentContainer.X;
                relY = container.Y - parentContainer.Y;
            }

            resolvedContainers.Add(new ResolvedContainer(container, relX, relY, parentCellId));
        }

        var resolvedNodes = new List<ResolvedNode>(document.Nodes.Count);
        foreach (var node in document.Nodes)
        {
            double relX = node.X;
            double relY = node.Y;
            string parentCellId = node.LayerId;

            if (!string.IsNullOrEmpty(node.ContainerId) &&
                containerMap.TryGetValue(node.ContainerId, out var parentContainer))
            {
                parentCellId = parentContainer.Id;
                relX = node.X - parentContainer.X;
                relY = node.Y - parentContainer.Y;
            }

            resolvedNodes.Add(new ResolvedNode(node, relX, relY, parentCellId));
        }

        return (resolvedContainers, resolvedNodes);
    }
}
