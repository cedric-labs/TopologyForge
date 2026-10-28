using System.Globalization;
using System.Text;
using TopologyForge.Rendering;

namespace TopologyForge.Tests.E2E.ScriptCoverage;

/// <summary>
/// Cumule sur tous les tests E2E les exécutions par ligne de <c>topology-map.js</c> et les écrit au format LCOV, que
/// SonarQube lit pour mesurer la couverture du script.
/// </summary>
public static class MapScriptLcovReport
{
    /// <summary>
    /// Variable d'environnement renseignée par <c>verify.ps1</c> avec le chemin du rapport. Sans elle, rien n'est mesuré.
    /// </summary>
    public const string OutputPathVariable = "TOPOLOGYFORGE_JS_LCOV_PATH";

    private const string MapScriptResourceName = "TopologyForge.Rendering.topology-map.js";

    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly Dictionary<int, int> LineHits = [];
    private static readonly Lazy<string> MapScript = new(ReadMapScriptResource);

    /// <summary>
    /// Chemin du rapport LCOV demandé, ou <see langword="null"/> si la mesure n'est pas demandée.
    /// </summary>
    public static string? OutputPath =>
        Environment.GetEnvironmentVariable(OutputPathVariable) is { Length: > 0 } path ? path : null;

    /// <summary>
    /// Ajoute un relevé V8 au cumul et réécrit le rapport complet.
    /// </summary>
    /// <param name="scriptSource">Source du bloc &lt;script&gt; de la page.</param>
    /// <param name="ranges">Plages de couverture de toutes les fonctions du script.</param>
    /// <param name="cancellationToken">Jeton d'annulation de l'écriture.</param>
    public static async Task AddAsync(string scriptSource, IReadOnlyList<V8CoverageRange> ranges, CancellationToken cancellationToken = default)
    {
        if (OutputPath is not { } outputPath)
        {
            return;
        }

        var hits = MapScriptLineHitCalculator.Calculate(scriptSource, MapScript.Value, ranges);
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var (line, count) in hits)
            {
                LineHits[line] = LineHits.GetValueOrDefault(line) + count;
            }

            // Le rapport est réécrit à chaque relevé : xUnit n'offre pas de point de sortie commun à toutes les classes de
            // test, et le dernier relevé contient ainsi le cumul de toute l'exécution.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
            await File.WriteAllTextAsync(outputPath, FormatLcov(), Encoding.ASCII, cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private static string FormatLcov()
    {
        var lcov = new StringBuilder();
        lcov.AppendLine("TN:");
        lcov.Append("SF:").AppendLine(FindMapScriptSourcePath());
        foreach (var (line, count) in LineHits.OrderBy(entry => entry.Key))
        {
            lcov.Append("DA:").Append(line.ToString(CultureInfo.InvariantCulture))
                .Append(',').AppendLine(count.ToString(CultureInfo.InvariantCulture));
        }

        lcov.Append("LF:").AppendLine(LineHits.Count.ToString(CultureInfo.InvariantCulture));
        lcov.Append("LH:").AppendLine(LineHits.Count(entry => entry.Value > 0).ToString(CultureInfo.InvariantCulture));
        lcov.AppendLine("end_of_record");
        return lcov.ToString();
    }

    // SonarQube rattache la couverture au fichier source du dépôt, pas à la ressource copiée dans bin/.
    private static string FindMapScriptSourcePath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TopologyForge.slnx")))
            {
                return Path.Combine(directory.FullName, "TopologyForge", "Rendering", "topology-map.js");
            }
        }

        throw new InvalidOperationException($"TopologyForge.slnx was not found above '{AppContext.BaseDirectory}'.");
    }

    private static string ReadMapScriptResource()
    {
        using var stream = typeof(TopologyHtmlRenderer).Assembly.GetManifestResourceStream(MapScriptResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{MapScriptResourceName}' is missing from the TopologyForge assembly.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
