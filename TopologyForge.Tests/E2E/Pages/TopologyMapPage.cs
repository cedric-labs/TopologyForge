using Microsoft.Playwright;

namespace TopologyForge.Tests.E2E.Pages;

/// <summary>
/// Page Object Model encapsulant les interactions avec la carte topologique rÃ©seau interactive HTML5.
/// </summary>
public sealed class TopologyMapPage(IPage page)
{
    public IPage Page { get; } = page;

    // Header & KPIs
    public ILocator TitleHeading => Page.Locator(".title-block h1");
    public ILocator SubTitle => Page.Locator(".title-block span");
    public ILocator KpiServers => Page.Locator("#kpiNodes");
    public ILocator KpiFlows => Page.Locator("#kpiEdges");
    public ILocator KpiPorts => Page.Locator("#kpiPorts");

    // Sidebar & Panneau de configuration
    public ILocator BtnToggleSidebar => Page.Locator("#btnToggleSidebar");
    public ILocator SidebarPanel => Page.Locator("#sidebarPanel");

    // SÃ©lecteurs d'affichage & layout
    public ILocator LayoutSelect => Page.Locator("#layoutSelect");
    public ILocator SortSelect => Page.Locator("#sortSelect");
    public ILocator ShowZonesCheckbox => Page.Locator("#showZonesCheck");
    public ILocator RoutingSelect => Page.Locator("#routingSelect");
    public ILocator SpacingSelect => Page.Locator("#spacingSelect");
    public ILocator NodeGapRange => Page.Locator("#nodeGapRange");
    public ILocator EdgeFanRange => Page.Locator("#edgeFanRange");
    public ILocator NodeGapValue => Page.Locator("#nodeGapVal");
    public ILocator EdgeFanValue => Page.Locator("#edgeFanVal");

    // Filtres
    public ILocator SearchInput => Page.Locator("#searchInput");
    public ILocator IncludePortsInput => Page.Locator("#includePortsInput");
    public ILocator ExcludePortsInput => Page.Locator("#excludePortsInput");
    public ILocator ExcludeHostsInput => Page.Locator("#excludeHostsInput");
    public ILocator DirectionSelect => Page.Locator("#directionSelect");
    public ILocator BtnApplyFilters => Page.Locator("#btnApplyFilters");
    public ILocator BtnClearFilters => Page.Locator("#btnClearFilters");

    // Actions & CLI
    public ILocator BtnResetZoom => Page.Locator("#btnResetZoom");
    public ILocator BtnRecomputeLayout => Page.Locator("#btnRecomputeLayout");
    public ILocator BtnExportSvg => Page.Locator("#btnExportSvg");
    public ILocator BtnCopyCliCommand => Page.Locator("#btnCopyCliCommand");
    public ILocator CliCommandText => Page.Locator("#cliCommandText");

    // Inspecteur de serveur
    public ILocator InspectorPanel => Page.Locator("#inspectorPanel");
    public ILocator InspectorContent => Page.Locator("#inspectorContent");

    // Canevas SVG & couches graphiques
    public ILocator SvgCanvas => Page.Locator("#topologySvg");
    public ILocator ViewportGroup => Page.Locator("#viewportGroup");
    public ILocator ZonesLayer => Page.Locator("#zonesLayer");
    public ILocator SubnetZones => Page.Locator("#zonesLayer > g.subnet-zone");
    public ILocator EdgesLayer => Page.Locator("#edgesLayer");
    public ILocator NodesLayer => Page.Locator("#nodesLayer");
    public ILocator LabelsLayer => Page.Locator("#labelsLayer");
    public ILocator Nodes => Page.Locator("#nodesLayer > g");
    public ILocator Edges => Page.Locator("#edgesLayer > path");
    public ILocator Labels => Page.Locator("#labelsLayer > text");

    // Pagination
    public ILocator PaginationBar => Page.Locator(".pagination-bar");
    public ILocator BtnPrevPage => Page.Locator("#btnPrevPage");
    public ILocator BtnNextPage => Page.Locator("#btnNextPage");
    public ILocator BtnLoadMore100 => Page.Locator("#btnLoadMore100");
    public ILocator BtnShowAll => Page.Locator("#btnShowAll");
    public ILocator PageStatus => Page.Locator("#pageStatus");

