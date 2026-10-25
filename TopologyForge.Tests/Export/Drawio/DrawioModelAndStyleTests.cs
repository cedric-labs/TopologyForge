namespace TopologyForge.Tests.Export.Drawio;

using FluentAssertions;
using TopologyForge.Export.Drawio.Model;
using TopologyForge.Export.Drawio.Styling;
using Xunit;

public sealed class DrawioModelAndStyleTests
{
    [Fact]
    public void BuildCardHtml_WhenCalled_GeneratesMinifiedSingleLineWithoutNewlines()
    {
        // Arrange
        var node = new DiagramNode(
            Id: "node_1",
            Label: "SRV-DATABASE-01 & PROD",
            Subtitle: "10.0.1.15",
            X: 100,
            Y: 200,
            Width: 200,
            Height: 90,
            RoleKey: "Database",
            Badges: [":5432/TCP", ":22/TCP", ":9100/TCP"]);

        var style = DiagramTheme.DarkModern.RoleStyles["Database"];

        // Act
        string html = DrawioHtmlCardBuilder.BuildCardHtml(node, style);

        // Assert
        html.Should().NotContain("\n")
            .And.NotContain("\r")
            .And.Contain("SRV-DATABASE-01 &amp; PROD")
            .And.Contain("10.0.1.15")
            .And.Contain(":5432/TCP")
            .And.Contain("DATABASE");
    }

    [Fact]
    public void BuildCardHtml_WhenBadgesExceedNine_AppendsOverflowBadge()
    {
        // Arrange
        var badges = Enumerable.Range(1, 15).Select(i => $":{8000 + i}/TCP").ToList();
        var node = new DiagramNode(
            Id: "node_many",
            Label: "SRV-CLUSTER",
            Subtitle: "10.0.1.20",
            X: 100,
            Y: 200,
            Width: 200,
            Height: 120,
            RoleKey: "Standard",
            Badges: badges);

        var style = DiagramTheme.DarkModern.RoleStyles["Standard"];

        // Act
        string html = DrawioHtmlCardBuilder.BuildCardHtml(node, style);

        // Assert
        html.Should().Contain("+6")
            .And.Contain(":8001/TCP")
            .And.Contain(":8009/TCP")
            .And.NotContain(":8010/TCP");
    }

    [Theory]
    [InlineData(0, 70)]
    [InlineData(1, 100)]
    [InlineData(3, 100)]
    [InlineData(4, 120)]
    [InlineData(6, 120)]
    [InlineData(7, 140)]
    [InlineData(12, 160)]
    public void ComputeCardHeight_WhenBadgesProvided_CalculatesGridAlignedHeight(int badgeCount, double expectedHeight)
    {
        // Act
        double height = DrawioHtmlCardBuilder.ComputeCardHeight(badgeCount, gridSize: 10);

        // Assert
        height.Should().Be(expectedHeight);
    }

    [Fact]
    public void DiagramTheme_DarkModernAndEnterpriseLight_ExposeConsistentRoles()
    {
        // Arrange & Act
        var dark = DiagramTheme.DarkModern;
        var light = DiagramTheme.EnterpriseLight;

        // Assert
        dark.RoleStyles.Should().ContainKeys("Standard", "AuditedRoot", "Database", "ExternalGateway");
        light.RoleStyles.Should().ContainKeys("Standard", "AuditedRoot", "Database", "ExternalGateway");
        dark.CanvasBackgroundColor.Should().Be("#0f172a");
        light.CanvasBackgroundColor.Should().Be("#ffffff");
    }
}
