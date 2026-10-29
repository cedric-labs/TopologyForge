using AwesomeAssertions;
using TopologyForge.Builders;
using TopologyForge.Filtering;
using TopologyForge.Models;
using TopologyForge.Rendering;

namespace TopologyForge.Tests.Rendering;

public sealed class TopologyHtmlRendererTests
{
    private static NetworkTopologyGraph CreateSampleGraph()
    {
        var evaluator = new TopologyFlowEvaluator(new TopologyFilterOptions());
        var builder = new TopologyGraphBuilder(evaluator);
        return builder.BuildGraph(
        [
            new TopologyFlowRecord("SRV-FRONT-01", "10.10.1.5", 443, "", "0.0.0.0", 0, TopologyTransportProtocol.Tcp, TopologyFlowDirection.Listening, "nginx", "nginx.conf", 100),
            new TopologyFlowRecord("SRV-FRONT-01", "10.10.1.5", 53000, "SRV-API-01", "10.10.2.10", 8080, TopologyTransportProtocol.Tcp, TopologyFlowDirection.Outbound, "nginx", "nginx.conf", 42),
            new TopologyFlowRecord("SRV-API-01", "10.10.2.10", 5432, "SRV-DB-01", "10.10.3.20", 54000, TopologyTransportProtocol.Tcp, TopologyFlowDirection.Inbound, "postgres", "postgres", 85)
        ]);
    }

    [Theory]
    [InlineData("</script><script>window.__pwned=1</script>")]
    [InlineData("</script ><script>window.__pwned=1</script >")]
    [InlineData("</SCRIPT x><!--")]
    public void RenderHtml_WhenHostNameContainsScriptMarkup_KeepsItInsideTheEmbeddedPayload(string hostName)
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = new NetworkTopologyGraph(
            DateTimeOffset.UtcNow,
            [new TopologyServerNode("N1", hostName, "10.0.0.1", true, [], 0, 0)],
            [],
            0,
            0);
        var expectedScriptEndTags = CountIgnoringCase(renderer.RenderHtml(CreateSampleGraph(), new TopologyRenderingOptions()), "</script");

        // Act
        var html = renderer.RenderHtml(graph, new TopologyRenderingOptions());

