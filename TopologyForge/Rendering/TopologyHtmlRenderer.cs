using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TopologyForge.Models;

namespace TopologyForge.Rendering;

/// <summary>
/// Générateur de cartes topologiques HTML5 autonomes (Single-File, zéro dépendance externe) avec moteur de routage anti-croisement multi-passes, pagination par tranche et pointes directionnelles IN/OUT.
/// </summary>
public sealed class TopologyHtmlRenderer : ITopologyHtmlRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    /// <inheritdoc />
    public Task WriteHtmlFileAsync(
        string outputPath,
        NetworkTopologyGraph graph,
        string title = "Network Flow & Server Topology Map",
        CancellationToken cancellationToken = default) =>
        WriteHtmlFileAsync(
            outputPath,
            graph,
            new TopologyRenderingOptions { Title = title },
            cancellationToken);

    /// <inheritdoc />
    public Task WriteHtmlFileAsync(
        string outputPath,
        NetworkTopologyGraph graph,
        TopologyRenderingOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var htmlContent = RenderHtml(graph, options);
        return File.WriteAllTextAsync(outputPath, htmlContent, Encoding.UTF8, cancellationToken);
    }

    /// <inheritdoc />
    public string RenderHtml(NetworkTopologyGraph graph, string title = "Network Flow & Server Topology Map") =>
        RenderHtml(graph, new TopologyRenderingOptions { Title = title });

    /// <inheritdoc />
    public string RenderHtml(NetworkTopologyGraph graph, TopologyRenderingOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        var safeTitle = EscapeHtmlText(string.IsNullOrWhiteSpace(options.Title) ? "Network Flow & Server Topology Map" : options.Title);
        var payloadJson = SerializeGraphPayload(graph, options);
        var generatedAtFormatted = graph.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

        var sb = new StringBuilder(65536);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.Append("  <title>").Append(safeTitle).AppendLine("</title>");
        AppendStyles(sb);
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        AppendBodyLayout(sb, safeTitle, generatedAtFormatted, graph);
        AppendEmbeddedScript(sb, payloadJson);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string SerializeGraphPayload(NetworkTopologyGraph graph, TopologyRenderingOptions options)
    {
        var defaultSort = options.DefaultSortMethod switch
        {
            TopologyNodeSortMethod.ConnectionDegree => "degree",
            TopologyNodeSortMethod.Alphabetical => "alpha",
            TopologyNodeSortMethod.SubnetIp => "ip",
            _ => "sugiyama"
        };

        var defaultRouting = options.DefaultRoutingMode switch
        {
            TopologyEdgeRoutingMode.OrthogonalCorridors => "orthogonal",
            TopologyEdgeRoutingMode.DirectStraight => "straight",
            _ => "bezier"
        };

        var defaultSpacing = options.DefaultSpacingProfile switch
        {
            TopologySpacingProfile.Compact => "compact",
            TopologySpacingProfile.Spacious => "spacious",
            _ => "balanced"
        };

        var defaultPageSize = options.DefaultPageSize > 0 ? options.DefaultPageSize : 100;

        var dto = new
        {
            GeneratedAtUtc = graph.GeneratedAtUtc.ToString("o", CultureInfo.InvariantCulture),
            graph.TotalProcessedFlows,
            graph.ExcludedFlowsCount,
            DefaultSort = defaultSort,
            DefaultRouting = defaultRouting,
            DefaultSpacing = defaultSpacing,
            DefaultPageSize = defaultPageSize,
            Nodes = graph.Nodes.Select(n => new
            {
                n.NodeId,
                n.HostName,
                n.IpAddress,
                n.IsLocalAuditedServer,
                n.InboundConnectionCount,
                n.OutboundConnectionCount,
                Ports = n.ListeningPorts.Select(p => new
                {
                    p.Port,
                    Protocol = p.Protocol.ToString().ToUpperInvariant(),
                    p.ProcessName,
                    p.ServiceTarget
                }).ToList()
            }).ToList(),
            Edges = graph.Edges.Select(e => new
            {
                e.EdgeId,
                e.SourceNodeId,
                e.TargetNodeId,
                e.TargetPort,
                Protocol = e.Protocol.ToString().ToUpperInvariant(),
                Direction = e.FlowDirection == TopologyFlowDirection.Inbound ? "IN" : "OUT",
                e.ProcessName,
                e.ServiceTarget,
                e.ObservationCount
            }).ToList()
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        return json.Replace("</script>", "<\\/script>", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeHtmlText(string input) =>
        input
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);

    private static void AppendStyles(StringBuilder sb)
    {
        sb.AppendLine(
            """
              <style>
                :root {
                  --bg-main: #0b0f19;
                  --bg-panel: #111827;
                  --bg-card: #1e293b;
                  --border-color: #334155;
                  --text-primary: #f8fafc;
                  --text-muted: #94a3b8;
                  --accent-local: #38bdf8;
                  --accent-remote: #a78bfa;
                  --accent-port: #10b981;
                  --accent-out: #38bdf8;
                  --accent-in: #10b981;
                  --accent-highlight: #f59e0b;
                }
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                  font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                  background-color: var(--bg-main);
                  color: var(--text-primary);
                  height: 100vh;
                  display: flex;
                  flex-direction: column;
                  overflow: hidden;
                }
                header {
                  background: var(--bg-panel);
                  border-bottom: 1px solid var(--border-color);
                  padding: 0.65rem 1.25rem;
                  display: flex;
                  align-items: center;
                  justify-content: space-between;
                  gap: 1rem;
                  flex-wrap: wrap;
                }
                .title-block h1 { font-size: 1.12rem; font-weight: 700; color: var(--text-primary); }
                .title-block span { font-size: 0.76rem; color: var(--text-muted); }
                .kpi-bar { display: flex; gap: 0.65rem; align-items: center; flex-wrap: wrap; }
                .kpi-badge {
                  background: var(--bg-card);
                  border: 1px solid var(--border-color);
                  border-radius: 6px;
                  padding: 0.3rem 0.65rem;
                  font-size: 0.78rem;
                }
                .kpi-badge strong { color: var(--accent-local); font-weight: 700; margin-left: 0.25rem; }
                .workspace { display: flex; flex: 1; overflow: hidden; position: relative; }
                .sidebar {
                  width: 350px;
                  background: var(--bg-panel);
                  border-right: 1px solid var(--border-color);
                  padding: 0.45rem 0.65rem;
                  display: flex;
                  flex-direction: column;
                  gap: 0.32rem;
                  overflow-y: auto;
                  scrollbar-width: thin;
                  transition: width 0.2s ease, padding 0.2s ease;
                  flex-shrink: 0;
                  z-index: 10;
                }
                .sidebar.collapsed {
                  width: 0;
                  padding: 0;
                  border-right: none;
                  overflow: hidden;
                }
                .sidebar-toggle-btn {
                  background: var(--bg-card);
                  border: 1px solid var(--border-color);
                  color: var(--text-primary);
                  padding: 0.26rem 0.65rem;
                  border-radius: 6px;
                  cursor: pointer;
                  font-size: 0.76rem;
                  font-weight: 600;
                  display: inline-flex;
                  align-items: center;
                  gap: 0.35rem;
                  transition: all 0.15s ease;
                  user-select: none;
                }
                .sidebar-toggle-btn:hover {
                  border-color: var(--accent-local);
                  color: #38bdf8;
                  background: rgba(56, 189, 248, 0.08);
                }
                .section-title {
                  font-size: 0.68rem;
                  text-transform: uppercase;
                  letter-spacing: 0.06em;
                  color: var(--accent-local);
                  font-weight: 700;
                  border-bottom: 1px solid #1e293b;
                  padding-bottom: 0.16rem;
                  margin-top: 0.06rem;
                  cursor: pointer;
                  user-select: none;
                  display: flex;
                  justify-content: space-between;
                  align-items: center;
                }
                .section-title:hover { color: #38bdf8; }
                .section-title::after {
                  content: "▾";
                  font-size: 0.72rem;
                  transition: transform 0.2s ease;
                }
                .section-title.collapsed::after {
                  transform: rotate(-90deg);
                }
                .section-body {
                  display: flex;
                  flex-direction: column;
                  gap: 0.24rem;
                }
                .section-body.collapsed { display: none; }
                .cli-command-box {
                  background: #0b0f19;
                  border: 1px solid var(--border-color);
                  border-radius: 5px;
                  padding: 0.28rem 0.45rem;
                  display: flex;
                  flex-direction: column;
                  gap: 0.22rem;
                }
                .cli-command-code {
                  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
                  font-size: 0.66rem;
                  color: #6ee7b7;
                  word-break: break-all;
                  user-select: all;
                  background: transparent;
                }
                .filter-group { display: flex; flex-direction: column; gap: 0.14rem; }
                .filter-group label {
                  font-size: 0.68rem;
                  color: var(--text-muted);
                  font-weight: 600;
                  display: flex;
                  justify-content: space-between;
                }
                .filter-group input[type="text"], .filter-group select {
                  background: var(--bg-card);
                  border: 1px solid var(--border-color);
                  color: var(--text-primary);
                  padding: 0.22rem 0.45rem;
                  border-radius: 4px;
                  font-size: 0.75rem;
                }
                .filter-group input[type="range"] {
                  width: 100%;
                  accent-color: var(--accent-local);
                  cursor: pointer;
                  height: 14px;
                }
                .grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 0.35rem; }
                .btn-row { display: flex; gap: 0.28rem; flex-wrap: wrap; }
                button {
                  background: var(--bg-card);
                  border: 1px solid var(--border-color);
                  color: var(--text-primary);
                  padding: 0.24rem 0.5rem;
                  border-radius: 4px;
                  cursor: pointer;
                  font-size: 0.73rem;
                  transition: background 0.15s, border-color 0.15s;
                }
                button.btn-primary {
                  background: rgba(14, 165, 233, 0.2);
                  border-color: var(--accent-local);
                  color: #38bdf8;
                  font-weight: 600;
                }
                button.btn-primary:hover:not(:disabled) {
                  background: var(--accent-local);
                  color: #ffffff;
                }
                button:hover:not(:disabled) { border-color: var(--accent-local); background: #26354d; }
                button:disabled { opacity: 0.45; cursor: not-allowed; }
                .canvas-area {
                  flex: 1;
                  position: relative;
                  background: radial-gradient(circle at center, #131c31 0%, #0b0f19 100%);
                  overflow: hidden;
                }
                #topologySvg { width: 100%; height: 100%; cursor: grab; user-select: none; }
                #topologySvg:active { cursor: grabbing; }
                .inspector {
                  border-top: 1px solid var(--border-color);
                  padding-top: 0.65rem;
                  font-size: 0.8rem;
                  display: flex;
                  flex-direction: column;
                  gap: 0.4rem;
                }
                .port-pill {
                  display: inline-block;
                  background: rgba(16, 185, 129, 0.15);
                  border: 1px solid var(--accent-port);
                  color: #6ee7b7;
                  border-radius: 4px;
                  padding: 0.1rem 0.38rem;
                  font-size: 0.72rem;
                  margin: 0.1rem;
                  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
                }
                .legend {
                  position: absolute;
                  bottom: 12px;
                  left: 12px;
                  background: rgba(17, 24, 39, 0.92);
                  border: 1px solid var(--border-color);
                  border-radius: 6px;
                  padding: 0.48rem 0.75rem;
                  font-size: 0.74rem;
                  display: flex;
                  gap: 0.95rem;
                  flex-wrap: wrap;
                }
                .pagination-bar {
                  position: absolute;
                  bottom: 12px;
                  right: 12px;
                  background: rgba(17, 24, 39, 0.94);
                  border: 1px solid var(--border-color);
                  border-radius: 6px;
                  padding: 0.4rem 0.65rem;
                  font-size: 0.75rem;
                  display: flex;
                  align-items: center;
                  gap: 0.45rem;
                  flex-wrap: wrap;
                }
                .page-status {
                  color: var(--text-primary);
                  font-weight: 600;
                  padding: 0 0.35rem;
                }
                .legend-item { display: flex; align-items: center; gap: 0.38rem; }
                .dot { width: 10px; height: 10px; border-radius: 50%; display: inline-block; }
                .arrow-sample { width: 18px; height: 3px; display: inline-block; border-radius: 2px; }
              </style>
            """);
    }

    private static void AppendBodyLayout(StringBuilder sb, string safeTitle, string generatedAtFormatted, NetworkTopologyGraph graph)
    {
        var totalPorts = graph.Nodes.Sum(n => n.ListeningPorts.Count);
        sb.AppendLine("  <header>");
        sb.AppendLine("    <div class=\"title-block\">");
        sb.Append("      <h1>").Append(safeTitle).AppendLine("</h1>");
        sb.Append("      <span>Generated on ").Append(generatedAtFormatted).AppendLine(" • Self-contained offline topology map (TopologyForge)</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"kpi-bar\">");
        sb.AppendLine("      <button id=\"btnToggleSidebar\" class=\"sidebar-toggle-btn\" type=\"button\" title=\"Toggle Sidebar (B)\">◀ Sidebar</button>");
        sb.Append("      <div class=\"kpi-badge\">Servers<strong id=\"kpiNodes\">").Append(graph.Nodes.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("</strong></div>");
        sb.Append("      <div class=\"kpi-badge\">Service Ports<strong id=\"kpiPorts\">").Append(totalPorts.ToString(CultureInfo.InvariantCulture)).AppendLine("</strong></div>");
        sb.Append("      <div class=\"kpi-badge\">Inter-Server Flows<strong id=\"kpiEdges\">").Append(graph.Edges.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("</strong></div>");
        sb.Append("      <div class=\"kpi-badge\">Filtered Flows<strong>").Append(graph.ExcludedFlowsCount.ToString(CultureInfo.InvariantCulture)).Append('/').Append(graph.TotalProcessedFlows.ToString(CultureInfo.InvariantCulture)).AppendLine("</strong></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </header>");

        sb.AppendLine("  <div class=\"workspace\">");
        sb.AppendLine("    <aside class=\"sidebar\" id=\"sidebarPanel\">");
        sb.AppendLine("      <div class=\"section-title\" data-target=\"sectionLayout\">1. Layout, Sorting &amp; Routing</div>");
        sb.AppendLine("      <div class=\"section-body\" id=\"sectionLayout\">");
        sb.AppendLine("        <div class=\"grid-2\">");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"layoutSelect\">Layout</label>");
        sb.AppendLine("            <select id=\"layoutSelect\">");
        sb.AppendLine("              <option value=\"layered\">Hierarchical (Layered)</option>");
        sb.AppendLine("              <option value=\"circular\">Concentric Ring</option>");
        sb.AppendLine("              <option value=\"grid\">Multi-Column Grid</option>");
        sb.AppendLine("            </select>");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"sortSelect\">Sorting Method</label>");
        sb.AppendLine("            <select id=\"sortSelect\">");
        sb.AppendLine("              <option value=\"sugiyama\">Sugiyama (Anti-crossing)</option>");
        sb.AppendLine("              <option value=\"degree\">Connection Degree</option>");
        sb.AppendLine("              <option value=\"alpha\">Alphabetical (Hostname)</option>");
        sb.AppendLine("              <option value=\"ip\">Subnet / IP Address</option>");
        sb.AppendLine("            </select>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"grid-2\">");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"routingSelect\">Arrow Routing</label>");
        sb.AppendLine("            <select id=\"routingSelect\">");
        sb.AppendLine("              <option value=\"bezier\">Curved Bezier (Obstacle bypass)</option>");
        sb.AppendLine("              <option value=\"orthogonal\">Orthogonal Corridors (90°)</option>");
        sb.AppendLine("              <option value=\"straight\">Direct (Border to border)</option>");
        sb.AppendLine("            </select>");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"spacingSelect\">Spacing Profile</label>");
        sb.AppendLine("            <select id=\"spacingSelect\">");
        sb.AppendLine("              <option value=\"compact\">Compact (Dense)</option>");
        sb.AppendLine("              <option value=\"balanced\">Balanced (Standard)</option>");
        sb.AppendLine("              <option value=\"spacious\">Spacious (100+ servers)</option>");
        sb.AppendLine("            </select>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"grid-2\">");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"nodeGapRange\"><span>Server Gap</span><span id=\"nodeGapVal\">1.0x</span></label>");
        sb.AppendLine("            <input id=\"nodeGapRange\" type=\"range\" min=\"0.65\" max=\"2.4\" step=\"0.05\" value=\"1.0\" />");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"edgeFanRange\"><span>Arrow Fan</span><span id=\"edgeFanVal\">18px</span></label>");
        sb.AppendLine("            <input id=\"edgeFanRange\" type=\"range\" min=\"6\" max=\"46\" step=\"2\" value=\"18\" />");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"section-title\" data-target=\"sectionFilters\">2. Inclusion / Exclusion Filters</div>");
        sb.AppendLine("      <div class=\"section-body\" id=\"sectionFilters\">");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"searchInput\">Global Search (Server, IP, Port, Process)</label>");
        sb.AppendLine("          <input id=\"searchInput\" type=\"text\" placeholder=\"e.g. SRV-SQL, 1433, nginx...\" />");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"grid-2\">");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"includePortsInput\">Include ports</label>");
        sb.AppendLine("            <input id=\"includePortsInput\" type=\"text\" placeholder=\"80, 443, 1433\" />");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"excludePortsInput\">Exclude ports</label>");
        sb.AppendLine("            <input id=\"excludePortsInput\" type=\"text\" placeholder=\"53, 123, 135\" />");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"grid-2\">");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"excludeHostsInput\">Exclude servers/IP</label>");
        sb.AppendLine("            <input id=\"excludeHostsInput\" type=\"text\" placeholder=\"SRV-MON, 10.99.\" />");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"directionSelect\">Flow Direction</label>");
        sb.AppendLine("            <select id=\"directionSelect\">");
        sb.AppendLine("              <option value=\"all\">All (IN &amp; OUT)</option>");
        sb.AppendLine("              <option value=\"OUT\">Outbound (OUT)</option>");
        sb.AppendLine("              <option value=\"IN\">Inbound (IN)</option>");
        sb.AppendLine("            </select>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"btn-row\" style=\"margin-top: 0.15rem;\">");
        sb.AppendLine("          <button id=\"btnApplyFilters\" type=\"button\" class=\"btn-primary\" style=\"flex: 1;\">Apply Filters</button>");
        sb.AppendLine("          <button id=\"btnClearFilters\" type=\"button\">Reset</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"btn-row\">");
        sb.AppendLine("        <button id=\"btnResetZoom\" type=\"button\">Center View</button>");
        sb.AppendLine("        <button id=\"btnRecomputeLayout\" type=\"button\">Reset Positions</button>");
        sb.AppendLine("        <button id=\"btnExportSvg\" type=\"button\">Export SVG</button>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"section-title\" data-target=\"sectionCli\">3. CLI Command (Reproduce Map)</div>");
        sb.AppendLine("      <div class=\"section-body\" id=\"sectionCli\">");
        sb.AppendLine("        <div class=\"cli-command-box\">");
        sb.AppendLine("          <div id=\"cliCommandText\" class=\"cli-command-code\">NetFlowAudit.Cli --map-only</div>");
        sb.AppendLine("          <button id=\"btnCopyCliCommand\" type=\"button\" class=\"btn-primary\" style=\"align-self: flex-start; padding: 0.25rem 0.5rem; font-size: 0.72rem;\">📋 Copy Command</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"inspector\" id=\"inspectorPanel\">");
        sb.AppendLine("        <div class=\"section-title\" data-target=\"sectionInspector\">4. Server Inspector (Drag / Click)</div>");
        sb.AppendLine("        <div class=\"section-body\" id=\"sectionInspector\">");
        sb.AppendLine("          <div id=\"inspectorContent\">Click on a server to isolate its inbound (IN) and outbound (OUT) routes, or drag it to adjust its position.</div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </aside>");

        sb.AppendLine("    <main class=\"canvas-area\">");
        sb.AppendLine("      <svg id=\"topologySvg\">");
        sb.AppendLine("        <defs>");
        sb.AppendLine("          <marker id=\"arrowOut\" viewBox=\"0 0 12 12\" refX=\"10.5\" refY=\"6\" markerWidth=\"8\" markerHeight=\"8\" orient=\"auto\">");
        sb.AppendLine("            <path d=\"M 0 1.5 L 11 6 L 0 10.5 L 2.6 6 z\" fill=\"#38bdf8\" />");
        sb.AppendLine("          </marker>");
        sb.AppendLine("          <marker id=\"arrowIn\" viewBox=\"0 0 12 12\" refX=\"10.5\" refY=\"6\" markerWidth=\"8\" markerHeight=\"8\" orient=\"auto\">");
        sb.AppendLine("            <path d=\"M 0 1.5 L 11 6 L 0 10.5 L 2.6 6 z\" fill=\"#10b981\" />");
        sb.AppendLine("          </marker>");
        sb.AppendLine("          <marker id=\"arrowHighlight\" viewBox=\"0 0 12 12\" refX=\"10.5\" refY=\"6\" markerWidth=\"9\" markerHeight=\"9\" orient=\"auto\">");
        sb.AppendLine("            <path d=\"M 0 1 L 11.5 6 L 0 11 L 2.8 6 z\" fill=\"#f59e0b\" />");
        sb.AppendLine("          </marker>");
        sb.AppendLine("          <marker id=\"originOut\" viewBox=\"0 0 8 8\" refX=\"4\" refY=\"4\" markerWidth=\"5\" markerHeight=\"5\">");
        sb.AppendLine("            <circle cx=\"4\" cy=\"4\" r=\"3\" fill=\"#38bdf8\" />");
        sb.AppendLine("          </marker>");
        sb.AppendLine("          <marker id=\"originIn\" viewBox=\"0 0 8 8\" refX=\"4\" refY=\"4\" markerWidth=\"5\" markerHeight=\"5\">");
        sb.AppendLine("            <circle cx=\"4\" cy=\"4\" r=\"3\" fill=\"#10b981\" />");
        sb.AppendLine("          </marker>");
        sb.AppendLine("        </defs>");
        sb.AppendLine("        <g id=\"viewportGroup\">");
        sb.AppendLine("          <g id=\"edgesLayer\"></g>");
        sb.AppendLine("          <g id=\"labelsLayer\"></g>");
        sb.AppendLine("          <g id=\"nodesLayer\"></g>");
        sb.AppendLine("        </g>");
        sb.AppendLine("      </svg>");
        sb.AppendLine("      <div class=\"legend\">");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"dot\" style=\"background:var(--accent-local)\"></span>Audited Server</div>");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"dot\" style=\"background:var(--accent-remote)\"></span>Remote Server</div>");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"arrow-sample\" style=\"background:var(--accent-out)\"></span>➔ Outbound Flow (OUT)</div>");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"arrow-sample\" style=\"background:var(--accent-in)\"></span>➔ Inbound Flow (IN)</div>");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"arrow-sample\" style=\"background:var(--accent-highlight)\"></span>➔ Highlighted Flow</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"pagination-bar\">");
        sb.AppendLine("        <button id=\"btnPrevPage\" type=\"button\">◀ Prev</button>");
        sb.AppendLine("        <span id=\"pageStatus\" class=\"page-status\">Page 1 / 1</span>");
        sb.AppendLine("        <button id=\"btnNextPage\" type=\"button\">Next ▶</button>");
        sb.AppendLine("        <button id=\"btnLoadMore100\" type=\"button\">+100 Servers</button>");
        sb.AppendLine("        <button id=\"btnShowAll\" type=\"button\">Show All</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </main>");
        sb.AppendLine("  </div>");
    }

    private static void AppendEmbeddedScript(StringBuilder sb, string payloadJson)
    {
        sb.AppendLine("  <script>");
        sb.Append("    const TOPOLOGY_DATA = ").Append(payloadJson).AppendLine(";");
        sb.AppendLine(
            """
                const svg = document.getElementById('topologySvg');
                const SVG_NS = svg.namespaceURI;
                const viewport = document.getElementById('viewportGroup');
                const edgesLayer = document.getElementById('edgesLayer');
                const labelsLayer = document.getElementById('labelsLayer');
                const nodesLayer = document.getElementById('nodesLayer');

                const layoutSelect = document.getElementById('layoutSelect');
                const sortSelect = document.getElementById('sortSelect');
                const routingSelect = document.getElementById('routingSelect');
                const spacingSelect = document.getElementById('spacingSelect');
                const nodeGapRange = document.getElementById('nodeGapRange');
                const edgeFanRange = document.getElementById('edgeFanRange');
                const nodeGapVal = document.getElementById('nodeGapVal');
                const edgeFanVal = document.getElementById('edgeFanVal');

                const searchInput = document.getElementById('searchInput');
                const includePortsInput = document.getElementById('includePortsInput');
                const excludePortsInput = document.getElementById('excludePortsInput');
                const excludeHostsInput = document.getElementById('excludeHostsInput');
                const directionSelect = document.getElementById('directionSelect');
                const btnApplyFilters = document.getElementById('btnApplyFilters');
                const inspectorContent = document.getElementById('inspectorContent');

                const btnPrevPage = document.getElementById('btnPrevPage');
                const btnNextPage = document.getElementById('btnNextPage');
                const btnLoadMore100 = document.getElementById('btnLoadMore100');
                const btnShowAll = document.getElementById('btnShowAll');
                const pageStatus = document.getElementById('pageStatus');

                const NODE_W = 236, NODE_H = 106;
                let panX = 50, panY = 50, scale = 1.0;
                let isPanning = false, startPanX = 0, startPanY = 0;
                let draggingNodeId = null, dragOffsetX = 0, dragOffsetY = 0, hasDragged = false;
                let selectedNodeId = null;
                let nodePositions = new Map();
                let currentPageIndex = 0;
                let currentPageSize = TOPOLOGY_DATA.defaultPageSize || 100;

                if (TOPOLOGY_DATA.defaultSort) sortSelect.value = TOPOLOGY_DATA.defaultSort;
                if (TOPOLOGY_DATA.defaultRouting) routingSelect.value = TOPOLOGY_DATA.defaultRouting;
                if (TOPOLOGY_DATA.defaultSpacing) spacingSelect.value = TOPOLOGY_DATA.defaultSpacing;

                function applySpacingPreset(profile) {
                  if (profile === 'compact') {
                    nodeGapRange.value = '0.75';
                    edgeFanRange.value = '10';
                  } else if (profile === 'spacious') {
                    nodeGapRange.value = '1.65';
                    edgeFanRange.value = '30';
                  } else {
                    nodeGapRange.value = '1.05';
                    edgeFanRange.value = '18';
                  }
                  nodeGapVal.textContent = `${parseFloat(nodeGapRange.value).toFixed(2)}x`;
                  edgeFanVal.textContent = `${edgeFanRange.value}px`;
                }

                applySpacingPreset(spacingSelect.value);

                function parsePortList(str) {
                  if (!str || !str.trim()) return [];
                  return str.split(/[,;\s]+/).map(s => parseInt(s.trim(), 10)).filter(n => !isNaN(n) && n > 0);
                }

                function parseTextTokens(str) {
                  if (!str || !str.trim()) return [];
                  return str.split(/[,;]+/).map(s => s.trim().toLowerCase()).filter(Boolean);
                }

                function ipToSortableNumber(ip) {
                  if (!ip) return Number.MAX_SAFE_INTEGER;
                  const parts = ip.trim().split('.');
                  if (parts.length !== 4) return Number.MAX_SAFE_INTEGER - 1;
                  return parts.reduce((acc, p) => (acc * 256) + (parseInt(p, 10) || 0), 0);
                }

                function updateTransform() {
                  viewport.setAttribute('transform', `translate(${panX}, ${panY}) scale(${scale})`);
                }

                function screenToWorld(clientX, clientY) {
                  const rect = svg.getBoundingClientRect();
                  return {
                    x: (clientX - rect.left - panX) / scale,
                    y: (clientY - rect.top - panY) / scale
                  };
                }

                svg.addEventListener('wheel', (e) => {
                  e.preventDefault();
                  const factor = e.deltaY < 0 ? 1.12 : 0.89;
                  scale = Math.max(0.15, Math.min(3.2, scale * factor));
                  updateTransform();
                });

                svg.addEventListener('mousedown', (e) => {
                  if (e.target === svg || e.target.parentNode === edgesLayer) {
                    isPanning = true;
                    startPanX = e.clientX - panX;
                    startPanY = e.clientY - panY;
                  }
                });

                window.addEventListener('mousemove', (e) => {
                  if (draggingNodeId) {
                    hasDragged = true;
                    const pt = screenToWorld(e.clientX, e.clientY);
                    nodePositions.set(draggingNodeId, { x: pt.x - dragOffsetX, y: pt.y - dragOffsetY });
                    renderGraph(false);
                    return;
                  }
                  if (isPanning) {
                    panX = e.clientX - startPanX;
                    panY = e.clientY - startPanY;
                    updateTransform();
                  }
                });

                window.addEventListener('mouseup', () => {
                  isPanning = false;
                  draggingNodeId = null;
                });

                function rankNodesByActivity(nodes, edges) {
                  const obsByNode = new Map();
                  edges.forEach(e => {
                    const obs = e.observationCount || 1;
                    obsByNode.set(e.sourceNodeId, (obsByNode.get(e.sourceNodeId) || 0) + obs);
                    obsByNode.set(e.targetNodeId, (obsByNode.get(e.targetNodeId) || 0) + obs);
                  });

                  const degreeOf = (n) => (n.inboundConnectionCount || 0) + (n.outboundConnectionCount || 0) + (n.ports ? n.ports.length : 0);

                  return [...nodes].sort((a, b) => {
                    if (a.isLocalAuditedServer !== b.isLocalAuditedServer) {
                      return a.isLocalAuditedServer ? -1 : 1;
                    }
                    const degDiff = degreeOf(b) - degreeOf(a);
                    if (degDiff !== 0) return degDiff;
                    const obsDiff = (obsByNode.get(b.nodeId) || 0) - (obsByNode.get(a.nodeId) || 0);
                    if (obsDiff !== 0) return obsDiff;
                    return a.hostName.localeCompare(b.hostName);
                  });
                }

                // Pass 2: Multi-pass alternating Barycenter & Weighted Median sweep
                function sortNodes(nodes, edges, sortMethod) {
                  const copy = [...nodes];
                  const degreeOf = (n) => (n.inboundConnectionCount || 0) + (n.outboundConnectionCount || 0);

                  if (sortMethod === 'alpha') {
                    return copy.sort((a, b) => a.hostName.localeCompare(b.hostName));
                  }
                  if (sortMethod === 'ip') {
                    return copy.sort((a, b) => ipToSortableNumber(a.ipAddress) - ipToSortableNumber(b.ipAddress) || a.hostName.localeCompare(b.hostName));
                  }
                  if (sortMethod === 'degree') {
                    return rankNodesByActivity(copy, edges);
                  }

                  const locals = copy.filter(n => n.isLocalAuditedServer).sort((a, b) => degreeOf(b) - degreeOf(a) || a.hostName.localeCompare(b.hostName));
                  const remotes = copy.filter(n => !n.isLocalAuditedServer).sort((a, b) => degreeOf(b) - degreeOf(a) || a.hostName.localeCompare(b.hostName));

                  const adjacency = new Map();
                  nodes.forEach(n => adjacency.set(n.nodeId, []));
                  edges.forEach(e => {
                    if (adjacency.has(e.sourceNodeId)) adjacency.get(e.sourceNodeId).push(e.targetNodeId);
                    if (adjacency.has(e.targetNodeId)) adjacency.get(e.targetNodeId).push(e.sourceNodeId);
                  });

                  const computeScore = (nodeId, rankMap, useMedian) => {
                    const neighbors = (adjacency.get(nodeId) || []).filter(id => rankMap.has(id)).map(id => rankMap.get(id)).sort((x, y) => x - y);
                    if (neighbors.length === 0) return 9999;
                    if (useMedian) {
                      const mid = Math.floor(neighbors.length / 2);
                      return neighbors.length % 2 === 1 ? neighbors[mid] : (neighbors[mid - 1] + neighbors[mid]) / 2;
                    }
                    return neighbors.reduce((s, v) => s + v, 0) / neighbors.length;
                  };

                  for (let pass = 0; pass < 8; pass++) {
                    const useMedian = (pass % 2 === 1);
                    const localRank = new Map();
                    locals.forEach((n, idx) => localRank.set(n.nodeId, idx));

                    remotes.sort((a, b) => {
                      const sa = computeScore(a.nodeId, localRank, useMedian);
                      const sb = computeScore(b.nodeId, localRank, useMedian);
                      if (Math.abs(sa - sb) > 0.001) return sa - sb;
                      return degreeOf(b) - degreeOf(a) || a.hostName.localeCompare(b.hostName);
                    });

                    const remoteRank = new Map();
                    remotes.forEach((n, idx) => remoteRank.set(n.nodeId, idx));

                    locals.sort((a, b) => {
                      const sa = computeScore(a.nodeId, remoteRank, useMedian);
                      const sb = computeScore(b.nodeId, remoteRank, useMedian);
                      if (Math.abs(sa - sb) > 0.001) return sa - sb;
                      return degreeOf(b) - degreeOf(a) || a.hostName.localeCompare(b.hostName);
                    });
                  }

                  return [...locals, ...remotes];
                }

                function segmentsIntersect(ax, ay, bx, by, cx, cy, dx, dy) {
                  const ccw = (p1x, p1y, p2x, p2y, p3x, p3y) => (p3y - p1y) * (p2x - p1x) > (p2y - p1y) * (p3x - p1x);
                  return (ccw(ax, ay, cx, cy, dx, dy) !== ccw(bx, by, cx, cy, dx, dy)) &&
                         (ccw(ax, ay, bx, by, cx, cy) !== ccw(ax, ay, bx, by, dx, dy));
                }

                function segmentIntersectsBox(x1, y1, x2, y2, boxPos, pad) {
                  const bx1 = boxPos.x - pad, by1 = boxPos.y - pad;
                  const bx2 = boxPos.x + NODE_W + pad, by2 = boxPos.y + NODE_H + pad;
                  if (Math.max(x1, x2) < bx1 || Math.min(x1, x2) > bx2 || Math.max(y1, y2) < by1 || Math.min(y1, y2) > by2) {
                    return false;
                  }
                  const cx = boxPos.x + NODE_W / 2, cy = boxPos.y + NODE_H / 2;
                  const lenSq = Math.pow(x2 - x1, 2) + Math.pow(y2 - y1, 2) || 1;
                  const t = Math.max(0, Math.min(1, ((cx - x1) * (x2 - x1) + (cy - y1) * (y2 - y1)) / lenSq));
                  const px = x1 + t * (x2 - x1), py = y1 + t * (y2 - y1);
                  return px >= bx1 && px <= bx2 && py >= by1 && py <= by2;
                }

                function computeLayoutCost(nodes, edges) {
                  let crossings = 0;
                  let boxCollisions = 0;
                  let lengthPenalty = 0;

                  const uniquePairs = [];
                  const seenPairs = new Set();
                  edges.forEach(e => {
                    const key = [e.sourceNodeId, e.targetNodeId].sort().join('::');
                    if (!seenPairs.has(key) && e.sourceNodeId !== e.targetNodeId) {
                      seenPairs.add(key);
                      uniquePairs.push({ u: e.sourceNodeId, v: e.targetNodeId });
                    }
                  });

                  const segs = [];
                  for (let i = 0; i < uniquePairs.length; i++) {
                    const pair = uniquePairs[i];
                    const p1 = nodePositions.get(pair.u);
                    const p2 = nodePositions.get(pair.v);
                    if (!p1 || !p2) continue;
                    const x1 = p1.x + NODE_W / 2, y1 = p1.y + NODE_H / 2;
                    const x2 = p2.x + NODE_W / 2, y2 = p2.y + NODE_H / 2;
                    segs.push({ u: pair.u, v: pair.v, x1, y1, x2, y2 });
                    lengthPenalty += Math.hypot(x2 - x1, y2 - y1) * 0.006;

                    for (let k = 0; k < nodes.length; k++) {
                      const n = nodes[k];
                      if (n.nodeId === pair.u || n.nodeId === pair.v) continue;
                      const nPos = nodePositions.get(n.nodeId);
                      if (nPos && segmentIntersectsBox(x1, y1, x2, y2, nPos, 12)) {
                        boxCollisions++;
                      }
                    }
                  }

                  for (let i = 0; i < segs.length; i++) {
                    for (let j = i + 1; j < segs.length; j++) {
                      const s1 = segs[i], s2 = segs[j];
                      if (s1.u === s2.u || s1.u === s2.v || s1.v === s2.u || s1.v === s2.v) continue;
                      if (segmentsIntersect(s1.x1, s1.y1, s1.x2, s1.y2, s2.x1, s2.y1, s2.x2, s2.y2)) {
                        crossings++;
                      }
                    }
                  }

                  return boxCollisions * 120 + crossings * 45 + lengthPenalty;
                }

                // Pass 3: Exact 2-Opt Swap Optimizer guided by geometric crossing & occlusion cost
                function optimizeNodePositions2Opt(nodes, edges) {
                  if (nodes.length <= 2 || edges.length === 0) return;

                  const swapGroups = [
                    nodes.filter(n => !n.isLocalAuditedServer),
                    nodes.filter(n => n.isLocalAuditedServer)
                  ];

                  let currentCost = computeLayoutCost(nodes, edges);
                  const maxPasses = nodes.length <= 45 ? 18 : 8;

                  for (let pass = 0; pass < maxPasses; pass++) {
                    let improved = false;
                    for (let g = 0; g < swapGroups.length; g++) {
                      const group = swapGroups[g];
                      if (group.length <= 1) continue;
                      for (let i = 0; i < group.length - 1; i++) {
                        for (let j = i + 1; j < group.length; j++) {
                          const idA = group[i].nodeId, idB = group[j].nodeId;
                          const posA = nodePositions.get(idA), posB = nodePositions.get(idB);
                          if (!posA || !posB) continue;

                          nodePositions.set(idA, posB);
                          nodePositions.set(idB, posA);
                          const candidateCost = computeLayoutCost(nodes, edges);

                          if (candidateCost < currentCost - 0.05) {
                            currentCost = candidateCost;
                            improved = true;
                          } else {
                            nodePositions.set(idA, posA);
                            nodePositions.set(idB, posB);
                          }
                        }
                      }
                    }
                    if (!improved) break;
                  }
                }

                // Pass 4: Force repulsion & edge-corridor clearance polish
                function applyForceRepulsionPolish(nodes, edges, gapFactor) {
                  if (nodes.length <= 1) return;
                  const minSepX = Math.round((NODE_W + 48) * Math.max(0.85, gapFactor * 0.9));
                  const minSepY = Math.round((NODE_H + 40) * Math.max(0.85, gapFactor * 0.9));

                  for (let iter = 0; iter < 6; iter++) {
                    // Push third-party boxes out of edge sightlines
                    edges.forEach(e => {
                      const p1 = nodePositions.get(e.sourceNodeId);
                      const p2 = nodePositions.get(e.targetNodeId);
                      if (!p1 || !p2) return;
                      const x1 = p1.x + NODE_W / 2, y1 = p1.y + NODE_H / 2;
                      const x2 = p2.x + NODE_W / 2, y2 = p2.y + NODE_H / 2;
                      const dx = x2 - x1, dy = y2 - y1;
                      const lenSq = dx * dx + dy * dy;
                      if (lenSq < 1) return;

                      nodes.forEach(n => {
                        if (n.nodeId === e.sourceNodeId || n.nodeId === e.targetNodeId || n.isLocalAuditedServer) return;
                        const pos = nodePositions.get(n.nodeId);
                        if (!pos) return;
                        const cx = pos.x + NODE_W / 2, cy = pos.y + NODE_H / 2;
                        const t = ((cx - x1) * dx + (cy - y1) * dy) / lenSq;
                        if (t <= 0.08 || t >= 0.92) return;
                        const projX = x1 + t * dx, projY = y1 + t * dy;
                        const distX = cx - projX, distY = cy - projY;
                        if (Math.abs(distX) < NODE_W * 0.62 && Math.abs(distY) < NODE_H * 0.72) {
                          const len = Math.hypot(dx, dy) || 1;
                          const nx = -dy / len, ny = dx / len;
                          const side = (distX * nx + distY * ny) >= 0 ? 1 : -1;
                          pos.x = Math.round(pos.x + nx * side * 24);
                          pos.y = Math.round(pos.y + ny * side * 24);
                        }
                      });
                    });

                    // Enforce box-to-box non-overlapping clearance
                    for (let i = 0; i < nodes.length; i++) {
                      for (let j = i + 1; j < nodes.length; j++) {
                        const pA = nodePositions.get(nodes[i].nodeId);
                        const pB = nodePositions.get(nodes[j].nodeId);
                        if (!pA || !pB) continue;
                        const dx = (pB.x + NODE_W / 2) - (pA.x + NODE_W / 2);
                        const dy = (pB.y + NODE_H / 2) - (pA.y + NODE_H / 2);
                        if (Math.abs(dx) < minSepX && Math.abs(dy) < minSepY) {
                          const overlapX = minSepX - Math.abs(dx);
                          const overlapY = minSepY - Math.abs(dy);
                          if (overlapX < overlapY) {
                            const pushX = (overlapX / 2 + 2) * (dx >= 0 ? 1 : -1);
                            if (!nodes[i].isLocalAuditedServer) pA.x = Math.round(pA.x - pushX);
                            if (!nodes[j].isLocalAuditedServer) pB.x = Math.round(pB.x + pushX);
                          } else {
                            const pushY = (overlapY / 2 + 2) * (dy >= 0 ? 1 : -1);
                            if (!nodes[i].isLocalAuditedServer) pA.y = Math.round(pA.y - pushY);
                            if (!nodes[j].isLocalAuditedServer) pB.y = Math.round(pB.y + pushY);
                          }
                        }
                      }
                    }
                  }
                }

                // Pass 1 + Pipeline Orchestrator
                function computeLayout(nodes, edges, layoutMode, sortMethod) {
                  nodePositions.clear();
                  const ordered = sortNodes(nodes, edges, sortMethod);
                  const gapFactor = parseFloat(nodeGapRange.value) || 1.0;
                  const stepX = Math.round(350 * gapFactor);
                  const stepY = Math.round(162 * gapFactor);

                  if (layoutMode === 'circular') {
                    const width = Math.max(960, svg.clientWidth || 1200);
                    const height = Math.max(680, svg.clientHeight || 800);
                    const cx = width / 2, cy = height / 2;
                    const radius = Math.max(250, ordered.length * 36 * gapFactor);
                    ordered.forEach((n, idx) => {
                      const angle = (2 * Math.PI * idx) / Math.max(1, ordered.length);
                      nodePositions.set(n.nodeId, {
                        x: Math.round(cx + radius * Math.cos(angle) - NODE_W / 2),
                        y: Math.round(cy + radius * Math.sin(angle) - NODE_H / 2)
                      });
                    });
                    optimizeNodePositions2Opt(ordered, edges);
                    return;
                  }

                  if (layoutMode === 'grid') {
                    const cols = Math.max(2, Math.ceil(Math.sqrt(ordered.length)));
                    ordered.forEach((n, idx) => {
                      const col = idx % cols;
                      const row = Math.floor(idx / cols);
                      const staggerY = (col % 2) * Math.round(stepY * 0.42);
                      nodePositions.set(n.nodeId, { x: 60 + col * stepX, y: 60 + row * stepY + staggerY });
                    });
                    optimizeNodePositions2Opt(ordered, edges);
                    applyForceRepulsionPolish(ordered, edges, gapFactor);
                    return;
                  }

                  const locals = ordered.filter(n => n.isLocalAuditedServer);
                  const remotes = ordered.filter(n => !n.isLocalAuditedServer);

                  // Pass 1A: 360-degree Concentric Sector Rings around Audited Hub(s) to use all 4 borders without column occlusion
                  if (locals.length >= 1 && locals.length <= 3 && remotes.length > 0) {
                    const width = Math.max(1100, svg.clientWidth || 1280);
                    const height = Math.max(740, svg.clientHeight || 840);
                    const hubCx = Math.round(width * 0.48);
                    const hubCy = Math.round(height * 0.48);

                    locals.forEach((n, idx) => {
                      const offsetY = Math.round((idx - (locals.length - 1) / 2) * (NODE_H + 56) * gapFactor);
                      nodePositions.set(n.nodeId, {
                        x: Math.round(hubCx - NODE_W / 2),
                        y: Math.round(hubCy + offsetY - NODE_H / 2)
                      });
                    });

                    let placed = 0;
                    let ringIdx = 0;
                    while (placed < remotes.length) {
                      const ringCapacity = 10 + ringIdx * 6;
                      const slice = remotes.slice(placed, placed + ringCapacity);
                      const rx = (390 + ringIdx * 330) * gapFactor;
                      const ry = (255 + ringIdx * 210) * gapFactor;
                      const angleOffset = (ringIdx % 2) * (Math.PI / Math.max(1, slice.length));

                      slice.forEach((n, idx) => {
                        const angle = angleOffset + (2 * Math.PI * idx) / Math.max(1, slice.length);
                        nodePositions.set(n.nodeId, {
                          x: Math.round(hubCx + rx * Math.cos(angle) - NODE_W / 2),
                          y: Math.round(hubCy + ry * Math.sin(angle) - NODE_H / 2)
                        });
                      });

                      placed += slice.length;
                      ringIdx++;
                    }

                    optimizeNodePositions2Opt(ordered, edges);
                    applyForceRepulsionPolish(ordered, edges, gapFactor);
                    return;
                  }

                  // Pass 1B: Centered & Staggered Multi-Layer Bipartite Layout
                  const maxPerColumn = Math.max(6, Math.min(16, Math.ceil(Math.sqrt(remotes.length) * 1.7)));
                  const remoteRows = Math.min(maxPerColumn, Math.max(1, remotes.length));
                  const totalRemoteHeight = (remoteRows - 1) * stepY;

                  const localRows = Math.min(maxPerColumn, Math.max(1, locals.length));
                  const totalLocalHeight = (localRows - 1) * stepY;
                  const localStartY = 60 + Math.max(0, Math.round((totalRemoteHeight - totalLocalHeight) / 2));

                  locals.forEach((n, idx) => {
                    const col = Math.floor(idx / maxPerColumn);
                    const row = idx % maxPerColumn;
                    nodePositions.set(n.nodeId, { x: 60 + col * stepX, y: localStartY + row * stepY });
                  });

                  const localCols = Math.max(1, Math.ceil(locals.length / maxPerColumn));
                  const remoteStartX = 60 + localCols * (stepX + 80);

                  remotes.forEach((n, idx) => {
                    const col = Math.floor(idx / maxPerColumn);
                    const row = idx % maxPerColumn;
                    const staggerY = (col % 2) * Math.round(stepY * 0.48);
                    nodePositions.set(n.nodeId, { x: remoteStartX + col * stepX, y: 60 + row * stepY + staggerY });
                  });

                  optimizeNodePositions2Opt(ordered, edges);
                  applyForceRepulsionPolish(ordered, edges, gapFactor);
                }

                // Pass 5A: Full-Border Angular Port Distribution (sorts & spaces anchor dots evenly per side)
                function determineFacingSide(fromPos, toPos) {
                  const dx = (toPos.x + NODE_W / 2) - (fromPos.x + NODE_W / 2);
                  const dy = (toPos.y + NODE_H / 2) - (fromPos.y + NODE_H / 2);
                  const halfW = NODE_W / 2 + 2;
                  const halfH = NODE_H / 2 + 2;
                  const scaleX = Math.abs(dx) > 0.001 ? halfW / Math.abs(dx) : 999999;
                  const scaleY = Math.abs(dy) > 0.001 ? halfH / Math.abs(dy) : 999999;
                  if (scaleX < scaleY) {
                    return dx >= 0 ? 'right' : 'left';
                  }
                  return dy >= 0 ? 'bottom' : 'top';
                }

                function computeAllEdgeAnchors(nodes, edges) {
                  const sideBuckets = new Map();
                  const addSlotRequest = (nodeId, side, edgeId, isTarget, otherPos) => {
                    const key = `${nodeId}::${side}`;
                    if (!sideBuckets.has(key)) sideBuckets.set(key, []);
                    sideBuckets.get(key).push({
                      edgeId,
                      isTarget,
                      otherCx: otherPos.x + NODE_W / 2,
                      otherCy: otherPos.y + NODE_H / 2
                    });
                  };

                  edges.forEach(e => {
                    const p1 = nodePositions.get(e.sourceNodeId);
                    const p2 = nodePositions.get(e.targetNodeId);
                    if (!p1 || !p2) return;
                    const srcSide = determineFacingSide(p1, p2);
                    const dstSide = determineFacingSide(p2, p1);
                    addSlotRequest(e.sourceNodeId, srcSide, e.edgeId, false, p2);
                    addSlotRequest(e.targetNodeId, dstSide, e.edgeId, true, p1);
                  });

                  const anchorResults = new Map();
                  sideBuckets.forEach((items, key) => {
                    const [nodeId, side] = key.split('::');
                    const basePos = nodePositions.get(nodeId);
                    if (!basePos) return;

                    // Sort strictly along border axis so exiting/entering arrows never cross at the box border
                    if (side === 'left' || side === 'right') {
                      items.sort((a, b) => (a.otherCy - b.otherCy) || (a.otherCx - b.otherCx) || a.edgeId.localeCompare(b.edgeId));
                    } else {
                      items.sort((a, b) => (a.otherCx - b.otherCx) || (a.otherCy - b.otherCy) || a.edgeId.localeCompare(b.edgeId));
                    }

                    const count = items.length;
                    items.forEach((item, idx) => {
                      let ax = basePos.x + NODE_W / 2;
                      let ay = basePos.y + NODE_H / 2;
                      if (side === 'left' || side === 'right') {
                        const marginY = 14;
                        const usableH = Math.max(12, NODE_H - marginY * 2);
                        const step = usableH / (count + 1);
                        ax = side === 'right' ? basePos.x + NODE_W + 2 : basePos.x - 2;
                        ay = basePos.y + marginY + step * (idx + 1);
                      } else {
                        const marginX = 18;
                        const usableW = Math.max(20, NODE_W - marginX * 2);
                        const step = usableW / (count + 1);
                        ax = basePos.x + marginX + step * (idx + 1);
                        ay = side === 'bottom' ? basePos.y + NODE_H + 2 : basePos.y - 2;
                      }
                      const endpointKey = `${item.edgeId}::${item.isTarget ? 'dst' : 'src'}`;
                      anchorResults.set(endpointKey, { x: Math.round(ax * 10) / 10, y: Math.round(ay * 10) / 10, side });
                    });
                  });

                  return anchorResults;
                }

                // Pass 5B: Multi-obstacle corridor waypoint finder
                function findCorridorWaypoint(p1, p2, sourceId, targetId, allNodes, fanOffset) {
                  const isPathBlocked = (ax, ay, bx, by) => {
                    for (let i = 0; i < allNodes.length; i++) {
                      const n = allNodes[i];
                      if (n.nodeId === sourceId || n.nodeId === targetId) continue;
                      const pos = nodePositions.get(n.nodeId);
                      if (pos && segmentIntersectsBox(ax, ay, bx, by, pos, 10)) {
                        return true;
                      }
                    }
                    return false;
                  };

                  if (!isPathBlocked(p1.x, p1.y, p2.x, p2.y)) {
                    return null;
                  }

                  const mx = (p1.x + p2.x) / 2;
                  const my = (p1.y + p2.y) / 2;
                  const dx = p2.x - p1.x, dy = p2.y - p1.y;
                  const len = Math.hypot(dx, dy) || 1;
                  const nx = -dy / len, ny = dx / len;

                  const candidateOffsets = [95, -95, 165, -165, 240, -240, 320, -320];
                  for (let i = 0; i < candidateOffsets.length; i++) {
                    const offset = candidateOffsets[i] + fanOffset;
                    const wx = mx + nx * offset;
                    const wy = my + ny * offset;
                    if (!isPathBlocked(p1.x, p1.y, wx, wy) && !isPathBlocked(wx, wy, p2.x, p2.y)) {
                      return { x: wx, y: wy };
                    }
                  }

                  return { x: mx + nx * (150 + fanOffset), y: my + ny * (150 + fanOffset) };
                }

                function sideNormalVector(side) {
                  if (side === 'right') return { nx: 1, ny: 0 };
                  if (side === 'left') return { nx: -1, ny: 0 };
                  if (side === 'bottom') return { nx: 0, ny: 1 };
                  return { nx: 0, ny: -1 };
                }

                function buildRoutedEdgePath(startAnchor, endAnchor, routingMode, fanOffset, waypoint) {
                  const x1 = startAnchor.x, y1 = startAnchor.y;
                  const x2 = endAnchor.x, y2 = endAnchor.y;
                  const dx = x2 - x1, dy = y2 - y1;
                  const dist = Math.hypot(dx, dy) || 1;
                  const perpX = -dy / dist, perpY = dx / dist;

                  if (routingMode === 'straight') {
                    const mx = waypoint ? waypoint.x : (x1 + x2) / 2 + perpX * fanOffset;
                    const my = waypoint ? waypoint.y : (y1 + y2) / 2 + perpY * fanOffset;
                    if (!waypoint && Math.abs(fanOffset) < 1) {
                      return { d: `M ${x1} ${y1} L ${x2} ${y2}`, labelX: mx, labelY: my - 6 };
                    }
                    return { d: `M ${x1} ${y1} Q ${mx} ${my} ${x2} ${y2}`, labelX: mx, labelY: my - 6 };
                  }

                  if (routingMode === 'orthogonal') {
                    if (waypoint) {
                      return {
                        d: `M ${x1} ${y1} L ${waypoint.x} ${y1} L ${waypoint.x} ${y2} L ${x2} ${y2}`,
                        labelX: waypoint.x,
                        labelY: (y1 + y2) / 2 - 6
                      };
                    }
                    const midX = (x1 + x2) / 2 + fanOffset * 1.4;
                    return {
                      d: `M ${x1} ${y1} L ${midX} ${y1} L ${midX} ${y2} L ${x2} ${y2}`,
                      labelX: midX,
                      labelY: (y1 + y2) / 2 - 6
                    };
                  }

                  if (waypoint) {
                    const bx = 0.25 * x1 + 0.5 * waypoint.x + 0.25 * x2;
                    const by = 0.25 * y1 + 0.5 * waypoint.y + 0.25 * y2;
                    return {
                      d: `M ${x1} ${y1} Q ${waypoint.x} ${waypoint.y} ${x2} ${y2}`,
                      labelX: bx,
                      labelY: by - 6
                    };
                  }

                  const n1 = sideNormalVector(startAnchor.side);
                  const n2 = sideNormalVector(endAnchor.side);
                  const tension = Math.max(48, Math.min(180, dist * 0.36));

                  const cx1 = x1 + n1.nx * tension + perpX * fanOffset;
                  const cy1 = y1 + n1.ny * tension + perpY * fanOffset;
                  const cx2 = x2 + n2.nx * tension + perpX * fanOffset;
                  const cy2 = y2 + n2.ny * tension + perpY * fanOffset;

                  const bx = 0.125 * x1 + 0.375 * cx1 + 0.375 * cx2 + 0.125 * x2;
                  const by = 0.125 * y1 + 0.375 * cy1 + 0.375 * cy2 + 0.125 * y2;

                  return {
                    d: `M ${x1} ${y1} C ${cx1} ${cy1}, ${cx2} ${cy2}, ${x2} ${y2}`,
                    labelX: bx,
                    labelY: by - 6
                  };
                }

                function filterAndPaginateGraph() {
                  const q = (searchInput.value || '').trim().toLowerCase();
                  const incPorts = parsePortList(includePortsInput.value);
                  const excPorts = parsePortList(excludePortsInput.value);
                  const excHosts = parseTextTokens(excludeHostsInput.value);
                  const dirFilter = directionSelect.value;

                  const filteredEdges = TOPOLOGY_DATA.edges.filter(e => {
                    if (dirFilter !== 'all' && e.direction !== dirFilter) return false;
                    if (incPorts.length > 0 && !incPorts.includes(e.targetPort)) return false;
                    if (excPorts.includes(e.targetPort)) return false;
                    return true;
                  });

                  const filteredNodes = TOPOLOGY_DATA.nodes.filter(n => {
                    const hostStr = `${n.hostName} ${n.ipAddress}`.toLowerCase();
                    if (excHosts.some(tok => hostStr.includes(tok))) return false;
                    if (q) {
                      const portMatch = n.ports.some(p => String(p.port).includes(q) || (p.processName || '').toLowerCase().includes(q));
                      if (!hostStr.includes(q) && !portMatch) return false;
                    }
                    const remainingPorts = n.ports.filter(p => !excPorts.includes(p.port) && (incPorts.length === 0 || incPorts.includes(p.port)));
                    const hasActiveEdge = filteredEdges.some(e => e.sourceNodeId === n.nodeId || e.targetNodeId === n.nodeId);

                    // When port or direction filters are applied, eliminate orphan servers with no remaining ports and no flows
                    if (incPorts.length > 0 || excPorts.length > 0 || dirFilter !== 'all') {
                      if (remainingPorts.length === 0 && !hasActiveEdge) return false;
                    }
                    return true;
                  });

                  // Rank servers so most active/audited servers appear on page 1
                  const rankedNodes = sortSelect.value === 'alpha' || sortSelect.value === 'ip'
                    ? sortNodes(filteredNodes, filteredEdges, sortSelect.value)
                    : rankNodesByActivity(filteredNodes, filteredEdges);

                  const totalFiltered = rankedNodes.length;
                  const totalPages = Math.max(1, Math.ceil(totalFiltered / currentPageSize));
                  if (currentPageIndex >= totalPages) currentPageIndex = totalPages - 1;
                  if (currentPageIndex < 0) currentPageIndex = 0;

                  const startIdx = currentPageIndex * currentPageSize;
                  const endIdx = Math.min(totalFiltered, startIdx + currentPageSize);
                  const pageSlice = rankedNodes.slice(startIdx, endIdx);
                  const pageNodeIds = new Set(pageSlice.map(n => n.nodeId));

                  // Always keep audited local servers connected to the visible page slice so no arrow is orphaned
                  filteredEdges.forEach(e => {
                    if (pageNodeIds.has(e.targetNodeId)) {
                      const src = filteredNodes.find(n => n.nodeId === e.sourceNodeId);
                      if (src && src.isLocalAuditedServer) pageNodeIds.add(src.nodeId);
                    }
                    if (pageNodeIds.has(e.sourceNodeId)) {
                      const dst = filteredNodes.find(n => n.nodeId === e.targetNodeId);
                      if (dst && dst.isLocalAuditedServer) pageNodeIds.add(dst.nodeId);
                    }
                  });

                  const pagedNodes = rankedNodes.filter(n => pageNodeIds.has(n.nodeId));
                  const activeEdges = filteredEdges.filter(e => pageNodeIds.has(e.sourceNodeId) && pageNodeIds.has(e.targetNodeId));

                  const displayStart = totalFiltered === 0 ? 0 : startIdx + 1;
                  pageStatus.textContent = `Page ${currentPageIndex + 1} / ${totalPages} (${displayStart}–${endIdx} of ${totalFiltered} servers)`;
                  const kpiNodesEl = document.getElementById('kpiNodes');
                  if (kpiNodesEl) kpiNodesEl.textContent = totalFiltered;
                  const kpiEdgesEl = document.getElementById('kpiEdges');
                  if (kpiEdgesEl) kpiEdgesEl.textContent = activeEdges.length;
                  btnPrevPage.disabled = currentPageIndex <= 0;
                  btnNextPage.disabled = currentPageIndex >= totalPages - 1;
                  btnLoadMore100.disabled = currentPageSize >= totalFiltered;
                  btnShowAll.disabled = currentPageSize >= totalFiltered && currentPageIndex === 0;

                  return { nodes: pagedNodes, edges: activeEdges, totalFiltered };
                }

                function renderInspector(node, edges, allNodesMap) {
                  if (!node) {
                    inspectorContent.innerHTML = 'Click on a server to isolate its inbound (IN) and outbound (OUT) routes, or drag it to adjust its position.';
                    return;
                  }
                  const visiblePorts = node.ports.filter(p => !excPorts.includes(p.port) && (incPorts.length === 0 || incPorts.includes(p.port)));
                  const portsHtml = visiblePorts.length > 0
                    ? visiblePorts.map(p => `<span class="port-pill">:${p.port}/${p.protocol}${p.processName ? ' (' + p.processName + ')' : ''}</span>`).join(' ')
                    : '<span style="color:var(--text-muted)">No active listening port</span>';

                  const outbound = edges.filter(e => e.sourceNodeId === node.nodeId);
                  const inbound = edges.filter(e => e.targetNodeId === node.nodeId);

                  const outHtml = outbound.map(e => {
                    const target = allNodesMap.get(e.targetNodeId);
                    return `<div><span style="color:var(--accent-out)">[OUT ➔]</span> <strong>${target ? target.hostName : e.targetNodeId}</strong> :${e.targetPort}/${e.protocol} (${e.processName || 'flow'})</div>`;
                  }).join('');

                  const inHtml = inbound.map(e => {
                    const source = allNodesMap.get(e.sourceNodeId);
                    return `<div><span style="color:var(--accent-in)">[⬅ IN]</span> <strong>${source ? source.hostName : e.sourceNodeId}</strong> to :${e.targetPort}/${e.protocol}</div>`;
                  }).join('');

                  inspectorContent.innerHTML = `
                    <div><strong>${node.hostName}</strong> (${node.ipAddress || 'IP N/A'})</div>
                    <div style="margin-top:4px;color:var(--text-muted)">Non-ephemeral service ports:</div>
                    <div>${portsHtml}</div>
                    <div style="margin-top:6px;color:var(--text-muted)">Outbound Flows OUT (${outbound.length}):</div>
                    <div>${outHtml || 'None'}</div>
                    <div style="margin-top:6px;color:var(--text-muted)">Inbound Flows IN (${inbound.length}):</div>
                    <div>${inHtml || 'None'}</div>
                  `;
                }

                function renderGraph(recomputePositions = false) {
                  const { nodes, edges } = filterAndPaginateGraph();
                  const nodesMap = new Map(nodes.map(n => [n.nodeId, n]));
                  document.getElementById('kpiNodes').textContent = String(nodes.length);
                  document.getElementById('kpiEdges').textContent = String(edges.length);

                  if (recomputePositions || nodePositions.size === 0) {
                    computeLayout(nodes, edges, layoutSelect.value, sortSelect.value);
                  }

                  const routingMode = routingSelect.value;
                  const fanSpacing = parseInt(edgeFanRange.value, 10) || 18;

                  const pairBuckets = new Map();
                  edges.forEach(e => {
                    const pairKey = [e.sourceNodeId, e.targetNodeId].sort().join('::');
                    if (!pairBuckets.has(pairKey)) pairBuckets.set(pairKey, []);
                    pairBuckets.get(pairKey).push(e);
                  });

                  const anchorMap = computeAllEdgeAnchors(nodes, edges);

                  const edgesFrag = document.createDocumentFragment();
                  const labelsFrag = document.createDocumentFragment();
                  const nodesFrag = document.createDocumentFragment();

                  edges.forEach(e => {
                    const p1 = nodePositions.get(e.sourceNodeId);
                    const p2 = nodePositions.get(e.targetNodeId);
                    if (!p1 || !p2) return;

                    const isHighlighted = selectedNodeId && (e.sourceNodeId === selectedNodeId || e.targetNodeId === selectedNodeId);
                    const isDimmed = selectedNodeId && !isHighlighted;

                    const pairKey = [e.sourceNodeId, e.targetNodeId].sort().join('::');
                    const siblings = pairBuckets.get(pairKey) || [e];
                    const siblingIdx = siblings.indexOf(e);
                    const fanOffset = (siblingIdx - (siblings.length - 1) / 2) * fanSpacing;

                    const startAnchor = anchorMap.get(`${e.edgeId}::src`) || { x: p1.x + NODE_W / 2, y: p1.y + NODE_H / 2, side: 'right' };
                    const endAnchor = anchorMap.get(`${e.edgeId}::dst`) || { x: p2.x + NODE_W / 2, y: p2.y + NODE_H / 2, side: 'left' };

                    const waypoint = findCorridorWaypoint(startAnchor, endAnchor, e.sourceNodeId, e.targetNodeId, nodes, fanOffset);
                    const route = buildRoutedEdgePath(startAnchor, endAnchor, routingMode, fanOffset, waypoint);

                    const isInbound = e.direction === 'IN';
                    const strokeColor = isHighlighted ? '#f59e0b' : (isInbound ? '#10b981' : '#38bdf8');
                    const markerEnd = isHighlighted ? 'url(#arrowHighlight)' : (isInbound ? 'url(#arrowIn)' : 'url(#arrowOut)');
                    const markerStart = isInbound ? 'url(#originIn)' : 'url(#originOut)';

                    const path = document.createElementNS(SVG_NS, 'path');
                    path.setAttribute('d', route.d);
                    path.setAttribute('fill', 'none');
                    path.setAttribute('stroke', strokeColor);
                    path.setAttribute('stroke-width', isHighlighted ? '2.6' : '1.75');
                    path.setAttribute('stroke-opacity', isDimmed ? '0.18' : '0.88');
                    path.setAttribute('marker-start', markerStart);
                    path.setAttribute('marker-end', markerEnd);
                    edgesFrag.appendChild(path);

                    if (!isDimmed && (edges.length <= 180 || isHighlighted)) {
                      const badgeText = `${isInbound ? 'IN' : 'OUT'} :${e.targetPort}/${e.protocol}`;
                      const label = document.createElementNS(SVG_NS, 'text');
                      label.setAttribute('x', String(Math.round(route.labelX)));
                      label.setAttribute('y', String(Math.round(route.labelY)));
                      label.setAttribute('text-anchor', 'middle');
                      label.setAttribute('fill', isHighlighted ? '#fbbf24' : (isInbound ? '#6ee7b7' : '#bae6fd'));
                      label.setAttribute('font-size', '10.5');
                      label.setAttribute('font-weight', isHighlighted ? 'bold' : 'normal');
                      label.textContent = badgeText;
                      labelsFrag.appendChild(label);
                    }
                  });

                  nodes.forEach(n => {
                    const pos = nodePositions.get(n.nodeId) || { x: 100, y: 100 };
                    const g = document.createElementNS(SVG_NS, 'g');
                    g.setAttribute('transform', `translate(${pos.x}, ${pos.y})`);
                    g.style.cursor = 'pointer';

                    const isSelected = n.nodeId === selectedNodeId;
                    const borderColor = isSelected ? '#f59e0b' : (n.isLocalAuditedServer ? '#38bdf8' : '#a78bfa');

                    const rect = document.createElementNS(SVG_NS, 'rect');
                    rect.setAttribute('width', String(NODE_W));
                    rect.setAttribute('height', String(NODE_H));
                    rect.setAttribute('rx', '8');
                    rect.setAttribute('fill', '#1e293b');
                    rect.setAttribute('stroke', borderColor);
                    rect.setAttribute('stroke-width', isSelected ? '2.8' : '1.8');
                    g.appendChild(rect);

                    const title = document.createElementNS(SVG_NS, 'text');
                    title.setAttribute('x', '12');
                    title.setAttribute('y', '23');
                    title.setAttribute('fill', '#f8fafc');
                    title.setAttribute('font-weight', 'bold');
                    title.setAttribute('font-size', '13');
                    title.textContent = n.hostName.length > 26 ? n.hostName.substring(0, 25) + '…' : n.hostName;
                    g.appendChild(title);

                    const sub = document.createElementNS(SVG_NS, 'text');
                    sub.setAttribute('x', '12');
                    sub.setAttribute('y', '41');
                    sub.setAttribute('fill', '#94a3b8');
                    sub.setAttribute('font-size', '11');
                    sub.textContent = `${n.ipAddress || 'IP N/A'} • ${n.isLocalAuditedServer ? 'Audited Server' : 'Remote Server'}`;
                    g.appendChild(sub);

                    const visiblePorts = n.ports.filter(p => !excPorts.includes(p.port) && (incPorts.length === 0 || incPorts.includes(p.port)));
                    const portSummary = visiblePorts.slice(0, 5).map(p => `:${p.port}`).join(' ') + (visiblePorts.length > 5 ? ` +${visiblePorts.length - 5}` : '');
                    const portsTxt = document.createElementNS(SVG_NS, 'text');
                    portsTxt.setAttribute('x', '12');
                    portsTxt.setAttribute('y', '66');
                    portsTxt.setAttribute('fill', '#34d399');
                    portsTxt.setAttribute('font-size', '11.5');
                    portsTxt.setAttribute('font-family', 'monospace');
                    portsTxt.textContent = portSummary || 'No exposed port';
                    g.appendChild(portsTxt);

                    const statsTxt = document.createElementNS(SVG_NS, 'text');
                    statsTxt.setAttribute('x', '12');
                    statsTxt.setAttribute('y', '90');
                    statsTxt.setAttribute('fill', '#cbd5e1');
                    statsTxt.setAttribute('font-size', '10.5');
                    statsTxt.textContent = `IN (Inbound): ${n.inboundConnectionCount}  |  OUT (Outbound): ${n.outboundConnectionCount}`;
                    g.appendChild(statsTxt);

                    g.addEventListener('mousedown', (ev) => {
                      ev.stopPropagation();
                      hasDragged = false;
                      draggingNodeId = n.nodeId;
                      const pt = screenToWorld(ev.clientX, ev.clientY);
                      dragOffsetX = pt.x - pos.x;
                      dragOffsetY = pt.y - pos.y;
                    });

                    g.addEventListener('click', (ev) => {
                      ev.stopPropagation();
                      if (hasDragged) return;
                      selectedNodeId = selectedNodeId === n.nodeId ? null : n.nodeId;
                      renderInspector(selectedNodeId ? n : null, edges, nodesMap);
                      renderGraph(false);
                    });

                    nodesFrag.appendChild(g);
                  });

                  edgesLayer.replaceChildren(edgesFrag);
                  labelsLayer.replaceChildren(labelsFrag);
                  nodesLayer.replaceChildren(nodesFrag);
                }

                function buildCliCommand() {
                  const parts = ['NetFlowAudit.Cli', '--map-only'];
                  const incPorts = (includePortsInput.value || '').trim();
                  if (incPorts) parts.push(`--include-ports "${incPorts}"`);
                  const excPorts = (excludePortsInput.value || '').trim();
                  if (excPorts) parts.push(`--exclude-ports "${excPorts}"`);
                  const excHosts = (excludeHostsInput.value || '').trim();
                  if (excHosts) parts.push(`--exclude-hosts "${excHosts}"`);
                  const q = (searchInput.value || '').trim();
                  if (q) parts.push(`--include-hosts "*${q}*"`);
                  if (directionSelect.value === 'OUT') parts.push('--direction OUT');
                  else if (directionSelect.value === 'IN') parts.push('--direction IN');
                  if (sortSelect.value && sortSelect.value !== 'sugiyama') parts.push(`--map-sort ${sortSelect.value}`);
                  if (routingSelect.value && routingSelect.value !== 'bezier') parts.push(`--map-routing ${routingSelect.value}`);
                  if (spacingSelect.value && spacingSelect.value !== 'balanced') parts.push(`--map-spacing ${spacingSelect.value}`);
                  return parts.join(' ');
                }

                function updateCliCommandDisplay() {
                  const cliEl = document.getElementById('cliCommandText');
                  if (cliEl) cliEl.textContent = buildCliCommand();
                }

                document.querySelectorAll('.section-title').forEach(st => {
                  st.addEventListener('click', () => {
                    const targetId = st.getAttribute('data-target');
                    if (!targetId) return;
                    const body = document.getElementById(targetId);
                    if (body) {
                      body.classList.toggle('collapsed');
                      st.classList.toggle('collapsed');
                    }
                  });
                });

                const btnToggleSidebar = document.getElementById('btnToggleSidebar');
                const sidebarPanel = document.getElementById('sidebarPanel');
                if (btnToggleSidebar && sidebarPanel) {
                  btnToggleSidebar.addEventListener('click', () => {
                    const isCollapsed = sidebarPanel.classList.toggle('collapsed');
                    btnToggleSidebar.textContent = isCollapsed ? '▶ Sidebar' : '◀ Sidebar';
                  });
                }

                const btnCopyCli = document.getElementById('btnCopyCliCommand');
                if (btnCopyCli) {
                  btnCopyCli.addEventListener('click', async () => {
                    const cmd = buildCliCommand();
                    try {
                      await navigator.clipboard.writeText(cmd);
                      const prev = btnCopyCli.textContent;
                      btnCopyCli.textContent = '✔ Copied!';
                      setTimeout(() => { btnCopyCli.textContent = prev; }, 2000);
                    } catch (e) {
                      prompt('Copy command:', cmd);
                    }
                  });
                }

                document.addEventListener('keydown', (ev) => {
                  if (ev.target.tagName !== 'INPUT' && ev.target.tagName !== 'SELECT' && ev.key.toLowerCase() === 'b') {
                    if (btnToggleSidebar && sidebarPanel) {
                      const isCollapsed = sidebarPanel.classList.toggle('collapsed');
                      btnToggleSidebar.textContent = isCollapsed ? '▶ Sidebar' : '◀ Sidebar';
                    }
                  }
                });

                function applyFilters() {
                  currentPageIndex = 0;
                  renderGraph(true);
                  updateCliCommandDisplay();
                }

                btnApplyFilters.addEventListener('click', applyFilters);

                [searchInput, includePortsInput, excludePortsInput, excludeHostsInput].forEach(el => {
                  el.addEventListener('keydown', (ev) => {
                    if (ev.key === 'Enter') {
                      ev.preventDefault();
                      applyFilters();
                    }
                  });
                });
                directionSelect.addEventListener('change', () => {
                  applyFilters();
                });
                layoutSelect.addEventListener('change', () => { renderGraph(true); updateCliCommandDisplay(); });
                sortSelect.addEventListener('change', () => { renderGraph(true); updateCliCommandDisplay(); });
                routingSelect.addEventListener('change', () => { renderGraph(false); updateCliCommandDisplay(); });

                spacingSelect.addEventListener('change', () => {
                  applySpacingPreset(spacingSelect.value);
                  renderGraph(true);
                  updateCliCommandDisplay();
                });

                nodeGapRange.addEventListener('input', () => {
                  nodeGapVal.textContent = `${parseFloat(nodeGapRange.value).toFixed(2)}x`;
                  renderGraph(true);
                });

                edgeFanRange.addEventListener('input', () => {
                  edgeFanVal.textContent = `${edgeFanRange.value}px`;
                  renderGraph(false);
                });

                btnPrevPage.addEventListener('click', () => {
                  if (currentPageIndex > 0) {
                    currentPageIndex--;
                    renderGraph(true);
                  }
                });

                btnNextPage.addEventListener('click', () => {
                  currentPageIndex++;
                  renderGraph(true);
                });

                btnLoadMore100.addEventListener('click', () => {
                  currentPageSize += 100;
                  renderGraph(true);
                });

                btnShowAll.addEventListener('click', () => {
                  currentPageIndex = 0;
                  currentPageSize = Math.max(100, TOPOLOGY_DATA.nodes.length);
                  renderGraph(true);
                });

                document.getElementById('btnResetZoom').addEventListener('click', () => {
                  panX = 50; panY = 50; scale = 1.0; updateTransform();
                });

                document.getElementById('btnRecomputeLayout').addEventListener('click', () => {
                  renderGraph(true);
                });

                document.getElementById('btnClearFilters').addEventListener('click', () => {
                  searchInput.value = '';
                  includePortsInput.value = '';
                  excludePortsInput.value = '';
                  excludeHostsInput.value = '';
                  directionSelect.value = 'all';
                  selectedNodeId = null;
                  currentPageIndex = 0;
                  currentPageSize = TOPOLOGY_DATA.defaultPageSize || 100;
                  renderInspector(null, [], new Map());
                  renderGraph(true);
                });

                document.getElementById('btnExportSvg').addEventListener('click', () => {
                  exportCurrentSvg();
                });

                function exportCurrentSvg() {
                  const svgEl = document.getElementById('topologySvg');
                  const viewportEl = document.getElementById('viewportGroup');
                  if (!svgEl || !viewportEl) return;

                  const nodeEls = Array.from(document.querySelectorAll('#nodesLayer > g'));
                  if (nodeEls.length === 0) {
                    alert('No visible nodes to export.');
                    return;
                  }

                  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
                  nodeEls.forEach(g => {
                    const transform = g.getAttribute('transform') || '';
                    const match = /translate\(\s*([-\d.]+)\s*,\s*([-\d.]+)\s*\)/.exec(transform);
                    if (match) {
                      const x = parseFloat(match[1]);
                      const y = parseFloat(match[2]);
                      minX = Math.min(minX, x);
                      minY = Math.min(minY, y);
                      maxX = Math.max(maxX, x + NODE_W);
                      maxY = Math.max(maxY, y + NODE_H);
                    }
                  });

                  if (!isFinite(minX) || !isFinite(minY)) {
                    minX = 0; minY = 0; maxX = 1600; maxY = 1200;
                  }

                  const margin = 60;
                  const cropX = Math.floor(minX - margin);
                  const cropY = Math.floor(minY - margin);
                  const cropW = Math.ceil((maxX - minX) + margin * 2);
                  const cropH = Math.ceil((maxY - minY) + margin * 2);

                  const clonedSvg = svgEl.cloneNode(true);
                  clonedSvg.removeAttribute('id');
                  clonedSvg.removeAttribute('class');
                  clonedSvg.setAttribute('xmlns', SVG_NS);
                  clonedSvg.setAttribute('viewBox', [cropX, cropY, cropW, cropH].join(' '));

                  clonedSvg.setAttribute('width', String(cropW));
                  clonedSvg.setAttribute('height', String(cropH));

                  const clonedViewport = clonedSvg.querySelector('#viewportGroup');
                  if (clonedViewport) {
                    clonedViewport.removeAttribute('transform');
                  }

                  const bgRect = document.createElementNS(SVG_NS, 'rect');
                  bgRect.setAttribute('x', String(cropX));
                  bgRect.setAttribute('y', String(cropY));
                  bgRect.setAttribute('width', String(cropW));
                  bgRect.setAttribute('height', String(cropH));
                  bgRect.setAttribute('fill', '#0b0f19');
                  clonedSvg.insertBefore(bgRect, clonedSvg.firstChild);

                  const serializer = new XMLSerializer();
                  const svgSource = '<?xml version="1.0" encoding="UTF-8"?>\n' + serializer.serializeToString(clonedSvg);
                  const blob = new Blob([svgSource], { type: 'image/svg+xml;charset=utf-8' });
                  const blobUrl = URL.createObjectURL(blob);

                  const downloadLink = document.createElement('a');
                  downloadLink.href = blobUrl;
                  downloadLink.download = 'network_topology_map.svg';
                  document.body.appendChild(downloadLink);
                  downloadLink.click();
                  document.body.removeChild(downloadLink);
                  URL.revokeObjectURL(blobUrl);
                }

                updateTransform();
                renderGraph(true);
                updateCliCommandDisplay();
              </script>
            """);
    }
}
