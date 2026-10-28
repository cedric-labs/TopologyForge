using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
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
        // Le JSON est inséré tel quel dans un bloc <script> : l'encodeur doit échapper <, > et & pour qu'un nom DNS
        // inverse ou un nom de processus contenant « </script » ou « <!-- » ne puisse pas fermer le bloc.
        // Toutes les plages Unicode restent lisibles (noms d'hôtes accentués).
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false
    };


    private const string MapScriptResourceName = "TopologyForge.Rendering.topology-map.js";

    // Le script de la carte vit dans Rendering/topology-map.js, en ressource embarquée, pour être relu et analysé comme
    // du JavaScript. Il est lu une seule fois puis copié dans chaque page, qui reste un fichier HTML autonome.
    private static readonly string MapScript = ReadMapScriptResource();

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
        AppendBodyLayout(sb, safeTitle, generatedAtFormatted, graph, EscapeHtmlText(options.CliCommandFormat.Prefix));
        AppendEmbeddedScript(sb, payloadJson);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string SerializeGraphPayload(NetworkTopologyGraph graph, TopologyRenderingOptions options)
    {
        var defaultLayout = options.DefaultLayoutMode switch
        {
            TopologyLayoutMode.Circular => "circular",
            TopologyLayoutMode.Grid => "grid",
            TopologyLayoutMode.ForceDirectedOrganic => "organic",
            TopologyLayoutMode.Tree => "tree",
            TopologyLayoutMode.HorizontalTree => "horizontal-tree",
            _ => "layered"
        };

        var defaultSort = options.DefaultSortMethod switch
        {
            TopologyNodeSortMethod.ConnectionDegree => "degree",
            TopologyNodeSortMethod.Alphabetical => "alpha",
            TopologyNodeSortMethod.SubnetIp => "ip",
            TopologyNodeSortMethod.FunctionalTier => "tiers",
            TopologyNodeSortMethod.SubnetCluster => "subnet",
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
            DefaultLayout = defaultLayout,
            DefaultSort = defaultSort,
            DefaultRouting = defaultRouting,
            DefaultSpacing = defaultSpacing,
            DefaultPageSize = defaultPageSize,
            InitialIncludePorts = options.InitialIncludePorts,
            InitialExcludePorts = options.InitialExcludePorts,
            InitialExcludeHosts = options.InitialExcludeHosts,
            InitialDirection = string.IsNullOrWhiteSpace(options.InitialDirection) ? "all" : options.InitialDirection,
            CliCommand = new
            {
                options.CliCommandFormat.Prefix,
                options.CliCommandFormat.LayoutOption,
                options.CliCommandFormat.SortOption,
                options.CliCommandFormat.RoutingOption,
                options.CliCommandFormat.SpacingOption
            },
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

        return JsonSerializer.Serialize(dto, JsonOptions);
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
                .filter-checkbox-group {
                  display: flex;
                  align-items: center;
                  gap: 0.45rem;
                  padding: 0.2rem 0;
                }
                .filter-checkbox-group input[type="checkbox"] {
                  accent-color: var(--accent-local);
                  cursor: pointer;
                  width: 14px;
                  height: 14px;
                }
                .filter-checkbox-group label {
                  font-size: 0.72rem;
                  color: var(--text-primary);
                  cursor: pointer;
                  user-select: none;
                  font-weight: 500;
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
                .subnet-zone { pointer-events: none; }
                .subnet-zone rect { transition: all 0.2s ease; }
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

    private static void AppendBodyLayout(StringBuilder sb, string safeTitle, string generatedAtFormatted, NetworkTopologyGraph graph, string safeCliPrefix)
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
        sb.AppendLine("              <option value=\"tree\">Tree (Top-Down Multi-Column)</option>");
        sb.AppendLine("              <option value=\"horizontal-tree\">Horizontal Tree (Left ➔ Right)</option>");
        sb.AppendLine("              <option value=\"organic\">Force-Directed (Organic)</option>");
        sb.AppendLine("              <option value=\"circular\">Concentric Ring</option>");
        sb.AppendLine("              <option value=\"grid\">Multi-Column Grid</option>");
        sb.AppendLine("            </select>");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"sortSelect\">Sorting Method</label>");
        sb.AppendLine("            <select id=\"sortSelect\">");
        sb.AppendLine("              <option value=\"sugiyama\">Sugiyama (Anti-crossing)</option>");
        sb.AppendLine("              <option value=\"tiers\">Functional Tiers (Client ➔ App ➔ Infra)</option>");
        sb.AppendLine("              <option value=\"subnet\">Subnet &amp; Security Zones</option>");
        sb.AppendLine("              <option value=\"degree\">Connection Degree</option>");
        sb.AppendLine("              <option value=\"alpha\">Alphabetical (Hostname)</option>");
        sb.AppendLine("              <option value=\"ip\">Subnet / IP Address</option>");
        sb.AppendLine("            </select>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"filter-checkbox-group\">");
        sb.AppendLine("          <input type=\"checkbox\" id=\"showZonesCheck\">");
        sb.AppendLine("          <label for=\"showZonesCheck\">Show Subnet Zones</label>");
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
        sb.AppendLine("          <input id=\"searchInput\" type=\"text\" placeholder=\"e.g. nas, 8096, jellyfin...\" />");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"grid-2\">");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"includePortsInput\">Include ports</label>");
        sb.AppendLine("            <input id=\"includePortsInput\" type=\"text\" placeholder=\"80, 443, 8096\" />");
        sb.AppendLine("          </div>");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"excludePortsInput\">Exclude ports</label>");
        sb.AppendLine("            <input id=\"excludePortsInput\" type=\"text\" placeholder=\"53, 123, 135\" />");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"grid-2\">");
        sb.AppendLine("          <div class=\"filter-group\">");
        sb.AppendLine("            <label for=\"excludeHostsInput\">Exclude servers/IP</label>");
        sb.AppendLine("            <input id=\"excludeHostsInput\" type=\"text\" placeholder=\"pihole, 192.168.1.\" />");
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
        sb.Append("          <div id=\"cliCommandText\" class=\"cli-command-code\">").Append(safeCliPrefix).AppendLine("</div>");
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
        sb.AppendLine("          <g id=\"zonesLayer\"></g>");
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
        sb.Append(MapScript);
        sb.AppendLine("  </script>");
    }

    private static string ReadMapScriptResource()
    {
        using var stream = typeof(TopologyHtmlRenderer).Assembly.GetManifestResourceStream(MapScriptResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{MapScriptResourceName}' is missing from the TopologyForge assembly.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
