namespace TopologyForge.Tests.Export.Drawio;

using AwesomeAssertions;
using TopologyForge.Export.Drawio;
using TopologyForge.Export.Drawio.Model;
using TopologyForge.Export.Drawio.Styling;
using Xunit;

public sealed class DrawioXmlRendererTests
{
    [Fact]
    public void Render_WhenCalledWithHierarchicalContainersAndNodes_ResolvesRelativeCoordinates()
    {
        // Arrange
        // Zone en (100, 80, W:500, H:400)
        var container = new DiagramContainer("zone_1", "Subnet 10.0.1.0/24", 100, 80, 500, 400);

        // Serveur enfant en coordonnées absolues (130, 130, W:200, H:90)
        // La coordonnée relative doit valoir X = 30, Y = 50
        var node = new DiagramNode(
            Id: "srv_1",
            Label: "SRV-APP",
            Subtitle: "10.0.1.10",
            X: 130,
            Y: 130,
            Width: 200,
            Height: 90,
            ContainerId: "zone_1",
            Shape: DiagramNodeShape.RichCardHtml,
            Metadata: new Dictionary<string, string> { ["ip"] = "10.0.1.10", ["os"] = "Linux" },
            TooltipTemplate: "IP: %ip%&#xa;OS: %os%");

        var edge = new DiagramEdge(
            Id: "e1",
            SourceNodeId: "srv_1",
            TargetNodeId: "srv_1",
            TargetPortLabel: ":443/TCP",
            TargetLabelPosition: 0.85,
            ExitAnchor: new DiagramAnchorPoint(0.5, 1.0),
            EntryAnchor: new DiagramAnchorPoint(0.5, 0.0),
            Waypoints: [new DiagramPoint(230, 250), new DiagramPoint(230, 300)]);

        var document = new DiagramDocument(
            Title: "Test Topology",
            Layers: [new DiagramLayer("layer_infra", "Infrastructure"), new DiagramLayer("layer_flows", "Flows")],
            Containers: [container],
            Nodes: [node],
            Edges: [edge],
            BackgroundColor: "#0f172a");

        var renderer = new DrawioXmlRenderer();

        // Act
        string xml = renderer.Render(document, DiagramTheme.DarkModern);

        // Assert
        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>")
            .And.Contain("<mxfile host=\"app.diagrams.net\"")
            .And.Contain("background=\"#0f172a\"")
            .And.Contain("parent=\"zone_1\"")
            .And.Contain("x=\"30\" y=\"50\"") // Coordonnées relatives vérifiées !
            .And.Contain("overflow=fill;")
            .And.Contain("placeholders=\"1\"")
            .And.Contain("exitDx=0;exitDy=0;exitPerimeter=0;")
            .And.Contain("entryDx=0;entryDy=0;entryPerimeter=0;")
            .And.Contain(":443/TCP")
            .And.Contain("<Array as=\"points\">");
    }
}
