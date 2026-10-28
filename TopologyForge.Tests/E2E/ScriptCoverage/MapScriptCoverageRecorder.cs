using System.Text.Json;
using Microsoft.Playwright;

namespace TopologyForge.Tests.E2E.ScriptCoverage;

/// <summary>
/// Mesure par le protocole CDP de Chromium la couverture V8 du script de la carte pendant un test E2E.
/// </summary>
public sealed class MapScriptCoverageRecorder(ICDPSession cdpSession)
{
    /// <summary>
    /// Démarre la mesure sur une page, avant toute navigation pour que le script inline soit instrumenté dès son exécution.
    /// </summary>
    /// <param name="page">Page Chromium pilotée par le test.</param>
    /// <returns>Enregistreur attaché à la page.</returns>
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
    /// Relève la couverture du document courant et l'ajoute au rapport LCOV. À appeler avant chaque navigation et avant la
    /// fermeture de la page, car les compteurs d'un document déchargé sont perdus.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation de l'écriture du rapport.</param>
    public async Task CollectAsync(CancellationToken cancellationToken = default)
    {
        // takePreciseCoverage remet les compteurs à zéro : les relevés successifs s'additionnent sans double comptage.
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

    // Seul le bloc <script> de la page porte une URL file: ; les expressions de Page.EvaluateAsync n'ont pas d'URL.
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
