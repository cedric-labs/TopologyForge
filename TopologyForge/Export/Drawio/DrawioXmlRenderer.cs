namespace TopologyForge.Export.Drawio;

using System.Globalization;
using System.Text;
using System.Xml;
using TopologyForge.Export.Drawio.Model;
using TopologyForge.Export.Drawio.Styling;

/// <summary>
/// Sérialiseur XML standard Draw.io / diagrams.net pour graphes et topologies.
/// </summary>
public sealed class DrawioXmlRenderer
{
    /// <summary>
    /// Sérialise le document de diagramme en XML Draw.io standard non compressé.
    /// </summary>
    public string Render(DiagramDocument document, DiagramTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var activeTheme = theme ?? DiagramTheme.DarkModern;

        var (resolvedContainers, resolvedNodes) = DiagramCoordinateTransformer.ResolveHierarchicalCoordinates(document);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            NewLineChars = "\n",
            OmitXmlDeclaration = false
        };

        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, settings))
        {
            writer.WriteStartDocument();

            // <mxfile>
            writer.WriteStartElement("mxfile");
            writer.WriteAttributeString("host", "app.diagrams.net");
            writer.WriteAttributeString("agent", "TopologyForge Draw.io Serializer");
            writer.WriteAttributeString("version", "24.0.0");

            // <diagram>
            writer.WriteStartElement("diagram");
            writer.WriteAttributeString("id", "diagram_root");
            writer.WriteAttributeString("name", document.Title);

            // <mxGraphModel>
            writer.WriteStartElement("mxGraphModel");
            writer.WriteAttributeString("dx", "1422");
            writer.WriteAttributeString("dy", "794");
            writer.WriteAttributeString("grid", "1");
            writer.WriteAttributeString("gridSize", document.GridSize.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("guides", "1");
            writer.WriteAttributeString("tooltips", "1");
            writer.WriteAttributeString("connect", "1");
            writer.WriteAttributeString("arrows", "1");
            writer.WriteAttributeString("fold", "1");
            writer.WriteAttributeString("page", "1");
            writer.WriteAttributeString("pageScale", "1");
            writer.WriteAttributeString("pageWidth", "1920");
            writer.WriteAttributeString("pageHeight", "1080");
            writer.WriteAttributeString("background", document.BackgroundColor ?? activeTheme.CanvasBackgroundColor);

            // <root>
            writer.WriteStartElement("root");

            // Cellule système 0
            WriteCell(writer, "0", null, null);

            // Calques utilisateur
            foreach (var layer in document.Layers)
            {
                WriteCell(writer, layer.Id, "0", layer.Name, isLayer: true);
            }

            // Zones / Conteneurs
            foreach (var rc in resolvedContainers)
            {
                WriteContainer(writer, rc, activeTheme);
            }

            // Nœuds / Serveurs
            foreach (var rn in resolvedNodes)
            {
                WriteNode(writer, rn, activeTheme);
            }

            // Arêtes / Liens de flux
            foreach (var edge in document.Edges)
            {
                WriteEdge(writer, edge, activeTheme);
            }

            writer.WriteEndElement(); // </root>
            writer.WriteEndElement(); // </mxGraphModel>
            writer.WriteEndElement(); // </diagram>
            writer.WriteEndElement(); // </mxfile>

            writer.WriteEndDocument();
            writer.Flush();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteCell(XmlWriter writer, string id, string? parentId, string? value, bool isLayer = false)
    {
        writer.WriteStartElement("mxCell");
        writer.WriteAttributeString("id", id);
        if (!string.IsNullOrEmpty(parentId))
        {
            writer.WriteAttributeString("parent", parentId);
        }

        if (isLayer && !string.IsNullOrEmpty(value))
        {
            writer.WriteAttributeString("value", value);
        }

        writer.WriteEndElement();
    }

    private static void WriteContainer(XmlWriter writer, DiagramCoordinateTransformer.ResolvedContainer rc, DiagramTheme theme)
    {
        var container = rc.Original;
        string style = BuildContainerStyleString(theme.ContainerStyle, container.CustomStyle);
        bool hasMetadata = container.Metadata is { Count: > 0 };

        if (hasMetadata)
        {
            writer.WriteStartElement("object");
            writer.WriteAttributeString("label", container.Label);
            writer.WriteAttributeString("id", container.Id);
            foreach (var kv in container.Metadata!)
            {
                writer.WriteAttributeString(kv.Key, kv.Value);
            }
        }

        writer.WriteStartElement("mxCell");
        if (!hasMetadata)
        {
            writer.WriteAttributeString("value", container.Label);
            writer.WriteAttributeString("id", container.Id);
        }

        writer.WriteAttributeString("style", style);
        writer.WriteAttributeString("vertex", "1");
        writer.WriteAttributeString("parent", rc.ParentCellId);

        writer.WriteStartElement("mxGeometry");
        writer.WriteAttributeString("x", rc.RelativeX.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("y", rc.RelativeY.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("width", container.Width.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("height", container.Height.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("as", "geometry");
        writer.WriteEndElement(); // </mxGeometry>

        writer.WriteEndElement(); // </mxCell>

        if (hasMetadata)
        {
            writer.WriteEndElement(); // </object>
        }
    }

    private static void WriteNode(XmlWriter writer, DiagramCoordinateTransformer.ResolvedNode rn, DiagramTheme theme)
    {
        var node = rn.Original;
        var baseStyle = theme.RoleStyles.TryGetValue(node.RoleKey, out var s)
            ? s
            : theme.RoleStyles.Values.First();

        string cellLabel = node.Shape == DiagramNodeShape.RichCardHtml
            ? DrawioHtmlCardBuilder.BuildCardHtml(node, baseStyle)
            : node.Label;

        string styleStr = BuildNodeStyleString(node.Shape, baseStyle, node.CustomStyle);
        bool hasMetadata = node.Metadata is { Count: > 0 } || !string.IsNullOrEmpty(node.TooltipTemplate);

        if (hasMetadata)
        {
            writer.WriteStartElement("object");
            writer.WriteAttributeString("label", cellLabel);
            writer.WriteAttributeString("id", node.Id);
            writer.WriteAttributeString("name", node.Label);
            writer.WriteAttributeString("placeholders", "1");

            if (!string.IsNullOrEmpty(node.TooltipTemplate))
            {
                writer.WriteAttributeString("tooltip", node.TooltipTemplate);
            }

            if (node.Metadata != null)
            {
                foreach (var kv in node.Metadata)
                {
                    writer.WriteAttributeString(kv.Key, kv.Value);
                }
            }
        }

        writer.WriteStartElement("mxCell");
        if (!hasMetadata)
        {
            writer.WriteAttributeString("value", cellLabel);
            writer.WriteAttributeString("id", node.Id);
        }

        writer.WriteAttributeString("style", styleStr);
        writer.WriteAttributeString("vertex", "1");
        writer.WriteAttributeString("parent", rn.ParentCellId);

        writer.WriteStartElement("mxGeometry");
        writer.WriteAttributeString("x", rn.RelativeX.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("y", rn.RelativeY.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("width", node.Width.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("height", node.Height.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("as", "geometry");
        writer.WriteEndElement(); // </mxGeometry>

        writer.WriteEndElement(); // </mxCell>

        if (hasMetadata)
        {
            writer.WriteEndElement(); // </object>
        }
    }

    private static void WriteEdge(XmlWriter writer, DiagramEdge edge, DiagramTheme theme)
    {
        var edgeStyle = theme.FlowStyles.TryGetValue(edge.StyleKey, out var es)
            ? es
            : theme.FlowStyles.Values.First();

        string styleStr = BuildEdgeStyleString(edge, edgeStyle);
        bool hasMetadata = edge.Metadata is { Count: > 0 };

        if (hasMetadata)
        {
            writer.WriteStartElement("object");
            writer.WriteAttributeString("label", edge.CenterLabel ?? string.Empty);
            writer.WriteAttributeString("id", edge.Id);
            foreach (var kv in edge.Metadata!)
            {
                writer.WriteAttributeString(kv.Key, kv.Value);
            }
        }

        writer.WriteStartElement("mxCell");
        if (!hasMetadata)
        {
            writer.WriteAttributeString("value", edge.CenterLabel ?? string.Empty);
            writer.WriteAttributeString("id", edge.Id);
        }

        writer.WriteAttributeString("style", styleStr);
        writer.WriteAttributeString("edge", "1");
        writer.WriteAttributeString("parent", edge.LayerId);
        writer.WriteAttributeString("source", edge.SourceNodeId);
        writer.WriteAttributeString("target", edge.TargetNodeId);

        writer.WriteStartElement("mxGeometry");
        writer.WriteAttributeString("relative", "1");
        writer.WriteAttributeString("as", "geometry");

        if (edge.Waypoints is { Count: > 0 } points)
        {
            writer.WriteStartElement("Array");
            writer.WriteAttributeString("as", "points");
            foreach (var pt in points)
            {
                writer.WriteStartElement("mxPoint");
                writer.WriteAttributeString("x", pt.X.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("y", pt.Y.ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }

            writer.WriteEndElement(); // </Array>
        }

        writer.WriteEndElement(); // </mxGeometry>
        writer.WriteEndElement(); // </mxCell>

        // Étiquette de port cible déportée
        if (!string.IsNullOrWhiteSpace(edge.TargetPortLabel))
        {
            writer.WriteStartElement("mxCell");
            writer.WriteAttributeString("id", $"{edge.Id}_tgt_lbl");
            writer.WriteAttributeString("value", edge.TargetPortLabel);
            writer.WriteAttributeString("style", "text;html=1;strokeColor=none;fillColor=none;align=center;verticalAlign=middle;fontSize=10;fontColor=#6ee7b7;");
            writer.WriteAttributeString("vertex", "1");
            writer.WriteAttributeString("connectable", "0");
            writer.WriteAttributeString("parent", edge.Id);

            writer.WriteStartElement("mxGeometry");
            writer.WriteAttributeString("x", edge.TargetLabelPosition.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("relative", "1");
            writer.WriteAttributeString("as", "geometry");

            writer.WriteStartElement("mxPoint");
            writer.WriteAttributeString("y", "-10");
            writer.WriteAttributeString("as", "offset");
            writer.WriteEndElement();

            writer.WriteEndElement(); // </mxGeometry>
            writer.WriteEndElement(); // </mxCell>
        }

        if (hasMetadata)
        {
            writer.WriteEndElement(); // </object>
        }
    }

    private static string BuildContainerStyleString(DiagramContainerStyle def, DiagramContainerStyle? custom)
    {
        var s = custom ?? def;
        int roundedVal = s.Rounded ? 1 : 0;
        int dashedVal = s.Dashed ? 1 : 0;
        return $"swimlane;startSize={s.HeaderHeight.ToString(CultureInfo.InvariantCulture)};rounded={roundedVal.ToString(CultureInfo.InvariantCulture)};arcSize={s.ArcSize.ToString(CultureInfo.InvariantCulture)};dashed={dashedVal.ToString(CultureInfo.InvariantCulture)};strokeColor={s.StrokeColor};fillColor={s.FillColor};strokeWidth={s.StrokeWidth.ToString(CultureInfo.InvariantCulture)};fontColor={s.FontColor};fontSize={s.FontSize.ToString(CultureInfo.InvariantCulture)};collapsible=1;";
    }

    private static string BuildNodeStyleString(DiagramNodeShape shape, DiagramNodeStyle def, DiagramNodeStyle? custom)
    {
        var s = custom ?? def;
        string shapeBase = shape switch
        {
            DiagramNodeShape.RichCardHtml => "rounded=1;html=1;whiteSpace=wrap;overflow=fill;align=left;verticalAlign=top;spacing=0;spacingTop=0;spacingBottom=0;spacingLeft=0;spacingRight=0;",
            DiagramNodeShape.Cylinder => "shape=cylinder3;boundedLbl=1;backgroundOutline=1;",
            DiagramNodeShape.Cloud => "shape=cloud;boundedLbl=1;",
            DiagramNodeShape.Hexagon => "shape=hexagon;perimeter=hexagonPerimeter2;",
            _ => $"rounded={(s.Rounded ? 1 : 0).ToString(CultureInfo.InvariantCulture)};arcSize={s.ArcSize.ToString(CultureInfo.InvariantCulture)};whiteSpace=wrap;html=1;"
        };

        int shadowVal = s.Shadow ? 1 : 0;
        int dashedVal = s.Dashed ? 1 : 0;

        return $"{shapeBase}shadow={shadowVal.ToString(CultureInfo.InvariantCulture)};dashed={dashedVal.ToString(CultureInfo.InvariantCulture)};fillColor={s.FillColor};strokeColor={s.StrokeColor};strokeWidth={s.StrokeWidth.ToString(CultureInfo.InvariantCulture)};fontColor={s.FontColor};fontSize={s.FontSize.ToString(CultureInfo.InvariantCulture)};";
    }

    private static string BuildEdgeStyleString(DiagramEdge edge, DiagramEdgeStyle def)
    {
        string routingStyle = edge.Routing switch
        {
            DiagramEdgeRouting.Curved => "edgeStyle=orthogonalEdgeStyle;curved=1;",
            DiagramEdgeRouting.Straight => "edgeStyle=straightEdgeStyle;",
            _ => "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;jettySize=auto;"
        };

        string exitAnchor = edge.ExitAnchor.HasValue
            ? $"exitX={edge.ExitAnchor.Value.X.ToString(CultureInfo.InvariantCulture)};exitY={edge.ExitAnchor.Value.Y.ToString(CultureInfo.InvariantCulture)};exitDx=0;exitDy=0;exitPerimeter={(edge.ExitAnchor.Value.Perimeter ? 1 : 0).ToString(CultureInfo.InvariantCulture)};"
            : string.Empty;

        string entryAnchor = edge.EntryAnchor.HasValue
            ? $"entryX={edge.EntryAnchor.Value.X.ToString(CultureInfo.InvariantCulture)};entryY={edge.EntryAnchor.Value.Y.ToString(CultureInfo.InvariantCulture)};entryDx=0;entryDy=0;entryPerimeter={(edge.EntryAnchor.Value.Perimeter ? 1 : 0).ToString(CultureInfo.InvariantCulture)};"
            : string.Empty;

        int dashedVal = def.Dashed ? 1 : 0;

        var (endArrow, startArrow) = edge.Direction switch
        {
            DiagramEdgeDirection.Bidirectional => (def.ArrowEnd, def.ArrowEnd),
            DiagramEdgeDirection.Undirected => ("none", "none"),
            _ => (def.ArrowEnd, def.ArrowStart)
        };

        return $"{routingStyle}html=1;strokeColor={def.StrokeColor};strokeWidth={def.StrokeWidth.ToString(CultureInfo.InvariantCulture)};fontColor={def.FontColor};fontSize={def.FontSize.ToString(CultureInfo.InvariantCulture)};dashed={dashedVal.ToString(CultureInfo.InvariantCulture)};endArrow={endArrow};startArrow={startArrow};{exitAnchor}{entryAnchor}";
    }
}
