using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;
using TopologyForge.Models;
using TopologyForge.Rendering;
using TopologyForge.Tests.E2E.Pages;
using TopologyForge.Tests.E2E.ScriptCoverage;

namespace TopologyForge.Tests.E2E;

/// <summary>
/// Suite de tests E2E Playwright automatisÃ©s validant le rendu structurel SVG, l'interactivitÃ© des contrÃ´les et la prÃ©cision des filtres.
/// </summary>
public sealed class TopologyMapE2ETests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private TopologyMapPage? _mapPage;
    private MapScriptCoverageRecorder? _coverageRecorder;
    private readonly List<string> _tempHtmlPaths = [];
    private readonly List<string> _pageErrors = [];

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _page = await _browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        _page.PageError += (_, message) => _pageErrors.Add(message);
        if (MapScriptLcovReport.OutputPath is not null)
        {
            _coverageRecorder = await MapScriptCoverageRecorder.StartAsync(_page);
        }

        _mapPage = new TopologyMapPage(_page);
        await OpenGraphAsync(CreateSampleTopologyGraph(), new TopologyRenderingOptions { Title = "E2E Home Lab Map" });
    }

    private async Task OpenGraphAsync(NetworkTopologyGraph graph, TopologyRenderingOptions options)
    {
        var htmlPath = Path.Combine(Path.GetTempPath(), $"topology_e2e_{Guid.NewGuid():N}.html");
        _tempHtmlPaths.Add(htmlPath);
        await new TopologyHtmlRenderer().WriteHtmlFileAsync(htmlPath, graph, options);
        if (_coverageRecorder is not null)
        {
            await _coverageRecorder.CollectAsync();
        }

        await _mapPage!.NavigateToFileAsync(htmlPath);
    }

    public async Task DisposeAsync()
    {
        if (_coverageRecorder is not null)
        {
            await _coverageRecorder.CollectAsync();
        }

        if (_page is not null)
        {
            await _page.CloseAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();

        foreach (var htmlPath in _tempHtmlPaths.Where(File.Exists))
        {
            File.Delete(htmlPath);
        }
    }

    [Fact]
    public async Task RenderStructure_WhenPageLoaded_DisplaysAllContainersAndSvgLayers()
    {
        _mapPage.Should().NotBeNull();
        (await _mapPage!.TitleHeading.IsVisibleAsync()).Should().BeTrue();
        (await _mapPage.TitleHeading.InnerTextAsync()).Should().Contain("E2E Home Lab Map");

        (await _mapPage.SidebarPanel.IsVisibleAsync()).Should().BeTrue();
        (await _mapPage.SvgCanvas.IsVisibleAsync()).Should().BeTrue();
        (await _mapPage.ViewportGroup.IsVisibleAsync()).Should().BeTrue();
        (await _mapPage.LegendContainer.IsVisibleAsync()).Should().BeTrue();
        (await _mapPage.PaginationBar.IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task RenderStructure_WhenPageLoaded_RendersSvgNodesAndEdgesCorrectly()
    {
        _mapPage.Should().NotBeNull();
        var nodeCount = await _mapPage!.GetNodeCountAsync();
        var edgeCount = await _mapPage.GetEdgeCountAsync();

        nodeCount.Should().Be(5);
        edgeCount.Should().Be(5);

        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("5");
        (await _mapPage.KpiFlows.InnerTextAsync()).Should().Be("5");
    }

    [Fact]
    public async Task BtnToggleSidebar_WhenClicked_TogglesSidebarCollapsedStateAndLabel()
    {
        _mapPage.Should().NotBeNull();
        (await _mapPage!.BtnToggleSidebar.InnerTextAsync()).Should().Contain("Sidebar");

        await _mapPage.ToggleSidebarAsync();
        var isCollapsed = await _mapPage.SidebarPanel.EvaluateAsync<bool>("el => el.classList.contains('collapsed')");
        isCollapsed.Should().BeTrue();

        await _mapPage.ToggleSidebarAsync();
        var isExpanded = await _mapPage.SidebarPanel.EvaluateAsync<bool>("el => !el.classList.contains('collapsed')");
        isExpanded.Should().BeTrue();
    }

    [Fact]
    public async Task FilterByPorts_WhenPortExcluded_PrunesOrphanNodesAndDecreasesCount()
    {
        _mapPage.Should().NotBeNull();
        (await _mapPage!.GetNodeCountAsync()).Should().Be(5);

        // Exclure le port 873 (nas n'a que ce port et ce flux)
        await _mapPage.FilterByPortsAsync(excludePorts: "873");

        var nodeCountAfter = await _mapPage.GetNodeCountAsync();
        var edgeCountAfter = await _mapPage.GetEdgeCountAsync();

        nodeCountAfter.Should().Be(4);
        edgeCountAfter.Should().Be(4);
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("4");
        (await _mapPage.KpiFlows.InnerTextAsync()).Should().Be("4");
    }

    [Fact]
    public async Task FilterByPorts_WhenPortIncluded_IsolatesTargetPortNodesAndEdges()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.FilterByPortsAsync(includePorts: "8123");

        var nodeCountAfter = await _mapPage.GetNodeCountAsync();
        var edgeCountAfter = await _mapPage.GetEdgeCountAsync();

        // Seuls desktop et homeassistant participent au port 8123
        nodeCountAfter.Should().Be(2);
        edgeCountAfter.Should().Be(1);
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("2");
        (await _mapPage.KpiFlows.InnerTextAsync()).Should().Be("1");
    }

    [Fact]
    public async Task Search_WhenServerNameMatched_ShowsServerWithItsNeighboursAndFlows()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.SearchAsync("sso");

        // sso et son seul voisin desktop, reliés par le flux E4
        (await _mapPage.GetNodeCountAsync()).Should().Be(2);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(1);
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("2");
    }

    [Fact]
    public async Task Search_WhenOutboundProcessNameMatched_ShowsTheFlowAndItsEndpoints()
    {
        _mapPage.Should().NotBeNull();

        // « curl » n'apparaît que sur l'arête sortante E2 (desktop → proxy)
        await _mapPage!.SearchAsync("curl");

        (await _mapPage.GetNodeCountAsync()).Should().Be(2);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(1);
        (await _mapPage.EdgeById("E2").CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Search_WhenNothingMatchesInTreeLayout_ClearsTheMapWithoutScriptError()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.SelectLayoutAsync("tree");

        await _mapPage.SearchAsync("no-such-server");

        (await _mapPage.GetNodeCountAsync()).Should().Be(0);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(0);
        _pageErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterByPorts_WhenIncludedPortRangeGiven_KeepsEveryPortInTheRange()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.FilterByPortsAsync(includePorts: "8100-8200");

        // 8123 est dans la plage : desktop → homeassistant
        (await _mapPage.GetNodeCountAsync()).Should().Be(2);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FilterByPorts_WhenExcludedPortRangeGiven_RemovesEveryPortInTheRange()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.FilterByPortsAsync(excludePorts: "800-900");

        // 873 est dans la plage : nas disparaît
        (await _mapPage.GetNodeCountAsync()).Should().Be(4);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task FilterByPorts_WhenPortIncluded_RecountsVisibleServicePorts()
    {
        _mapPage.Should().NotBeNull();
        (await _mapPage!.KpiPorts.InnerTextAsync()).Should().Be("5");

        await _mapPage.FilterByPortsAsync(includePorts: "8123");

        // Seul le port d'écoute 8123 de homeassistant reste visible
        (await _mapPage.KpiPorts.InnerTextAsync()).Should().Be("1");
    }

    [Fact]
    public async Task KpiServers_WhenResultsSpanSeveralPages_ShowsTheFilteredTotal()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateSampleTopologyGraph(), new TopologyRenderingOptions { DefaultPageSize = 2 });

        (await _mapPage!.GetNodeCountAsync()).Should().Be(2);
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("5");
        (await _mapPage.KpiFlows.InnerTextAsync()).Should().Be("5");
    }

    [Fact]
    public async Task RenderGraph_WhenTwoServersExchangeFlowsBothWays_DrawsTheTwoArrowsApart()
    {
        _mapPage.Should().NotBeNull();

        // E2 (desktop → proxy) et E3 (proxy → desktop) partagent la même paire de serveurs :
        // l'éventail doit courber les deux flèches de part et d'autre, donc les écarter davantage que leurs cordes.
        var chordDistance = await _mapPage!.GetEdgeChordMidpointDistanceAsync("E2", "E3");

        var curveDistance = await _mapPage.GetEdgeMidpointDistanceAsync("E2", "E3");

        curveDistance.Should().BeGreaterThan(chordDistance + 5);
    }

    [Fact]
    public async Task Inspector_WhenNamesContainMarkup_DisplaysThemAsText()
    {
        _mapPage.Should().NotBeNull();
        const string hostilePayload = "<img src=x onerror=\"window.__xss=1\">";
        var graph = new NetworkTopologyGraph(
            DateTimeOffset.UtcNow,
            [
                new("LOCAL-01", "LOCAL-01", "192.168.1.10", true, [], 0, 1),
                new("REMOTE-01", hostilePayload, "10.0.1.20", false, [new(443, TopologyTransportProtocol.Tcp, hostilePayload, "")], 1, 0)
            ],
            [new("E1", "LOCAL-01", "REMOTE-01", 443, TopologyTransportProtocol.Tcp, hostilePayload, "", 3, TopologyFlowDirection.Outbound)],
            3,
            0);
        await OpenGraphAsync(graph, new TopologyRenderingOptions());

        await _mapPage!.ClickNodeByHostNameAsync("LOCAL-01");

        (await _mapPage.GetInspectorImageCountAsync()).Should().Be(0);
        (await _mapPage.InspectorContent.InnerTextAsync()).Should().Contain(hostilePayload);
        (await _mapPage.IsGlobalDefinedAsync("__xss")).Should().BeFalse();
    }

    [Fact]
    public async Task SelectDirection_WhenFilteredToInbound_HidesOutboundEdges()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.SelectDirectionAsync("IN");

        var edgeCount = await _mapPage.GetEdgeCountAsync();
        edgeCount.Should().Be(1); // Seul le flux IN sur le port 8080

        var nodeCount = await _mapPage.GetNodeCountAsync();
        nodeCount.Should().Be(2); // desktop et proxy (les autres sans flux IN sont élagués)
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("2");
        (await _mapPage.KpiFlows.InnerTextAsync()).Should().Be("1");
    }

    [Fact]
    public async Task SelectDirection_WhenFilteredToOutbound_HidesInboundEdgesAndPrunesNonOutboundNodes()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.SelectDirectionAsync("OUT");

        var edgeCount = await _mapPage.GetEdgeCountAsync();
        edgeCount.Should().Be(4); // 4 flux Outbound

        var nodeCount = await _mapPage.GetNodeCountAsync();
        nodeCount.Should().Be(5); // desktop + 4 serveurs distants connectés en sortie
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("5");
        (await _mapPage.KpiFlows.InnerTextAsync()).Should().Be("4");
    }

    [Fact]
    public async Task BtnClearFilters_WhenClicked_RestoresInitialState()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.FilterByPortsAsync(includePorts: "8123");
        (await _mapPage.GetNodeCountAsync()).Should().Be(2);

        await _mapPage.ClearFiltersAsync();
        (await _mapPage.GetNodeCountAsync()).Should().Be(5);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task BtnClearFilters_WhenClicked_RemovesFiltersFromTheDisplayedCliCommand()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.FilterByPortsAsync(includePorts: "8123");
        (await _mapPage.CliCommandText.InnerTextAsync()).Should().Contain("8123");

        await _mapPage.ClearFiltersAsync();

        (await _mapPage.CliCommandText.InnerTextAsync()).Should().NotContain("8123");
    }

    [Fact]
    public async Task Node_WhenClicked_DisplaysServerDetailsInInspector()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.ClickNodeAsync(0);

        var content = await _mapPage.InspectorContent.InnerTextAsync();
        content.Should().NotContain("Click on a server to isolate");
        content.Should().MatchRegex(@"(desktop|homeassistant|proxy|sso|nas)");
    }

    [Fact]
    public async Task LayoutSelect_WhenChangedToCircular_UpdatesTransformPositions()
    {
        _mapPage.Should().NotBeNull();
        var initialTransform = await _mapPage!.Nodes.First.GetAttributeAsync("transform");

        await _mapPage.SelectLayoutAsync("circular");
        var circularTransform = await _mapPage.Nodes.First.GetAttributeAsync("transform");

        circularTransform.Should().NotBeNull();
    }

    [Fact]
    public async Task RoutingSelect_WhenChangedToOrthogonal_UpdatesPathDefinition()
    {
        _mapPage.Should().NotBeNull();
        var initialPath = await _mapPage!.Edges.First.GetAttributeAsync("d");

        await _mapPage.SelectRoutingAsync("orthogonal");
        var orthogonalPath = await _mapPage.Edges.First.GetAttributeAsync("d");

        orthogonalPath.Should().NotBe(initialPath);
    }

    [Fact]
    public async Task BtnResetZoom_WhenClicked_ResetsViewportTransform()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.ResetZoomAsync();
        var transform = await _mapPage.ViewportGroup.GetAttributeAsync("transform");
        transform.Should().Be("translate(50, 50) scale(1)");
    }

    [Fact]
    public async Task BtnExportSvg_WhenClicked_GeneratesDownload()
    {
        _mapPage.Should().NotBeNull();
        var downloadTask = _page!.WaitForDownloadAsync();
        await _mapPage!.BtnExportSvg.ClickAsync();
        var download = await downloadTask;
        download.SuggestedFilename.Should().Be("network_topology_map.svg");
    }

    [Fact]
    public async Task LayoutSelect_WhenChangedToOrganic_ComputesForceDirectedPositions()
    {
        _mapPage.Should().NotBeNull();
        var initialTransform = await _mapPage!.Nodes.First.GetAttributeAsync("transform");

        await _mapPage.SelectLayoutAsync("organic");
        var organicTransform = await _mapPage.Nodes.First.GetAttributeAsync("transform");

        organicTransform.Should().NotBeNull();
        (await _mapPage.GetNodeCountAsync()).Should().Be(5);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task LayoutSelect_WhenChangedToTree_PositionsNodesInTopDownHierarchy()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.SelectLayoutAsync("tree");

        (await _mapPage.GetNodeCountAsync()).Should().Be(5);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(5);

        var firstTransform = await _mapPage.Nodes.First.GetAttributeAsync("transform");
        firstTransform.Should().Contain("translate(");
    }

    [Fact]
    public async Task LayoutSelect_WhenChangedToHorizontalTree_PositionsNodesInLeftToRightFlow()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.SelectLayoutAsync("horizontal-tree");

        (await _mapPage.GetNodeCountAsync()).Should().Be(5);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(5);

        var firstTransform = await _mapPage.Nodes.First.GetAttributeAsync("transform");
        firstTransform.Should().Contain("translate(");
    }

    [Fact]
    public async Task SortSelect_WhenChangedToTiers_ArrangesFunctionalArchitectureLayers()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.SelectSortAsync("tiers");

        var nodeCount = await _mapPage.GetNodeCountAsync();
        nodeCount.Should().Be(5);
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("5");
    }

    [Fact]
    public async Task SortSelect_WhenChangedToSubnet_DisplaysSubnetZoneEnclosures()
    {
        _mapPage.Should().NotBeNull();

        // Default layout (Sugiyama) has zones hidden initially
        (await _mapPage!.ShowZonesCheckbox.IsCheckedAsync()).Should().BeFalse();
        (await _mapPage.SubnetZones.CountAsync()).Should().Be(0);

        // Selecting subnet sort automatically checks Show Subnet Zones and renders them
        await _mapPage.SelectSortAsync("subnet");
        (await _mapPage.ShowZonesCheckbox.IsCheckedAsync()).Should().BeTrue();
        var zoneCount = await _mapPage.SubnetZones.CountAsync();
        zoneCount.Should().BeGreaterThan(0);

        // Unchecking manually hides the zones without full reload
        await _mapPage.ShowZonesCheckbox.UncheckAsync();
        (await _mapPage.SubnetZones.CountAsync()).Should().Be(0);

        // Re-checking restores the zones
        await _mapPage.ShowZonesCheckbox.CheckAsync();
        (await _mapPage.SubnetZones.CountAsync()).Should().Be(zoneCount);
    }

    [Fact]
    public async Task ShowPortsCheckbox_WhenUnchecked_RemovesArrowLabelsAndKeepsPortsInInspector()
    {
        _mapPage.Should().NotBeNull();
        (await _mapPage!.ShowPortsCheckbox.IsCheckedAsync()).Should().BeTrue();
        (await _mapPage.Labels.CountAsync()).Should().Be(5);

        await _mapPage.ShowPortsCheckbox.UncheckAsync();

        (await _mapPage.Labels.CountAsync()).Should().Be(0);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(5);
        (await _mapPage.CliCommandText.InnerTextAsync()).Should().EndWith("--map-edge-labels none");
        await _mapPage.ClickNodeAsync(0);
        (await _mapPage.InspectorContent.InnerTextAsync()).Should().MatchRegex(@":\d+/(TCP|UDP)");
    }

    [Fact]
    public async Task ShowPortsCheckbox_WhenEdgeLabelModeIsNoneAndChecked_RestoresArrowLabels()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateSampleTopologyGraph(),
            new TopologyRenderingOptions { DefaultEdgeLabelMode = TopologyEdgeLabelMode.None });
        (await _mapPage!.ShowPortsCheckbox.IsCheckedAsync()).Should().BeFalse();
        (await _mapPage.Labels.CountAsync()).Should().Be(0);
        (await _mapPage.CliCommandText.InnerTextAsync()).Should().Contain("--map-edge-labels none");

        await _mapPage.ShowPortsCheckbox.CheckAsync();

        (await _mapPage.Labels.CountAsync()).Should().Be(5);
        (await _mapPage.CliCommandText.InnerTextAsync()).Should().NotContain("--map-edge-labels");
    }

    [Theory]
    [InlineData("nas", "proxy")]
    [InlineData("proxy", "nas")]
    public async Task UndirectedPairKey_WhenEndpointsAreSwapped_ReturnsTheSameOrdinalKey(string firstNodeId, string secondNodeId)
    {
        _mapPage.Should().NotBeNull();

        var key = await _mapPage!.UndirectedPairKeyAsync(firstNodeId, secondNodeId);

        key.Should().Be("nas::proxy");
    }

    [Fact]
    public async Task UndirectedPairKey_WhenIdsDifferOnlyByCase_OrdersUppercaseFirst()
    {
        _mapPage.Should().NotBeNull();

        // Même ordre que l'ancien tri par défaut des chaînes : ordinal UTF-16, majuscules avant minuscules.
        var key = await _mapPage!.UndirectedPairKeyAsync("nas", "NAS");

        key.Should().Be("NAS::nas");
    }

    [Fact]
    public async Task SegmentIntersectsBox_WhenSegmentClipsBoxCorner_ReturnsTrue()
    {
        _mapPage.Should().NotBeNull();

        // Le segment entre dans la boîte (236 × 106 à l'origine) entre (220, 0) et (236, 16), mais son point le plus
        // proche du centre de la boîte est son extrémité (200, -20), hors de la boîte.
        var intersects = await _mapPage!.SegmentIntersectsNodeBoxAsync(200, -20, 260, 40, 0, 0, 0);

        intersects.Should().BeTrue();
    }

    [Fact]
    public async Task SegmentIntersectsBox_WhenSegmentPassesBesideCorner_ReturnsFalse()
    {
        _mapPage.Should().NotBeNull();

        // Le segment passe au-dessus du coin supérieur droit (en x = 236, il est en y = -18).
        var intersects = await _mapPage!.SegmentIntersectsNodeBoxAsync(180, -60, 300, 30, 0, 0, 0);

        intersects.Should().BeFalse();
    }

    [Fact]
    public async Task BuildRoutedEdgePath_WhenOrthogonalDetourIsVertical_PassesThroughTheWaypointRow()
    {
        _mapPage.Should().NotBeNull();

        // Arête horizontale bloquée par une boîte : le point de passage est décalé vers le bas, à la verticale du milieu.
        var d = await _mapPage!.BuildOrthogonalEdgePathAsync(0, 100, "right", 600, 100, "left", 300, 250);

        var yCoordinates = Regex.Matches(d, @"(-?[\d.]+) (-?[\d.]+)").Select(m => double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
        yCoordinates.Should().Contain(250);
    }

    [Fact]
    public async Task SortSelect_WhenSubnetColumnsWrap_KeepsSubnetZonesApart()
    {
        _mapPage.Should().NotBeNull();

        // Le sous-réseau de 9 serveurs occupe 3 colonnes ; la colonne suivante doit commencer après la plus large,
        // pas après la largeur du dernier sous-réseau placé (1 colonne).
        List<TopologyServerNode> nodes = [Audited("desktop", "192.168.1.50")];
        nodes.AddRange(Enumerable.Range(1, 9).Select(i => Remote($"iot-0{i}", $"192.168.10.{10 + i}")));
        nodes.AddRange(new[] { 30, 40, 50, 60 }.Select(subnet => Remote($"vlan-{subnet}", $"192.168.{subnet}.2")));
        var edges = nodes.Skip(1).Select((n, i) => Flow($"E{i}", "desktop", n.NodeId)).ToList();
        await OpenGraphAsync(new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0), new TopologyRenderingOptions());

        await _mapPage!.SelectSortAsync("subnet");

        FindOverlappingBoxes(await _mapPage.GetSubnetZoneBoxesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SortSelect_WhenSwappingServersAcrossSubnetsWouldShortenFlows_KeepsSubnetZonesApart()
    {
        _mapPage.Should().NotBeNull();

        // Échanger nas-02 et backup-01 éviterait deux collisions de flèches, mais mélangerait les deux sous-réseaux.
        List<TopologyServerNode> nodes =
        [
            Audited("desktop", "192.168.1.50"),
            Remote("nas-01", "192.168.10.11"),
            Remote("nas-02", "192.168.10.12"),
            Remote("backup-01", "192.168.20.11"),
            Remote("backup-02", "192.168.20.12")
        ];
        List<TopologyServiceEdge> edges = [Flow("E1", "desktop", "nas-01"), Flow("E2", "desktop", "backup-01"), Flow("E3", "nas-02", "backup-02")];
        await OpenGraphAsync(new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0), new TopologyRenderingOptions());

        await _mapPage!.SelectSortAsync("subnet");

        FindOverlappingBoxes(await _mapPage.GetSubnetZoneBoxesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task LayoutSelect_WhenOrganicWithSeveralAuditedServers_KeepsTheirBoxesApart()
    {
        _mapPage.Should().NotBeNull();
        List<TopologyServerNode> nodes =
        [
            Audited("pve-01", "192.168.1.10"),
            Audited("docker-01", "192.168.1.30"),
            Remote("pihole", "192.168.1.2"),
            Remote("homeassistant", "192.168.1.40")
        ];
        List<TopologyServiceEdge> edges = [Flow("E1", "pve-01", "pihole"), Flow("E2", "docker-01", "pihole"), Flow("E3", "docker-01", "homeassistant")];
        await OpenGraphAsync(new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0), new TopologyRenderingOptions());

        await _mapPage!.SelectLayoutAsync("organic");

        var first = await _mapPage.GetNodeOriginAsync("pve-01");
        var second = await _mapPage.GetNodeOriginAsync("docker-01");
        FindOverlappingBoxes([[first[0], first[1], NodeWidth, NodeHeight], [second[0], second[1], NodeWidth, NodeHeight]]).Should().BeEmpty();
    }

    [Theory]
    [InlineData("grid", "bezier")]
    [InlineData("grid", "orthogonal")]
    [InlineData("tree", "bezier")]
    [InlineData("tree", "orthogonal")]
    [InlineData("layered", "straight")]
    public async Task RenderGraph_WhenManyFlowsLeaveOneServer_KeepsArrowLabelsApart(string layout, string routing)
    {
        _mapPage.Should().NotBeNull();

        // Les flux qui partent d'un même serveur vers des voisins alignés ont des milieux proches :
        // sans écartement, leurs étiquettes « OUT :port/proto » se superposent et deviennent illisibles.
        List<TopologyServerNode> nodes = [Audited("pve-01", "192.168.1.10")];
        nodes.AddRange(Enumerable.Range(1, 6).Select(i => Remote($"lxc-0{i}", $"192.168.1.{20 + i}")));
        var edges = nodes.Skip(1).Select((n, i) => Flow($"E{i}", "pve-01", n.NodeId)).ToList();
        edges.Add(Flow("R1", "lxc-01", "pve-01"));
        edges.Add(Flow("R2", "pve-01", "lxc-01"));
        await OpenGraphAsync(new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0), new TopologyRenderingOptions());

        await _mapPage!.SelectLayoutAsync(layout);
        await _mapPage.SelectRoutingAsync(routing);

        var labelBoxes = await _mapPage.GetEdgeLabelBoxesAsync();
        labelBoxes.Should().HaveCount(edges.Count);
        FindOverlappingBoxes(labelBoxes).Should().BeEmpty();
    }

    [Theory]
    [InlineData("bezier", TopologySpacingProfile.Balanced)]
    [InlineData("orthogonal", TopologySpacingProfile.Balanced)]
    [InlineData("straight", TopologySpacingProfile.Balanced)]
    [InlineData("bezier", TopologySpacingProfile.Compact)]
    [InlineData("orthogonal", TopologySpacingProfile.Compact)]
    [InlineData("straight", TopologySpacingProfile.Compact)]
    public async Task RenderGraph_WhenStackedAuditedServersAreLinked_KeepsArrowLabelsOffServerBoxes(string routing, TopologySpacingProfile spacing)
    {
        _mapPage.Should().NotBeNull();

        // En mise en page layered, les serveurs audités sont empilés dans une même colonne : les flux qui les relient
        // ont leur milieu près des boîtes voisines, qui sont dessinées par-dessus les étiquettes et les masquent.
        // En espacement compact, l'écart entre deux boîtes empilées doit encore laisser passer une étiquette.
        List<TopologyServerNode> nodes =
        [
            Audited("desktop", "192.168.1.10"),
            Audited("pve-01", "192.168.1.20"),
            Audited("nas", "192.168.1.30"),
            Audited("pihole", "192.168.1.2"),
            Remote("homeassistant", "192.168.1.40"),
            Remote("proxy", "192.168.1.50")
        ];
        List<TopologyServiceEdge> edges =
        [
            Flow("E1", "desktop", "pve-01"),
            Flow("E2", "desktop", "nas"),
            Flow("E3", "pve-01", "nas"),
            Flow("E4", "nas", "pihole"),
            Flow("E5", "desktop", "pihole"),
            Flow("E6", "pve-01", "homeassistant"),
            Flow("E7", "nas", "proxy"),
            Flow("E8", "pihole", "proxy")
        ];
        await OpenGraphAsync(
            new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0),
            new TopologyRenderingOptions { DefaultSpacingProfile = spacing });

        await _mapPage!.SelectLayoutAsync("layered");
        await _mapPage.SelectRoutingAsync(routing);

        var labelBoxes = await _mapPage.GetEdgeLabelBoxesAsync();
        var nodeBoxes = await _mapPage.GetNodeBoxesAsync();
        labelBoxes.Should().HaveCount(edges.Count);
        FindCrossingBoxes(labelBoxes, nodeBoxes).Should().BeEmpty();
    }

    [Fact]
    public async Task PlaceEdgeLabels_WhenFourLabelsShareTheGapBetweenStackedBoxes_MovesTheLastOneSideways()
    {
        _mapPage.Should().NotBeNull();

        // L'écart de 42 px entre deux boîtes empilées ne loge que trois lignes : la quatrième étiquette, au bord de la boîte
        // du haut, n'a plus de ligne libre et resterait masquée par cette boîte si elle ne pouvait pas se décaler sur le côté.
        double[][] serverBoxes = [[60, 100, NodeWidth, NodeHeight], [60, 248, NodeWidth, NodeHeight]];
        double[][] origins = [[130, 210], [150, 210], [170, 210], [190, 210]];

        var labelBoxes = await _mapPage!.PlaceEdgeLabelsAsync("OUT :2049/TCP", origins, serverBoxes);

        FindCrossingBoxes(labelBoxes, serverBoxes).Should().BeEmpty();
        FindOverlappingBoxes(labelBoxes).Should().BeEmpty();
        labelBoxes.Select((box, i) => Math.Abs(box[0] + (box[2] / 2) - origins[i][0]) <= box[2])
            .Should().AllSatisfy(isNearItsArrow => isNearItsArrow.Should().BeTrue());
    }

    [Fact]
    public async Task PlaceEdgeLabels_WhenNoFreeRowRemains_KeepsTheLabelOnItsArrow()
    {
        _mapPage.Should().NotBeNull();

        // 13 rangées sont disponibles (la position d'origine et 6 décalages de chaque côté) : la 14e étiquette reste sur sa flèche.
        var ordinates = await _mapPage!.PlaceIdenticalEdgeLabelsAsync(14, 400, 300);

        ordinates.Take(13).Should().OnlyHaveUniqueItems();
        ordinates[13].Should().Be(300);
    }

    private const double NodeWidth = 236;
    private const double NodeHeight = 106;

    private static List<string> FindOverlappingBoxes(double[][] boxes)
    {
        var overlaps = new List<string>();
        for (var i = 0; i < boxes.Length; i++)
        {
            for (var j = i + 1; j < boxes.Length; j++)
            {
                var (a, b) = (boxes[i], boxes[j]);
                if (a[0] < b[0] + b[2] && b[0] < a[0] + a[2] && a[1] < b[1] + b[3] && b[1] < a[1] + a[3])
                {
                    overlaps.Add($"[{string.Join(", ", a)}] ∩ [{string.Join(", ", b)}]");
                }
            }
        }

        return overlaps;
    }

    private static List<string> FindCrossingBoxes(double[][] firstBoxes, double[][] secondBoxes) =>
        [.. firstBoxes.SelectMany(a => secondBoxes
            .Where(b => a[0] < b[0] + b[2] && b[0] < a[0] + a[2] && a[1] < b[1] + b[3] && b[1] < a[1] + a[3])
            .Select(b => $"[{string.Join(", ", a)}] ∩ [{string.Join(", ", b)}]"))];

    private static TopologyServerNode Audited(string name, string ipAddress) => new(name, name, ipAddress, true, [], 0, 1);

    private static TopologyServerNode Remote(string name, string ipAddress) =>
        new(name, name, ipAddress, false, [new(443, TopologyTransportProtocol.Tcp, "nginx", "")], 1, 0);

    private static TopologyServiceEdge Flow(string edgeId, string sourceNodeId, string targetNodeId) =>
        new(edgeId, sourceNodeId, targetNodeId, 443, TopologyTransportProtocol.Tcp, "curl", "nginx", 5, TopologyFlowDirection.Outbound);

    private static NetworkTopologyGraph CreateSampleTopologyGraph()
    {
        var localPorts = new List<ServerListenerDescriptor>
        {
            new(8080, TopologyTransportProtocol.Tcp, "kestrel", "Web API Service")
        };

        var nodes = new List<TopologyServerNode>
        {
            new("desktop", "desktop", "192.168.1.10", true, localPorts, 1, 4),
            new("homeassistant", "homeassistant", "192.168.20.20", false, [new(8123, TopologyTransportProtocol.Tcp, "hass", "Home Assistant")], 1, 0),
            new("proxy", "proxy", "192.168.20.30", false, [new(443, TopologyTransportProtocol.Tcp, "nginx", "Reverse Proxy")], 1, 1),
            new("sso", "sso", "192.168.20.40", false, [new(9091, TopologyTransportProtocol.Tcp, "authelia", "Authelia")], 1, 0),
            new("nas", "nas", "192.168.20.50", false, [new(873, TopologyTransportProtocol.Tcp, "rsync", "Backup Daemon")], 1, 0)
        };

        var edges = new List<TopologyServiceEdge>
        {
            new("E1", "desktop", "homeassistant", 8123, TopologyTransportProtocol.Tcp, "firefox", "hass", 15, TopologyFlowDirection.Outbound),
            new("E2", "desktop", "proxy", 443, TopologyTransportProtocol.Tcp, "curl", "nginx", 25, TopologyFlowDirection.Outbound),
            new("E3", "proxy", "desktop", 8080, TopologyTransportProtocol.Tcp, "nginx", "kestrel", 40, TopologyFlowDirection.Inbound),
            new("E4", "desktop", "sso", 9091, TopologyTransportProtocol.Tcp, "firefox", "authelia", 8, TopologyFlowDirection.Outbound),
            new("E5", "desktop", "nas", 873, TopologyTransportProtocol.Tcp, "agent", "rsync", 5, TopologyFlowDirection.Outbound)
        };

        return new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, 93, 0);
    }
}
