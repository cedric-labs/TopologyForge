using System.Globalization;
using System.Text;
using TopologyForge.Rendering;

namespace TopologyForge.Tests.E2E.ScriptCoverage;

/// <summary>
/// Accumulates, across all E2E tests, the per-line executions of <c>topology-map.js</c> and writes them in LCOV format, which
/// SonarQube reads to measure the script coverage.
/// </summary>
public static class MapScriptLcovReport
{
    /// <summary>
    /// Environment variable set by <c>verify.ps1</c> to the report path. Without it, nothing is measured.
    /// </summary>
    public const string OutputPathVariable = "TOPOLOGYFORGE_JS_LCOV_PATH";

    private const string MapScriptResourceName = "TopologyForge.Rendering.topology-map.js";

    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly Dictionary<int, int> LineHits = [];
    private static readonly Lazy<string> MapScript = new(ReadMapScriptResource);

    /// <summary>
    /// Path of the requested LCOV report, or <see langword="null"/> when no measurement is requested.
    /// </summary>
    public static string? OutputPath =>
        Environment.GetEnvironmentVariable(OutputPathVariable) is { Length: > 0 } path ? path : null;

    /// <summary>
    /// Adds a V8 reading to the total and rewrites the complete report.
    /// </summary>
    /// <param name="scriptSource">Source of the page's &lt;script&gt; block.</param>
    /// <param name="ranges">Coverage ranges of every function of the script.</param>
    /// <param name="cancellationToken">Cancellation token for the write.</param>
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

            // The report is rewritten on each reading: xUnit offers no exit point shared by every test
            // class, so the last reading holds the total of the whole run.
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

    // SonarQube attaches the coverage to the repository source file, not to the resource copied into bin/.
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
