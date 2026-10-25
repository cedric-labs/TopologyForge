namespace TopologyForge.Geometry;

/// <summary>
/// Utilitaire d'alignement sur la grille magnétique Draw.io et de calcul d'ancres relatives fiabilisées.
/// </summary>
public static class GridSnapper
{
    /// <summary>
    /// Aligne une valeur scalaire sur le multiple de grille le plus proche.
    /// </summary>
    public static double Snap(double value, int gridSize = 10)
    {
        if (gridSize <= 1)
        {
            return Math.Round(value);
        }

        return Math.Round(value / gridSize, MidpointRounding.AwayFromZero) * gridSize;
    }

    /// <summary>
    /// Répartit N ancres sur une tranche de boîte [boxOrigin, boxOrigin + boxLength]
    /// en garantissant que la coordonnée absolue résultante tombe sur un pas de grille,
    /// puis retourne à la fois le ratio normalisé [0..1] pour Draw.io et la coordonnée absolue.
    /// </summary>
    public static (double NormalizedRatio, double AbsoluteCoord) ComputeGridAlignedAnchor(
        double boxOrigin,
        double boxLength,
        int siblingIndex,
        int siblingCount,
        int gridSize = 10)
    {
        if (siblingCount <= 1 || boxLength <= gridSize * 2)
        {
            double midCoord = Snap(boxOrigin + (boxLength / 2.0), gridSize);
            double midRatio = Math.Clamp((midCoord - boxOrigin) / boxLength, 0.1, 0.9);
            return (Math.Round(midRatio, 4), midCoord);
        }

        double rawRatio = (siblingIndex + 1.0) / (siblingCount + 1.0);
        double rawAbsCoord = boxOrigin + (boxLength * rawRatio);

        double minStepRequired = siblingCount * gridSize;
        double finalAbsCoord = boxLength >= minStepRequired * 1.5
            ? Snap(rawAbsCoord, gridSize)
            : Math.Round(rawAbsCoord, 1);

        double minCoord = boxOrigin + Math.Max(gridSize, boxLength * 0.12);
        double maxCoord = boxOrigin + boxLength - Math.Max(gridSize, boxLength * 0.12);
        finalAbsCoord = Math.Clamp(finalAbsCoord, minCoord, maxCoord);

        double alignedRatio = (finalAbsCoord - boxOrigin) / boxLength;
        return (Math.Round(alignedRatio, 4), finalAbsCoord);
    }
}