        // Assert
        CountIgnoringCase(html, "</script").Should().Be(expectedScriptEndTags);
        html.Should().NotContain("<!--");
    }

    private static int CountIgnoringCase(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.OrdinalIgnoreCase);
        }

        return count;
    }

    [Fact]
    public void RenderHtml_WhenNoCliCommandFormatGiven_KeepsTheLegacyCommandSyntax()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();

        // Act
        var html = renderer.RenderHtml(CreateSampleGraph(), new TopologyRenderingOptions());

        // Assert
        html.Should().Contain("\"prefix\":\"NetFlowAudit.Cli --map-only\"");
        html.Should().Contain("\"layoutOption\":\"--map-layout\"");
        html.Should().Contain("\"sortOption\":\"--map-sort\"");
        html.Should().Contain("\"routingOption\":\"--map-routing\"");
        html.Should().Contain("\"spacingOption\":\"--map-spacing\"");
        html.Should().Contain("\"edgeLabelsOption\":\"--map-edge-labels\"");
    }

    [Fact]
    public void RenderHtml_WhenNoEdgeLabelModeGiven_ShowsPortsOnArrowsWithACheckbox()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();

        // Act
        var html = renderer.RenderHtml(CreateSampleGraph(), new TopologyRenderingOptions());

        // Assert
        html.Should().Contain("<input type=\"checkbox\" id=\"showPortsCheck\" checked>");
        html.Should().Contain("<label for=\"showPortsCheck\">Show Ports</label>");
        html.Should().Contain("\"defaultEdgeLabels\":\"ports\"");
    }

    [Fact]
    public void RenderHtml_WhenEdgeLabelModeIsNone_SerializesNoneAsDefault()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var options = new TopologyRenderingOptions { DefaultEdgeLabelMode = TopologyEdgeLabelMode.None };

        // Act
        var html = renderer.RenderHtml(CreateSampleGraph(), options);

        // Assert
        html.Should().Contain("\"defaultEdgeLabels\":\"none\"");
    }

    [Fact]
    public void RenderHtml_WhenCliCommandFormatGiven_BuildsTheCopiedCommandFromIt()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var options = new TopologyRenderingOptions
        {
            CliCommandFormat = new TopologyCliCommandFormat
            {
                Prefix = "netflowaudit map",
                LayoutOption = "--layout",
                SortOption = "--sort",
                RoutingOption = "--routing",
                SpacingOption = "--spacing"
            }
        };

        // Act
        var html = renderer.RenderHtml(CreateSampleGraph(), options);

        // Assert
        html.Should().Contain("\"prefix\":\"netflowaudit map\"");
        html.Should().Contain("\"layoutOption\":\"--layout\"");
        html.Should().Contain(">netflowaudit map</div>");
        html.Should().NotContain("'--map-only'");
        html.Should().NotContain("`--map-layout ");
    }

    [Fact]
    public void RenderHtml_WhenGraphProvided_GeneratesSelfContainedOfflineHtmlWithNodesAndPorts()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();

        // Act
        var html = renderer.RenderHtml(graph, "Production Network Topology");

        // Assert
        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("<html lang=\"en\">");
        html.Should().Contain("Production Network Topology");
        html.Should().Contain("SRV-FRONT-01");
        html.Should().Contain("SRV-API-01");
        html.Should().Contain("SRV-DB-01");
        html.Should().Contain("443");
        html.Should().Contain("8080");
        html.Should().Contain("5432");
        // Verify 100% offline: no external CDN script or stylesheet
        html.Should().NotContain("https://");
        html.Should().NotContain("http://");
    }

    [Fact]
    public void RenderHtml_WhenRenderingOptionsProvided_ConfiguresSortRoutingSpacingAndDirectionalMarkers()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();
        var options = new TopologyRenderingOptions
        {
            Title = "Enterprise Fleet Topology",
            DefaultSortMethod = TopologyNodeSortMethod.BarycentricSugiyama,
            DefaultRoutingMode = TopologyEdgeRoutingMode.OrthogonalCorridors,
            DefaultSpacingProfile = TopologySpacingProfile.Spacious
        };

        // Act
        var html = renderer.RenderHtml(graph, options);

        // Assert
        html.Should().Contain("Enterprise Fleet Topology");
        html.Should().Contain("id=\"arrowOut\"");
        html.Should().Contain("id=\"arrowIn\"");
        html.Should().Contain("id=\"arrowHighlight\"");
        html.Should().Contain("id=\"arrowMixed\"");
        html.Should().Contain("Merged IN and OUT Flows");
        html.Should().Contain("id=\"sortSelect\"");
        html.Should().Contain("id=\"routingSelect\"");
        html.Should().Contain("id=\"spacingSelect\"");
        html.Should().Contain("id=\"nodeGapRange\"");
        html.Should().Contain("id=\"edgeFanRange\"");
        html.Should().Contain("\"defaultSort\":\"sugiyama\"");
        html.Should().Contain("\"defaultRouting\":\"orthogonal\"");
        html.Should().Contain("\"defaultSpacing\":\"spacious\"");
    }

    [Fact]
    public void RenderHtml_WhenBusRoutingConfigured_SerializesBusAndOffersItInTheRoutingSelect()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var options = new TopologyRenderingOptions { DefaultRoutingMode = TopologyEdgeRoutingMode.Bus };

        // Act
        var html = renderer.RenderHtml(CreateSampleGraph(), options);

        // Assert
        html.Should().Contain("\"defaultRouting\":\"bus\"");
        html.Should().Contain("<option value=\"bus\">");
    }

    [Fact]
    public void RenderHtml_WhenOrganicLayoutAndFunctionalTierConfigured_SerializesExpectedDefaults()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();
        var options = new TopologyRenderingOptions
        {
            DefaultLayoutMode = TopologyLayoutMode.ForceDirectedOrganic,
            DefaultSortMethod = TopologyNodeSortMethod.FunctionalTier
        };

        // Act
        var html = renderer.RenderHtml(graph, options);

        // Assert
        html.Should().Contain("\"defaultLayout\":\"organic\"");
        html.Should().Contain("\"defaultSort\":\"tiers\"");
    }

    [Fact]
    public void RenderHtml_WhenSubnetClusterConfigured_SerializesSubnetSortMethod()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();
        var options = new TopologyRenderingOptions
        {
            DefaultSortMethod = TopologyNodeSortMethod.SubnetCluster
        };

        // Act
        var html = renderer.RenderHtml(graph, options);

        // Assert
        html.Should().Contain("\"defaultSort\":\"subnet\"");
    }

    [Fact]
    public void RenderHtml_WhenTreeLayoutConfigured_SerializesTreeLayoutMode()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();
        var options = new TopologyRenderingOptions
        {
            DefaultLayoutMode = TopologyLayoutMode.Tree
        };

        // Act
        var html = renderer.RenderHtml(graph, options);

        // Assert
        html.Should().Contain("\"defaultLayout\":\"tree\"");
    }

    [Fact]
    public void RenderHtml_WhenHorizontalTreeLayoutConfigured_SerializesHorizontalTreeLayoutMode()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();
        var options = new TopologyRenderingOptions
        {
            DefaultLayoutMode = TopologyLayoutMode.HorizontalTree
        };

        // Act
        var html = renderer.RenderHtml(graph, options);

        // Assert
        html.Should().Contain("\"defaultLayout\":\"horizontal-tree\"");
    }

    [Fact]
    public void RenderHtml_WhenRendered_ContainsEnhancedLayoutAndSortingOptions()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();

        // Act
        var html = renderer.RenderHtml(graph, "Enhanced Options Test");

        // Assert
        html.Should().Contain("<option value=\"tree\">Tree (Top-Down Multi-Column)</option>");
        html.Should().Contain("<option value=\"horizontal-tree\">Horizontal Tree (Left ➔ Right)</option>");
        html.Should().Contain("<option value=\"organic\">Force-Directed (Organic)</option>");
        html.Should().Contain("<option value=\"tiers\">Functional Tiers (Client ➔ App ➔ Infra)</option>");
        html.Should().Contain("<option value=\"subnet\">Subnet &amp; Security Zones</option>");
        html.Should().Contain("id=\"showZonesCheck\"");
        html.Should().Contain("id=\"zonesLayer\"");
        html.Should().Contain("SpatialHashGrid");
        html.Should().Contain("simulateForceDirectedLayout");
        html.Should().Contain("classifyFunctionalTier");
        html.Should().Contain("computeSubnetZones");
        html.Should().Contain("layoutTree");
    }

    [Fact]
    public void RenderHtml_WhenPaginationConfigured_EmbedsPaginationControlsAndEnglishLabels()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();
        var options = new TopologyRenderingOptions
        {
            DefaultPageSize = 100
        };

        // Act
        var html = renderer.RenderHtml(graph, options);

        // Assert
        html.Should().Contain("\"defaultPageSize\":100");
        html.Should().Contain("id=\"btnPrevPage\"");
        html.Should().Contain("id=\"btnNextPage\"");
        html.Should().Contain("id=\"btnLoadMore100\"");
        html.Should().Contain("id=\"btnShowAll\"");
        html.Should().Contain("id=\"pageStatus\"");
        html.Should().Contain("Inclusion / Exclusion Filters");
        html.Should().Contain("Server Inspector");
        html.Should().Contain("Reset Positions");
        html.Should().Contain("id=\"btnApplyFilters\"");
        html.Should().Contain("Apply Filters");
        html.Should().Contain("id=\"btnToggleSidebar\"");
        html.Should().Contain("id=\"btnCopyCliCommand\"");
        html.Should().Contain("id=\"cliCommandText\"");
        html.Should().Contain("CLI Command (Reproduce Map)");
        html.Should().Contain("data-target=\"sectionLayout\"");
        html.Should().Contain("data-target=\"sectionFilters\"");
    }

    [Fact]
    public void RenderHtml_WhenSpecialCharactersInTitle_EscapesHtmlCorrectly()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();

        // Act
        var html = renderer.RenderHtml(graph, "Audit <script>alert(1)</script> & Flows");

        // Assert
        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    [Fact]
    public async Task WriteHtmlFileAsync_WhenValidPathProvided_WritesCompleteHtmlDocumentToDisk()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();
        var tempFile = Path.Combine(Path.GetTempPath(), $"topology_map_{Guid.NewGuid():N}.html");

        try
        {
            // Act
            await renderer.WriteHtmlFileAsync(tempFile, graph, "Test Export");

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Contain("SRV-FRONT-01");
            content.Should().Contain("Test Export");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task WriteHtmlFileAsync_WhenRenderingOptionsOverloadUsed_WritesConfiguredHtmlDocumentToDisk()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();
        var tempFile = Path.Combine(Path.GetTempPath(), $"topology_opts_{Guid.NewGuid():N}.html");
        var options = new TopologyRenderingOptions
        {
            Title = "Export Options",
            DefaultSortMethod = TopologyNodeSortMethod.ConnectionDegree,
            DefaultRoutingMode = TopologyEdgeRoutingMode.BezierCurved,
            DefaultSpacingProfile = TopologySpacingProfile.Compact
        };

        try
        {
            // Act
            await renderer.WriteHtmlFileAsync(tempFile, graph, options);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Contain("Export Options");
            content.Should().Contain("\"defaultSort\":\"degree\"");
            content.Should().Contain("\"defaultRouting\":\"bezier\"");
            content.Should().Contain("\"defaultSpacing\":\"compact\"");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void RenderHtml_WhenGraphRendered_EmbedsMultiPassCrossingMinimizationAndAngularPortDistribution()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();

        // Act
        var html = renderer.RenderHtml(graph, "Crossing Minimization Test");

        // Assert
        html.Should().Contain("optimizeNodePositions2Opt");
        html.Should().Contain("applyForceRepulsionPolish");
        html.Should().Contain("computeAllEdgeAnchors");
        html.Should().Contain("findCorridorWaypoint");
    }

    [Fact]
    public void RenderHtml_WhenRendered_ContainsExportSvgButtonAndHandler()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();

        // Act
        var html = renderer.RenderHtml(graph, "SVG Export Test");

        // Assert
        html.Should().Contain("id=\"btnExportSvg\"");
        html.Should().Contain("Export SVG");
        html.Should().Contain("network_topology_map.svg");
    }

    [Fact]
    public void RenderHtml_WhenRendered_InlinesTheEmbeddedMapScriptResource()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();
        using var resourceStream = typeof(TopologyHtmlRenderer).Assembly
            .GetManifestResourceStream("TopologyForge.Rendering.topology-map.js");
        resourceStream.Should().NotBeNull();
        using var reader = new StreamReader(resourceStream!);
        var mapScript = reader.ReadToEnd();

        // Act
        var html = renderer.RenderHtml(graph, "Embedded Script Test");

        // Assert
        mapScript.Should().Contain("function renderGraph(");
        html.Should().Contain(mapScript);
    }

    [Fact]
    public void RenderHtml_WhenRendered_PlacesSidebarButtonInKpiBarAndEmbedsOrphanFilterLogic()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();

        // Act
        var html = renderer.RenderHtml(graph, "Sidebar Placement & Orphan Filter Test");

        // Assert
        // Sidebar button must be inside kpi-bar right before the Servers badge
        html.Should().Contain("btnToggleSidebar");
        html.Should().Contain("remainingPorts.length === 0 && !hasActiveEdge");
        html.Should().Contain("getElementById('kpiNodes').textContent = String(totalFiltered);");
    }

    [Fact]
    public void RenderHtml_WhenInitialFiltersAndWildcardsConfigured_EmbedsMatchesPatternAndInitialValues()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();
        var options = new TopologyRenderingOptions
        {
            InitialExcludeHosts = "*.local,SRV-MON-*",
            InitialIncludePorts = "80,443",
            InitialExcludePorts = "53",
            InitialDirection = "OUT",
            DefaultSpacingProfile = TopologySpacingProfile.Compact
        };

        // Act
        var html = renderer.RenderHtml(graph, options);

        // Assert
        html.Should().Contain("matchesPattern");
        html.Should().Contain("*.local,SRV-MON-*");
        html.Should().Contain("80,443");
        html.Should().Contain("53");
        html.Should().Contain("OUT");
        html.Should().Contain("applySpacingPreset(TOPOLOGY_DATA.defaultSpacing);");
    }
}
