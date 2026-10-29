namespace TopologyForge.Export.Drawio.Styling;

using System.Net;
using System.Text;
using TopologyForge.Export.Drawio.Model;

/// <summary>
/// Builds minified HTML fragments for device cards in Draw.io.
/// </summary>
public static class DrawioHtmlCardBuilder
{
    private const int BaseHeaderAndIpHeight = 64;
    private const int BadgeRowHeight = 22;
    private const int BadgesPerRow = 3;
    private const int MaxDisplayedBadges = 9;

    /// <summary>
    /// Computes the height the card needs for its number of badges,
    /// aligned on the magnetic grid step.
    /// </summary>
    public static double ComputeCardHeight(int badgeCount, int gridSize = 10)
    {
        if (badgeCount <= 0)
        {
            return SnapToGrid(BaseHeaderAndIpHeight, gridSize);
        }

        int visibleBadges = Math.Min(badgeCount, MaxDisplayedBadges + 1);
        int rows = (int)Math.Ceiling(visibleBadges / (double)BadgesPerRow);
        int rawHeight = BaseHeaderAndIpHeight + (rows * BadgeRowHeight) + 8;

        return SnapToGrid(rawHeight, gridSize);
    }

    /// <summary>
    /// Builds the minified HTML fragment on a single line (no \n), as Draw.io requires.
    /// </summary>
    public static string BuildCardHtml(
        DiagramNode node,
        DiagramNodeStyle style,
        string headerBgColor = "#1e293b",
        string accentColor = "#38bdf8")
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(style);

        string safeRole = WebUtility.HtmlEncode(node.RoleKey.ToUpperInvariant());
        string safeTitle = WebUtility.HtmlEncode(node.Label);
        string safeSubtitle = WebUtility.HtmlEncode(node.Subtitle ?? string.Empty);

        var sb = new StringBuilder(512);

        // Minified root container
        sb.Append($"<div style=\"width:100%;height:100%;box-sizing:border-box;font-family:{style.FontFamily};color:{style.FontColor};\">");

        // Header band
        sb.Append($"<div style=\"background-color:{headerBgColor};border-bottom:1px solid {style.StrokeColor};padding:6px 10px;\">");
        sb.Append($"<div style=\"font-size:9px;font-weight:700;letter-spacing:0.6px;color:{accentColor};margin-bottom:2px;\">{safeRole}</div>");
        sb.Append($"<div style=\"font-size:{style.FontSize}px;font-weight:700;color:{style.FontColor};white-space:nowrap;overflow:hidden;text-overflow:ellipsis;\">{safeTitle}</div>");
        sb.Append("</div>");

        // Corps (Sous-titre / IP + Badges)
        sb.Append("<div style=\"padding:6px 10px;\">");
        if (!string.IsNullOrWhiteSpace(safeSubtitle))
        {
            sb.Append($"<div style=\"font-family:Consolas,monospace;font-size:11px;color:#94a3b8;margin-bottom:6px;\">{safeSubtitle}</div>");
        }

        if (node.Badges is { Count: > 0 } badges)
        {
            sb.Append("<div style=\"line-height:1.4;\">");
            int count = Math.Min(badges.Count, MaxDisplayedBadges);
            for (int i = 0; i < count; i++)
            {
                string safeBadge = WebUtility.HtmlEncode(badges[i]);
                sb.Append($"<span style=\"display:inline-block;background-color:{headerBgColor};border:1px solid #475569;color:#e2e8f0;font-family:Consolas,monospace;font-size:9px;padding:1px 5px;border-radius:4px;margin:0 4px 4px 0;\">{safeBadge}</span>");
            }

            if (badges.Count > MaxDisplayedBadges)
            {
                int remaining = badges.Count - MaxDisplayedBadges;
                sb.Append($"<span style=\"display:inline-block;background-color:#0f172a;border:1px dashed {accentColor};color:{accentColor};font-family:Consolas,monospace;font-size:9px;font-weight:700;padding:1px 5px;border-radius:4px;margin:0 4px 4px 0;\">+{remaining}</span>");
            }
            sb.Append("</div>");
        }

        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static double SnapToGrid(double value, int gridSize) =>
        gridSize <= 1 ? Math.Round(value) : Math.Ceiling(value / gridSize) * gridSize;
}
