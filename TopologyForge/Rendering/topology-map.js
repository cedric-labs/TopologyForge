const svg = document.getElementById('topologySvg');
const SVG_NS = svg.namespaceURI;
const viewport = document.getElementById('viewportGroup');
const zonesLayer = document.getElementById('zonesLayer');
const edgesLayer = document.getElementById('edgesLayer');
const labelsLayer = document.getElementById('labelsLayer');
const nodesLayer = document.getElementById('nodesLayer');

const layoutSelect = document.getElementById('layoutSelect');
const sortSelect = document.getElementById('sortSelect');
const showZonesCheck = document.getElementById('showZonesCheck');
const showPortsCheck = document.getElementById('showPortsCheck');
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
// Deux boîtes empilées gardent entre elles la hauteur de trois lignes d'étiquettes, quel que soit l'espacement :
// deux étiquettes voisines doivent pouvoir s'y superposer, sinon l'une d'elles ne trouve aucune place libre
// et finit masquée par une boîte.
const STACKED_NODE_MIN_GAP = 42;
// Sans étiquettes de ports, l'écart ne sert plus qu'au passage des flèches et de leurs lignes de séparation en bus.
const STACKED_NODE_MIN_GAP_WITHOUT_LABELS = 24;

function arePortLabelsShown() {
  return !showPortsCheck || showPortsCheck.checked;
}

function rowStep(gapFactor) {
  const minGap = arePortLabelsShown() ? STACKED_NODE_MIN_GAP : STACKED_NODE_MIN_GAP_WITHOUT_LABELS;
  return Math.max(Math.round(162 * gapFactor), NODE_H + minGap);
}
let panX = 50, panY = 50, scale = 1.0;
let isPanning = false, startPanX = 0, startPanY = 0;
let draggingNodeId = null, dragOffsetX = 0, dragOffsetY = 0, hasDragged = false;
let selectedNodeId = null;
let nodePositions = new Map();
let currentPageIndex = 0;
let currentPageSize = TOPOLOGY_DATA.defaultPageSize || 100;

if (TOPOLOGY_DATA.defaultLayout) layoutSelect.value = TOPOLOGY_DATA.defaultLayout;
if (TOPOLOGY_DATA.defaultSort) {
  sortSelect.value = TOPOLOGY_DATA.defaultSort;
  if (sortSelect.value === 'subnet' && showZonesCheck) {
    showZonesCheck.checked = true;
  }
}
if (TOPOLOGY_DATA.defaultRouting) routingSelect.value = TOPOLOGY_DATA.defaultRouting;
if (showPortsCheck) showPortsCheck.checked = TOPOLOGY_DATA.defaultEdgeLabels !== 'none';
if (TOPOLOGY_DATA.defaultSpacing) {
  spacingSelect.value = TOPOLOGY_DATA.defaultSpacing;
  applySpacingPreset(TOPOLOGY_DATA.defaultSpacing);
}
if (TOPOLOGY_DATA.initialIncludePorts) includePortsInput.value = TOPOLOGY_DATA.initialIncludePorts;
if (TOPOLOGY_DATA.initialExcludePorts) excludePortsInput.value = TOPOLOGY_DATA.initialExcludePorts;
if (TOPOLOGY_DATA.initialExcludeHosts) excludeHostsInput.value = TOPOLOGY_DATA.initialExcludeHosts;
if (TOPOLOGY_DATA.initialDirection) directionSelect.value = TOPOLOGY_DATA.initialDirection;

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
  nodeGapVal.textContent = `${Number.parseFloat(nodeGapRange.value).toFixed(2)}x`;
  edgeFanVal.textContent = `${edgeFanRange.value}px`;
}

applySpacingPreset(spacingSelect.value);

// Accepte les ports isolés et les plages « 8000-8080 », comme l'option CLI qui alimente les filtres initiaux.
function parsePortList(str) {
  if (!str || !str.trim()) return [];
  return str.split(/[,;\s]+/).map(token => {
    const bounds = token.split('-').map(b => Number.parseInt(b.trim(), 10));
    if (bounds.length === 1) return { from: bounds[0], to: bounds[0] };
    if (bounds.length === 2) return { from: Math.min(bounds[0], bounds[1]), to: Math.max(bounds[0], bounds[1]) };
    return null;
  }).filter(r => r && !Number.isNaN(r.from) && !Number.isNaN(r.to) && r.from > 0);
}

// Les noms d'hôtes (DNS inverse) et de processus viennent de l'extérieur : tout ce qui part dans innerHTML passe ici.
function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function buildPortFilter() {
  const included = parsePortList(includePortsInput.value);
  const excluded = parsePortList(excludePortsInput.value);
  const inRanges = (ranges, port) => ranges.some(r => port >= r.from && port <= r.to);
  return {
    isActive: included.length > 0 || excluded.length > 0,
    allows: port => !inRanges(excluded, port) && (included.length === 0 || inRanges(included, port))
  };
}

function parseTextTokens(str) {
  if (!str || !str.trim()) return [];
  return str.split(/[,;]+/).map(s => s.trim().toLowerCase()).filter(Boolean);
}

function matchesPattern(text, pattern) {
  if (!pattern || !text) return false;
  const p = pattern.trim().toLowerCase();
  const t = text.trim().toLowerCase();
  if (p === '*' || p === '') return true;
  if (p.includes('*') || p.includes('?')) {
    const escaped = p.replace(/[.+^${}()|[\]\\]/g, '\\$&')
                     .replace(/\*/g, '.*')
                     .replace(/\?/g, '.');
    return new RegExp('^' + escaped + '$', 'i').test(t);
  }
  return t.includes(p);
}

