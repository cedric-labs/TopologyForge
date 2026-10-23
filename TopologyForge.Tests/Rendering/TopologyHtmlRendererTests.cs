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
            new TopologyFlowRecord("SRV-API-01", "10.10.2.10", 54000, "SRV-DB-01", "10.10.3.20", 5432, TopologyTransportProtocol.Tcp, TopologyFlowDirection.Outbound, "dotnet", "Api.dll", 85)
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
}
