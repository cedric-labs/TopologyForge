namespace TopologyForge.Tests.E2E.ScriptCoverage;

/// <summary>
/// Plage de couverture par blocs renvoyée par V8 (<c>Profiler.takePreciseCoverage</c>), en offsets du source du script.
/// </summary>
public sealed record V8CoverageRange(int StartOffset, int EndOffset, int Count);

/// <summary>
/// Convertit la couverture V8 du script inline de la page en nombre d'exécutions par ligne de <c>topology-map.js</c>.
/// </summary>
public static class MapScriptLineHitCalculator
{
    /// <summary>
    /// Calcule les exécutions de chaque ligne de code du script de la carte.
    /// </summary>
    /// <param name="scriptSource">Source du bloc &lt;script&gt; tel que V8 l'a compilé.</param>
    /// <param name="mapScript">Contenu de la ressource <c>topology-map.js</c>.</param>
    /// <param name="ranges">Plages de couverture de toutes les fonctions du script.</param>
    /// <returns>Exécutions par numéro de ligne (base 1) de <c>topology-map.js</c>.</returns>
    /// <exception cref="InvalidOperationException">Le script de la carte est absent de <paramref name="scriptSource"/>.</exception>
    public static IReadOnlyDictionary<int, int> Calculate(string scriptSource, string mapScript, IReadOnlyList<V8CoverageRange> ranges)
    {
        // Le parseur HTML remplace CRLF par LF dans le contenu d'un <script> : les offsets V8 portent sur ce texte normalisé.
        var normalizedMapScript = mapScript.ReplaceLineEndings("\n");
        var scriptStart = scriptSource.IndexOf(normalizedMapScript, StringComparison.Ordinal);
        if (scriptStart < 0)
        {
            throw new InvalidOperationException("The page script does not contain topology-map.js: the coverage cannot be mapped to its lines.");
        }

        var hits = new Dictionary<int, int>();
        var lineStart = 0;
        var lineNumber = 1;
        foreach (var line in normalizedMapScript.Split('\n'))
        {
            var code = line.TrimStart();
            if (IsExecutableLine(code))
            {
                var offset = scriptStart + lineStart + (line.Length - code.Length);
                hits[lineNumber] = InnermostCountAt(ranges, offset);
            }

            lineStart += line.Length + 1;
            lineNumber++;
        }

        return hits;
    }

    // Une ligne réduite à des délimiteurs fermants n'est pas une instruction : la compter fausserait le ratio de couverture.
    private static bool IsExecutableLine(string code) =>
        code.Length > 0
        && !code.StartsWith("//", StringComparison.Ordinal)
        && !code.StartsWith("/*", StringComparison.Ordinal)
        && !code.StartsWith('*')
        && code.Any(c => c is not ('}' or ')' or ']' or ';' or ',' or ' '));

    // V8 emboîte les plages de couverture par blocs : la plus courte qui contient l'offset donne son nombre d'exécutions.
    private static int InnermostCountAt(IReadOnlyList<V8CoverageRange> ranges, int offset)
    {
        var innermost = ranges
            .Where(range => range.StartOffset <= offset && offset < range.EndOffset)
            .MinBy(range => range.EndOffset - range.StartOffset);
        return innermost?.Count ?? 0;
    }
}
