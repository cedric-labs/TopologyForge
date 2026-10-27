namespace TopologyForge.Tests.Geometry;

using AwesomeAssertions;
using TopologyForge.Export.Drawio.Model;
using TopologyForge.Geometry;
using Xunit;

public sealed class TopologyEdgeRouterTests
{
    [Fact]
    public void GridSnapper_Snap_AlignsOnSpecifiedGridStep()
    {
        // Act & Assert
        GridSnapper.Snap(143.7, 10).Should().Be(140.0);
        GridSnapper.Snap(146.2, 10).Should().Be(150.0);
        GridSnapper.Snap(102.5, 10).Should().Be(100.0);
    }

    [Fact]
    public void GridSnapper_ComputeGridAlignedAnchor_ProducesAbsoluteCoordOnGridAndValidRatio()
    {
        // Arrange
        double origin = 100.0;
        double length = 90.0;

        // Act
        var (ratio, absCoord) = GridSnapper.ComputeGridAlignedAnchor(origin, length, 0, 3, gridSize: 10);

        // Assert
        (absCoord % 10.0).Should().Be(0.0);
        ratio.Should().BeInRange(0.1, 0.9);
        absCoord.Should().BeInRange(origin, origin + length);
    }

    [Fact]
    public void RouteEdges_WhenOpposingFlowsExist_PreventsXCrossingAndMaintainsParallelOffset()
    {
        // Arrange: srv_A en (100, 100) et srv_B en (500, 100)
        var nodeA = new DiagramNode("srv_A", "Server A", "10.0.1.1", 100, 100, 200, 90);
        var nodeB = new DiagramNode("srv_B", "Server B", "10.0.1.2", 500, 100, 200, 90);

        // edge1: A -> B:443
        var edge1 = new DiagramEdge("e1", "srv_A", "srv_B", TargetPortLabel: ":443/TCP");
        // edge2: B -> A:5432
        var edge2 = new DiagramEdge("e2", "srv_B", "srv_A", TargetPortLabel: ":5432/TCP");

        var router = new TopologyEdgeRouter(gridSize: 10, fanSpacing: 20);

        // Act
        var routed = router.RouteEdges([nodeA, nodeB], [edge1, edge2]);

        // Assert
        routed.Should().HaveCount(2);
        var r1 = routed.Single(e => e.Id == "e1");
        var r2 = routed.Single(e => e.Id == "e2");

        r1.Waypoints.Should().NotBeNullOrEmpty();
        r2.Waypoints.Should().NotBeNullOrEmpty();

        // Les deux arêtes doivent avoir des couloirs distincts distants d'au moins 20px
        double diffY = Math.Abs(r1.Waypoints![0].Y - r2.Waypoints![0].Y);
        diffY.Should().BeGreaterThanOrEqualTo(20.0);

        // Les ancres ne doivent pas se chevaucher
        r1.ExitAnchor.Should().NotBe(r2.EntryAnchor);
    }

    [Fact]
    public void RouteEdges_WhenObstacleBetweenNodes_RoutesAroundObstacleBoundingBox()
    {
        // Arrange: A en (100, 100), Obstacle en (350, 80, W:200, H:100), B en (650, 100)
        var nodeA = new DiagramNode("srv_A", "Server A", "10.0.1.1", 100, 100, 150, 80);
        var nodeObstacle = new DiagramNode("srv_Obs", "Obstacle", "10.0.1.99", 300, 80, 200, 100);
        var nodeB = new DiagramNode("srv_B", "Server B", "10.0.1.2", 600, 100, 150, 80);

        var edge = new DiagramEdge("e_detour", "srv_A", "srv_B", TargetPortLabel: ":80/TCP");
        var router = new TopologyEdgeRouter(gridSize: 10, fanSpacing: 20);

        // Act
        var routed = router.RouteEdges([nodeA, nodeObstacle, nodeB], [edge]);

        // Assert
        var routedEdge = routed.Single();
        routedEdge.Waypoints.Should().NotBeNull().And.HaveCountGreaterThanOrEqualTo(2);

        // Vérifier qu'aucun point de passage ou segment ne traverse la boîte obstacle protégée
        var obsBox = CorridorWaypointFinder.BoundingBox.FromNode(nodeObstacle, padding: 16.0);
        bool hasCollision = obsBox.IntersectsSegment(routedEdge.Waypoints![0], routedEdge.Waypoints[1]);
        hasCollision.Should().BeFalse();
    }
}
