namespace TopologyForge.Export.Drawio.Adapters;

/// <summary>
/// Places rectangles left to right and starts a new row when the next one would exceed a maximum width.
/// Each row is as tall as its tallest rectangle, so rectangles of different heights never overlap.
/// </summary>
internal static class RowPacker
{
    /// <summary>
    /// Returns the top-left corner of each rectangle, in the order of <paramref name="sizes"/>.
    /// A rectangle wider than <paramref name="maxRowWidth"/> still gets a row of its own.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)> Pack(
        IReadOnlyList<(double Width, double Height)> sizes,
        double originX,
        double originY,
        double maxRowWidth,
        double horizontalGap,
        double verticalGap)
    {
        var positions = new List<(double X, double Y)>(sizes.Count);
        double x = originX;
        double y = originY;
        double rowHeight = 0.0;

        foreach (var (width, height) in sizes)
        {
            bool startsNewRow = x > originX && x + width > originX + maxRowWidth;
            if (startsNewRow)
            {
                y += rowHeight + verticalGap;
                x = originX;
                rowHeight = 0.0;
            }

            positions.Add((x, y));
            x += width + horizontalGap;
            rowHeight = Math.Max(rowHeight, height);
        }

        return positions;
    }
}
