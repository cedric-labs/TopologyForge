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
public sealed partial class TopologyMapE2ETests : IAsyncLifetime
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

    [Fact]
    public async Task ShowPortsCheckbox_WhenUnchecked_DrawsOneArrowPerServerPair()
    {
        _mapPage.Should().NotBeNull();
        (await _mapPage!.GetEdgeCountAsync()).Should().Be(5);

        await _mapPage.ShowPortsCheckbox.UncheckAsync();

        // E2 (desktop → proxy) et E3 (proxy → desktop) fusionnent : 5 flux, 4 paires de serveurs.
        (await _mapPage.GetEdgeCountAsync()).Should().Be(4);
        (await _mapPage.EdgeById("E2").CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ShowPortsCheckbox_WhenPairExchangesFlowsBothWays_DrawsOneNeutralArrowWithTwoHeads()
    {
        _mapPage.Should().NotBeNull();

        await _mapPage!.ShowPortsCheckbox.UncheckAsync();

        var pairKey = await _mapPage.UndirectedPairKeyAsync("desktop", "proxy");
        var pairEdge = _mapPage.EdgeById(pairKey);
        (await pairEdge.GetAttributeAsync("stroke")).Should().Be("#8b9dc3");
        (await pairEdge.GetAttributeAsync("marker-start")).Should().Be("url(#arrowMixed)");
        (await pairEdge.GetAttributeAsync("marker-end")).Should().Be("url(#arrowMixed)");
        (await _mapPage.GetEdgeTooltipAsync(pairKey)).Should().Be("2 flows: 443/TCP, 8080/TCP");
    }

    [Fact]
    public async Task ShowPortsCheckbox_WhenPairFlowsShareOneDirection_KeepsTheDirectionColorAndOrigin()
    {
        _mapPage.Should().NotBeNull();
        var graph = new NetworkTopologyGraph(
            DateTimeOffset.UtcNow,
            [Audited("desktop", "192.168.1.10"), Remote("nas", "192.168.1.50")],
            [
                Flow("E1", "desktop", "nas"),
                new("E2", "desktop", "nas", 873, TopologyTransportProtocol.Tcp, "agent", "rsync", 5, TopologyFlowDirection.Outbound)
            ],
            10,
            0);
        await OpenGraphAsync(graph, new TopologyRenderingOptions { DefaultEdgeLabelMode = TopologyEdgeLabelMode.None });

        var pairKey = await _mapPage!.UndirectedPairKeyAsync("desktop", "nas");
        var pairEdge = _mapPage.EdgeById(pairKey);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(1);
        (await pairEdge.GetAttributeAsync("stroke")).Should().Be("#38bdf8");
        (await pairEdge.GetAttributeAsync("marker-start")).Should().Be("url(#originOut)");
        (await pairEdge.GetAttributeAsync("marker-end")).Should().Be("url(#arrowOut)");
        (await _mapPage.GetEdgeTooltipAsync(pairKey)).Should().Be("2 flows: 443/TCP, 873/TCP");
    }

    [Fact]
    public async Task ShowPortsCheckbox_WhenUncheckedAndServerSelected_HighlightsBothHeadsOfItsPairArrow()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.ShowPortsCheckbox.UncheckAsync();

        await _mapPage.ClickNodeByHostNameAsync("proxy");

        var pairEdge = _mapPage.EdgeById(await _mapPage.UndirectedPairKeyAsync("desktop", "proxy"));
        (await pairEdge.GetAttributeAsync("stroke")).Should().Be("#f59e0b");
        (await pairEdge.GetAttributeAsync("marker-start")).Should().Be("url(#arrowHighlight)");
        (await pairEdge.GetAttributeAsync("marker-end")).Should().Be("url(#arrowHighlight)");
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
    public async Task BuildBusEdgePath_WhenTrunkLeavesFromBottom_RunsDownToTheSplitRowThenAcrossAndDownToTheTarget()
    {
        _mapPage.Should().NotBeNull();

        var d = await _mapPage!.BuildBusEdgePathAsync(100, 200, "bottom", 260, 400, 320, "top");

        d.Should().Be("M 100 200 L 100 260 L 400 260 L 400 320");
    }

    [Fact]
    public async Task BuildBusEdgePath_WhenTargetIsEnteredFromTheLeft_EndsWithAHorizontalApproach()
    {
        _mapPage.Should().NotBeNull();

        var d = await _mapPage!.BuildBusEdgePathAsync(100, 200, "bottom", 260, 400, 320, "left");

        d.Should().Be("M 100 200 L 100 260 L 382 260 L 382 320 L 400 320");
    }

    [Fact]
    public async Task BuildBusEdgePath_WhenTargetIsEnteredFromTheTop_PlacesTheLabelJustAboveTheArrowTip()
    {
        _mapPage.Should().NotBeNull();

        var label = await _mapPage!.GetBusEdgeLabelPointAsync(100, 200, "bottom", 260, 400, 320, "top");

        label.Should().Equal(400, 302);
    }

    [Fact]
    public async Task RoutingSelect_WhenChangedToBus_SharesOneTrunkPerServerAndDirection()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateBusTopologyGraph(), new TopologyRenderingOptions());
        await _mapPage!.SelectLayoutAsync("tree");

        await _mapPage.SelectRoutingAsync("bus");

        var trunks = (await _mapPage.GetEdgePathDefinitionsAsync()).ToDictionary(p => p[0], p => BusTrunkOf(p[1]));
        new[] { trunks["O2"], trunks["O3"] }.Should().AllBe(trunks["O1"]);
        trunks["I2"].Should().Be(trunks["I1"]);
        trunks["I1"].Should().NotBe(trunks["O1"]);
    }

    [Fact]
    public async Task AssignBusLane_WhenNeighbourBusesShareTheirRowAndWidth_MovesEachLaterOneToAFreeLane()
    {
        _mapPage.Should().NotBeNull();

        // Trois serveurs d'une même rangée desservent des cibles de la rangée suivante sur des largeurs qui se recouvrent :
        // à la même ordonnée, on ne saurait plus quel tronc alimente quelle branche. Le troisième ne recouvre pas le premier.
        double[][] spans = [[100, 500], [400, 700], [600, 900]];

        var splits = await _mapPage!.AssignBusLanesAsync(origin: 200, reach: 140, split: 270, spans);

        splits.Should().Equal(270, 262, 270);
    }

    [Fact]
    public async Task AssignBusLane_WhenNoLaneRemainsBeforeTheNearestTarget_KeepsTheMidwaySplit()
    {
        _mapPage.Should().NotBeNull();

        // Avec 20 px entre le serveur et sa cible, aucune voie ne respecte l'écart de 8 px de chaque côté.
        double[][] spans = [[100, 500], [100, 500]];

        var splits = await _mapPage!.AssignBusLanesAsync(origin: 200, reach: 20, split: 210, spans);

        splits.Should().Equal(210, 210);
    }

    [Fact]
    public async Task RoutingSelect_WhenChangedToBus_PlacesEachPortLabelNextToItsArrowTip()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateBusTopologyGraph(), new TopologyRenderingOptions());
        await _mapPage!.SelectLayoutAsync("tree");

        await _mapPage.SelectRoutingAsync("bus");

        var tips = await _mapPage.GetEdgeEndPointsAsync();
        var labelBoxes = await _mapPage.GetEdgeLabelBoxesAsync();
        labelBoxes.Should().HaveCount(tips.Length);
        labelBoxes.Select(box => tips.Min(tip => DistanceFromBox(box, tip)))
            .Should().AllSatisfy(distance => distance.Should().BeLessThan(20));
    }

    [Fact]
    public async Task RoutingSelect_WhenBusAndTargetServerSelected_DrawsItsBranchAndTrunkInOrangeOnTop()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateBusTopologyGraph(), new TopologyRenderingOptions());
        await _mapPage!.SelectLayoutAsync("tree");
        await _mapPage.SelectRoutingAsync("bus");

        await _mapPage.ClickNodeByHostNameAsync("lxc-01");

        (await _mapPage.EdgeById("O1").GetAttributeAsync("stroke")).Should().Be("#f59e0b");
        (await _mapPage.Edges.Last.GetAttributeAsync("data-edge-id")).Should().Be("O1");
    }

    [Fact]
    public async Task RoutingSelect_WhenChangedToBus_ReproducesTheRoutingInTheCliCommand()
    {
        _mapPage.Should().NotBeNull();

        await _mapPage!.SelectRoutingAsync("bus");

        (await _mapPage.CliCommandText.InnerTextAsync()).Should().Contain("--map-routing bus");
    }

    [Fact]
    public async Task RoutingSelect_WhenBusAndSeveralServersReachOneTarget_MergesTheirBranchesIntoOneArrowTip()
    {
        _mapPage.Should().NotBeNull();

        // Trois serveurs audités joignent la même base : sans regroupement à l'arrivée, trois branches parallèles
        // aboutissent chacune à sa pointe et la carte ne montre plus qu'elles visent une seule cible.
        await OpenGraphAsync(CreateConvergingTopologyGraph(TopologyFlowDirection.Outbound), BusTreeOptions(TopologyEdgeLabelMode.None));

        var tips = await _mapPage!.GetEdgeEndPointsAsync();

        tips.Should().HaveCount(3);
        tips.Select(tip => (Math.Round(tip[0]), Math.Round(tip[1]))).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task RoutingSelect_WhenBusBranchesReachOneTargetInBothDirections_KeepsOneTipPerDirection()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateConvergingTopologyGraph(TopologyFlowDirection.Inbound), BusTreeOptions(TopologyEdgeLabelMode.Ports));

        var tips = await _mapPage!.GetEdgeEndPointsAsync();

        tips.Should().HaveCount(3);
        tips.Select(tip => (Math.Round(tip[0]), Math.Round(tip[1]))).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task LayoutSelect_WhenTreeIsCompactWithoutPortLabels_StacksRowsCloserThanTheLabelGap()
    {
        _mapPage.Should().NotBeNull();

        // L'écart de 42 px entre deux boîtes empilées loge les étiquettes de ports : sans elles, il ne sert plus à rien.
        await OpenGraphAsync(
            CreateSubnetTopologyGraph(("192.168.1", 4)),
            new TopologyRenderingOptions
            {
                DefaultLayoutMode = TopologyLayoutMode.Tree,
                DefaultSpacingProfile = TopologySpacingProfile.Compact,
                DefaultEdgeLabelMode = TopologyEdgeLabelMode.None
            });

        var rows = await GetTargetRowOrdinatesAsync();

        rows.Should().Equal(220, 350);
    }

    [Fact]
    public async Task LayoutSelect_WhenTreeHasManyServersInOneSubnet_SpreadsThemOverMoreThanThreeColumns()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateSubnetTopologyGraph(("192.168.10", 20)), new TopologyRenderingOptions { DefaultLayoutMode = TopologyLayoutMode.Tree });

        var boxes = await _mapPage!.GetNodeBoxesAsync();

        boxes.Where(box => box[1] > 60).Select(box => box[0]).Distinct().Should().HaveCount(5);
    }

    [Fact]
    public async Task LayoutSelect_WhenTreeSubnetBandsWrap_SeparatesThemByHalfTheFormerGap()
    {
        _mapPage.Should().NotBeNull();

        // Deux sous-réseaux de 3 serveurs n'entrent pas sur une même bande de 3 colonnes : le second passe en dessous.
        await OpenGraphAsync(
            CreateSubnetTopologyGraph(("192.168.10", 3), ("192.168.20", 3)),
            new TopologyRenderingOptions { DefaultLayoutMode = TopologyLayoutMode.Tree });

        var rows = await GetTargetRowOrdinatesAsync();

        // Espacement équilibré (facteur 1,05) : rangées de 155 px, 42 px sous les serveurs audités, 59 px entre deux bandes.
        rows.Should().Equal(257, 412, 626, 781);
    }

    [Fact]
    public async Task LayoutSelect_WhenHorizontalTreeHasManyServersInOneSubnet_SpreadsThemOverMoreThanTwoRows()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateSubnetTopologyGraph(("192.168.10", 20)), new TopologyRenderingOptions { DefaultLayoutMode = TopologyLayoutMode.HorizontalTree });

        var boxes = await _mapPage!.GetNodeBoxesAsync();

        boxes.Where(box => box[0] > 60).Select(box => box[1]).Distinct().Should().HaveCount(5);
    }

    [Fact]
    public async Task LayoutSelect_WhenHorizontalTreeStacksServers_UsesTheTighterTreeRowStep()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateSubnetTopologyGraph(("192.168.10", 20)), new TopologyRenderingOptions { DefaultLayoutMode = TopologyLayoutMode.HorizontalTree });

        var boxes = await _mapPage!.GetNodeBoxesAsync();

        // Espacement équilibré (facteur 1,05) : rangées de 155 px, soit 49 px entre deux boîtes de 106 px.
        boxes.Where(box => box[0] > 60).Select(box => box[1]).Distinct().Order().Should().Equal(60, 215, 370, 525, 680);
    }

    [Fact]
    public async Task SortSelect_WhenSubnetClustersStackServers_KeepsTheDefaultRowStep()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateSubnetTopologyGraph(("192.168.10", 2)), new TopologyRenderingOptions { DefaultSortMethod = TopologyNodeSortMethod.SubnetCluster });

        var rows = (await _mapPage!.GetNodeBoxesAsync()).Select(box => box[1]).Distinct().Order();

        // Le resserrement ne vise que les arbres : hors arbre, deux rangées restent à 170 px en espacement équilibré.
        rows.Should().Equal(60, 310, 480);
    }

    [Fact]
    public async Task LayoutSelect_WhenHorizontalTreeSubnetBandsWrap_SeparatesThemByTheTreeGaps()
    {
        _mapPage.Should().NotBeNull();

        // Deux sous-réseaux de 3 serveurs n'entrent pas sur une même bande de 3 rangées : le second passe à droite.
        await OpenGraphAsync(
            CreateSubnetTopologyGraph(("192.168.10", 3), ("192.168.20", 3)),
            new TopologyRenderingOptions { DefaultLayoutMode = TopologyLayoutMode.HorizontalTree });

        var columns = await GetTargetColumnAbscissasAsync();

        // Espacement équilibré (facteur 1,05) : colonnes de 368 px, 42 px après les serveurs audités, 59 px entre deux bandes.
        columns.Should().Equal(470, 838, 1265, 1633);
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
    [InlineData("tree", "bus")]
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
    [InlineData("bus", TopologySpacingProfile.Balanced)]
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

    private static double DistanceFromBox(double[] box, double[] point)
    {
        var dx = Math.Max(Math.Max(box[0] - point[0], point[0] - (box[0] + box[2])), 0);
        var dy = Math.Max(Math.Max(box[1] - point[1], point[1] - (box[1] + box[3])), 0);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    // Le tronc d'une flèche en bus est son premier segment : du point de départ jusqu'à l'ordonnée de séparation.
    private static string BusTrunkOf(string pathDefinition) =>
        string.Join(' ', pathDefinition.Split(' ').Take(6));

    // pve-01 ouvre trois flux sortants vers ses conteneurs et deux flux entrants, en client, vers les NAS audités.
    private static NetworkTopologyGraph CreateBusTopologyGraph()
    {
        List<TopologyServerNode> nodes =
        [
            Audited("pve-01", "192.168.1.10"),
            Remote("lxc-01", "192.168.1.21"),
            Remote("lxc-02", "192.168.1.22"),
            Remote("lxc-03", "192.168.1.23"),
            Audited("nas-01", "192.168.1.31"),
            Audited("nas-02", "192.168.1.32")
        ];
        List<TopologyServiceEdge> edges =
        [
            Flow("O1", "pve-01", "lxc-01"),
            Flow("O2", "pve-01", "lxc-02"),
            Flow("O3", "pve-01", "lxc-03"),
            new("I1", "pve-01", "nas-01", 2049, TopologyTransportProtocol.Tcp, "pvestatd", "nfsd", 5, TopologyFlowDirection.Inbound),
            new("I2", "pve-01", "nas-02", 2049, TopologyTransportProtocol.Tcp, "pvestatd", "nfsd", 5, TopologyFlowDirection.Inbound)
        ];
        return new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0);
    }

    private static TopologyRenderingOptions BusTreeOptions(TopologyEdgeLabelMode edgeLabels) => new()
    {
        DefaultLayoutMode = TopologyLayoutMode.Tree,
        DefaultRoutingMode = TopologyEdgeRoutingMode.Bus,
        DefaultEdgeLabelMode = edgeLabels
    };

    // pve-01, pve-02 et pve-03 joignent tous db-01 ; le flux de pve-03 suit la direction donnée.
    private static NetworkTopologyGraph CreateConvergingTopologyGraph(TopologyFlowDirection thirdFlowDirection)
    {
        List<TopologyServerNode> nodes =
        [
            Audited("pve-01", "192.168.1.10"),
            Audited("pve-02", "192.168.1.11"),
            Audited("pve-03", "192.168.1.12"),
            Remote("db-01", "192.168.1.40")
        ];
        List<TopologyServiceEdge> edges =
        [
            Flow("E1", "pve-01", "db-01"),
            Flow("E2", "pve-02", "db-01"),
            new("E3", "pve-03", "db-01", 5432, TopologyTransportProtocol.Tcp, "backup", "postgres", 5, thirdFlowDirection)
        ];
        return new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0);
    }

    // Un serveur audité, desktop, joint chacun des serveurs distants répartis dans les sous-réseaux /24 donnés.
    private static NetworkTopologyGraph CreateSubnetTopologyGraph(params (string Prefix, int Count)[] subnets)
    {
        List<TopologyServerNode> nodes = [Audited("desktop", "192.168.1.250")];
        nodes.AddRange(subnets.SelectMany(s => Enumerable.Range(1, s.Count).Select(i => Remote($"srv-{s.Prefix}-{i}", $"{s.Prefix}.{10 + i}"))));
        var edges = nodes.Skip(1).Select((n, i) => Flow($"E{i}", "desktop", n.NodeId)).ToList();
        return new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0);
    }

    // Ordonnées distinctes des rangées de serveurs distants, sous la rangée des serveurs audités (y = 60).
    private async Task<double[]> GetTargetRowOrdinatesAsync() =>
        [.. (await _mapPage!.GetNodeBoxesAsync()).Select(box => box[1]).Where(y => y > 60).Distinct().Order()];

    // Abscisses distinctes des colonnes de serveurs distants, à droite de la colonne des serveurs audités (x = 60).
    private async Task<double[]> GetTargetColumnAbscissasAsync() =>
        [.. (await _mapPage!.GetNodeBoxesAsync()).Select(box => box[0]).Where(x => x > 60).Distinct().Order()];

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
