namespace TopologyForge.Tests.E2E.ScriptCoverage;

/// <summary>
/// Block coverage range returned by V8 (<c>Profiler.takePreciseCoverage</c>), as offsets in the script source.
/// </summary>
public sealed record V8CoverageRange(int StartOffset, int EndOffset, int Count);

/// <summary>
/// Converts the V8 coverage of the page's inline script into execution counts per line of <c>topology-map.js</c>.
/// </summary>
public static class MapScriptLineHitCalculator
{
    /// <summary>
    /// Computes the executions of each code line of the map script.
    /// </summary>
    /// <param name="scriptSource">Source of the &lt;script&gt; block as V8 compiled it.</param>
    /// <param name="mapScript">Content of the <c>topology-map.js</c> resource.</param>
    /// <param name="ranges">Coverage ranges of every function of the script.</param>
    /// <returns>Executions per line number (1-based) of <c>topology-map.js</c>.</returns>
    /// <exception cref="InvalidOperationException">The map script is missing from <paramref name="scriptSource"/>.</exception>
    public static IReadOnlyDictionary<int, int> Calculate(string scriptSource, string mapScript, IReadOnlyList<V8CoverageRange> ranges)
    {
        // The HTML parser replaces CRLF with LF in the content of a <script>: V8 offsets refer to this normalised text.
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

    // A line made only of closing delimiters is not a statement: counting it would skew the coverage ratio.
    private static bool IsExecutableLine(string code) =>
        code.Length > 0
        && !code.StartsWith("//", StringComparison.Ordinal)
        && !code.StartsWith("/*", StringComparison.Ordinal)
        && !code.StartsWith('*')
        && code.Any(c => c is not ('}' or ')' or ']' or ';' or ',' or ' '));

    // V8 nests block coverage ranges: the shortest one containing the offset gives its execution count.
    private static int InnermostCountAt(IReadOnlyList<V8CoverageRange> ranges, int offset)
    {
        var innermost = ranges
            .Where(range => range.StartOffset <= offset && offset < range.EndOffset)
            .MinBy(range => range.EndOffset - range.StartOffset);
        return innermost?.Count ?? 0;
    }
}
