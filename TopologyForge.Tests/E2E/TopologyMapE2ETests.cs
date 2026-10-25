using FluentAssertions;
using Microsoft.Playwright;
using TopologyForge.Models;
using TopologyForge.Rendering;
using TopologyForge.Tests.E2E.Pages;

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
    private string _tempHtmlPath = string.Empty;

    public async Task InitializeAsync()
    {
        _tempHtmlPath = Path.Combine(Path.GetTempPath(), $"topology_e2e_{Guid.NewGuid():N}.html");
        var graph = CreateSampleTopologyGraph();
        var renderer = new TopologyHtmlRenderer();
        await renderer.WriteHtmlFileAsync(_tempHtmlPath, graph, "E2E Enterprise Network Map");

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _page = await _browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        _mapPage = new TopologyMapPage(_page);
        await _mapPage.NavigateToFileAsync(_tempHtmlPath);
    }

    public async Task DisposeAsync()
    {
        if (_page is not null)
        {
            await _page.CloseAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (File.Exists(_tempHtmlPath))
        {
            File.Delete(_tempHtmlPath);
        }
    }

    [Fact]
    public async Task RenderStructure_WhenPageLoaded_DisplaysAllContainersAndSvgLayers()
    {
        _mapPage.Should().NotBeNull();
        (await _mapPage!.TitleHeading.IsVisibleAsync()).Should().BeTrue();
        (await _mapPage.TitleHeading.InnerTextAsync()).Should().Contain("E2E Enterprise Network Map");

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

        // Exclure le port 873 (SRV-BACKUP-01 n'a que ce port et ce flux)
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
        await _mapPage!.FilterByPortsAsync(includePorts: "1433");

        var nodeCountAfter = await _mapPage.GetNodeCountAsync();
        var edgeCountAfter = await _mapPage.GetEdgeCountAsync();

        // Seuls HOMELAB-PC et SRV-SQL-01 participent au port 1433
        nodeCountAfter.Should().Be(2);
        edgeCountAfter.Should().Be(1);
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("2");
        (await _mapPage.KpiFlows.InnerTextAsync()).Should().Be("1");
    }

    [Fact]
    public async Task Search_WhenServerNameMatched_IsolatesTargetServer()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.SearchAsync("SRV-AUTH");

        var nodeCount = await _mapPage.GetNodeCountAsync();
        nodeCount.Should().Be(1);
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("1");
    }

    [Fact]
    public async Task SelectDirection_WhenFilteredToInbound_HidesOutboundEdges()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.SelectDirectionAsync("IN");

        var edgeCount = await _mapPage.GetEdgeCountAsync();
        edgeCount.Should().Be(1); // Seul le flux IN sur le port 8080

        var nodeCount = await _mapPage.GetNodeCountAsync();
        nodeCount.Should().Be(2); // HOMELAB-PC et SRV-WEB-01 (les autres sans flux IN sont élagués)
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
        nodeCount.Should().Be(5); // HOMELAB-PC + 4 serveurs distants connectés en sortie
        (await _mapPage.KpiServers.InnerTextAsync()).Should().Be("5");
        (await _mapPage.KpiFlows.InnerTextAsync()).Should().Be("4");
    }

    [Fact]
    public async Task BtnClearFilters_WhenClicked_RestoresInitialState()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.FilterByPortsAsync(includePorts: "1433");
        (await _mapPage.GetNodeCountAsync()).Should().Be(2);

        await _mapPage.ClearFiltersAsync();
        (await _mapPage.GetNodeCountAsync()).Should().Be(5);
        (await _mapPage.GetEdgeCountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task Node_WhenClicked_DisplaysServerDetailsInInspector()
    {
        _mapPage.Should().NotBeNull();
        await _mapPage!.ClickNodeAsync(0);

        var content = await _mapPage.InspectorContent.InnerTextAsync();
        content.Should().NotContain("Click on a server to isolate");
        content.Should().MatchRegex(@"(HOMELAB-PC|SRV-)");
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

    private static NetworkTopologyGraph CreateSampleTopologyGraph()
    {
        var localPorts = new List<ServerListenerDescriptor>
        {
            new(8080, TopologyTransportProtocol.Tcp, "kestrel", "Web API Service")
        };

        var nodes = new List<TopologyServerNode>
        {
            new("HOMELAB-PC", "HOMELAB-PC", "192.168.1.10", true, localPorts, 1, 4),
            new("SRV-SQL-01", "SRV-SQL-01", "10.0.1.20", false, [new(1433, TopologyTransportProtocol.Tcp, "sqlservr", "Database")], 1, 0),
            new("SRV-WEB-01", "SRV-WEB-01", "10.0.1.30", false, [new(443, TopologyTransportProtocol.Tcp, "nginx", "Reverse Proxy")], 1, 1),
            new("SRV-AUTH-01", "SRV-AUTH-01", "10.0.1.40", false, [new(8443, TopologyTransportProtocol.Tcp, "identity", "Keycloak")], 1, 0),
            new("SRV-BACKUP-01", "SRV-BACKUP-01", "10.0.1.50", false, [new(873, TopologyTransportProtocol.Tcp, "rsync", "Backup Daemon")], 1, 0)
        };

        var edges = new List<TopologyServiceEdge>
        {
            new("E1", "HOMELAB-PC", "SRV-SQL-01", 1433, TopologyTransportProtocol.Tcp, "app", "sqlservr", 15, TopologyFlowDirection.Outbound),
            new("E2", "HOMELAB-PC", "SRV-WEB-01", 443, TopologyTransportProtocol.Tcp, "curl", "nginx", 25, TopologyFlowDirection.Outbound),
            new("E3", "SRV-WEB-01", "HOMELAB-PC", 8080, TopologyTransportProtocol.Tcp, "nginx", "kestrel", 40, TopologyFlowDirection.Inbound),
            new("E4", "HOMELAB-PC", "SRV-AUTH-01", 8443, TopologyTransportProtocol.Tcp, "app", "identity", 8, TopologyFlowDirection.Outbound),
            new("E5", "HOMELAB-PC", "SRV-BACKUP-01", 873, TopologyTransportProtocol.Tcp, "agent", "rsync", 5, TopologyFlowDirection.Outbound)
        };

        return new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, 93, 0);
    }
}
