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
/// Automated Playwright E2E tests checking the SVG structure, the interactivity of the controls and the accuracy of the filters.
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

        // Exclude port 873 (nas only has this port and this flow)
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

        // Only desktop and homeassistant use port 8123
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

        // sso and its only neighbour desktop, linked by flow E4
        (await _mapPage.GetNodeCountAsync()).Should().Be(2);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(1);
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("2");
    }

    [Fact]
    public async Task Search_WhenOutboundProcessNameMatched_ShowsTheFlowAndItsEndpoints()
    {
        _mapPage.Should().NotBeNull();

        // "curl" only appears on the outbound edge E2 (desktop → proxy)
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

        // 8123 is in the range: desktop → homeassistant
        (await _mapPage.GetNodeCountAsync()).Should().Be(2);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FilterByPorts_WhenExcludedPortRangeGiven_RemovesEveryPortInTheRange()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.FilterByPortsAsync(excludePorts: "800-900");

        // 873 is in the range: nas disappears
        (await _mapPage.GetNodeCountAsync()).Should().Be(4);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task FilterByPorts_WhenPortIncluded_RecountsVisibleServicePorts()
    {
        _mapPage.Should().NotBeNull();
        (await _mapPage!.KpiPorts.InnerTextAsync()).Should().Be("5");

        await _mapPage.FilterByPortsAsync(includePorts: "8123");

        // Only the listening port 8123 of homeassistant stays visible
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

        // E2 (desktop → proxy) and E3 (proxy → desktop) share the same server pair:
        // the fan must bend both arrows on either side, so they end up further apart than their chords.
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
        nodeCount.Should().Be(2); // desktop and proxy (the others, without IN flows, are pruned)
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
        nodeCount.Should().Be(5); // desktop + 4 remote servers reached outbound
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

        // E2 (desktop → proxy) and E3 (proxy → desktop) merge: 5 flows, 4 server pairs.
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

        // Same order as the former default string sort: UTF-16 ordinal, upper case before lower case.
        var key = await _mapPage!.UndirectedPairKeyAsync("nas", "NAS");

        key.Should().Be("NAS::nas");
    }

    [Fact]
    public async Task SegmentIntersectsBox_WhenSegmentClipsBoxCorner_ReturnsTrue()
    {
        _mapPage.Should().NotBeNull();

        // The segment enters the box (236 × 106 at the origin) between (220, 0) and (236, 16), but its point closest
        // to the centre of the box is its end (200, -20), outside the box.
        var intersects = await _mapPage!.SegmentIntersectsNodeBoxAsync(200, -20, 260, 40, 0, 0, 0);

        intersects.Should().BeTrue();
    }

    [Fact]
    public async Task SegmentIntersectsBox_WhenSegmentPassesBesideCorner_ReturnsFalse()
    {
        _mapPage.Should().NotBeNull();

        // The segment passes above the top-right corner (at x = 236, it is at y = -18).
        var intersects = await _mapPage!.SegmentIntersectsNodeBoxAsync(180, -60, 300, 30, 0, 0, 0);

        intersects.Should().BeFalse();
    }

    [Fact]
    public async Task BuildRoutedEdgePath_WhenOrthogonalDetourIsVertical_PassesThroughTheWaypointRow()
    {
        _mapPage.Should().NotBeNull();

        // Horizontal edge blocked by a box: the waypoint moves down, vertically below the middle.
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

        // Three servers of one row serve targets of the next row over overlapping widths:
        // at the same ordinate, nobody could tell which trunk feeds which branch. The third one does not overlap the first.
        double[][] spans = [[100, 500], [400, 700], [600, 900]];

        var splits = await _mapPage!.AssignBusLanesAsync(origin: 200, reach: 140, split: 270, spans);

        splits.Should().Equal(270, 262, 270);
    }

    [Fact]
    public async Task AssignBusLane_WhenNoLaneRemainsBeforeTheNearestTarget_KeepsTheMidwaySplit()
    {
        _mapPage.Should().NotBeNull();

        // With 20 px between the server and its target, no lane keeps the 8 px gap on each side.
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
    public async Task CliCommandText_WhenMapIsOpenedWithDefaultOptions_ReproducesEveryRenderingOption()
    {
        _mapPage.Should().NotBeNull();

        var command = await _mapPage!.CliCommandText.InnerTextAsync();

        command.Should().Contain("--map-layout layered");
        command.Should().Contain("--map-sort sugiyama");
        command.Should().Contain("--map-routing bezier");
        command.Should().Contain("--map-spacing balanced");
    }

    [Fact]
    public async Task SortSelect_WhenChangedBackToSugiyama_ReproducesTheSortInTheCliCommand()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.SelectSortAsync("degree");

        await _mapPage.SelectSortAsync("sugiyama");

        (await _mapPage.CliCommandText.InnerTextAsync()).Should().Contain("--map-sort sugiyama");
    }

    [Fact]
    public async Task SortSelect_WhenCliCommandFormatNamesTheSugiyamaValue_UsesThatNameInTheCliCommand()
    {
        _mapPage.Should().NotBeNull();
        var options = new TopologyRenderingOptions
        {
            CliCommandFormat = new TopologyCliCommandFormat { SugiyamaSortValue = "barycentric" }
        };
        await OpenGraphAsync(CreateSampleTopologyGraph(), options);

        (await _mapPage!.CliCommandText.InnerTextAsync()).Should().Contain("--map-sort barycentric");
    }

    [Fact]
    public async Task RoutingSelect_WhenBusAndSeveralServersReachOneTarget_MergesTheirBranchesIntoOneArrowTip()
    {
        _mapPage.Should().NotBeNull();

        // Three audited servers reach the same database: without merging at the arrival, three parallel branches
        // each end at their own head and the map no longer shows that they aim at a single target.
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

        // The 42 px gap between two stacked boxes holds the port labels: without them, it serves no purpose.
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

        // Two subnets of 3 servers do not fit in one band of 3 columns: the second one goes below.
        await OpenGraphAsync(
            CreateSubnetTopologyGraph(("192.168.10", 3), ("192.168.20", 3)),
            new TopologyRenderingOptions { DefaultLayoutMode = TopologyLayoutMode.Tree });

        var rows = await GetTargetRowOrdinatesAsync();

        // Balanced spacing (factor 1.05): 155 px rows, 42 px below the audited servers, 59 px between two bands.
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

        // Balanced spacing (factor 1.05): 155 px rows, i.e. 49 px between two 106 px boxes.
        boxes.Where(box => box[0] > 60).Select(box => box[1]).Distinct().Order().Should().Equal(60, 215, 370, 525, 680);
    }

    [Fact]
    public async Task SortSelect_WhenSubnetClustersStackServers_KeepsTheDefaultRowStep()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(CreateSubnetTopologyGraph(("192.168.10", 2)), new TopologyRenderingOptions { DefaultSortMethod = TopologyNodeSortMethod.SubnetCluster });

        var rows = (await _mapPage!.GetNodeBoxesAsync()).Select(box => box[1]).Distinct().Order();

        // Only trees are tightened: outside trees, two rows stay 170 px apart with balanced spacing.
        rows.Should().Equal(60, 310, 480);
    }

    [Fact]
    public async Task SortSelect_WhenSubnetClustersStack_SeparatesThemByTheSpacingGap()
    {
        _mapPage.Should().NotBeNull();
        await OpenGraphAsync(
            CreateSubnetTopologyGraph(("192.168.10", 2), ("192.168.20", 2)),
            new TopologyRenderingOptions { DefaultSortMethod = TopologyNodeSortMethod.SubnetCluster });

        var rows = (await _mapPage!.GetNodeBoxesAsync()).Select(box => box[1]).Distinct().Order();

        // The first subnet starts at 310 px (polishing then moves it away from the arrows towards the second one);
        // with balanced spacing (factor 1.05), the second follows at 2 × 170 + 42 px, i.e. 64 px between their zones.
        rows.TakeLast(2).Should().Equal(692, 862);
    }

    [Fact]
    public async Task LayoutSelect_WhenHorizontalTreeSubnetBandsWrap_SeparatesThemByTheTreeGaps()
    {
        _mapPage.Should().NotBeNull();

        // Two subnets of 3 servers do not fit in one band of 3 rows: the second one goes to the right.
        await OpenGraphAsync(
            CreateSubnetTopologyGraph(("192.168.10", 3), ("192.168.20", 3)),
            new TopologyRenderingOptions { DefaultLayoutMode = TopologyLayoutMode.HorizontalTree });

        var columns = await GetTargetColumnAbscissasAsync();

        // Balanced spacing (factor 1.05): 368 px columns, 42 px after the audited servers, 59 px between two bands.
        columns.Should().Equal(470, 838, 1265, 1633);
    }

    [Fact]
    public async Task SortSelect_WhenSubnetColumnsWrap_KeepsSubnetZonesApart()
    {
        _mapPage.Should().NotBeNull();

        // The 9-server subnet takes 3 columns; the next column must start after the widest one,
        // not after the width of the last subnet placed (1 column).
        List<TopologyServerNode> nodes = [Audited("desktop", "192.168.1.50")];
        nodes.AddRange(Enumerable.Range(1, 9).Select(i => Remote($"iot-0{i}", $"192.168.10.{10 + i}")));
        nodes.AddRange(new[] { 30, 40, 50, 60 }.Select(subnet => Remote($"vlan-{subnet}", $"192.168.{subnet}.2")));
        var edges = nodes.Skip(1).Select((n, i) => Flow($"E{i}", "desktop", n.NodeId)).ToList();
        await OpenGraphAsync(new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0), new TopologyRenderingOptions());

        await _mapPage!.SelectSortAsync("subnet");

        FindOverlappingBoxes(await _mapPage.GetSubnetZoneBoxesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SortSelect_WhenServersHavePublicAddresses_GroupsThemInOneInternetZone()
    {
        _mapPage.Should().NotBeNull();

        List<TopologyServerNode> nodes = [Audited("desktop", "192.168.1.50"), Remote("nas", "192.168.1.60")];
        nodes.AddRange(new[] { "1.1.1.1", "93.184.216.34", "151.101.1.69" }.Select(ip => Remote($"pub-{ip}", ip)));
        var edges = nodes.Skip(1).Select((n, i) => Flow($"E{i}", "desktop", n.NodeId)).ToList();
        await OpenGraphAsync(new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0), new TopologyRenderingOptions());

        await _mapPage!.SelectSortAsync("subnet");

        var labels = await _mapPage.GetSubnetZoneLabelsAsync();
        labels.Should().Contain("Zone: Internet (3 servers)");
        labels.Where(l => l.Contains("Internet", StringComparison.Ordinal)).Should().ContainSingle();
    }

    [Fact]
    public async Task SortSelect_WhenSwappingServersAcrossSubnetsWouldShortenFlows_KeepsSubnetZonesApart()
    {
        _mapPage.Should().NotBeNull();

        // Swapping nas-02 and backup-01 would avoid two arrow collisions, but would mix the two subnets.
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

        // Flows leaving the same server towards aligned neighbours have close midpoints:
        // without spreading, their "OUT :port/proto" labels overlap and become unreadable.
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

        // In the layered layout, the audited servers are stacked in one column: the flows between them
        // have their midpoint near the neighbouring boxes, which are drawn over the labels and hide them.
        // With compact spacing, the gap between two stacked boxes must still let a label through.
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

        // The 42 px gap between two stacked boxes only holds three lines: the fourth label, at the edge of the top
        // box, has no free line left and would stay hidden by that box if it could not move sideways.
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

        // 13 rows are available (the original position and 6 offsets on each side): the 14th label stays on its arrow.
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

    // The trunk of a bus arrow is its first segment: from the starting point down to the separation ordinate.
    private static string BusTrunkOf(string pathDefinition) =>
        string.Join(' ', pathDefinition.Split(' ').Take(6));

    // pve-01 opens three outbound flows to its containers and two inbound flows, as a client, to the audited NAS.
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

    // pve-01, pve-02 and pve-03 all reach db-01; the pve-03 flow follows the given direction.
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

    // One audited server, desktop, reaches each of the remote servers spread over the given /24 subnets.
    private static NetworkTopologyGraph CreateSubnetTopologyGraph(params (string Prefix, int Count)[] subnets)
    {
        List<TopologyServerNode> nodes = [Audited("desktop", "192.168.1.250")];
        nodes.AddRange(subnets.SelectMany(s => Enumerable.Range(1, s.Count).Select(i => Remote($"srv-{s.Prefix}-{i}", $"{s.Prefix}.{10 + i}"))));
        var edges = nodes.Skip(1).Select((n, i) => Flow($"E{i}", "desktop", n.NodeId)).ToList();
        return new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edges.Count, 0);
    }

    // Distinct ordinates of the remote server rows, below the row of audited servers (y = 60).
    private async Task<double[]> GetTargetRowOrdinatesAsync() =>
        [.. (await _mapPage!.GetNodeBoxesAsync()).Select(box => box[1]).Where(y => y > 60).Distinct().Order()];

    // Distinct abscissas of the remote server columns, to the right of the column of audited servers (x = 60).
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