function ipToSortableNumber(ip) {
  if (!ip) return Number.MAX_SAFE_INTEGER;
  const parts = ip.trim().split('.');
  if (parts.length !== 4) return Number.MAX_SAFE_INTEGER - 1;
  return parts.reduce((acc, p) => (acc * 256) + (Number.parseInt(p, 10) || 0), 0);
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

// Functional Tier Classifier (Clients -> Ingress -> Apps -> Infra/DB)
function classifyFunctionalTier(node) {
  const inCount = node.inboundConnectionCount || 0;
  const outCount = node.outboundConnectionCount || 0;
  const dbPorts = [1433, 1521, 3306, 5432, 6379, 27017, 9200, 445, 2049, 389, 636];
  const webPorts = [80, 443, 8080, 8443, 53];
  const ports = node.ports || [];
  const hasDbPort = ports.some(p => dbPorts.includes(p.port));
  const hasWebPort = ports.some(p => webPorts.includes(p.port));

  if (hasDbPort || (inCount > 0 && outCount === 0) || (inCount >= 4 && inCount > outCount * 2.5)) {
    return 3; // Databases, backend storage & infrastructure sinks
  }
  if ((outCount > 0 && inCount === 0) || (outCount >= 3 && outCount > inCount * 2.5 && !hasWebPort)) {
    return 0; // Pure clients / consumers
  }
  if (hasWebPort || (inCount > 0 && outCount > 0 && outCount >= inCount)) {
    return 1; // Ingress, web entrypoints & gateways
  }
  return 2; // Application services
}

// Subnet and Security Zone Helpers
function extractSubnetPrefix(ip) {
  if (!ip || typeof ip !== 'string') return 'External / Unknown';
  const trimmed = ip.trim();
  if (trimmed.startsWith('127.')) return '127.0.0.0/8 (Loopback)';
  const parts = trimmed.split('.');
  if (parts.length === 4 && parts.every(p => !Number.isNaN(Number.parseInt(p, 10)))) {
    return `${parts[0]}.${parts[1]}.${parts[2]}.0/24`;
  }
  return 'Unassigned Subnet';
}

function computeSubnetZones(nodes) {
  const subnetMap = new Map();
  nodes.forEach(n => {
    const subnet = extractSubnetPrefix(n.ipAddress);
    if (!subnetMap.has(subnet)) subnetMap.set(subnet, []);
    subnetMap.get(subnet).push(n);
  });

  const zones = [];
  subnetMap.forEach((subNodes, subnet) => {
    if (subNodes.length === 0) return;
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    subNodes.forEach(n => {
      const pos = nodePositions.get(n.nodeId);
      if (!pos) return;
      minX = Math.min(minX, pos.x);
      minY = Math.min(minY, pos.y);
      maxX = Math.max(maxX, pos.x + NODE_W);
      maxY = Math.max(maxY, pos.y + NODE_H);
    });
    if (Number.isFinite(minX) && Number.isFinite(minY)) {
      zones.push({
        subnet,
        nodeCount: subNodes.length,
        x: minX - 22,
        y: minY - 28,
        w: (maxX - minX) + 44,
        h: (maxY - minY) + 42
      });
    }
  });
  return zones;
}

// Spatial Hash Grid for fast candidate pruning in 2-Opt and collision detection
class SpatialHashGrid {
  constructor(cellSize = 250) {
    this.cellSize = cellSize;
    this.cells = new Map();
  }
  key(gx, gy) {
    return `${gx}:${gy}`;
  }
  insertBox(node, pos) {
    const minGx = Math.floor((pos.x - 12) / this.cellSize);
    const maxGx = Math.floor((pos.x + NODE_W + 12) / this.cellSize);
    const minGy = Math.floor((pos.y - 12) / this.cellSize);
    const maxGy = Math.floor((pos.y + NODE_H + 12) / this.cellSize);
    for (let gx = minGx; gx <= maxGx; gx++) {
      for (let gy = minGy; gy <= maxGy; gy++) {
        const k = this.key(gx, gy);
        let cell = this.cells.get(k);
        if (!cell) { cell = { boxes: [], segs: [] }; this.cells.set(k, cell); }
        cell.boxes.push(node);
      }
    }
  }
  insertSegment(seg) {
    const minGx = Math.floor(Math.min(seg.x1, seg.x2) / this.cellSize);
    const maxGx = Math.floor(Math.max(seg.x1, seg.x2) / this.cellSize);
    const minGy = Math.floor(Math.min(seg.y1, seg.y2) / this.cellSize);
    const maxGy = Math.floor(Math.max(seg.y1, seg.y2) / this.cellSize);
    for (let gx = minGx; gx <= maxGx; gx++) {
      for (let gy = minGy; gy <= maxGy; gy++) {
        const k = this.key(gx, gy);
        let cell = this.cells.get(k);
        if (!cell) { cell = { boxes: [], segs: [] }; this.cells.set(k, cell); }
        cell.segs.push(seg);
      }
    }
  }
  getCandidateBoxes(seg) {
    const minGx = Math.floor(Math.min(seg.x1, seg.x2) / this.cellSize);
    const maxGx = Math.floor(Math.max(seg.x1, seg.x2) / this.cellSize);
    const minGy = Math.floor(Math.min(seg.y1, seg.y2) / this.cellSize);
    const maxGy = Math.floor(Math.max(seg.y1, seg.y2) / this.cellSize);
    const set = new Set();
    for (let gx = minGx; gx <= maxGx; gx++) {
      for (let gy = minGy; gy <= maxGy; gy++) {
        const cell = this.cells.get(this.key(gx, gy));
        if (cell) {
          for (let i = 0; i < cell.boxes.length; i++) set.add(cell.boxes[i]);
        }
      }
    }
    return set;
  }
  getCandidateSegments(seg) {
    const minGx = Math.floor(Math.min(seg.x1, seg.x2) / this.cellSize);
    const maxGx = Math.floor(Math.max(seg.x1, seg.x2) / this.cellSize);
    const minGy = Math.floor(Math.min(seg.y1, seg.y2) / this.cellSize);
    const maxGy = Math.floor(Math.max(seg.y1, seg.y2) / this.cellSize);
    const set = new Set();
    for (let gx = minGx; gx <= maxGx; gx++) {
      for (let gy = minGy; gy <= maxGy; gy++) {
        const cell = this.cells.get(this.key(gx, gy));
        if (cell) {
          for (let i = 0; i < cell.segs.length; i++) {
            const other = cell.segs[i];
            if (other.index > seg.index) set.add(other);
          }
        }
      }
    }
    return set;
  }
}

// Pass 2: Multi-pass alternating Barycenter, Functional Tiers & Subnet Sweep
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
  if (sortMethod === 'tiers') {
    const t0 = copy.filter(n => classifyFunctionalTier(n) === 0);
    const t1 = copy.filter(n => classifyFunctionalTier(n) === 1);
    const t2 = copy.filter(n => classifyFunctionalTier(n) === 2);
    const t3 = copy.filter(n => classifyFunctionalTier(n) === 3);
    const sortByDegree = (arr) => arr.sort((a, b) => degreeOf(b) - degreeOf(a) || a.hostName.localeCompare(b.hostName));
    return [...sortByDegree(t0), ...sortByDegree(t1), ...sortByDegree(t2), ...sortByDegree(t3)];
  }
  if (sortMethod === 'subnet') {
    const subnetMap = new Map();
    copy.forEach(n => {
      const sub = extractSubnetPrefix(n.ipAddress);
      if (!subnetMap.has(sub)) subnetMap.set(sub, []);
      subnetMap.get(sub).push(n);
    });
    const sortedSubnets = Array.from(subnetMap.keys()).sort((a, b) => {
      const aHasLocal = subnetMap.get(a).some(n => n.isLocalAuditedServer);
      const bHasLocal = subnetMap.get(b).some(n => n.isLocalAuditedServer);
      if (aHasLocal && !bHasLocal) return -1;
      if (!aHasLocal && bHasLocal) return 1;
      return a.localeCompare(b);
    });
    const res = [];
    sortedSubnets.forEach(sub => {
      const group = subnetMap.get(sub).sort((a, b) => ipToSortableNumber(a.ipAddress) - ipToSortableNumber(b.ipAddress) || a.hostName.localeCompare(b.hostName));
      res.push(...group);
    });
    return res;
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

// Rognage de Liang-Barsky : tester seulement le point du segment le plus proche du centre ne suffit pas,
// car sur une boîte plus large que haute un segment peut couper un coin sans que ce point y soit.
function segmentIntersectsBox(x1, y1, x2, y2, boxPos, pad) {
  const dx = x2 - x1, dy = y2 - y1;
  const boundaries = [
    [-dx, x1 - (boxPos.x - pad)],
    [dx, (boxPos.x + NODE_W + pad) - x1],
    [-dy, y1 - (boxPos.y - pad)],
    [dy, (boxPos.y + NODE_H + pad) - y1]
  ];
  let tEnter = 0, tExit = 1;
  for (const [p, q] of boundaries) {
    if (p === 0) {
      if (q < 0) return false;
      continue;
    }
    const t = q / p;
    if (p < 0) tEnter = Math.max(tEnter, t);
    else tExit = Math.min(tExit, t);
    if (tEnter > tExit) return false;
  }
  return true;
}

// Clé commune aux deux sens d'une paire de serveurs, pour regrouper les flux A→B et B→A. La comparaison
// ordinale reproduit l'ordre du tri par défaut des chaînes, utilisé jusqu'ici.
function undirectedPairKey(firstNodeId, secondNodeId) {
  return firstNodeId < secondNodeId
    ? `${firstNodeId}::${secondNodeId}`
    : `${secondNodeId}::${firstNodeId}`;
}

function computeLayoutCost(nodes, edges) {
  let crossings = 0;
  let boxCollisions = 0;
  let lengthPenalty = 0;

  const uniquePairs = [];
  const seenPairs = new Set();
  edges.forEach(e => {
    const key = undirectedPairKey(e.sourceNodeId, e.targetNodeId);
    if (!seenPairs.has(key) && e.sourceNodeId !== e.targetNodeId) {
      seenPairs.add(key);
      uniquePairs.push({ u: e.sourceNodeId, v: e.targetNodeId });
    }
  });

  const grid = new SpatialHashGrid(260);
  nodes.forEach(n => {
    const pos = nodePositions.get(n.nodeId);
    if (pos) grid.insertBox(n, pos);
  });

  const segs = [];
  for (let i = 0; i < uniquePairs.length; i++) {
    const pair = uniquePairs[i];
    const p1 = nodePositions.get(pair.u);
    const p2 = nodePositions.get(pair.v);
    if (!p1 || !p2) continue;
    const x1 = p1.x + NODE_W / 2, y1 = p1.y + NODE_H / 2;
    const x2 = p2.x + NODE_W / 2, y2 = p2.y + NODE_H / 2;
    const seg = { index: i, u: pair.u, v: pair.v, x1, y1, x2, y2 };
    segs.push(seg);
    grid.insertSegment(seg);
    lengthPenalty += Math.hypot(x2 - x1, y2 - y1) * 0.005;

    const candidateBoxes = grid.getCandidateBoxes(seg);
    candidateBoxes.forEach(n => {
      if (n.nodeId === pair.u || n.nodeId === pair.v) return;
      const nPos = nodePositions.get(n.nodeId);
      if (nPos && segmentIntersectsBox(x1, y1, x2, y2, nPos, 12)) {
        boxCollisions++;
      }
    });
  }

  for (let i = 0; i < segs.length; i++) {
    const s1 = segs[i];
    const candidateSegs = grid.getCandidateSegments(s1);
    candidateSegs.forEach(s2 => {
      if (s1.u === s2.u || s1.u === s2.v || s1.v === s2.u || s1.v === s2.v) return;
      if (segmentsIntersect(s1.x1, s1.y1, s1.x2, s1.y2, s2.x1, s2.y1, s2.x2, s2.y2)) {
        crossings++;
      }
    });
  }

  return boxCollisions * 120 + crossings * 45 + lengthPenalty;
}

// Pass 3: Exact 2-Opt Swap Optimizer accelerated by Spatial Hash Grid & Bounded Windowing
function optimizeNodePositions2Opt(nodes, edges, swapGroupKey = () => '') {
  if (nodes.length <= 2 || edges.length === 0 || nodes.length > 160) return;

  const groupsByKey = new Map();
  nodes.forEach(n => {
    const key = `${n.isLocalAuditedServer}::${swapGroupKey(n)}`;
    if (!groupsByKey.has(key)) groupsByKey.set(key, []);
    groupsByKey.get(key).push(n);
  });
  const swapGroups = Array.from(groupsByKey.values());

  let currentCost = computeLayoutCost(nodes, edges);
  const maxPasses = nodes.length <= 35 ? 12 : (nodes.length <= 70 ? 4 : 2);
  const maxWindow = nodes.length <= 35 ? nodes.length : (nodes.length <= 70 ? 12 : 6);

  for (let pass = 0; pass < maxPasses; pass++) {
    let improved = false;
    for (let g = 0; g < swapGroups.length; g++) {
      const group = swapGroups[g];
      if (group.length <= 1) continue;
      for (let i = 0; i < group.length - 1; i++) {
        const limitJ = Math.min(group.length, i + maxWindow);
        for (let j = i + 1; j < limitJ; j++) {
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
  const minSepY = Math.max(Math.round((NODE_H + 40) * Math.max(0.85, gapFactor * 0.9)), NODE_H + STACKED_NODE_MIN_GAP);

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

// Organic Force-Directed Layout Simulation (Hooke springs + Coulomb repulsion)
function simulateForceDirectedLayout(nodes, edges, gapFactor) {
  if (nodes.length === 0) return;
  const width = Math.max(1200, svg.clientWidth || 1400);
  const height = Math.max(800, svg.clientHeight || 900);
  const cx = width / 2, cy = height / 2;

  // Les serveurs audités restent fixes pendant la simulation : posés tous au centre, ils resteraient superposés.
  // Ils sont donc répartis sur un cercle dont la corde entre deux voisins laisse la place d'une boîte.
  const locals = nodes.filter(n => n.isLocalAuditedServer);
  const localRadius = locals.length > 1 ? ((NODE_W + 80) * gapFactor) / (2 * Math.sin(Math.PI / locals.length)) : 0;

  nodes.forEach((n, idx) => {
    if (n.isLocalAuditedServer) {
      const angle = (2 * Math.PI * locals.indexOf(n)) / Math.max(1, locals.length);
      nodePositions.set(n.nodeId, {
        x: Math.round(cx + localRadius * Math.cos(angle) - NODE_W / 2),
        y: Math.round(cy + localRadius * Math.sin(angle) - NODE_H / 2),
        vx: 0,
        vy: 0
      });
    } else {
      const angle = (2 * Math.PI * idx) / Math.max(1, nodes.length);
      const r = 260 * gapFactor + Math.sqrt(idx) * 60 * gapFactor;
      nodePositions.set(n.nodeId, {
        x: Math.round(cx + r * Math.cos(angle) - NODE_W / 2),
        y: Math.round(cy + r * Math.sin(angle) - NODE_H / 2),
        vx: 0,
        vy: 0
      });
    }
  });

  const idealDist = Math.max(220, 260 * gapFactor);
  const kSpring = 0.042;
  const kRepulsion = 180000 * Math.pow(gapFactor, 1.5);
  const kGravity = 0.024;
  const iterations = 140;

  const edgeWeights = new Map();
  edges.forEach(e => {
    const key = undirectedPairKey(e.sourceNodeId, e.targetNodeId);
    const count = (edgeWeights.get(key) || 0) + (e.observationCount || 1);
    edgeWeights.set(key, count);
  });

  const nodeLookup = new Map(nodes.map(n => [n.nodeId, n]));

  for (let step = 0; step < iterations; step++) {
    const temp = 1.0 - (step / iterations);

    for (let i = 0; i < nodes.length; i++) {
      const pA = nodePositions.get(nodes[i].nodeId);
      if (!pA) continue;
      const ax = pA.x + NODE_W / 2, ay = pA.y + NODE_H / 2;

      for (let j = i + 1; j < nodes.length; j++) {
        const pB = nodePositions.get(nodes[j].nodeId);
        if (!pB) continue;
        const bx = pB.x + NODE_W / 2, by = pB.y + NODE_H / 2;
        const dx = bx - ax, dy = by - ay;
        const distSq = dx * dx + dy * dy || 1;
        const dist = Math.sqrt(distSq);

        const force = kRepulsion / distSq;
        const fx = (dx / dist) * force;
        const fy = (dy / dist) * force;

        if (!nodes[i].isLocalAuditedServer) { pA.vx -= fx; pA.vy -= fy; }
        if (!nodes[j].isLocalAuditedServer) { pB.vx += fx; pB.vy += fy; }
      }

      if (!nodes[i].isLocalAuditedServer) {
        pA.vx += (cx - ax) * kGravity;
        pA.vy += (cy - ay) * kGravity;
      }
    }

    edgeWeights.forEach((weight, pairKey) => {
      const [idA, idB] = pairKey.split('::');
      const pA = nodePositions.get(idA);
      const pB = nodePositions.get(idB);
      if (!pA || !pB) return;

      const ax = pA.x + NODE_W / 2, ay = pA.y + NODE_H / 2;
      const bx = pB.x + NODE_W / 2, by = pB.y + NODE_H / 2;
      const dx = bx - ax, dy = by - ay;
      const dist = Math.hypot(dx, dy) || 1;

      const targetD = idealDist / Math.min(1.8, Math.log10(weight + 9));
      const displacement = dist - targetD;
      const force = displacement * kSpring;
      const fx = (dx / dist) * force;
      const fy = (dy / dist) * force;

      const nA = nodeLookup.get(idA);
      const nB = nodeLookup.get(idB);
      if (nA && !nA.isLocalAuditedServer) { pA.vx += fx; pA.vy += fy; }
      if (nB && !nB.isLocalAuditedServer) { pB.vx += fx; pB.vy += fy; }
    });

    const maxStep = 45 * temp;
    nodes.forEach(n => {
      if (n.isLocalAuditedServer) return;
      const p = nodePositions.get(n.nodeId);
      if (!p) return;
      const v = Math.hypot(p.vx, p.vy);
      if (v > 0.001) {
        const move = Math.min(v * 0.45, maxStep);
        p.x += Math.round((p.vx / v) * move);
        p.y += Math.round((p.vy / v) * move);
      }
      p.vx *= 0.5;
      p.vy *= 0.5;
    });
  }

  applyForceRepulsionPolish(nodes, edges, gapFactor);
  optimizeNodePositions2Opt(nodes, edges);
}

// Une colonne avance de 350 px et une rangée de 162 px : pour qu'une matrice de N serveurs garde à peu près
// les proportions d'un écran 16:9, elle compte environ 0,83 colonne par rangée, soit √(0,83 N) colonnes.
const TREE_COLUMNS_PER_ROW = 0.83;
// Les écarts sous les serveurs audités et entre deux bandes de sous-réseaux suivent l'espacement. Au plus serré
// (facteur 0,65, rangées écartées de 24 px), une bande reste à 60 px de la suivante : assez pour les 42 px de marge
// que prend la zone de sous-réseau autour de ses boîtes.
const TREE_ROOT_GAP = 40;
const TREE_BAND_GAP = 56;

function treeColumnCount(serverCount) {
  return Math.ceil(Math.sqrt(TREE_COLUMNS_PER_ROW * serverCount));
}

function layoutTree(ordered, edges, gapFactor, isHorizontal) {
  const stepX = Math.round(350 * gapFactor);
  const stepY = rowStep(gapFactor);

  const inDegree = new Map();
  const outDegree = new Map();
  ordered.forEach(n => { inDegree.set(n.nodeId, 0); outDegree.set(n.nodeId, 0); });
  edges.forEach(e => {
    outDegree.set(e.sourceNodeId, (outDegree.get(e.sourceNodeId) || 0) + 1);
    inDegree.set(e.targetNodeId, (inDegree.get(e.targetNodeId) || 0) + 1);
  });

  const roots = ordered.filter(n => n.isLocalAuditedServer);
  if (roots.length === 0) {
    const zeroIn = ordered.filter(n => (inDegree.get(n.nodeId) || 0) === 0 && (outDegree.get(n.nodeId) || 0) > 0);
    if (zeroIn.length > 0) {
      roots.push(...zeroIn.slice(0, 3));
    } else {
      const sortedByOut = [...ordered].sort((a, b) => (outDegree.get(b.nodeId) || 0) - (outDegree.get(a.nodeId) || 0));
      roots.push(sortedByOut[0]);
    }
  }

  const rootSet = new Set(roots.map(r => r.nodeId));
  const targets = ordered.filter(n => !rootSet.has(n.nodeId));

  if (targets.length === 0) {
    roots.forEach((r, idx) => {
      nodePositions.set(r.nodeId, isHorizontal ? { x: 60, y: 60 + idx * stepY } : { x: 60 + idx * stepX, y: 60 });
    });
    return;
  }

  const subnetMap = new Map();
  targets.forEach(n => {
    const sub = extractSubnetPrefix(n.ipAddress);
    if (!subnetMap.has(sub)) subnetMap.set(sub, []);
    subnetMap.get(sub).push(n);
  });

  // Sort subnets: largest subnets first, then alphabetical
  const sortedSubnets = Array.from(subnetMap.entries()).sort((a, b) => {
    if (b[1].length !== a[1].length) return b[1].length - a[1].length;
    return a[0].localeCompare(b[0]);
  });

  if (!isHorizontal) {
    // Strictly bound max columns across the screen so targets wrap cleanly in row bands
    const MAX_COLS = targets.length <= 4 ? Math.max(2, targets.length) : (targets.length <= 9 ? 3 : Math.max(4, treeColumnCount(targets.length)));

    let currentBandY = 60 + stepY + Math.round(TREE_ROOT_GAP * gapFactor);
    let currentBandCol = 0;
    let maxRowsInCurrentBand = 0;
    let maxPlacedX = 60;

    sortedSubnets.forEach(([subnet, subNodes]) => {
      let colsInSub = subNodes.length <= 2 ? 1 : (subNodes.length > 8 ? Math.max(3, treeColumnCount(subNodes.length)) : 2);
      if (colsInSub > MAX_COLS) colsInSub = MAX_COLS;

      // Wrap to next horizontal band if this subnet exceeds remaining columns
      if (currentBandCol + colsInSub > MAX_COLS && currentBandCol > 0) {
        currentBandY += maxRowsInCurrentBand * stepY + Math.round(TREE_BAND_GAP * gapFactor);
        currentBandCol = 0;
        maxRowsInCurrentBand = 0;
      }

      const rowsInSub = Math.ceil(subNodes.length / colsInSub);
      const subStartX = 60 + currentBandCol * stepX;
      const subStartY = currentBandY;

      subNodes.forEach((n, idx) => {
        const col = Math.floor(idx / rowsInSub);
        const row = idx % rowsInSub;
        const px = subStartX + col * stepX;
        const py = subStartY + row * stepY;
        nodePositions.set(n.nodeId, { x: px, y: py });
        maxPlacedX = Math.max(maxPlacedX, px + NODE_W);
      });

      currentBandCol += colsInSub;
      maxRowsInCurrentBand = Math.max(maxRowsInCurrentBand, rowsInSub);
    });

    // Center root(s) horizontally above the children matrix
    const totalChildrenWidth = maxPlacedX - 60;
    const totalRootsWidth = (roots.length - 1) * (stepX + 40) + NODE_W;
    const rootStartX = Math.max(60, 60 + Math.round((totalChildrenWidth - totalRootsWidth) / 2));
    roots.forEach((r, idx) => {
      nodePositions.set(r.nodeId, {
        x: rootStartX + idx * (stepX + 40),
        y: 60
      });
    });
  } else {
    // Transposée de la disposition verticale : les rangées jouent le rôle des colonnes, avec les mêmes écarts.
    const MAX_ROWS = targets.length <= 4 ? Math.max(2, targets.length) : (targets.length <= 9 ? 3 : Math.max(4, treeColumnCount(targets.length)));

    let currentBandX = 60 + stepX + Math.round(TREE_ROOT_GAP * gapFactor);
    let currentBandRow = 0;
    let maxColsInCurrentBand = 0;
    let maxPlacedY = 60;

    sortedSubnets.forEach(([subnet, subNodes]) => {
      let rowsInSub = subNodes.length <= 2 ? 1 : (subNodes.length > 8 ? Math.max(3, treeColumnCount(subNodes.length)) : 2);
      if (rowsInSub > MAX_ROWS) rowsInSub = MAX_ROWS;
      const colsInSub = Math.ceil(subNodes.length / rowsInSub);

      if (currentBandRow + rowsInSub > MAX_ROWS && currentBandRow > 0) {
        currentBandX += maxColsInCurrentBand * stepX + Math.round(TREE_BAND_GAP * gapFactor);
        currentBandRow = 0;
        maxColsInCurrentBand = 0;
      }

      const subStartX = currentBandX;
      const subStartY = 60 + currentBandRow * stepY;

      subNodes.forEach((n, idx) => {
        const col = idx % colsInSub;
        const row = Math.floor(idx / colsInSub);
        const px = subStartX + col * stepX;
        const py = subStartY + row * stepY;
        nodePositions.set(n.nodeId, { x: px, y: py });
        maxPlacedY = Math.max(maxPlacedY, py + NODE_H);
      });

      currentBandRow += rowsInSub;
      maxColsInCurrentBand = Math.max(maxColsInCurrentBand, colsInSub);
    });

    const totalChildrenHeight = maxPlacedY - 60;
    const totalRootsHeight = (roots.length - 1) * (stepY + 30) + NODE_H;
    const rootStartY = Math.max(60, 60 + Math.round((totalChildrenHeight - totalRootsHeight) / 2));
    roots.forEach((r, idx) => {
      nodePositions.set(r.nodeId, {
        x: 60,
        y: rootStartY + idx * (stepY + 30)
      });
    });
  }
}

// Pass 1 + Pipeline Orchestrator
function computeLayout(nodes, edges, layoutMode, sortMethod) {
  nodePositions.clear();
  // Un filtre sans résultat ne laisse rien à placer ; les dispositions en arbre supposent au moins une racine.
  if (nodes.length === 0) return;
  const ordered = sortNodes(nodes, edges, sortMethod);
  const gapFactor = Number.parseFloat(nodeGapRange.value) || 1.0;
  const stepX = Math.round(350 * gapFactor);
  const stepY = rowStep(gapFactor);

  if (layoutMode === 'tree') {
    layoutTree(ordered, edges, gapFactor, false);
    return;
  }

  if (layoutMode === 'horizontal-tree') {
    layoutTree(ordered, edges, gapFactor, true);
    return;
  }

  if (layoutMode === 'organic') {
    simulateForceDirectedLayout(ordered, edges, gapFactor);
    return;
  }

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

  if (sortMethod === 'tiers') {
    const tierBuckets = [[], [], [], []];
    ordered.forEach(n => {
      const t = classifyFunctionalTier(n);
      tierBuckets[t].push(n);
    });

    let startX = 60;
    for (let t = 0; t < 4; t++) {
      const bucket = tierBuckets[t];
      if (bucket.length === 0) continue;
      const maxRows = Math.max(8, Math.ceil(Math.sqrt(bucket.length) * 1.8));
      bucket.forEach((n, idx) => {
        const col = Math.floor(idx / maxRows);
        const row = idx % maxRows;
        const staggerY = (col % 2) * Math.round(stepY * 0.42);
        nodePositions.set(n.nodeId, {
          x: startX + col * stepX,
          y: 70 + row * stepY + staggerY
        });
      });
      const colsInTier = Math.max(1, Math.ceil(bucket.length / maxRows));
      startX += colsInTier * stepX + 60;
    }
    optimizeNodePositions2Opt(ordered, edges);
    applyForceRepulsionPolish(ordered, edges, gapFactor);
    return;
  }

  if (sortMethod === 'subnet') {
    const subnetMap = new Map();
    ordered.forEach(n => {
      const sub = extractSubnetPrefix(n.ipAddress);
      if (!subnetMap.has(sub)) subnetMap.set(sub, []);
      subnetMap.get(sub).push(n);
    });

    const locals = ordered.filter(n => n.isLocalAuditedServer);
    let curX = 60;
    let startClusterY = 60;

    if (locals.length > 0) {
      locals.forEach((loc, idx) => {
        nodePositions.set(loc.nodeId, { x: 60 + idx * (stepX + 40), y: 60 });
      });
      startClusterY = 60 + stepY + 80;
    }

    let curY = startClusterY;
    let columnWidth = 0;
    subnetMap.forEach((subNodes) => {
      const unplacedNodes = subNodes.filter(n => !nodePositions.has(n.nodeId));
      if (unplacedNodes.length === 0) return;

      const colsInSub = unplacedNodes.length > 8 ? 3 : (unplacedNodes.length >= 3 ? 2 : 1);
      const rowsInSub = Math.ceil(unplacedNodes.length / colsInSub);

      unplacedNodes.forEach((n, idx) => {
        const col = Math.floor(idx / rowsInSub);
        const row = idx % rowsInSub;
        nodePositions.set(n.nodeId, {
          x: curX + col * stepX,
          y: curY + row * stepY
        });
      });

      // La colonne suivante commence après le sous-réseau le plus large de la colonne, pas après le dernier placé.
      columnWidth = Math.max(columnWidth, colsInSub * stepX);
      const subHeight = rowsInSub * stepY;
      curY += subHeight + 110;
      if (curY > 1400) {
        curY = startClusterY;
        curX += columnWidth + 110;
        columnWidth = 0;
      }
    });
    // Un échange entre deux sous-réseaux raccourcirait des flèches, mais ferait se chevaucher leurs zones.
    optimizeNodePositions2Opt(ordered, edges, n => extractSubnetPrefix(n.ipAddress));
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
function determineFacingSide(fromPos, toPos, currentLayout) {
  if (currentLayout === 'tree') {
    if (toPos.y >= fromPos.y + NODE_H / 2) return 'bottom';
    if (toPos.y + NODE_H / 2 <= fromPos.y) return 'top';
  } else if (currentLayout === 'horizontal-tree') {
    if (toPos.x >= fromPos.x + NODE_W / 2) return 'right';
    if (toPos.x + NODE_W / 2 <= fromPos.x) return 'left';
  }

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
    const curLayout = layoutSelect ? layoutSelect.value : 'layered';
    const srcSide = determineFacingSide(p1, p2, curLayout);
    const dstSide = determineFacingSide(p2, p1, curLayout);
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
    // Le détour est perpendiculaire à l'arête : pour une arête plutôt horizontale, il est vertical et le couloir
    // libre est une ligne horizontale à la hauteur du point de passage, que seul waypoint.y décrit.
    if (waypoint && Math.abs(waypoint.y - (y1 + y2) / 2) > Math.abs(waypoint.x - (x1 + x2) / 2)) {
      const stub = 18;
      const n1 = sideNormalVector(startAnchor.side);
      const n2 = sideNormalVector(endAnchor.side);
      const sx1 = x1 + n1.nx * stub, sx2 = x2 + n2.nx * stub;
      const points = [[x1, y1], [sx1, y1], [sx1, waypoint.y], [sx2, waypoint.y], [sx2, y2], [x2, y2]]
        .filter((p, i, all) => i === 0 || p[0] !== all[i - 1][0] || p[1] !== all[i - 1][1]);
      return {
        d: points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p[0]} ${p[1]}`).join(' '),
        labelX: (x1 + x2) / 2,
        labelY: waypoint.y - 6
      };
    }
    if (waypoint) {
      return {
        d: `M ${x1} ${y1} L ${waypoint.x} ${y1} L ${waypoint.x} ${y2} L ${x2} ${y2}`,
        labelX: waypoint.x,
        labelY: (y1 + y2) / 2 - 6
      };
    }
    if (startAnchor.side === 'bottom' || startAnchor.side === 'top') {
      const midY = (y1 + y2) / 2 + fanOffset * 1.4;
      return {
        d: `M ${x1} ${y1} L ${x1} ${midY} L ${x2} ${midY} L ${x2} ${y2}`,
        labelX: (x1 + x2) / 2,
        labelY: midY - 6
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

// Les troncs IN et OUT d'un même serveur sont décalés de part et d'autre du milieu de son bord, et leurs lignes de séparation
// d'une demi-ligne de part et d'autre, pour que leurs segments ne se superposent jamais.
const BUS_TRUNK_SENSE_OFFSET = { OUT: -14, MIXED: 0, IN: 14 };
const BUS_SPLIT_SENSE_SHIFT = { OUT: -6, MIXED: 0, IN: 6 };
const BUS_MIN_TRUNK_LENGTH = 18;
const BUS_END_STUB = 18;
// La pointe d'une flèche occupe environ 14 px avant l'ancrage : l'étiquette se place juste avant elle, sur la branche.
const BUS_LABEL_TIP_GAP = 18;
const BUS_LANE_GAP = 8;
const BUS_MAX_LANE_SHIFTS = 8;

// Des serveurs voisins dont les cibles sont dans la même rangée ont la même ligne de séparation : superposées, on ne saurait
// plus quel tronc alimente quelle branche. Une ligne qui en chevauche une autre déjà placée s'en écarte d'une voie à la fois,
// en alternant, sans quitter l'espace entre le serveur et sa cible la plus proche ; sans voie libre, elle garde sa position.
function assignBusLane(lanes, lane) {
  for (let step = 0; step <= BUS_MAX_LANE_SHIFTS; step++) {
    const direction = step % 2 === 0 ? 1 : -1;
    const split = lane.split + direction * Math.ceil(step / 2) * BUS_LANE_GAP;
    const depth = (split - lane.origin) * lane.outward;
    if (depth < BUS_LANE_GAP || depth > lane.reach - BUS_LANE_GAP) continue;
    const isFree = !lanes.some(other => other.isVertical === lane.isVertical
      && Math.abs(other.split - split) < BUS_LANE_GAP
      && lane.from < other.to && other.from < lane.to);
    if (isFree) {
      lanes.push({ ...lane, split });
      return split;
    }
  }
  lanes.push(lane);
  return lane.split;
}

// Les points d'arrivée sont plus écartés du milieu du bord que les départs de troncs (± 14 px), pour qu'un serveur
// qui reçoit et émet par le même bord ne superpose jamais une arrivée à l'un de ses troncs.
const BUS_ARRIVAL_SENSE_OFFSET = { OUT: -42, MIXED: 28, IN: 42 };

// Les branches qui visent un même serveur par le même bord et dans le même sens aboutissent au même point :
// elles se rejoignent sur leur dernier segment et ne montrent qu'une pointe, comme les troncs regroupent les départs.
function mergeBusArrivals(drawnEdges, anchorMap) {
  drawnEdges.forEach(drawn => {
    const key = `${drawn.edge.edgeId}::dst`;
    const end = anchorMap.get(key);
    const pos = nodePositions.get(drawn.edge.targetNodeId);
    if (!end || !pos) return;
    const offset = BUS_ARRIVAL_SENSE_OFFSET[drawn.sense];
    anchorMap.set(key, end.side === 'top' || end.side === 'bottom'
      ? { ...end, x: pos.x + NODE_W / 2 + offset }
      : { ...end, y: pos.y + NODE_H / 2 + offset });
  });
}

// Regroupe les flèches par serveur source, bord de départ et sens : chaque groupe partage un tronc qui quitte le bord
// à hauteur fixe, puis se sépare à mi-chemin de la cible la plus proche, à la même ligne pour toutes ses branches.
function computeBusTrunks(drawnEdges, anchorMap) {
  const groups = new Map();
  drawnEdges.forEach(drawn => {
    const start = anchorMap.get(`${drawn.edge.edgeId}::src`);
    const end = anchorMap.get(`${drawn.edge.edgeId}::dst`);
    if (!start || !end) return;
    const key = `${drawn.edge.sourceNodeId}::${start.side}::${drawn.sense}`;
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push({ drawn, start, end });
  });

  const trunks = new Map();
  const lanes = [];
  groups.forEach(members => {
    const { drawn, start } = members[0];
    const pos = nodePositions.get(drawn.edge.sourceNodeId);
    if (!pos) return;
    const isVertical = start.side === 'top' || start.side === 'bottom';
    const normal = sideNormalVector(start.side);
    const outward = normal.nx + normal.ny;
    const offset = BUS_TRUNK_SENSE_OFFSET[drawn.sense];
    const trunk = isVertical
      ? { x: pos.x + NODE_W / 2 + offset, y: start.y, side: start.side }
      : { x: start.x, y: pos.y + NODE_H / 2 + offset, side: start.side };
    const origin = isVertical ? trunk.y : trunk.x;
    const reach = Math.min(...members.map(m => ((isVertical ? m.end.y : m.end.x) - origin) * outward));
    const alongBus = members.map(m => isVertical ? m.end.x : m.end.y).concat(isVertical ? trunk.x : trunk.y);
    const split = assignBusLane(lanes, {
      isVertical,
      origin,
      outward,
      reach,
      split: origin + outward * Math.max(BUS_MIN_TRUNK_LENGTH, reach / 2) + BUS_SPLIT_SENSE_SHIFT[drawn.sense],
      from: Math.min(...alongBus) - BUS_END_STUB,
      to: Math.max(...alongBus) + BUS_END_STUB
    });
    members.forEach(m => trunks.set(m.drawn.edgeId, { trunk, split }));
  });
  return trunks;
}

function busLabelPoint(endAnchor) {
  const n = sideNormalVector(endAnchor.side);
  if (n.ny < 0) return { x: endAnchor.x, y: endAnchor.y - BUS_LABEL_TIP_GAP };
  if (n.ny > 0) return { x: endAnchor.x, y: endAnchor.y + BUS_LABEL_TIP_GAP + EDGE_LABEL_HEIGHT - 3 };
  return { x: endAnchor.x + n.nx * BUS_LABEL_TIP_GAP * 2.5, y: endAnchor.y - 6 };
}

// Le tronc part perpendiculairement au bord jusqu'à la ligne de séparation, la branche longe cette ligne jusqu'à la cible
// puis l'aborde perpendiculairement à son bord d'arrivée, avec un court talon quand ce bord n'est pas face au tronc.
function buildBusEdgePath(trunkStart, split, endAnchor) {
  const x1 = trunkStart.x, y1 = trunkStart.y;
  const x2 = endAnchor.x, y2 = endAnchor.y;
  const n2 = sideNormalVector(endAnchor.side);
  const isVertical = trunkStart.side === 'top' || trunkStart.side === 'bottom';
  const stubX = x2 + n2.nx * BUS_END_STUB, stubY = y2 + n2.ny * BUS_END_STUB;
  const points = (isVertical
    ? [[x1, y1], [x1, split], [stubX, split], [stubX, y2], [x2, y2]]
    : [[x1, y1], [split, y1], [split, stubY], [x2, stubY], [x2, y2]])
    .filter((p, i, all) => i === 0 || p[0] !== all[i - 1][0] || p[1] !== all[i - 1][1]);
  const label = busLabelPoint(endAnchor);
  return {
    d: points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p[0]} ${p[1]}`).join(' '),
    labelX: label.x,
    labelY: label.y
  };
}

const EDGE_LABEL_CHAR_WIDTH = 6.2;
const EDGE_LABEL_HEIGHT = 14;
// Les décalages avancent d'une demi-ligne pour qu'une étiquette trouve sa place dans l'écart entre deux boîtes empilées,
// quelle que soit l'ordonnée du milieu de sa flèche ; 24 demi-lignes gardent la portée de 6 lignes de chaque côté.
const EDGE_LABEL_SHIFT = EDGE_LABEL_HEIGHT / 2;
const EDGE_LABEL_MAX_SHIFTS = 24;

// Les étiquettes sont placées avant d'être insérées dans le DOM : leur largeur est estimée d'après le nombre de caractères
// (≈ 5,5 px mesurés à 10,5 px, majorés pour le gras) plutôt que mesurée par getBBox, qui forcerait une mise en page par étiquette.
function edgeLabelBox(x, y, text) {
  const width = text.length * EDGE_LABEL_CHAR_WIDTH;
  return { left: x - width / 2, right: x + width / 2, top: y - EDGE_LABEL_HEIGHT + 3, bottom: y + 3 };
}

function edgeLabelBoxesOverlap(a, b) {
  return a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;
}

// La marge absorbe l'écart entre la boîte estimée d'une étiquette et celle que le navigateur rend réellement.
const NODE_LABEL_CLEARANCE = 2;

// Les boîtes de serveurs sont dessinées au-dessus des étiquettes : une étiquette posée sur l'une d'elles serait masquée.
function nodeLabelObstacles(nodes) {
  return nodes
    .map(n => nodePositions.get(n.nodeId))
    .filter(pos => pos)
    .map(pos => ({
      left: pos.x - NODE_LABEL_CLEARANCE,
      right: pos.x + NODE_W + NODE_LABEL_CLEARANCE,
      top: pos.y - NODE_LABEL_CLEARANCE,
      bottom: pos.y + NODE_H + NODE_LABEL_CLEARANCE
    }));
}

function findFreeLabelRow(x, label, placed, obstacles) {
  for (let step = 0; step <= EDGE_LABEL_MAX_SHIFTS; step++) {
    const direction = step % 2 === 0 ? 1 : -1;
    const y = label.y + direction * Math.ceil(step / 2) * EDGE_LABEL_SHIFT;
    const box = edgeLabelBox(x, y, label.text);
    const isFree = !placed.some(other => edgeLabelBoxesOverlap(box, other))
      && !obstacles.some(obstacle => edgeLabelBoxesOverlap(box, obstacle));
    if (isFree) return { x, y, box };
  }
  return null;
}

// Chaque étiquette garde le milieu de sa flèche si la place est libre ; sinon elle monte ou descend d'une demi-ligne à la fois,
// en alternant, jusqu'à la première position qui ne touche ni une autre étiquette ni un obstacle. Sans position libre, elle
// reste sur sa flèche : sur une carte très dense, une étiquette superposée à une autre reste en partie lisible et vaut mieux
// qu'une étiquette éloignée de son flux. Une étiquette qu'une boîte de serveur masquerait sur sa flèche est en revanche
// invisible : elle tente alors les mêmes lignes décalées d'une largeur d'étiquette à droite, puis à gauche.
function placeEdgeLabels(labels, obstacles = []) {
  const placed = [];
  return labels.map(label => {
    const onArrow = edgeLabelBox(label.x, label.y, label.text);
    const width = onArrow.right - onArrow.left;
    const isHiddenOnArrow = obstacles.some(obstacle => edgeLabelBoxesOverlap(onArrow, obstacle));
    const columns = isHiddenOnArrow ? [label.x, label.x + width, label.x - width] : [label.x];
    for (const x of columns) {
      const spot = findFreeLabelRow(x, label, placed, obstacles);
      if (spot) {
        placed.push(spot.box);
        return { x: spot.x, y: spot.y };
      }
    }
    placed.push(onArrow);
    return { x: label.x, y: label.y };
  });
}

function filterAndPaginateGraph() {
  const q = (searchInput.value || '').trim().toLowerCase();
  const portFilter = buildPortFilter();
  const excHosts = parseTextTokens(excludeHostsInput.value);
  const dirFilter = directionSelect.value;

  const directionAndPortEdges = TOPOLOGY_DATA.edges.filter(e => {
    if (dirFilter !== 'all' && e.direction !== dirFilter) return false;
    if (!portFilter.allows(e.targetPort)) return false;
    return true;
  });

  // Une recherche garde les serveurs trouvés avec leurs voisins directs et les flux qui les relient :
  // sans les voisins, aucune flèche ne resterait affichée. Le processus client d'un flux sortant n'est
  // porté que par l'arête, d'où la recherche aussi sur les arêtes.
  const nodeMatchesQuery = n =>
    matchesPattern(n.hostName, q) || matchesPattern(n.ipAddress, q) || matchesPattern(`${n.hostName} ${n.ipAddress}`, q)
    || n.ports.some(p => String(p.port).includes(q) || matchesPattern(p.processName || '', q) || matchesPattern(p.serviceTarget || '', q));
  const edgeMatchesQuery = e =>
    String(e.targetPort).includes(q) || matchesPattern(e.processName || '', q) || matchesPattern(e.serviceTarget || '', q);
  const matchedNodeIds = new Set(q ? TOPOLOGY_DATA.nodes.filter(nodeMatchesQuery).map(n => n.nodeId) : []);
  const filteredEdges = q
    ? directionAndPortEdges.filter(e => matchedNodeIds.has(e.sourceNodeId) || matchedNodeIds.has(e.targetNodeId) || edgeMatchesQuery(e))
    : directionAndPortEdges;
  const searchNodeIds = new Set(matchedNodeIds);
  if (q) filteredEdges.forEach(e => { searchNodeIds.add(e.sourceNodeId); searchNodeIds.add(e.targetNodeId); });

  const filteredNodes = TOPOLOGY_DATA.nodes.filter(n => {
    const hostStr = `${n.hostName} ${n.ipAddress}`;
    if (excHosts.some(tok => matchesPattern(n.hostName, tok) || matchesPattern(n.ipAddress, tok) || matchesPattern(hostStr, tok))) return false;
    if (q && !searchNodeIds.has(n.nodeId)) return false;
    const hasActiveEdge = filteredEdges.some(e => e.sourceNodeId === n.nodeId || e.targetNodeId === n.nodeId);
    if (!n.isLocalAuditedServer && !hasActiveEdge) return false;

    const remainingPorts = n.ports.filter(p => portFilter.allows(p.port));
    if (dirFilter === 'OUT' && !hasActiveEdge) return false;
    if (dirFilter === 'IN' && !hasActiveEdge && remainingPorts.length === 0) return false;
    if (portFilter.isActive && remainingPorts.length === 0 && !hasActiveEdge) return false;
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
  // Les compteurs d'en-tête portent sur tout le résultat filtré ; la barre de pagination dit ce que montre la page.
  const filteredNodeIds = new Set(rankedNodes.map(n => n.nodeId));
  const totalFilteredEdges = filteredEdges.filter(e => filteredNodeIds.has(e.sourceNodeId) && filteredNodeIds.has(e.targetNodeId)).length;
  const totalVisiblePorts = rankedNodes.reduce((sum, n) => sum + n.ports.filter(p => portFilter.allows(p.port)).length, 0);
  document.getElementById('kpiNodes').textContent = String(totalFiltered);
  document.getElementById('kpiEdges').textContent = String(totalFilteredEdges);
  document.getElementById('kpiPorts').textContent = String(totalVisiblePorts);
  btnPrevPage.disabled = currentPageIndex <= 0;
  btnNextPage.disabled = currentPageIndex >= totalPages - 1;
  btnLoadMore100.disabled = currentPageSize >= totalFiltered;
  btnShowAll.disabled = currentPageSize >= totalFiltered && currentPageIndex === 0;

  return { nodes: pagedNodes, edges: activeEdges, totalFiltered };
}

function renderInspector(node, edges, allNodesMap) {
  const portFilter = buildPortFilter();
  if (!node) {
    inspectorContent.innerHTML = 'Click on a server to isolate its inbound (IN) and outbound (OUT) routes, or drag it to adjust its position.';
    return;
  }
  const visiblePorts = node.ports.filter(p => portFilter.allows(p.port));
  const portsHtml = visiblePorts.length > 0
    ? visiblePorts.map(p => `<span class="port-pill">:${p.port}/${escapeHtml(p.protocol)}${p.processName ? ' (' + escapeHtml(p.processName) + ')' : ''}</span>`).join(' ')
    : '<span style="color:var(--text-muted)">No active listening port</span>';

  const outbound = edges.filter(e => e.sourceNodeId === node.nodeId);
  const inbound = edges.filter(e => e.targetNodeId === node.nodeId);

  const outHtml = outbound.map(e => {
    const target = allNodesMap.get(e.targetNodeId);
    return `<div><span style="color:var(--accent-out)">[OUT ➔]</span> <strong>${escapeHtml(target ? target.hostName : e.targetNodeId)}</strong> :${e.targetPort}/${escapeHtml(e.protocol)} (${escapeHtml(e.processName || 'flow')})</div>`;
  }).join('');

  const inHtml = inbound.map(e => {
    const source = allNodesMap.get(e.sourceNodeId);
    return `<div><span style="color:var(--accent-in)">[⬅ IN]</span> <strong>${escapeHtml(source ? source.hostName : e.sourceNodeId)}</strong> to :${e.targetPort}/${escapeHtml(e.protocol)}</div>`;
  }).join('');

  inspectorContent.innerHTML = `
    <div><strong>${escapeHtml(node.hostName)}</strong> (${escapeHtml(node.ipAddress || 'IP N/A')})</div>
    <div style="margin-top:4px;color:var(--text-muted)">Non-ephemeral service ports:</div>
    <div>${portsHtml}</div>
    <div style="margin-top:6px;color:var(--text-muted)">Outbound Flows OUT (${outbound.length}):</div>
    <div>${outHtml || 'None'}</div>
    <div style="margin-top:6px;color:var(--text-muted)">Inbound Flows IN (${inbound.length}):</div>
    <div>${inHtml || 'None'}</div>
  `;
}

function describeFlowEdge(e, siblings, fanSpacing) {
  const siblingIdx = siblings.indexOf(e);
  // Le rang dans l'éventail est attribué dans l'ordre canonique de la paire, mais le routage calcule sa normale
  // dans le sens de l'arête : une arête B→A doit inverser son décalage pour rester du côté que lui assigne le rang.
  const isCanonicalOrder = e.sourceNodeId <= e.targetNodeId;
  const isInbound = e.direction === 'IN';
  return {
    edge: e,
    edgeId: e.edgeId,
    sense: isInbound ? 'IN' : 'OUT',
    fanOffset: (siblingIdx - (siblings.length - 1) / 2) * fanSpacing * (isCanonicalOrder ? 1 : -1),
    stroke: isInbound ? '#10b981' : '#38bdf8',
    markerStart: isInbound ? 'url(#originIn)' : 'url(#originOut)',
    markerEnd: isInbound ? 'url(#arrowIn)' : 'url(#arrowOut)',
    isDoubleHeaded: false,
    label: `${isInbound ? 'IN' : 'OUT'} :${e.targetPort}/${e.protocol}`,
    labelFill: isInbound ? '#6ee7b7' : '#bae6fd',
    tooltip: null
  };
}

const PAIR_EDGE_STYLES = {
  IN: { stroke: '#10b981', arrow: 'url(#arrowIn)', origin: 'url(#originIn)' },
  OUT: { stroke: '#38bdf8', arrow: 'url(#arrowOut)', origin: 'url(#originOut)' },
  MIXED: { stroke: '#8b9dc3', arrow: 'url(#arrowMixed)', origin: 'url(#originMixed)' }
};

// Sans étiquettes de ports, l'éventail des flux d'une paire n'apporte plus d'information et encombre la carte :
// une seule flèche les résume, avec une pointe à chaque extrémité qui reçoit un flux. Elle ne garde la couleur
// IN ou OUT que si tous ses flux la partagent ; les ports restent lisibles dans l'infobulle et l'inspecteur.
function describePairEdge(pairKey, flows) {
  const edge = flows[0];
  const isDoubleHeaded = flows.some(f => f.sourceNodeId !== edge.sourceNodeId);
  const directions = new Set(flows.map(f => f.direction === 'IN' ? 'IN' : 'OUT'));
  const sense = directions.size === 1 ? [...directions][0] : 'MIXED';
  const style = PAIR_EDGE_STYLES[sense];
  const ports = [...new Set(flows.map(f => `${f.targetPort}/${f.protocol}`))];
  return {
    edge,
    edgeId: pairKey,
    sense,
    fanOffset: 0,
    stroke: style.stroke,
    markerStart: isDoubleHeaded ? style.arrow : style.origin,
    markerEnd: style.arrow,
    isDoubleHeaded,
    label: null,
    labelFill: null,
    tooltip: `${flows.length} ${flows.length === 1 ? 'flow' : 'flows'}: ${ports.join(', ')}`
  };
}

function renderGraph(recomputePositions = false) {
  const portFilter = buildPortFilter();
  const { nodes, edges } = filterAndPaginateGraph();
  const nodesMap = new Map(nodes.map(n => [n.nodeId, n]));

  if (recomputePositions || nodePositions.size === 0) {
    computeLayout(nodes, edges, layoutSelect.value, sortSelect.value);
  }

  const routingMode = routingSelect.value;
  const fanSpacing = Number.parseInt(edgeFanRange.value, 10) || 18;

  const pairBuckets = new Map();
  edges.forEach(e => {
    const pairKey = undirectedPairKey(e.sourceNodeId, e.targetNodeId);
    if (!pairBuckets.has(pairKey)) pairBuckets.set(pairKey, []);
    pairBuckets.get(pairKey).push(e);
  });

  const drawnEdges = arePortLabelsShown()
    ? edges.map(e => describeFlowEdge(e, pairBuckets.get(undirectedPairKey(e.sourceNodeId, e.targetNodeId)) || [e], fanSpacing))
    : Array.from(pairBuckets, ([pairKey, flows]) => describePairEdge(pairKey, flows));
  const anchorMap = computeAllEdgeAnchors(nodes, drawnEdges.map(drawn => drawn.edge));
  if (routingMode === 'bus') mergeBusArrivals(drawnEdges, anchorMap);
  const busTrunks = routingMode === 'bus' ? computeBusTrunks(drawnEdges, anchorMap) : null;
  if (busTrunks && selectedNodeId) {
    // Les flèches d'un bus se superposent sur leur tronc : celles du serveur sélectionné sont tracées en dernier
    // pour que leur tronc orange ne soit pas recouvert par celui, estompé, des autres branches.
    const touchesSelection = drawn => drawn.edge.sourceNodeId === selectedNodeId || drawn.edge.targetNodeId === selectedNodeId;
    drawnEdges.sort((a, b) => Number(touchesSelection(a)) - Number(touchesSelection(b)));
  }

  const edgesFrag = document.createDocumentFragment();
  const labelsFrag = document.createDocumentFragment();
  const nodesFrag = document.createDocumentFragment();
  const pendingLabels = [];

  drawnEdges.forEach(drawn => {
    const e = drawn.edge;
    const p1 = nodePositions.get(e.sourceNodeId);
    const p2 = nodePositions.get(e.targetNodeId);
    if (!p1 || !p2) return;

    const isHighlighted = selectedNodeId && (e.sourceNodeId === selectedNodeId || e.targetNodeId === selectedNodeId);
    const isDimmed = selectedNodeId && !isHighlighted;

    const startAnchor = anchorMap.get(`${e.edgeId}::src`) || { x: p1.x + NODE_W / 2, y: p1.y + NODE_H / 2, side: 'right' };
    const endAnchor = anchorMap.get(`${e.edgeId}::dst`) || { x: p2.x + NODE_W / 2, y: p2.y + NODE_H / 2, side: 'left' };

    const bus = busTrunks && busTrunks.get(drawn.edgeId);
    const route = bus
      ? buildBusEdgePath(bus.trunk, bus.split, endAnchor)
      : buildRoutedEdgePath(startAnchor, endAnchor, routingMode, drawn.fanOffset,
        findCorridorWaypoint(startAnchor, endAnchor, e.sourceNodeId, e.targetNodeId, nodes, drawn.fanOffset));

    const path = document.createElementNS(SVG_NS, 'path');
    path.setAttribute('d', route.d);
    path.setAttribute('data-edge-id', drawn.edgeId);
    path.setAttribute('fill', 'none');
    path.setAttribute('stroke', isHighlighted ? '#f59e0b' : drawn.stroke);
    path.setAttribute('stroke-width', isHighlighted ? '2.6' : '1.75');
    path.setAttribute('stroke-opacity', isDimmed ? '0.18' : '0.88');
    path.setAttribute('marker-start', isHighlighted && drawn.isDoubleHeaded ? 'url(#arrowHighlight)' : drawn.markerStart);
    path.setAttribute('marker-end', isHighlighted ? 'url(#arrowHighlight)' : drawn.markerEnd);
    if (drawn.tooltip) {
      const tooltip = document.createElementNS(SVG_NS, 'title');
      tooltip.textContent = drawn.tooltip;
      path.appendChild(tooltip);
    }
    edgesFrag.appendChild(path);

    if (drawn.label && !isDimmed && (edges.length <= 180 || isHighlighted)) {
      pendingLabels.push({
        x: route.labelX,
        y: route.labelY,
        text: drawn.label,
        fill: isHighlighted ? '#fbbf24' : drawn.labelFill,
        isHighlighted
      });
    }
  });

  // Les étiquettes du serveur sélectionné sont placées en premier pour rester au milieu de leurs flèches.
  pendingLabels.sort((a, b) => Number(b.isHighlighted) - Number(a.isHighlighted));
  const labelPositions = placeEdgeLabels(pendingLabels, nodeLabelObstacles(nodes));
  pendingLabels.forEach((pending, index) => {
    const label = document.createElementNS(SVG_NS, 'text');
    label.setAttribute('x', String(Math.round(labelPositions[index].x)));
    label.setAttribute('y', String(Math.round(labelPositions[index].y)));
    label.setAttribute('text-anchor', 'middle');
    label.setAttribute('fill', pending.fill);
    label.setAttribute('font-size', '10.5');
    label.setAttribute('font-weight', pending.isHighlighted ? 'bold' : 'normal');
    label.textContent = pending.text;
    labelsFrag.appendChild(label);
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

    const visiblePorts = n.ports.filter(p => portFilter.allows(p.port));
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

  const zonesFrag = document.createDocumentFragment();
  if (showZonesCheck && showZonesCheck.checked) {
    const zones = computeSubnetZones(nodes);
    zones.forEach(z => {
      const g = document.createElementNS(SVG_NS, 'g');
      g.setAttribute('class', 'subnet-zone');
      const r = document.createElementNS(SVG_NS, 'rect');
      r.setAttribute('x', String(Math.round(z.x)));
      r.setAttribute('y', String(Math.round(z.y)));
      r.setAttribute('width', String(Math.round(z.w)));
      r.setAttribute('height', String(Math.round(z.h)));
      r.setAttribute('rx', '14');
      r.setAttribute('fill', 'rgba(30, 41, 59, 0.55)');
      r.setAttribute('stroke', '#64748b');
      r.setAttribute('stroke-width', '1.8');
      r.setAttribute('stroke-dasharray', '6,4');
      g.appendChild(r);

      const labelText = `Zone: ${z.subnet} (${z.nodeCount} ${z.nodeCount > 1 ? 'servers' : 'server'})`;
      const badgeWidth = Math.min(Math.max(130, labelText.length * 7.5 + 16), Math.max(60, z.w - 16));

      const badge = document.createElementNS(SVG_NS, 'rect');
      badge.setAttribute('x', String(Math.round(z.x + 8)));
      badge.setAttribute('y', String(Math.round(z.y + 6)));
      badge.setAttribute('width', String(Math.round(badgeWidth)));
      badge.setAttribute('height', '20');
      badge.setAttribute('rx', '4');
      badge.setAttribute('fill', '#1e293b');
      badge.setAttribute('stroke', '#475569');
      badge.setAttribute('stroke-width', '1');
      g.appendChild(badge);

      const txt = document.createElementNS(SVG_NS, 'text');
      txt.setAttribute('x', String(Math.round(z.x + 15)));
      txt.setAttribute('y', String(Math.round(z.y + 20)));
      txt.setAttribute('fill', '#f1f5f9');
      txt.setAttribute('font-size', '11.5');
      txt.setAttribute('font-weight', '700');
      txt.textContent = labelText;
      g.appendChild(txt);
      zonesFrag.appendChild(g);
    });
  }

  zonesLayer.replaceChildren(zonesFrag);
  edgesLayer.replaceChildren(edgesFrag);
  labelsLayer.replaceChildren(labelsFrag);
  nodesLayer.replaceChildren(nodesFrag);
}

function buildCliCommand() {
  const cli = TOPOLOGY_DATA.cliCommand;
  const parts = [cli.prefix];
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
  if (layoutSelect.value && layoutSelect.value !== 'layered') parts.push(`${cli.layoutOption} ${layoutSelect.value}`);
  if (sortSelect.value && sortSelect.value !== 'sugiyama') parts.push(`${cli.sortOption} ${sortSelect.value}`);
  if (routingSelect.value && routingSelect.value !== 'bezier') parts.push(`${cli.routingOption} ${routingSelect.value}`);
  if (spacingSelect.value && spacingSelect.value !== 'balanced') parts.push(`${cli.spacingOption} ${spacingSelect.value}`);
  if (showPortsCheck && !showPortsCheck.checked) parts.push(`${cli.edgeLabelsOption} none`);
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
sortSelect.addEventListener('change', () => {
  if (sortSelect.value === 'subnet' && showZonesCheck) {
    showZonesCheck.checked = true;
  }
  renderGraph(true);
  updateCliCommandDisplay();
});
if (showZonesCheck) {
  showZonesCheck.addEventListener('change', () => {
    renderGraph(false);
  });
}
if (showPortsCheck) {
  // L'écart minimal entre deux rangées dépend de la présence des étiquettes : la disposition est recalculée.
  showPortsCheck.addEventListener('change', () => { renderGraph(true); updateCliCommandDisplay(); });
}
routingSelect.addEventListener('change', () => { renderGraph(false); updateCliCommandDisplay(); });

spacingSelect.addEventListener('change', () => {
  applySpacingPreset(spacingSelect.value);
  renderGraph(true);
  updateCliCommandDisplay();
});

nodeGapRange.addEventListener('input', () => {
  nodeGapVal.textContent = `${Number.parseFloat(nodeGapRange.value).toFixed(2)}x`;
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
  updateCliCommandDisplay();
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
      const x = Number.parseFloat(match[1]);
      const y = Number.parseFloat(match[2]);
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x + NODE_W);
      maxY = Math.max(maxY, y + NODE_H);
    }
  });

  if (!Number.isFinite(minX) || !Number.isFinite(minY)) {
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
