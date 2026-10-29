namespace TopologyForge.Geometry;

using TopologyForge.Export.Drawio.Model;

/// <summary>
/// Orthogonal edge router that spreads anchors canonically,
/// inserts Z-shaped waypoints (fan-out) and avoids AABB obstacles.
/// </summary>
public sealed class TopologyEdgeRouter(int gridSize = 10, int fanSpacing = 20)
{
    private enum BoxFace { Top, Bottom, Left, Right }

    /// <summary>
    /// Computes the routing, relative anchors and waypoints of each edge.
    /// </summary>
    public IReadOnlyList<DiagramEdge> RouteEdges(
        IReadOnlyList<DiagramNode> nodes,
        IReadOnlyList<DiagramEdge> edges,
        bool forceVerticalTreeAnchors = false)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var allBoxes = nodes.Select(n => CorridorWaypointFinder.BoundingBox.FromNode(n)).ToList();

        // 1. Canonical grouping by server pair (independent of the A->B or B->A direction)
        var pairGroups = edges
            .Where(e => nodeMap.ContainsKey(e.SourceNodeId) && nodeMap.ContainsKey(e.TargetNodeId))
            .GroupBy(e => GetCanonicalPairKey(e.SourceNodeId, e.TargetNodeId))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var routedEdges = new List<DiagramEdge>(edges.Count);

        foreach (var edge in edges)
        {
            if (!nodeMap.TryGetValue(edge.SourceNodeId, out var srcNode) ||
                !nodeMap.TryGetValue(edge.TargetNodeId, out var tgtNode) ||
                edge.SourceNodeId == edge.TargetNodeId)
            {
                routedEdges.Add(edge);
                continue;
            }

            string pairKey = GetCanonicalPairKey(edge.SourceNodeId, edge.TargetNodeId);
            var siblings = pairGroups[pairKey];
            int siblingIdx = siblings.IndexOf(edge);
            int siblingCount = siblings.Count;

            // 2. Canonical frame (nodeA = first in ordinal order, nodeB = second)
            // Keeps two opposite flows (A->B and B->A) from crossing in an X
            bool isCanonicalOrder = string.CompareOrdinal(edge.SourceNodeId, edge.TargetNodeId) <= 0;
            var nodeA = isCanonicalOrder ? srcNode : tgtNode;
            var nodeB = isCanonicalOrder ? tgtNode : srcNode;

            var (faceA, faceB, isHorizontal) = DetermineFacingSides(nodeA, nodeB, forceVerticalTreeAnchors);

            // 3. Normalised [0..1] anchors aligned on the grid
            var (anchorA, absPointA) = ComputeFaceAnchor(nodeA, faceA, siblingIdx, siblingCount);
            var (anchorB, absPointB) = ComputeFaceAnchor(nodeB, faceB, siblingIdx, siblingCount);

            var exitAnchor = isCanonicalOrder ? anchorA : anchorB;
            var entryAnchor = isCanonicalOrder ? anchorB : anchorA;
            var startPoint = isCanonicalOrder ? absPointA : absPointB;
            var endPoint = isCanonicalOrder ? absPointB : absPointA;

            // 4. fanOffset aligned on the grid
            double rawFanOffset = (siblingIdx - ((siblingCount - 1) / 2.0)) * fanSpacing;
            double snappedFanOffset = GridSnapper.Snap(rawFanOffset, gridSize);

            // 5. Obstacles: every server except the source and the target
            var obstacles = allBoxes
                .Where(b => b.Id != srcNode.Id && b.Id != tgtNode.Id)
                .ToList();

            // 6. Waypoints (Z bend and obstacle avoidance)
            var waypoints = CorridorWaypointFinder.BuildOrthogonalWaypoints(
                startPoint,
                endPoint,
                isHorizontal,
                snappedFanOffset,
                obstacles,
                gridSize);

            // 7. Labels staggered along the edge so that they do not overlap
            double targetLabelPos = siblingCount > 1
                ? Math.Clamp(0.72 + (siblingIdx * 0.08), 0.65, 0.92)
                : 0.8;

            double centerLabelPos = siblingCount > 1
                ? Math.Clamp(-0.25 + (siblingIdx * 0.25), -0.4, 0.4)
                : 0.0;

            routedEdges.Add(edge with
            {
                ExitAnchor = exitAnchor,
                EntryAnchor = entryAnchor,
                Waypoints = waypoints,
                TargetLabelPosition = targetLabelPos,
                CenterLabelPosition = centerLabelPos
            });
        }

        return routedEdges;
    }

    private static string GetCanonicalPairKey(string id1, string id2) =>
        string.CompareOrdinal(id1, id2) <= 0 ? $"{id1}::{id2}" : $"{id2}::{id1}";

    private static (BoxFace FaceA, BoxFace FaceB, bool IsHorizontalDominant) DetermineFacingSides(
        DiagramNode a,
        DiagramNode b,
        bool forceVerticalTree)
    {
        double centerAx = a.X + (a.Width / 2.0);
        double centerAy = a.Y + (a.Height / 2.0);
        double centerBx = b.X + (b.Width / 2.0);
        double centerBy = b.Y + (b.Height / 2.0);

        double dx = centerBx - centerAx;
        double dy = centerBy - centerAy;

        if (forceVerticalTree || Math.Abs(dy) >= Math.Abs(dx))
        {
            return dy >= 0
                ? (BoxFace.Bottom, BoxFace.Top, false)
                : (BoxFace.Top, BoxFace.Bottom, false);
        }

        return dx >= 0
            ? (BoxFace.Right, BoxFace.Left, true)
            : (BoxFace.Left, BoxFace.Right, true);
    }

    private (DiagramAnchorPoint Anchor, DiagramPoint AbsolutePoint) ComputeFaceAnchor(
        DiagramNode node,
        BoxFace face,
        int siblingIdx,
        int siblingCount)
    {
        switch (face)
        {
            case BoxFace.Left:
                {
                    var (ratioY, absY) = GridSnapper.ComputeGridAlignedAnchor(
                        node.Y, node.Height, siblingIdx, siblingCount, gridSize);
                    return (new DiagramAnchorPoint(0.0, ratioY, Perimeter: false), new DiagramPoint(node.X, absY));
                }
            case BoxFace.Right:
                {
                    var (ratioY, absY) = GridSnapper.ComputeGridAlignedAnchor(
                        node.Y, node.Height, siblingIdx, siblingCount, gridSize);
                    return (new DiagramAnchorPoint(1.0, ratioY, Perimeter: false), new DiagramPoint(node.X + node.Width, absY));
                }
            case BoxFace.Top:
                {
                    var (ratioX, absX) = GridSnapper.ComputeGridAlignedAnchor(
                        node.X, node.Width, siblingIdx, siblingCount, gridSize);
                    return (new DiagramAnchorPoint(ratioX, 0.0, Perimeter: false), new DiagramPoint(absX, node.Y));
                }
            default: // Bottom
                {
                    var (ratioX, absX) = GridSnapper.ComputeGridAlignedAnchor(
                        node.X, node.Width, siblingIdx, siblingCount, gridSize);
                    return (new DiagramAnchorPoint(ratioX, 1.0, Perimeter: false), new DiagramPoint(absX, node.Y + node.Height));
                }
        }
    }
}