    // LÃ©gende
    public ILocator LegendContainer => Page.Locator(".legend");

    public Task NavigateToFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var normalizedPath = Path.GetFullPath(filePath).Replace('\\', '/');
        var fileUri = normalizedPath.StartsWith('/') ? $"file://{normalizedPath}" : $"file:///{normalizedPath}";
        return Page.GotoAsync(fileUri, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
    }

    public Task ToggleSidebarAsync() => BtnToggleSidebar.ClickAsync();

    public async Task SearchAsync(string query)
    {
        await SearchInput.FillAsync(query).ConfigureAwait(false);
        await BtnApplyFilters.ClickAsync().ConfigureAwait(false);
    }

    public async Task FilterByPortsAsync(string? includePorts = null, string? excludePorts = null)
    {
        if (includePorts is not null)
        {
            await IncludePortsInput.FillAsync(includePorts).ConfigureAwait(false);
        }

        if (excludePorts is not null)
        {
            await ExcludePortsInput.FillAsync(excludePorts).ConfigureAwait(false);
        }

        await BtnApplyFilters.ClickAsync().ConfigureAwait(false);
    }

    public Task SelectDirectionAsync(string direction) => DirectionSelect.SelectOptionAsync(direction);

    public Task SelectLayoutAsync(string layout) => LayoutSelect.SelectOptionAsync(layout);

    public Task SelectSortAsync(string sort) => SortSelect.SelectOptionAsync(sort);

    public Task SelectRoutingAsync(string routing) => RoutingSelect.SelectOptionAsync(routing);

    public Task ClearFiltersAsync() => BtnClearFilters.ClickAsync();

    public Task ResetZoomAsync() => BtnResetZoom.ClickAsync();

    public Task ResetPositionsAsync() => BtnRecomputeLayout.ClickAsync();

    public Task ClickNodeAsync(int index) => Nodes.Nth(index).ClickAsync();

    public Task<int> GetNodeCountAsync() => Nodes.CountAsync();

    public Task<int> GetEdgeCountAsync() => Edges.CountAsync();

    public ILocator EdgeById(string edgeId) => Page.Locator($"#edgesLayer > path[data-edge-id='{edgeId}']");

    public Task ClickNodeByHostNameAsync(string hostName) =>
        Nodes.Filter(new LocatorFilterOptions { HasText = hostName }).First.ClickAsync();

    public Task<int> GetInspectorImageCountAsync() => InspectorContent.Locator("img").CountAsync();

    public Task<bool> IsGlobalDefinedAsync(string name) =>
        Page.EvaluateAsync<bool>("name => typeof window[name] !== 'undefined'", name);

    /// <summary>
    /// Distance en pixels entre les points médians de deux arêtes, mesurée sur la géométrie SVG réelle.
    /// </summary>
    public Task<double> GetEdgeMidpointDistanceAsync(string firstEdgeId, string secondEdgeId) =>
        MeasureDistanceAsync(firstEdgeId, secondEdgeId,
            "path => { const p = path.getPointAtLength(path.getTotalLength() / 2); return [p.x, p.y]; }");

    /// <summary>
    /// Distance en pixels entre les milieux des cordes (segments extrémité-extrémité) de deux arêtes :
    /// c'est l'écart qu'auraient les deux arêtes si elles étaient tracées en ligne droite.
    /// </summary>
    public Task<double> GetEdgeChordMidpointDistanceAsync(string firstEdgeId, string secondEdgeId) =>
        MeasureDistanceAsync(firstEdgeId, secondEdgeId,
            "path => { const a = path.getPointAtLength(0), b = path.getPointAtLength(path.getTotalLength()); return [(a.x + b.x) / 2, (a.y + b.y) / 2]; }");

    /// <summary>
    /// Clé de paire non orientée que la page calcule pour regrouper les flux entre deux serveurs.
    /// </summary>
    public Task<string> UndirectedPairKeyAsync(string firstNodeId, string secondNodeId) =>
        Page.EvaluateAsync<string>("a => undirectedPairKey(a[0], a[1])", new[] { firstNodeId, secondNodeId });

