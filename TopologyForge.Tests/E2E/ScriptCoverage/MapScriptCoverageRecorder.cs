using System.Text.Json;
using Microsoft.Playwright;

namespace TopologyForge.Tests.E2E.ScriptCoverage;

/// <summary>
/// Measures, through the Chromium CDP protocol, the V8 coverage of the map script during an E2E test.
/// </summary>
public sealed class MapScriptCoverageRecorder(ICDPSession cdpSession)
{
    /// <summary>
    /// Starts measuring on a page, before any navigation, so that the inline script is instrumented as soon as it runs.
    /// </summary>
    /// <param name="page">Chromium page driven by the test.</param>
    /// <returns>Recorder attached to the page.</returns>
    public static async Task<MapScriptCoverageRecorder> StartAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var cdpSession = await page.Context.NewCDPSessionAsync(page);
        await cdpSession.SendAsync("Debugger.enable");
        await cdpSession.SendAsync("Profiler.enable");
        await cdpSession.SendAsync(
            "Profiler.startPreciseCoverage",
            new Dictionary<string, object> { ["callCount"] = true, ["detailed"] = true });
        return new MapScriptCoverageRecorder(cdpSession);
    }

    /// <summary>
    /// Takes the coverage of the current document and adds it to the LCOV report. Call it before each navigation and before
    /// closing the page, because the counters of an unloaded document are lost.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for writing the report.</param>
    public async Task CollectAsync(CancellationToken cancellationToken = default)
    {
        // takePreciseCoverage resets the counters: successive readings add up without double counting.
        var coverage = await cdpSession.SendAsync("Profiler.takePreciseCoverage");
        if (coverage is not { } result)
        {
            return;
        }

        foreach (var script in result.GetProperty("result").EnumerateArray().Where(IsPageScript))
        {
            var scriptSource = await ReadScriptSourceAsync(script.GetProperty("scriptId").GetString());
            if (scriptSource is null)
            {
                continue;
            }

            var ranges = script.GetProperty("functions").EnumerateArray()
                .SelectMany(function => function.GetProperty("ranges").EnumerateArray())
                .Select(range => new V8CoverageRange(
                    range.GetProperty("startOffset").GetInt32(),
                    range.GetProperty("endOffset").GetInt32(),
                    range.GetProperty("count").GetInt32()))
                .ToList();
            await MapScriptLcovReport.AddAsync(scriptSource, ranges, cancellationToken);
        }
    }

    // Only the page's <script> block has a file: URL; Page.EvaluateAsync expressions have no URL.
    private static bool IsPageScript(JsonElement script) =>
        script.GetProperty("url").GetString() is { } url
        && url.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

    private async Task<string?> ReadScriptSourceAsync(string? scriptId)
    {
        if (scriptId is null)
        {
            return null;
        }

        var response = await cdpSession.SendAsync(
            "Debugger.getScriptSource",
            new Dictionary<string, object> { ["scriptId"] = scriptId });
        return response?.GetProperty("scriptSource").GetString();
    }
}
