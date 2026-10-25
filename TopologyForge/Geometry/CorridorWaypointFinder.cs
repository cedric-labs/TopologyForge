namespace TopologyForge.Geometry;

using System.Runtime.InteropServices;
using TopologyForge.Export.Drawio.Model;

/// <summary>
/// Détecteur de collisions AABB et recherche de couloirs de déviation orthogonaux (Z-bend et U-bend).
/// </summary>
public static class CorridorWaypointFinder
{
    private static readonly double[] CandidateOffsets = [0, 90, -90, 160, -160, 240, -240, 320, -320];
    private const double ObstaclePadding = 16.0;

    /// <summary>
    /// Boîte englobante alignée sur les axes avec marge de sécurité.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct BoundingBox(string Id, double Left, double Top, double Right, double Bottom)
    {
        public static BoundingBox FromNode(DiagramNode node, double padding = ObstaclePadding)
        {
            ArgumentNullException.ThrowIfNull(node);
            return new BoundingBox(
                node.Id,
                node.X - padding,
                node.Y - padding,
                node.X + node.Width + padding,
                node.Y + node.Height + padding);
        }

        public bool IntersectsSegment(DiagramPoint p1, DiagramPoint p2)
        {
            double minX = Math.Min(p1.X, p2.X);
            double maxX = Math.Max(p1.X, p2.X);
            double minY = Math.Min(p1.Y, p2.Y);
            double maxY = Math.Max(p1.Y, p2.Y);

            // Segment orthogonal horizontal ou vertical (test AABB rapide et exact)
            if (Math.Abs(p1.X - p2.X) < 0.01 || Math.Abs(p1.Y - p2.Y) < 0.01)
            {
                return maxX >= Left && minX <= Right && maxY >= Top && minY <= Bottom;
            }

            // Repli pour un segment diagonal (algorithme de Liang-Barsky)
            return IntersectsDiagonal(p1, p2);
        }

        private bool IntersectsDiagonal(DiagramPoint p1, DiagramPoint p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double[] p = [-dx, dx, -dy, dy];
            double[] q = [p1.X - Left, Right - p1.X, p1.Y - Top, Bottom - p1.Y];
            double u1 = 0.0, u2 = 1.0;

            for (int i = 0; i < 4; i++)
            {
                if (Math.Abs(p[i]) < 1e-6)
                {
                    if (q[i] < 0) return false;
                }
                else
                {
                    double t = q[i] / p[i];
                    if (p[i] < 0) u1 = Math.Max(u1, t);
                    else u2 = Math.Min(u2, t);
                }
            }

            return u1 <= u2;
        }
    }

    /// <summary>
    /// Calcule les waypoints orthogonaux entre startPoint et endPoint en intégrant
    /// le fanOffset et en évitant les boîtes d'obstacles.
    /// </summary>
    public static IReadOnlyList<DiagramPoint> BuildOrthogonalWaypoints(
        DiagramPoint startPoint,
        DiagramPoint endPoint,
        bool isHorizontalDominant,
        double baseFanOffset,
        IReadOnlyList<BoundingBox> obstacles,
        int gridSize = 10)
    {
        ArgumentNullException.ThrowIfNull(obstacles);

        if (isHorizontalDominant)
        {
            double baseMidX = GridSnapper.Snap((startPoint.X + endPoint.X) / 2.0, gridSize) + baseFanOffset;

            foreach (double corridorOffset in CandidateOffsets)
            {
                double candidateMidX = GridSnapper.Snap(baseMidX + corridorOffset, gridSize);
                var wp1 = new DiagramPoint(candidateMidX, startPoint.Y);
                var wp2 = new DiagramPoint(candidateMidX, endPoint.Y);

                if (!HasCollision(startPoint, wp1, wp2, endPoint, obstacles))
                {
                    return [wp1, wp2];
                }
            }

            return BuildHorizontalUBend(startPoint, endPoint, baseFanOffset, obstacles, gridSize);
        }
        else
        {
            double baseMidY = GridSnapper.Snap((startPoint.Y + endPoint.Y) / 2.0, gridSize) + baseFanOffset;

            foreach (double corridorOffset in CandidateOffsets)
            {
                double candidateMidY = GridSnapper.Snap(baseMidY + corridorOffset, gridSize);
                var wp1 = new DiagramPoint(startPoint.X, candidateMidY);
                var wp2 = new DiagramPoint(endPoint.X, candidateMidY);

                if (!HasCollision(startPoint, wp1, wp2, endPoint, obstacles))
                {
                    return [wp1, wp2];
                }
            }

            return BuildVerticalUBend(startPoint, endPoint, baseFanOffset, obstacles, gridSize);
        }
    }

    private static IReadOnlyList<DiagramPoint> BuildHorizontalUBend(
        DiagramPoint start,
        DiagramPoint end,
        double fanOffset,
        IReadOnlyList<BoundingBox> obstacles,
        int gridSize)
    {
        double baseY = GridSnapper.Snap(Math.Min(start.Y, end.Y), gridSize);
        foreach (double offset in CandidateOffsets.Skip(1))
        {
            double detourY = GridSnapper.Snap(baseY + offset + fanOffset, gridSize);
            var wp1 = new DiagramPoint(start.X, detourY);
            var wp2 = new DiagramPoint(end.X, detourY);
            if (!HasCollision(start, wp1, wp2, end, obstacles))
            {
                return [wp1, wp2];
            }
        }

        double fallbackMidX = GridSnapper.Snap(((start.X + end.X) / 2.0) + fanOffset, gridSize);
        return [new DiagramPoint(fallbackMidX, start.Y), new DiagramPoint(fallbackMidX, end.Y)];
    }

    private static IReadOnlyList<DiagramPoint> BuildVerticalUBend(
        DiagramPoint start,
        DiagramPoint end,
        double fanOffset,
        IReadOnlyList<BoundingBox> obstacles,
        int gridSize)
    {
        double baseX = GridSnapper.Snap(Math.Min(start.X, end.X), gridSize);
        foreach (double offset in CandidateOffsets.Skip(1))
        {
            double detourX = GridSnapper.Snap(baseX + offset + fanOffset, gridSize);
            var wp1 = new DiagramPoint(detourX, start.Y);
            var wp2 = new DiagramPoint(detourX, end.Y);
            if (!HasCollision(start, wp1, wp2, end, obstacles))
            {
                return [wp1, wp2];
            }
        }

        double fallbackMidY = GridSnapper.Snap(((start.Y + end.Y) / 2.0) + fanOffset, gridSize);
        return [new DiagramPoint(start.X, fallbackMidY), new DiagramPoint(end.X, fallbackMidY)];
    }

    private static bool HasCollision(
        DiagramPoint p0,
        DiagramPoint p1,
        DiagramPoint p2,
        DiagramPoint p3,
        IReadOnlyList<BoundingBox> obstacles)
    {
        foreach (var box in obstacles)
        {
            if (box.IntersectsSegment(p0, p1) ||
                box.IntersectsSegment(p1, p2) ||
                box.IntersectsSegment(p2, p3))
            {
                return true;
            }
        }

        return false;
    }
}
