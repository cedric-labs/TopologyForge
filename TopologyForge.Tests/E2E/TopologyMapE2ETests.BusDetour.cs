using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using TopologyForge.Models;
using TopologyForge.Rendering;

namespace TopologyForge.Tests.E2E;

// Routage en bus dans les dispositions en arbre : une branche qui viserait une rangée éloignée en traversant
// les boîtes des rangées intermédiaires doit les contourner par les couloirs entre colonnes.
public sealed partial class TopologyMapE2ETests
{
    [Fact]
    public async Task RoutingSelect_WhenBusBranchDescendsPastTreeRows_GoesAroundTheBoxes()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateSubnetTopologyGraph(("192.168.10", 8)), BusTreeOptions(TopologyEdgeLabelMode.None));

        var crossings = await FindBoxCrossingsAsync();

        crossings.Should().BeEmpty();
    }

    [Fact]
    public async Task RoutingSelect_WhenBusBranchCrossesHorizontalTreeColumns_GoesAroundTheBoxes()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(
            CreateSubnetTopologyGraph(("192.168.10", 8)),
            BusTreeOptions(TopologyEdgeLabelMode.Ports) with { DefaultLayoutMode = TopologyLayoutMode.HorizontalTree });

        var crossings = await FindBoxCrossingsAsync();

        crossings.Should().BeEmpty();
    }

    [Fact]
    public async Task RoutingSelect_WhenBusBranchesOfTwoServersShareACorridor_KeepThemOnSeparateLanes()
    {
        _mapPage.Should().NotBeNull();

        // Les deux serveurs audités visent les mêmes cibles : leurs branches descendent dans les mêmes couloirs, où elles
        // ne doivent pas se superposer. Seuls comptent les segments verticaux avant le dernier : les branches qui visent
        // une même cible se rejoignent ensuite dans l'écart qui la précède, jusqu'à leur pointe commune.
        await OpenGraphAsync(CreateTwoSourceSubnetGraph(), BusTreeOptions(TopologyEdgeLabelMode.None));

        var paths = await _mapPage!.GetEdgePathDefinitionsAsync();
        var segments = paths.SelectMany(p => ParsePathSegments(p[1]).SkipLast(1).Where(IsVertical).Select(s => (Source: p[0].Split("::")[0], Segment: s))).ToList();

        var overlaps = segments.SelectMany(a => segments.Where(b => string.CompareOrdinal(a.Source, b.Source) < 0 && AreCollinearAndOverlapping(a.Segment, b.Segment)));
        overlaps.Should().BeEmpty();
    }

    [Theory]
    [InlineData(TopologyLayoutMode.Tree)]
    [InlineData(TopologyLayoutMode.HorizontalTree)]
    public async Task RoutingSelect_WhenBusBranchGoesAroundTreeRows_LeavesThePortLabelsUncrossed(TopologyLayoutMode layout)
    {
        _mapPage.Should().NotBeNull();

        // Une étiquette se lit sur la flèche qui aboutit à sa cible ; un segment qui la barre en travers la rend illisible.
        await OpenGraphAsync(CreateTwoSourceSubnetGraph(), BusTreeOptions(TopologyEdgeLabelMode.Ports) with { DefaultLayoutMode = layout });

        var labels = await _mapPage!.GetEdgeLabelBoxesAsync();
        var paths = await _mapPage.GetEdgePathDefinitionsAsync();
        var acrossArrivals = paths.SelectMany(p => ParsePathSegments(p[1]))
            .Where(s => IsVertical(s) == (layout == TopologyLayoutMode.HorizontalTree));

        acrossArrivals.Where(s => labels.Any(label => SegmentEntersBox(s, label))).Should().BeEmpty();
    }

    // Deux serveurs audités joignent chacun les huit serveurs d'un même sous-réseau.
    private static NetworkTopologyGraph CreateTwoSourceSubnetGraph()
    {
        List<TopologyServerNode> nodes = [Audited("desktop-a", "192.168.1.250"), Audited("desktop-b", "192.168.1.251")];
        nodes.AddRange(Enumerable.Range(1, 8).Select(i => Remote($"srv-{i}", $"192.168.10.{10 + i}")));
        var edges = nodes.Skip(2)
            .SelectMany(n => new[] { Flow($"A{n.NodeId}", "desktop-a", n.NodeId), Flow($"B{n.NodeId}", "desktop-b", n.NodeId) })
            .ToList();
        return new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0);
    }

    // Segments de tracé qui entrent dans une boîte de serveur ; un segment qui part du bord d'une boîte ou y aboutit n'y entre pas.
    private async Task<string[]> FindBoxCrossingsAsync()
    {
        var boxes = await _mapPage!.GetNodeBoxesAsync();
        var paths = await _mapPage.GetEdgePathDefinitionsAsync();
        return [.. paths.SelectMany(p => ParsePathSegments(p[1])
            .Where(s => boxes.Any(box => SegmentEntersBox(s, box)))
            .Select(s => string.Create(CultureInfo.InvariantCulture, $"{p[0]} : ({s[0]}, {s[1]}) -> ({s[2]}, {s[3]})")))];
    }

    private static double[][] ParsePathSegments(string d)
    {
        var points = Regex.Matches(d, @"(-?[\d.]+) (-?[\d.]+)")
            .Select(m => new[] { double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) })
            .ToList();
        return [.. points.Zip(points.Skip(1), (a, b) => new[] { a[0], a[1], b[0], b[1] })];
    }

    // Les tracés en bus sont faits de segments horizontaux ou verticaux : il suffit de comparer des intervalles.
    private static bool SegmentEntersBox(double[] s, double[] box)
    {
        const double inset = 1;
        var (left, top, right, bottom) = (box[0] + inset, box[1] + inset, box[0] + box[2] - inset, box[1] + box[3] - inset);
        return Math.Max(s[0], s[2]) > left && Math.Min(s[0], s[2]) < right && Math.Max(s[1], s[3]) > top && Math.Min(s[1], s[3]) < bottom;
    }

    private static bool IsVertical(double[] s) => Math.Abs(s[0] - s[2]) < 0.5;

    private static bool AreCollinearAndOverlapping(double[] a, double[] b)
    {
        if (IsVertical(a) != IsVertical(b)) return false;
        var (fixedIndex, movingIndex) = IsVertical(a) ? (0, 1) : (1, 0);
        if (Math.Abs(a[fixedIndex] - b[fixedIndex]) >= 1) return false;
        var overlap = Math.Min(Math.Max(a[movingIndex], a[movingIndex + 2]), Math.Max(b[movingIndex], b[movingIndex + 2]))
            - Math.Max(Math.Min(a[movingIndex], a[movingIndex + 2]), Math.Min(b[movingIndex], b[movingIndex + 2]));
        return overlap > 1;
    }
}
