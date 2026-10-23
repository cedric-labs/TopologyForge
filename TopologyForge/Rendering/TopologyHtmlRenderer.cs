using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TopologyForge.Models;

namespace TopologyForge.Rendering;

/// <summary>
/// Générateur de cartes topologiques HTML5 autonomes (Single-File, zéro dépendance externe) avec moteur de routage anti-croisement et pointes directionnelles IN/OUT.
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
        string title = "Cartographie des Flux & Serveurs",
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
    public string RenderHtml(NetworkTopologyGraph graph, string title = "Cartographie des Flux & Serveurs") =>
        RenderHtml(graph, new TopologyRenderingOptions { Title = title });

    /// <inheritdoc />
    public string RenderHtml(NetworkTopologyGraph graph, TopologyRenderingOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        var safeTitle = EscapeHtmlText(string.IsNullOrWhiteSpace(options.Title) ? "Cartographie des Flux & Serveurs" : options.Title);
        var payloadJson = SerializeGraphPayload(graph, options);
        var generatedAtFormatted = graph.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

        var sb = new StringBuilder(49152);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"fr\">");
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

        var dto = new
        {
            GeneratedAtUtc = graph.GeneratedAtUtc.ToString("o", CultureInfo.InvariantCulture),
            graph.TotalProcessedFlows,
            graph.ExcludedFlowsCount,
            DefaultSort = defaultSort,
            DefaultRouting = defaultRouting,
            DefaultSpacing = defaultSpacing,
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
                .workspace { display: flex; flex: 1; overflow: hidden; }
                .sidebar {
                  width: 350px;
                  background: var(--bg-panel);
                  border-right: 1px solid var(--border-color);
                  padding: 0.85rem 1rem;
                  display: flex;
                  flex-direction: column;
                  gap: 0.7rem;
                  overflow-y: auto;
                }
                .section-title {
                  font-size: 0.72rem;
                  text-transform: uppercase;
                  letter-spacing: 0.06em;
                  color: var(--accent-local);
                  font-weight: 700;
                  border-bottom: 1px solid #1e293b;
                  padding-bottom: 0.25rem;
                  margin-top: 0.2rem;
                }
                .filter-group { display: flex; flex-direction: column; gap: 0.28rem; }
                .filter-group label {
                  font-size: 0.73rem;
                  color: var(--text-muted);
                  font-weight: 600;
                  display: flex;
                  justify-content: space-between;
                }
                .filter-group input[type="text"], .filter-group select {
                  background: var(--bg-card);
                  border: 1px solid var(--border-color);
                  color: var(--text-primary);
                  padding: 0.4rem 0.55rem;
                  border-radius: 5px;
                  font-size: 0.82rem;
                }
                .filter-group input[type="range"] {
                  width: 100%;
                  accent-color: var(--accent-local);
                  cursor: pointer;
                }
                .grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 0.5rem; }
                .btn-row { display: flex; gap: 0.4rem; flex-wrap: wrap; }
                button {
                  background: var(--bg-card);
                  border: 1px solid var(--border-color);
                  color: var(--text-primary);
                  padding: 0.38rem 0.65rem;
                  border-radius: 5px;
                  cursor: pointer;
                  font-size: 0.78rem;
                  transition: background 0.15s, border-color 0.15s;
                }
                button:hover { border-color: var(--accent-local); background: #26354d; }
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
        sb.Append("      <span>Généré le ").Append(generatedAtFormatted).AppendLine(" • Cartographie interactive hors-ligne (TopologyForge)</span>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"kpi-bar\">");
        sb.Append("      <div class=\"kpi-badge\">Serveurs<strong id=\"kpiNodes\">").Append(graph.Nodes.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("</strong></div>");
        sb.Append("      <div class=\"kpi-badge\">Ports Services<strong id=\"kpiPorts\">").Append(totalPorts.ToString(CultureInfo.InvariantCulture)).AppendLine("</strong></div>");
        sb.Append("      <div class=\"kpi-badge\">Flux Inter-Serveurs<strong id=\"kpiEdges\">").Append(graph.Edges.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("</strong></div>");
        sb.Append("      <div class=\"kpi-badge\">Flux Filtrés<strong>").Append(graph.ExcludedFlowsCount.ToString(CultureInfo.InvariantCulture)).Append('/').Append(graph.TotalProcessedFlows.ToString(CultureInfo.InvariantCulture)).AppendLine("</strong></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </header>");

        sb.AppendLine("  <div class=\"workspace\">");
        sb.AppendLine("    <aside class=\"sidebar\">");
        sb.AppendLine("      <div class=\"section-title\">1. Disposition, Tri &amp; Routage</div>");
        sb.AppendLine("      <div class=\"grid-2\">");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"layoutSelect\">Disposition</label>");
        sb.AppendLine("          <select id=\"layoutSelect\">");
        sb.AppendLine("            <option value=\"layered\">Hiérarchique (Couches)</option>");
        sb.AppendLine("            <option value=\"circular\">Anneau Concentrique</option>");
        sb.AppendLine("            <option value=\"grid\">Grille Multi-Colonnes</option>");
        sb.AppendLine("          </select>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"sortSelect\">Méthode de Tri</label>");
        sb.AppendLine("          <select id=\"sortSelect\">");
        sb.AppendLine("            <option value=\"sugiyama\">Sugiyama (Anti-croisement)</option>");
        sb.AppendLine("            <option value=\"degree\">Densité de connexions</option>");
        sb.AppendLine("            <option value=\"alpha\">Alphabétique (Serveur)</option>");
        sb.AppendLine("            <option value=\"ip\">Sous-réseau / IP</option>");
        sb.AppendLine("          </select>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"grid-2\">");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"routingSelect\">Routage des Flèches</label>");
        sb.AppendLine("          <select id=\"routingSelect\">");
        sb.AppendLine("            <option value=\"bezier\">Courbes Bézier (Contournement)</option>");
        sb.AppendLine("            <option value=\"orthogonal\">Couloirs Orthogonaux (90°)</option>");
        sb.AppendLine("            <option value=\"straight\">Direct (Bord à bord)</option>");
        sb.AppendLine("          </select>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"spacingSelect\">Profil d'Écartement</label>");
        sb.AppendLine("          <select id=\"spacingSelect\">");
        sb.AppendLine("            <option value=\"compact\">Compact (Dense)</option>");
        sb.AppendLine("            <option value=\"balanced\">Équilibré (Standard)</option>");
        sb.AppendLine("            <option value=\"spacious\">Aéré (100+ serveurs)</option>");
        sb.AppendLine("          </select>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"grid-2\">");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"nodeGapRange\"><span>Écart Serveurs</span><span id=\"nodeGapVal\">1.0x</span></label>");
        sb.AppendLine("          <input id=\"nodeGapRange\" type=\"range\" min=\"0.65\" max=\"2.4\" step=\"0.05\" value=\"1.0\" />");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"edgeFanRange\"><span>Écart Flèches</span><span id=\"edgeFanVal\">18px</span></label>");
        sb.AppendLine("          <input id=\"edgeFanRange\" type=\"range\" min=\"6\" max=\"46\" step=\"2\" value=\"18\" />");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"section-title\">2. Filtres d'Inclusion &amp; Exclusion</div>");
        sb.AppendLine("      <div class=\"filter-group\">");
        sb.AppendLine("        <label for=\"searchInput\">Recherche globale (Serveur, IP, Port, Processus)</label>");
        sb.AppendLine("        <input id=\"searchInput\" type=\"text\" placeholder=\"Ex: SRV-SQL, 1433, nginx...\" />");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"grid-2\">");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"includePortsInput\">Inclure ports</label>");
        sb.AppendLine("          <input id=\"includePortsInput\" type=\"text\" placeholder=\"80, 443, 1433\" />");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"excludePortsInput\">Exclure ports</label>");
        sb.AppendLine("          <input id=\"excludePortsInput\" type=\"text\" placeholder=\"53, 123, 135\" />");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"grid-2\">");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"excludeHostsInput\">Exclure serveurs/IP</label>");
        sb.AppendLine("          <input id=\"excludeHostsInput\" type=\"text\" placeholder=\"SRV-MON, 10.99.\" />");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"filter-group\">");
        sb.AppendLine("          <label for=\"directionSelect\">Sens des Flux</label>");
        sb.AppendLine("          <select id=\"directionSelect\">");
        sb.AppendLine("            <option value=\"all\">Tous (IN &amp; OUT)</option>");
        sb.AppendLine("            <option value=\"OUT\">Sortants (OUT)</option>");
        sb.AppendLine("            <option value=\"IN\">Entrants (IN)</option>");
        sb.AppendLine("          </select>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"btn-row\">");
        sb.AppendLine("        <button id=\"btnResetZoom\" type=\"button\">Centrer la vue</button>");
        sb.AppendLine("        <button id=\"btnRecomputeLayout\" type=\"button\">Recalculer routes</button>");
        sb.AppendLine("        <button id=\"btnClearFilters\" type=\"button\">Réinitialiser</button>");
        sb.AppendLine("      </div>");

        sb.AppendLine("      <div class=\"inspector\" id=\"inspectorPanel\">");
        sb.AppendLine("        <div class=\"section-title\">3. Inspecteur de Serveur (Glisser / Cliquer)</div>");
        sb.AppendLine("        <div id=\"inspectorContent\">Cliquez sur un serveur pour isoler ses routes entrantes (IN) et sortantes (OUT), ou glissez-le pour ajuster sa position.</div>");
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
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"dot\" style=\"background:var(--accent-local)\"></span>Serveur Audité</div>");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"dot\" style=\"background:var(--accent-remote)\"></span>Serveur Distant</div>");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"arrow-sample\" style=\"background:var(--accent-out)\"></span>➔ Flux Sortant (OUT)</div>");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"arrow-sample\" style=\"background:var(--accent-in)\"></span>➔ Flux Entrant (IN)</div>");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"arrow-sample\" style=\"background:var(--accent-highlight)\"></span>➔ Flux Sélectionné</div>");
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
                const inspectorContent = document.getElementById('inspectorContent');

                const NODE_W = 236, NODE_H = 106;
                let panX = 50, panY = 50, scale = 1.0;
                let isPanning = false, startPanX = 0, startPanY = 0;
                let draggingNodeId = null, dragOffsetX = 0, dragOffsetY = 0, hasDragged = false;
                let selectedNodeId = null;
                let nodePositions = new Map();

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
                    return copy.sort((a, b) => degreeOf(b) - degreeOf(a) || a.hostName.localeCompare(b.hostName));
                  }

                  // Heuristique Barycentrique de Sugiyama (minimisation des croisements)
                  const locals = copy.filter(n => n.isLocalAuditedServer).sort((a, b) => degreeOf(b) - degreeOf(a));
                  const remotes = copy.filter(n => !n.isLocalAuditedServer);

                  const localRank = new Map();
                  locals.forEach((n, idx) => localRank.set(n.nodeId, idx));

                  const computeRemoteBarycenter = (remoteNode) => {
                    let sum = 0, count = 0;
                    edges.forEach(e => {
                      if (e.targetNodeId === remoteNode.nodeId && localRank.has(e.sourceNodeId)) {
                        sum += localRank.get(e.sourceNodeId);
                        count++;
                      } else if (e.sourceNodeId === remoteNode.nodeId && localRank.has(e.targetNodeId)) {
                        sum += localRank.get(e.targetNodeId);
                        count++;
                      }
                    });
                    return count > 0 ? sum / count : 9999;
                  };

                  remotes.sort((a, b) => {
                    const ba = computeRemoteBarycenter(a);
                    const bb = computeRemoteBarycenter(b);
                    if (Math.abs(ba - bb) > 0.01) return ba - bb;
                    return degreeOf(b) - degreeOf(a) || a.hostName.localeCompare(b.hostName);
                  });

                  // Seconde passe (Cibles -> Sources) pour affiner l'alignement
                  const remoteRank = new Map();
                  remotes.forEach((n, idx) => remoteRank.set(n.nodeId, idx));
                  locals.sort((a, b) => {
                    let sumA = 0, cntA = 0, sumB = 0, cntB = 0;
                    edges.forEach(e => {
                      if (e.sourceNodeId === a.nodeId && remoteRank.has(e.targetNodeId)) { sumA += remoteRank.get(e.targetNodeId); cntA++; }
                      if (e.targetNodeId === a.nodeId && remoteRank.has(e.sourceNodeId)) { sumA += remoteRank.get(e.sourceNodeId); cntA++; }
                      if (e.sourceNodeId === b.nodeId && remoteRank.has(e.targetNodeId)) { sumB += remoteRank.get(e.targetNodeId); cntB++; }
                      if (e.targetNodeId === b.nodeId && remoteRank.has(e.sourceNodeId)) { sumB += remoteRank.get(e.sourceNodeId); cntB++; }
                    });
                    const baryA = cntA > 0 ? sumA / cntA : 9999;
                    const baryB = cntB > 0 ? sumB / cntB : 9999;
                    return baryA - baryB || degreeOf(b) - degreeOf(a);
                  });

                  return [...locals, ...remotes];
                }

                function computeLayout(nodes, edges, layoutMode, sortMethod) {
                  nodePositions.clear();
                  const ordered = sortNodes(nodes, edges, sortMethod);
                  const gapFactor = parseFloat(nodeGapRange.value) || 1.0;
                  const stepX = Math.round(360 * gapFactor);
                  const stepY = Math.round(156 * gapFactor);

                  if (layoutMode === 'circular') {
                    const width = Math.max(960, svg.clientWidth || 1200);
                    const height = Math.max(680, svg.clientHeight || 800);
                    const cx = width / 2, cy = height / 2;
                    const radius = Math.max(240, ordered.length * 36 * gapFactor);
                    ordered.forEach((n, idx) => {
                      const angle = (2 * Math.PI * idx) / Math.max(1, ordered.length);
                      nodePositions.set(n.nodeId, {
                        x: Math.round(cx + radius * Math.cos(angle) - NODE_W / 2),
                        y: Math.round(cy + radius * Math.sin(angle) - NODE_H / 2)
                      });
                    });
                    return;
                  }

                  if (layoutMode === 'grid') {
                    const cols = Math.max(2, Math.ceil(Math.sqrt(ordered.length)));
                    ordered.forEach((n, idx) => {
                      const col = idx % cols;
                      const row = Math.floor(idx / cols);
                      nodePositions.set(n.nodeId, { x: 60 + col * stepX, y: 60 + row * stepY });
                    });
                    return;
                  }

                  // Mode Hiérarchique multi-colonnes adapté aux parcs de 100+ serveurs
                  const locals = ordered.filter(n => n.isLocalAuditedServer);
                  const remotes = ordered.filter(n => !n.isLocalAuditedServer);
                  const maxPerColumn = Math.max(6, Math.min(18, Math.ceil(Math.sqrt(remotes.length) * 1.8)));

                  locals.forEach((n, idx) => {
                    const col = Math.floor(idx / maxPerColumn);
                    const row = idx % maxPerColumn;
                    nodePositions.set(n.nodeId, { x: 60 + col * stepX, y: 60 + row * stepY });
                  });

                  const localCols = Math.max(1, Math.ceil(locals.length / maxPerColumn));
                  const remoteStartX = 60 + localCols * (stepX + 80);

                  remotes.forEach((n, idx) => {
                    const col = Math.floor(idx / maxPerColumn);
                    const row = idx % maxPerColumn;
                    nodePositions.set(n.nodeId, { x: remoteStartX + col * stepX, y: 60 + row * stepY });
                  });
                }

                // Calcule le point d'ancrage exact sur la bordure extérieure du rectangle serveur + décalage de slot
                function computeBorderAnchor(fromPos, toPos, isTarget, slotOffset) {
                  const cx1 = fromPos.x + NODE_W / 2, cy1 = fromPos.y + NODE_H / 2;
                  const cx2 = toPos.x + NODE_W / 2, cy2 = toPos.y + NODE_H / 2;
                  const dx = isTarget ? (cx1 - cx2) : (cx2 - cx1);
                  const dy = isTarget ? (cy1 - cy2) : (cy2 - cy1);
                  const basePos = isTarget ? toPos : fromPos;
                  const centerX = basePos.x + NODE_W / 2;
                  const centerY = basePos.y + NODE_H / 2;

                  const halfW = NODE_W / 2 + 2;
                  const halfH = NODE_H / 2 + 2;

                  if (Math.abs(dx) < 0.001 && Math.abs(dy) < 0.001) {
                    return { x: centerX + halfW, y: centerY, side: 'right' };
                  }

                  const scaleX = Math.abs(dx) > 0 ? halfW / Math.abs(dx) : 999999;
                  const scaleY = Math.abs(dy) > 0 ? halfH / Math.abs(dy) : 999999;

                  if (scaleX < scaleY) {
                    const side = dx > 0 ? 'right' : 'left';
                    const clampedOffset = Math.max(-halfH + 12, Math.min(halfH - 12, slotOffset));
                    return {
                      x: centerX + (dx > 0 ? halfW : -halfW),
                      y: centerY + dy * scaleX * 0.35 + clampedOffset,
                      side
                    };
                  } else {
                    const side = dy > 0 ? 'bottom' : 'top';
                    const clampedOffset = Math.max(-halfW + 16, Math.min(halfW - 16, slotOffset));
                    return {
                      x: centerX + dx * scaleY * 0.35 + clampedOffset,
                      y: centerY + (dy > 0 ? halfH : -halfH),
                      side
                    };
                  }
                }

                // Détecte si le segment direct [p1, p2] traverse la boîte d'un serveur tiers
                function findBlockingObstacle(p1, p2, sourceId, targetId, allNodes) {
                  const minX = Math.min(p1.x, p2.x), maxX = Math.max(p1.x, p2.x);
                  const minY = Math.min(p1.y, p2.y), maxY = Math.max(p1.y, p2.y);

                  for (let i = 0; i < allNodes.length; i++) {
                    const n = allNodes[i];
                    if (n.nodeId === sourceId || n.nodeId === targetId) continue;
                    const pos = nodePositions.get(n.nodeId);
                    if (!pos) continue;

                    const bx1 = pos.x - 10, by1 = pos.y - 10;
                    const bx2 = pos.x + NODE_W + 10, by2 = pos.y + NODE_H + 10;

                    if (maxX < bx1 || minX > bx2 || maxY < by1 || minY > by2) continue;

                    // Test de proximité de la boîte au segment
                    const boxCy = pos.y + NODE_H / 2;
                    const boxCx = pos.x + NODE_W / 2;
                    const t = Math.max(0, Math.min(1, ((boxCx - p1.x) * (p2.x - p1.x) + (boxCy - p1.y) * (p2.y - p1.y)) / (Math.pow(p2.x - p1.x, 2) + Math.pow(p2.y - p1.y, 2) || 1)));
                    const projX = p1.x + t * (p2.x - p1.x);
                    const projY = p1.y + t * (p2.y - p1.y);

                    if (projX >= bx1 && projX <= bx2 && projY >= by1 && projY <= by2) {
                      return { pos, boxCx, boxCy };
                    }
                  }
                  return null;
                }

                function buildRoutedEdgePath(startAnchor, endAnchor, routingMode, fanOffset, obstacle) {
                  const x1 = startAnchor.x, y1 = startAnchor.y;
                  const x2 = endAnchor.x, y2 = endAnchor.y;
                  const dx = x2 - x1, dy = y2 - y1;

                  if (routingMode === 'straight') {
                    const len = Math.hypot(dx, dy) || 1;
                    const nx = -dy / len, ny = dx / len;
                    const mx = (x1 + x2) / 2 + nx * fanOffset;
                    const my = (y1 + y2) / 2 + ny * fanOffset;
                    if (Math.abs(fanOffset) < 1) {
                      return { d: `M ${x1} ${y1} L ${x2} ${y2}`, labelX: mx, labelY: my - 6 };
                    }
                    return { d: `M ${x1} ${y1} Q ${mx} ${my} ${x2} ${y2}`, labelX: mx, labelY: my - 6 };
                  }

                  if (routingMode === 'orthogonal') {
                    let midX = (x1 + x2) / 2 + fanOffset * 1.4;
                    if (obstacle) {
                      const detourY = (y1 + y2) / 2 <= obstacle.boxCy
                        ? obstacle.pos.y - 28 - Math.abs(fanOffset)
                        : obstacle.pos.y + NODE_H + 28 + Math.abs(fanOffset);
                      const c1x = x1 + (dx >= 0 ? 28 : -28);
                      const c2x = x2 - (dx >= 0 ? 28 : -28);
                      return {
                        d: `M ${x1} ${y1} L ${c1x} ${y1} L ${c1x} ${detourY} L ${c2x} ${detourY} L ${c2x} ${y2} L ${x2} ${y2}`,
                        labelX: (c1x + c2x) / 2,
                        labelY: detourY - 6
                      };
                    }
                    return {
                      d: `M ${x1} ${y1} L ${midX} ${y1} L ${midX} ${y2} L ${x2} ${y2}`,
                      labelX: midX,
                      labelY: (y1 + y2) / 2 - 6
                    };
                  }

                  // Mode Bézier Cubique avec évitement d'obstacle et éventail
                  let detourShiftY = fanOffset * 1.6;
                  if (obstacle) {
                    const bypassDir = ((y1 + y2) / 2 <= obstacle.boxCy) ? -1 : 1;
                    detourShiftY += bypassDir * (NODE_H + 45);
                  }

                  const tension = Math.max(60, Math.abs(dx) * 0.42);
                  const dirX = dx >= 0 ? 1 : -1;
                  const cx1 = x1 + dirX * tension;
                  const cy1 = y1 + detourShiftY;
                  const cx2 = x2 - dirX * tension;
                  const cy2 = y2 + detourShiftY;

                  // Point médian sur la courbe de Bézier cubique (t = 0.5)
                  const bx = 0.125 * x1 + 0.375 * cx1 + 0.375 * cx2 + 0.125 * x2;
                  const by = 0.125 * y1 + 0.375 * cy1 + 0.375 * cy2 + 0.125 * y2;

                  return {
                    d: `M ${x1} ${y1} C ${cx1} ${cy1}, ${cx2} ${cy2}, ${x2} ${y2}`,
                    labelX: bx,
                    labelY: by - 6
                  };
                }

                function filterGraph() {
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
                    if (incPorts.length > 0 || dirFilter !== 'all') {
                      const hasPort = incPorts.length > 0 && n.ports.some(p => incPorts.includes(p.port));
                      const hasEdge = filteredEdges.some(e => e.sourceNodeId === n.nodeId || e.targetNodeId === n.nodeId);
                      if (!hasPort && !hasEdge) return false;
                    }
                    return true;
                  });

                  const nodeIds = new Set(filteredNodes.map(n => n.nodeId));
                  const activeEdges = filteredEdges.filter(e => nodeIds.has(e.sourceNodeId) && nodeIds.has(e.targetNodeId));
                  return { nodes: filteredNodes, edges: activeEdges };
                }

                function renderInspector(node, edges, allNodesMap) {
                  if (!node) {
                    inspectorContent.innerHTML = 'Cliquez sur un serveur pour isoler ses routes entrantes (IN) et sortantes (OUT), ou glissez-le pour ajuster sa position.';
                    return;
                  }
                  const portsHtml = node.ports.length > 0
                    ? node.ports.map(p => `<span class="port-pill">:${p.port}/${p.protocol}${p.processName ? ' (' + p.processName + ')' : ''}</span>`).join(' ')
                    : '<span style="color:var(--text-muted)">Aucun port d\'écoute direct</span>';

                  const outbound = edges.filter(e => e.sourceNodeId === node.nodeId);
                  const inbound = edges.filter(e => e.targetNodeId === node.nodeId);

                  const outHtml = outbound.map(e => {
                    const target = allNodesMap.get(e.targetNodeId);
                    return `<div><span style="color:var(--accent-out)">[OUT ➔]</span> <strong>${target ? target.hostName : e.targetNodeId}</strong> :${e.targetPort}/${e.protocol} (${e.processName || 'flux'})</div>`;
                  }).join('');

                  const inHtml = inbound.map(e => {
                    const source = allNodesMap.get(e.sourceNodeId);
                    return `<div><span style="color:var(--accent-in)">[⬅ IN]</span> <strong>${source ? source.hostName : e.sourceNodeId}</strong> vers :${e.targetPort}/${e.protocol}</div>`;
                  }).join('');

                  inspectorContent.innerHTML = `
                    <div><strong>${node.hostName}</strong> (${node.ipAddress || 'IP N/A'})</div>
                    <div style="margin-top:4px;color:var(--text-muted)">Ports non éphémères :</div>
                    <div>${portsHtml}</div>
                    <div style="margin-top:6px;color:var(--text-muted)">Flux Sortants OUT (${outbound.length}) :</div>
                    <div>${outHtml || 'Aucun'}</div>
                    <div style="margin-top:6px;color:var(--text-muted)">Flux Entrants IN (${inbound.length}) :</div>
                    <div>${inHtml || 'Aucun'}</div>
                  `;
                }

                function renderGraph(recomputePositions = false) {
                  const { nodes, edges } = filterGraph();
                  const nodesMap = new Map(nodes.map(n => [n.nodeId, n]));
                  document.getElementById('kpiNodes').textContent = String(nodes.length);
                  document.getElementById('kpiEdges').textContent = String(edges.length);

                  if (recomputePositions || nodePositions.size === 0) {
                    computeLayout(nodes, edges, layoutSelect.value, sortSelect.value);
                  }

                  const routingMode = routingSelect.value;
                  const fanSpacing = parseInt(edgeFanRange.value, 10) || 18;

                  // Groupement des liens partageant la même paire de serveurs pour l'écartement en éventail
                  const pairBuckets = new Map();
                  const nodePortSlots = new Map();

                  edges.forEach(e => {
                    const pairKey = [e.sourceNodeId, e.targetNodeId].sort().join('::');
                    if (!pairBuckets.has(pairKey)) pairBuckets.set(pairKey, []);
                    pairBuckets.get(pairKey).push(e);

                    const srcList = nodePortSlots.get(e.sourceNodeId) || [];
                    srcList.push(e.edgeId);
                    nodePortSlots.set(e.sourceNodeId, srcList);

                    const dstList = nodePortSlots.get(e.targetNodeId) || [];
                    dstList.push(e.edgeId);
                    nodePortSlots.set(e.targetNodeId, dstList);
                  });

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

                    const srcSlots = nodePortSlots.get(e.sourceNodeId) || [e.edgeId];
                    const dstSlots = nodePortSlots.get(e.targetNodeId) || [e.edgeId];
                    const srcSlotOffset = (srcSlots.indexOf(e.edgeId) - (srcSlots.length - 1) / 2) * 9;
                    const dstSlotOffset = (dstSlots.indexOf(e.edgeId) - (dstSlots.length - 1) / 2) * 9;

                    const startAnchor = computeBorderAnchor(p1, p2, false, srcSlotOffset);
                    const endAnchor = computeBorderAnchor(p1, p2, true, dstSlotOffset);

                    const obstacle = findBlockingObstacle(startAnchor, endAnchor, e.sourceNodeId, e.targetNodeId, nodes);
                    const route = buildRoutedEdgePath(startAnchor, endAnchor, routingMode, fanOffset, obstacle);

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
                    sub.textContent = `${n.ipAddress || 'IP N/A'} • ${n.isLocalAuditedServer ? 'Serveur Audité' : 'Serveur Distant'}`;
                    g.appendChild(sub);

                    const portSummary = n.ports.slice(0, 5).map(p => `:${p.port}`).join(' ') + (n.ports.length > 5 ? ` +${n.ports.length - 5}` : '');
                    const portsTxt = document.createElementNS(SVG_NS, 'text');
                    portsTxt.setAttribute('x', '12');
                    portsTxt.setAttribute('y', '66');
                    portsTxt.setAttribute('fill', '#34d399');
                    portsTxt.setAttribute('font-size', '11.5');
                    portsTxt.setAttribute('font-family', 'monospace');
                    portsTxt.textContent = portSummary || 'Aucun port exposé';
                    g.appendChild(portsTxt);

                    const statsTxt = document.createElementNS(SVG_NS, 'text');
                    statsTxt.setAttribute('x', '12');
                    statsTxt.setAttribute('y', '90');
                    statsTxt.setAttribute('fill', '#cbd5e1');
                    statsTxt.setAttribute('font-size', '10.5');
                    statsTxt.textContent = `IN (Entrants): ${n.inboundConnectionCount}  |  OUT (Sortants): ${n.outboundConnectionCount}`;
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

                [searchInput, includePortsInput, excludePortsInput, excludeHostsInput].forEach(el => {
                  el.addEventListener('input', () => renderGraph(true));
                });
                directionSelect.addEventListener('change', () => renderGraph(true));
                layoutSelect.addEventListener('change', () => renderGraph(true));
                sortSelect.addEventListener('change', () => renderGraph(true));
                routingSelect.addEventListener('change', () => renderGraph(false));

                spacingSelect.addEventListener('change', () => {
                  applySpacingPreset(spacingSelect.value);
                  renderGraph(true);
                });

                nodeGapRange.addEventListener('input', () => {
                  nodeGapVal.textContent = `${parseFloat(nodeGapRange.value).toFixed(2)}x`;
                  renderGraph(true);
                });

                edgeFanRange.addEventListener('input', () => {
                  edgeFanVal.textContent = `${edgeFanRange.value}px`;
                  renderGraph(false);
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
                  renderInspector(null, [], new Map());
                  renderGraph(true);
                });

                updateTransform();
                renderGraph(true);
              </script>
            """);
    }
}