    /// <summary>
    /// Évalue le test de collision de la page entre un segment et la boîte d'un serveur placée en (boxX, boxY).
    /// </summary>
    public Task<bool> SegmentIntersectsNodeBoxAsync(double x1, double y1, double x2, double y2, double boxX, double boxY, double padding) =>
        Page.EvaluateAsync<bool>(
            "a => segmentIntersectsBox(a[0], a[1], a[2], a[3], { x: a[4], y: a[5] }, a[6])",
            new[] { x1, y1, x2, y2, boxX, boxY, padding });

    /// <summary>
    /// Attribut <c>d</c> du tracé orthogonal que la page construit entre deux ancrages, avec un point de passage imposé.
    /// </summary>
    public Task<string> BuildOrthogonalEdgePathAsync(double startX, double startY, string startSide, double endX, double endY, string endSide, double waypointX, double waypointY) =>
        Page.EvaluateAsync<string>(
            "a => buildRoutedEdgePath({ x: a[0], y: a[1], side: a[2] }, { x: a[3], y: a[4], side: a[5] }, 'orthogonal', 0, { x: a[6], y: a[7] }).d",
            new object[] { startX, startY, startSide, endX, endY, endSide, waypointX, waypointY });

    /// <summary>
    /// Rectangles [x, y, largeur, hauteur] des zones de sous-réseau affichées.
    /// </summary>
    public Task<double[][]> GetSubnetZoneBoxesAsync() =>
        ZonesLayer.EvaluateAsync<double[][]>(
            "layer => Array.from(layer.querySelectorAll('g.subnet-zone > rect:first-of-type')).map(r => ['x', 'y', 'width', 'height'].map(name => Number(r.getAttribute(name))))");

    /// <summary>
    /// Rectangles [x, y, largeur, hauteur] des étiquettes de flèches affichées, mesurés sur le texte rendu.
    /// </summary>
    public Task<double[][]> GetEdgeLabelBoxesAsync() =>
        LabelsLayer.EvaluateAsync<double[][]>(
            "layer => Array.from(layer.querySelectorAll(':scope > text')).map(t => { const b = t.getBBox(); return [b.x, b.y, b.width, b.height]; })");

    /// <summary>
    /// Rectangles [x, y, largeur, hauteur] des boîtes de serveurs affichées.
    /// </summary>
    public Task<double[][]> GetNodeBoxesAsync() =>
        NodesLayer.EvaluateAsync<double[][]>(
            "layer => Array.from(layer.querySelectorAll(':scope > g')).map(g => { const m = /translate\\(([-\\d.]+),\\s*([-\\d.]+)\\)/.exec(g.getAttribute('transform')); const r = g.querySelector('rect'); return [Number(m[1]), Number(m[2]), Number(r.getAttribute('width')), Number(r.getAttribute('height'))]; })");

    /// <summary>
    /// Ordonnées que la page attribue à <paramref name="count"/> étiquettes identiques demandées au même point (x, y).
    /// </summary>
    public Task<double[]> PlaceIdenticalEdgeLabelsAsync(int count, double x, double y) =>
        Page.EvaluateAsync<double[]>(
            "a => placeEdgeLabels(Array.from({ length: a[0] }, () => ({ x: a[1], y: a[2], text: 'OUT :443/TCP' }))).map(p => p.y)",
            new[] { count, x, y });

    /// <summary>
    /// Coin supérieur gauche [x, y] de la boîte du serveur affiché sous ce nom.
    /// </summary>
    public Task<double[]> GetNodeOriginAsync(string hostName) =>
        Nodes.Filter(new LocatorFilterOptions { HasText = hostName }).First.EvaluateAsync<double[]>(
            "g => { const m = /translate\\(([-\\d.]+),\\s*([-\\d.]+)\\)/.exec(g.getAttribute('transform')); return [Number(m[1]), Number(m[2])]; }");

    private async Task<double> MeasureDistanceAsync(string firstEdgeId, string secondEdgeId, string pointScript)
    {
        var first = await EdgeById(firstEdgeId).EvaluateAsync<double[]>(pointScript).ConfigureAwait(false);
        var second = await EdgeById(secondEdgeId).EvaluateAsync<double[]>(pointScript).ConfigureAwait(false);
        return Math.Sqrt(Math.Pow(first[0] - second[0], 2) + Math.Pow(first[1] - second[1], 2));
    }
}
