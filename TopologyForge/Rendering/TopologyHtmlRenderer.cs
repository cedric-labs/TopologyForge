using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TopologyForge.Models;

namespace TopologyForge.Rendering;

/// <summary>
/// Générateur de cartes topologiques HTML5 autonomes (Single-File, zéro dépendance externe) avec moteur de graphe interactif.
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
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(graph);

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var htmlContent = RenderHtml(graph, title);
        return File.WriteAllTextAsync(outputPath, htmlContent, Encoding.UTF8, cancellationToken);
    }

    /// <inheritdoc />
    public string RenderHtml(NetworkTopologyGraph graph, string title = "Cartographie des Flux & Serveurs")
    {
        ArgumentNullException.ThrowIfNull(graph);

        var safeTitle = EscapeHtmlText(string.IsNullOrWhiteSpace(title) ? "Cartographie des Flux & Serveurs" : title);
        var payloadJson = SerializeGraphPayload(graph);
        var generatedAtFormatted = graph.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

        var sb = new StringBuilder(32768);
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

    private static string SerializeGraphPayload(NetworkTopologyGraph graph)
    {
        var dto = new
        {
            GeneratedAtUtc = graph.GeneratedAtUtc.ToString("o", CultureInfo.InvariantCulture),
            graph.TotalProcessedFlows,
            graph.ExcludedFlowsCount,
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
                  --accent-edge: #64748b;
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
                  padding: 0.75rem 1.25rem;
                  display: flex;
                  align-items: center;
                  justify-content: space-between;
                  gap: 1rem;
                  flex-wrap: wrap;
                }
                .title-block h1 { font-size: 1.15rem; font-weight: 700; color: var(--text-primary); }
                .title-block span { font-size: 0.78rem; color: var(--text-muted); }
                .kpi-bar { display: flex; gap: 0.75rem; align-items: center; flex-wrap: wrap; }
                .kpi-badge {
                  background: var(--bg-card);
                  border: 1px solid var(--border-color);
                  border-radius: 6px;
                  padding: 0.35rem 0.7rem;
                  font-size: 0.8rem;
                }
                .kpi-badge strong { color: var(--accent-local); font-weight: 700; margin-left: 0.25rem; }
                .workspace {
                  display: flex;
                  flex: 1;
                  overflow: hidden;
                }
                .sidebar {
                  width: 330px;
                  background: var(--bg-panel);
                  border-right: 1px solid var(--border-color);
                  padding: 1rem;
                  display: flex;
                  flex-direction: column;
                  gap: 0.9rem;
                  overflow-y: auto;
                }
                .filter-group { display: flex; flex-direction: column; gap: 0.35rem; }
                .filter-group label { font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--text-muted); font-weight: 600; }
                .filter-group input, .filter-group select {
                  background: var(--bg-card);
                  border: 1px solid var(--border-color);
                  color: var(--text-primary);
                  padding: 0.45rem 0.6rem;
                  border-radius: 5px;
                  font-size: 0.85rem;
                }
                .btn-row { display: flex; gap: 0.4rem; flex-wrap: wrap; }
                button {
                  background: var(--bg-card);
                  border: 1px solid var(--border-color);
                  color: var(--text-primary);
                  padding: 0.4rem 0.65rem;
                  border-radius: 5px;
                  cursor: pointer;
                  font-size: 0.8rem;
                  transition: background 0.15s, border-color 0.15s;
                }
                button:hover { border-color: var(--accent-local); background: #26354d; }
                .canvas-area {
                  flex: 1;
                  position: relative;
                  background: radial-gradient(circle at center, #131c31 0%, #0b0f19 100%);
                  overflow: hidden;
                }
                #topologySvg { width: 100%; height: 100%; cursor: grab; }
                #topologySvg:active { cursor: grabbing; }
                .inspector {
                  border-top: 1px solid var(--border-color);
                  padding-top: 0.75rem;
                  font-size: 0.82rem;
                  display: flex;
                  flex-direction: column;
                  gap: 0.45rem;
                }
                .port-pill {
                  display: inline-block;
                  background: rgba(16, 185, 129, 0.15);
                  border: 1px solid var(--accent-port);
                  color: #6ee7b7;
                  border-radius: 4px;
                  padding: 0.12rem 0.4rem;
                  font-size: 0.74rem;
                  margin: 0.12rem;
                  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
                }
                .legend {
                  position: absolute;
                  bottom: 12px;
                  left: 12px;
                  background: rgba(17, 24, 39, 0.9);
                  border: 1px solid var(--border-color);
                  border-radius: 6px;
                  padding: 0.5rem 0.75rem;
                  font-size: 0.75rem;
                  display: flex;
                  gap: 1rem;
                }
                .legend-item { display: flex; align-items: center; gap: 0.4rem; }
                .dot { width: 10px; height: 10px; border-radius: 50%; display: inline-block; }
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
        sb.AppendLine("      <div class=\"filter-group\">");
        sb.AppendLine("        <label for=\"searchInput\">Recherche (Serveur, IP, Port, Processus)</label>");
        sb.AppendLine("        <input id=\"searchInput\" type=\"text\" placeholder=\"Ex: SRV-SQL, 1433, nginx...\" />");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"filter-group\">");
        sb.AppendLine("        <label for=\"includePortsInput\">Inclure uniquement ces ports</label>");
        sb.AppendLine("        <input id=\"includePortsInput\" type=\"text\" placeholder=\"Ex: 80, 443, 1433, 5432\" />");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"filter-group\">");
        sb.AppendLine("        <label for=\"excludePortsInput\">Exclure ces ports</label>");
        sb.AppendLine("        <input id=\"excludePortsInput\" type=\"text\" placeholder=\"Ex: 53, 123, 135\" />");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"filter-group\">");
        sb.AppendLine("        <label for=\"excludeHostsInput\">Exclure ces serveurs / IPs</label>");
        sb.AppendLine("        <input id=\"excludeHostsInput\" type=\"text\" placeholder=\"Ex: SRV-MON, 10.0.99.\" />");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"filter-group\">");
        sb.AppendLine("        <label for=\"layoutSelect\">Disposition Topologique</label>");
        sb.AppendLine("        <select id=\"layoutSelect\">");
        sb.AppendLine("          <option value=\"layered\">Hiérarchique (Sources ➔ Cibles)</option>");
        sb.AppendLine("          <option value=\"circular\">Anneau Concentrique</option>");
        sb.AppendLine("          <option value=\"grid\">Grille Structurée</option>");
        sb.AppendLine("        </select>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"btn-row\">");
        sb.AppendLine("        <button id=\"btnResetZoom\" type=\"button\">Centrer la vue</button>");
        sb.AppendLine("        <button id=\"btnClearFilters\" type=\"button\">Réinitialiser filtres</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"inspector\" id=\"inspectorPanel\">");
        sb.AppendLine("        <label style=\"font-size:0.75rem;text-transform:uppercase;color:var(--text-muted);font-weight:600;\">Détails de sélection</label>");
        sb.AppendLine("        <div id=\"inspectorContent\">Cliquez sur un serveur pour inspecter ses ports non éphémères et ses connexions distantes.</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </aside>");
        sb.AppendLine("    <main class=\"canvas-area\">");
        sb.AppendLine("      <svg id=\"topologySvg\">");
        sb.AppendLine("        <defs>");
        sb.AppendLine("          <marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\">");
        sb.AppendLine("            <path d=\"M 0 1 L 10 5 L 0 9 z\" fill=\"#64748b\" />");
        sb.AppendLine("          </marker>");
        sb.AppendLine("          <marker id=\"arrowHighlight\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\">");
        sb.AppendLine("            <path d=\"M 0 1 L 10 5 L 0 9 z\" fill=\"#f59e0b\" />");
        sb.AppendLine("          </marker>");
        sb.AppendLine("        </defs>");
        sb.AppendLine("        <g id=\"viewportGroup\"></g>");
        sb.AppendLine("      </svg>");
        sb.AppendLine("      <div class=\"legend\">");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"dot\" style=\"background:var(--accent-local)\"></span>Serveur Audité</div>");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"dot\" style=\"background:var(--accent-remote)\"></span>Serveur Distant</div>");
        sb.AppendLine("        <div class=\"legend-item\"><span class=\"dot\" style=\"background:var(--accent-port)\"></span>Port Non Éphémère</div>");
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
                const searchInput = document.getElementById('searchInput');
                const includePortsInput = document.getElementById('includePortsInput');
                const excludePortsInput = document.getElementById('excludePortsInput');
                const excludeHostsInput = document.getElementById('excludeHostsInput');
                const layoutSelect = document.getElementById('layoutSelect');
                const inspectorContent = document.getElementById('inspectorContent');

                let panX = 40, panY = 40, scale = 1.0;
                let isPanning = false, startPanX = 0, startPanY = 0;
                let selectedNodeId = null;
                let nodePositions = new Map();

                function parsePortList(str) {
                  if (!str || !str.trim()) return [];
                  return str.split(/[,;\s]+/).map(s => parseInt(s.trim(), 10)).filter(n => !isNaN(n) && n > 0);
                }

                function parseTextTokens(str) {
                  if (!str || !str.trim()) return [];
                  return str.split(/[,;]+/).map(s => s.trim().toLowerCase()).filter(Boolean);
                }

                function updateTransform() {
                  viewport.setAttribute('transform', `translate(${panX}, ${panY}) scale(${scale})`);
                }

                svg.addEventListener('wheel', (e) => {
                  e.preventDefault();
                  const factor = e.deltaY < 0 ? 1.12 : 0.89;
                  scale = Math.max(0.2, Math.min(3.0, scale * factor));
                  updateTransform();
                });

                svg.addEventListener('mousedown', (e) => {
                  if (e.target === svg) {
                    isPanning = true;
                    startPanX = e.clientX - panX;
                    startPanY = e.clientY - panY;
                  }
                });

                window.addEventListener('mousemove', (e) => {
                  if (isPanning) {
                    panX = e.clientX - startPanX;
                    panY = e.clientY - startPanY;
                    updateTransform();
                  }
                });

                window.addEventListener('mouseup', () => { isPanning = false; });

                function computeLayout(nodes, layoutMode) {
                  nodePositions.clear();
                  const width = Math.max(900, svg.clientWidth || 1100);
                  const height = Math.max(620, svg.clientHeight || 720);

                  if (layoutMode === 'circular') {
                    const cx = width / 2, cy = height / 2;
                    const radius = Math.min(width, height) * 0.36;
                    nodes.forEach((n, idx) => {
                      const angle = (2 * Math.PI * idx) / Math.max(1, nodes.length);
                      nodePositions.set(n.nodeId, { x: cx + radius * Math.cos(angle) - 110, y: cy + radius * Math.sin(angle) - 45 });
                    });
                    return;
                  }

                  if (layoutMode === 'grid') {
                    const cols = Math.max(1, Math.ceil(Math.sqrt(nodes.length)));
                    nodes.forEach((n, idx) => {
                      const col = idx % cols;
                      const row = Math.floor(idx / cols);
                      nodePositions.set(n.nodeId, { x: 60 + col * 290, y: 60 + row * 175 });
                    });
                    return;
                  }

                  const locals = nodes.filter(n => n.isLocalAuditedServer);
                  const remotes = nodes.filter(n => !n.isLocalAuditedServer);
                  locals.forEach((n, idx) => {
                    nodePositions.set(n.nodeId, { x: 80, y: 70 + idx * 175 });
                  });
                  remotes.forEach((n, idx) => {
                    const col = Math.floor(idx / 5);
                    const row = idx % 5;
                    nodePositions.set(n.nodeId, { x: 460 + col * 310, y: 60 + row * 155 });
                  });
                }

                function filterGraph() {
                  const q = (searchInput.value || '').trim().toLowerCase();
                  const incPorts = parsePortList(includePortsInput.value);
                  const excPorts = parsePortList(excludePortsInput.value);
                  const excHosts = parseTextTokens(excludeHostsInput.value);

                  const filteredEdges = TOPOLOGY_DATA.edges.filter(e => {
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
                    if (incPorts.length > 0) {
                      const hasPort = n.ports.some(p => incPorts.includes(p.port));
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
                    inspectorContent.innerHTML = 'Cliquez sur un serveur pour inspecter ses ports non éphémères et ses connexions distantes.';
                    return;
                  }
                  const portsHtml = node.ports.length > 0
                    ? node.ports.map(p => `<span class="port-pill">:${p.port}/${p.protocol}${p.processName ? ' (' + p.processName + ')' : ''}</span>`).join(' ')
                    : '<span style="color:var(--text-muted)">Aucun port d\'écoute direct</span>';

                  const outbound = edges.filter(e => e.sourceNodeId === node.nodeId);
                  const inbound = edges.filter(e => e.targetNodeId === node.nodeId);

                  const outHtml = outbound.map(e => {
                    const target = allNodesMap.get(e.targetNodeId);
                    return `<div>➔ <strong>${target ? target.hostName : e.targetNodeId}</strong> :${e.targetPort}/${e.protocol} (${e.processName || 'flux'})</div>`;
                  }).join('');

                  const inHtml = inbound.map(e => {
                    const source = allNodesMap.get(e.sourceNodeId);
                    return `<div>⬅ <strong>${source ? source.hostName : e.sourceNodeId}</strong> vers :${e.targetPort}/${e.protocol}</div>`;
                  }).join('');

                  inspectorContent.innerHTML = `
                    <div><strong>${node.hostName}</strong> (${node.ipAddress || 'IP N/A'})</div>
                    <div style="margin-top:4px;color:var(--text-muted)">Ports non éphémères :</div>
                    <div>${portsHtml}</div>
                    <div style="margin-top:6px;color:var(--text-muted)">Sortants (${outbound.length}) :</div>
                    <div>${outHtml || 'Aucun'}</div>
                    <div style="margin-top:6px;color:var(--text-muted)">Entrants (${inbound.length}) :</div>
                    <div>${inHtml || 'Aucun'}</div>
                  `;
                }

                function renderGraph(recomputePositions = false) {
                  const { nodes, edges } = filterGraph();
                  const nodesMap = new Map(nodes.map(n => [n.nodeId, n]));
                  document.getElementById('kpiNodes').textContent = String(nodes.length);
                  document.getElementById('kpiEdges').textContent = String(edges.length);

                  if (recomputePositions || nodePositions.size === 0) {
                    computeLayout(nodes, layoutSelect.value);
                  }

                  viewport.innerHTML = '';
                  const NODE_W = 230, NODE_H = 105;

                  edges.forEach(e => {
                    const p1 = nodePositions.get(e.sourceNodeId);
                    const p2 = nodePositions.get(e.targetNodeId);
                    if (!p1 || !p2) return;

                    const isHighlighted = selectedNodeId && (e.sourceNodeId === selectedNodeId || e.targetNodeId === selectedNodeId);
                    const x1 = p1.x + NODE_W / 2, y1 = p1.y + NODE_H / 2;
                    const x2 = p2.x + NODE_W / 2, y2 = p2.y + NODE_H / 2;

                    const line = document.createElementNS(SVG_NS, 'line');
                    line.setAttribute('x1', String(x1));
                    line.setAttribute('y1', String(y1));
                    line.setAttribute('x2', String(x2));
                    line.setAttribute('y2', String(y2));
                    line.setAttribute('stroke', isHighlighted ? '#f59e0b' : '#475569');
                    line.setAttribute('stroke-width', isHighlighted ? '2.5' : '1.6');
                    line.setAttribute('marker-end', isHighlighted ? 'url(#arrowHighlight)' : 'url(#arrow)');
                    viewport.appendChild(line);

                    const mx = (x1 + x2) / 2, my = (y1 + y2) / 2 - 6;
                    const label = document.createElementNS(SVG_NS, 'text');
                    label.setAttribute('x', String(mx));
                    label.setAttribute('y', String(my));
                    label.setAttribute('text-anchor', 'middle');
                    label.setAttribute('fill', isHighlighted ? '#fbbf24' : '#cbd5e1');
                    label.setAttribute('font-size', '11');
                    label.textContent = `:${e.targetPort}/${e.protocol}`;
                    viewport.appendChild(label);
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
                    title.setAttribute('y', '24');
                    title.setAttribute('fill', '#f8fafc');
                    title.setAttribute('font-weight', 'bold');
                    title.setAttribute('font-size', '13');
                    title.textContent = n.hostName.length > 25 ? n.hostName.substring(0, 24) + '…' : n.hostName;
                    g.appendChild(title);

                    const sub = document.createElementNS(SVG_NS, 'text');
                    sub.setAttribute('x', '12');
                    sub.setAttribute('y', '42');
                    sub.setAttribute('fill', '#94a3b8');
                    sub.setAttribute('font-size', '11');
                    sub.textContent = `${n.ipAddress || 'IP N/A'} • ${n.isLocalAuditedServer ? 'Serveur Audité' : 'Serveur Distant'}`;
                    g.appendChild(sub);

                    const portSummary = n.ports.slice(0, 5).map(p => `:${p.port}`).join(' ') + (n.ports.length > 5 ? ` +${n.ports.length - 5}` : '');
                    const portsTxt = document.createElementNS(SVG_NS, 'text');
                    portsTxt.setAttribute('x', '12');
                    portsTxt.setAttribute('y', '68');
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
                    statsTxt.textContent = `Entrants: ${n.inboundConnectionCount} | Sortants: ${n.outboundConnectionCount}`;
                    g.appendChild(statsTxt);

                    g.addEventListener('click', (ev) => {
                      ev.stopPropagation();
                      selectedNodeId = selectedNodeId === n.nodeId ? null : n.nodeId;
                      renderInspector(selectedNodeId ? n : null, edges, nodesMap);
                      renderGraph(false);
                    });

                    viewport.appendChild(g);
                  });
                }

                [searchInput, includePortsInput, excludePortsInput, excludeHostsInput].forEach(el => {
                  el.addEventListener('input', () => renderGraph(true));
                });
                layoutSelect.addEventListener('change', () => renderGraph(true));
                document.getElementById('btnResetZoom').addEventListener('click', () => {
                  panX = 40; panY = 40; scale = 1.0; updateTransform();
                });
                document.getElementById('btnClearFilters').addEventListener('click', () => {
                  searchInput.value = '';
                  includePortsInput.value = '';
                  excludePortsInput.value = '';
                  excludeHostsInput.value = '';
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
