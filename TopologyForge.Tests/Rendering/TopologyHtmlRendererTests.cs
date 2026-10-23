using FluentAssertions;
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

    [Fact]
    public void RenderHtml_WhenGraphProvided_GeneratesSelfContainedOfflineHtmlWithNodesAndPorts()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();

        // Act
        var html = renderer.RenderHtml(graph, "Carte Réseau Production");

        // Assert
        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("Carte Réseau Production");
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
            Title = "Topologie Grand Parc",
            DefaultSortMethod = TopologyNodeSortMethod.BarycentricSugiyama,
            DefaultRoutingMode = TopologyEdgeRoutingMode.OrthogonalCorridors,
            DefaultSpacingProfile = TopologySpacingProfile.Spacious
        };

        // Act
        var html = renderer.RenderHtml(graph, options);

        // Assert
        html.Should().Contain("Topologie Grand Parc");
        html.Should().Contain("id=\"arrowOut\"");
        html.Should().Contain("id=\"arrowIn\"");
        html.Should().Contain("id=\"arrowHighlight\"");
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
    public void RenderHtml_WhenSpecialCharactersInTitle_EscapesHtmlCorrectly()
    {
        // Arrange
        var renderer = new TopologyHtmlRenderer();
        var graph = CreateSampleGraph();

        // Act
        var html = renderer.RenderHtml(graph, "Audit <script>alert(1)</script> & Flux");

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
}
