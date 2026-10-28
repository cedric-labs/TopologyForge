using AwesomeAssertions;

namespace TopologyForge.Tests.E2E.ScriptCoverage;

public sealed class MapScriptLineHitCalculatorTests
{
    private const string MapScript = "function a() {\r\n  return 1;\r\n}\r\n// note\r\nconst b = 2;\r\n";
    private const string NormalizedMapScript = "function a() {\n  return 1;\n}\n// note\nconst b = 2;\n";
    private const string Payload = "\n    const TOPOLOGY_DATA = {};\n";

    private static readonly string ScriptSource = Payload + NormalizedMapScript + "  ";

    [Fact]
    public void Calculate_WhenFunctionIsNeverCalled_ReportsItsLinesWithZeroHits()
    {
        // Arrange
        var functionStart = Payload.Length;
        var functionEnd = functionStart + NormalizedMapScript.IndexOf('}', StringComparison.Ordinal) + 1;
        var ranges = new[]
        {
            new V8CoverageRange(0, ScriptSource.Length, 1),
            new V8CoverageRange(functionStart, functionEnd, 0)
        };

        // Act
        var hits = MapScriptLineHitCalculator.Calculate(ScriptSource, MapScript, ranges);

        // Assert
        hits.Should().BeEquivalentTo(new Dictionary<int, int> { [1] = 0, [2] = 0, [5] = 1 });
    }

    [Fact]
    public void Calculate_WhenRangesAreNested_UsesTheInnermostCount()
    {
        // Arrange
        var functionStart = Payload.Length;
        var functionEnd = functionStart + NormalizedMapScript.IndexOf('}', StringComparison.Ordinal) + 1;
        var ranges = new[]
        {
            new V8CoverageRange(functionStart, functionEnd, 3),
            new V8CoverageRange(0, ScriptSource.Length, 1)
        };

        // Act
        var hits = MapScriptLineHitCalculator.Calculate(ScriptSource, MapScript, ranges);

        // Assert
        hits[2].Should().Be(3);
        hits[5].Should().Be(1);
    }

    [Fact]
    public void Calculate_WhenMapScriptIsMissingFromSource_ThrowsInvalidOperationException()
    {
        // Act
        var act = () => MapScriptLineHitCalculator.Calculate("const other = 1;", MapScript, []);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*topology-map.js*");
    }
}
