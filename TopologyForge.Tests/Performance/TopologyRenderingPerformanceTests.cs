using System.Diagnostics;
using FluentAssertions;
using Microsoft.Playwright;
using TopologyForge.Models;
using TopologyForge.Rendering;
using TopologyForge.Tests.E2E.Pages;
using Xunit.Abstractions;

namespace TopologyForge.Tests.Performance;

/// <summary>
/// Tests de performance et benchmarks mesurant les temps de gÃ©nÃ©ration et de rendu navigateur
/// pour diffÃ©rentes volumÃ©tries de serveurs N et de liens Y.
/// </summary>
public sealed class TopologyRenderingPerformanceTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData(10, 25, 100)]      // 10 serveurs, 25 liens (seuil max 100 ms)
    [InlineData(50, 200, 250)]     // 50 serveurs, 200 liens (seuil max 250 ms)
    [InlineData(200, 1000, 600)]   // 200 serveurs, 1 000 liens (seuil max 600 ms)
    [InlineData(1000, 5000, 2000)] // 1 000 serveurs, 5 000 liens (seuil max 2 000 ms)
    public void RenderHtml_WhenGivenVaryingScale_ExecutesWithinTimeBudget(
        int serverCount,
        int edgeCount,
        int maxAllowedMs)
    {
        var graph = GenerateSyntheticGraph(serverCount, edgeCount);
        var renderer = new TopologyHtmlRenderer();

        // Warmup JIT
        _ = renderer.RenderHtml(new NetworkTopologyGraph(DateTimeOffset.UtcNow, [], [], 0, 0));

        var sw = Stopwatch.StartNew();
        var html = renderer.RenderHtml(graph, new TopologyRenderingOptions
        {
            Title = $"Benchmark {serverCount} Nodes, {edgeCount} Edges"
        });
        sw.Stop();

        _output.WriteLine($"[HTML Render] Servers: {serverCount,5} | Edges: {edgeCount,5} | Elapsed: {sw.ElapsedMilliseconds,4} ms | Size: {html.Length / 1024,5} KB");

        html.Should().NotBeNullOrWhiteSpace();
        html.Length.Should().BeGreaterThan(1000);
        sw.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(maxAllowedMs,
            $"la gÃ©nÃ©ration HTML pour {serverCount} serveurs et {edgeCount} liens doit respecter le budget de {maxAllowedMs} ms");
    }

    [Theory]
    [InlineData(10, 25)]
    [InlineData(50, 150)]
    [InlineData(200, 500)]
    public async Task BrowserRender_WhenGivenVaryingScale_LoadsAndRendersInHeadlessPlaywright(
        int serverCount,
        int edgeCount)
    {
        var tempHtmlPath = Path.Combine(Path.GetTempPath(), $"perf_bench_{serverCount}_{Guid.NewGuid():N}.html");
        try
        {
            var graph = GenerateSyntheticGraph(serverCount, edgeCount);
            var renderer = new TopologyHtmlRenderer();
            await renderer.WriteHtmlFileAsync(tempHtmlPath, graph, $"Benchmark Map {serverCount} Nodes");

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
            });

            var mapPage = new TopologyMapPage(page);

            var sw = Stopwatch.StartNew();
            await mapPage.NavigateToFileAsync(tempHtmlPath);
            await mapPage.SvgCanvas.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            sw.Stop();

            var renderedNodes = await mapPage.GetNodeCountAsync();
            var renderedEdges = await mapPage.GetEdgeCountAsync();

            _output.WriteLine($"[Browser Headless] Servers: {serverCount,4} | Edges: {edgeCount,4} | Browser Load: {sw.ElapsedMilliseconds,4} ms | Rendered Nodes: {renderedNodes,4} | Rendered Edges: {renderedEdges,4}");

            renderedNodes.Should().BeGreaterThan(0);
            sw.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(5000,
                $"le chargement navigateur pour {serverCount} serveurs doit s'exÃ©cuter en moins de 5 secondes");
        }
        finally
        {
            if (File.Exists(tempHtmlPath))
            {
                File.Delete(tempHtmlPath);
            }
        }
    }

    private static NetworkTopologyGraph GenerateSyntheticGraph(int serverCount, int edgeCount)
    {
        var random = new Random(42);
        var nodes = new List<TopologyServerNode>(serverCount);

        for (var i = 0; i < serverCount; i++)
        {
            var isLocal = i == 0;
            var host = isLocal ? "SRV-AUDITED-HUB" : $"SRV-NODE-{i:D4}";
            var ip = $"10.{(i / 256) % 256}.{i % 256}.{(i * 7) % 254 + 1}";
            var port = 1000 + (i % 50);

            var listeningPorts = new List<ServerListenerDescriptor>
            {
                new(port, TopologyTransportProtocol.Tcp, isLocal ? "core-api" : "worker-service", "Production Service")
            };

            nodes.Add(new TopologyServerNode(
                host,
                host,
                ip,
                isLocal,
                listeningPorts,
                InboundConnectionCount: 0,
                OutboundConnectionCount: 0));
        }

        var edges = new List<TopologyServiceEdge>(edgeCount);
        for (var i = 0; i < edgeCount; i++)
        {
            var srcIdx = random.Next(0, serverCount);
            var dstIdx = random.Next(0, serverCount);
            if (srcIdx == dstIdx)
            {
                dstIdx = (srcIdx + 1) % serverCount;
            }

            var srcNode = nodes[srcIdx];
            var dstNode = nodes[dstIdx];
            var targetPort = dstNode.ListeningPorts.Count > 0 ? dstNode.ListeningPorts[0].Port : 443;
            var edgeId = $"{srcNode.NodeId}->{dstNode.NodeId}:{targetPort}/Tcp";

            edges.Add(new TopologyServiceEdge(
                edgeId,
                srcNode.NodeId,
                dstNode.NodeId,
                targetPort,
                TopologyTransportProtocol.Tcp,
                "bench-proc",
                "bench-target",
                ObservationCount: random.Next(1, 100),
                FlowDirection: TopologyFlowDirection.Outbound));
        }

        return new NetworkTopologyGraph(DateTimeOffset.UtcNow, nodes, edges, edgeCount, 0);
    }
}
