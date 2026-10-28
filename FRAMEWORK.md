<p align="right">
  <b>English</b> | <a href="FRAMEWORK.fr.md">Français</a>
</p>

# TopologyForge — Framework Specification & AI Documentation

This document is the technical manifest intended for AI agents and software engineers. It outlines all capabilities, architectural components, and public contracts exposed by **TopologyForge**.

---

## 🎯 Scope & Responsibility

TopologyForge is a shared .NET library dedicated to **topological modeling, declarative multi-criteria filtering (inclusions/exclusions), and generating standalone, interactive HTML5/SVG network topology maps** (single-file offline execution with zero external CDN dependencies).

---

## 🚀 Key Capabilities

* **Immutable Topological Modeling (`TopologyForge.Models`)**:
  * `NetworkPortRange`: Validated port range Value Object (`0..65535`), with default IANA ephemeral ranges (`49152-65535`) and composite expression parsing (`"80, 443, 8000-8080"`).
  * `TopologyFlowRecord`: Normalized socket or listener flow record.
  * `TopologyServerNode`, `ServerListenerDescriptor`, `TopologyServiceEdge` (with `FlowDirection` inbound `IN` or outbound `OUT`), `NetworkTopologyGraph`: Consolidated graph of audited and remote servers, non-ephemeral service listeners, and directional communication edges.
* **Declarative Inclusion & Exclusion Filtering (`TopologyForge.Filtering`)**:
  * `TopologyFilterOptions` & `TopologyFlowEvaluator` (`ITopologyFlowEvaluator`):
    * Automatic client ephemeral port pruning (`49152-65535`).
    * Port and port-range inclusions and exclusions (`IncludedPorts`, `ExcludedPorts`).
    * Hostname wildcard and regex matching (`IncludedHostPatterns`, `ExcludedHostPatterns`).
    * IPv4/IPv6 address and CIDR subnet matching (`IncludedIpRanges`, `ExcludedIpRanges`) via `CidrSubnetMatcher`.
    * Process and target executable matching (`IncludedProcesses`, `ExcludedProcesses`) and flow direction filters (`IncludeListeners`, `IncludeInboundFlows`, `IncludeOutboundFlows`).
* **Aggregation & Graph Consolidation (`TopologyForge.Builders`)**:
  * `TopologyGraphBuilder` (`ITopologyGraphBuilder`): Consolidates flows per server host, strictly binds non-ephemeral service ports to the host running the daemon (local port when listening/inbound, remote port when outbound), and merges redundant connections while accumulating flow observations.
* **RFC 4180 CSV Parsing (`TopologyForge.Parsers`)**:
  * `TopologyCsvParser` (`ITopologyCsvParser`): Streaming asynchronous or in-memory parsing of network flow logs (such as `NetFlowAudit`) with dynamic column header discovery.
* **Standalone HTML5 Map Rendering & 5-Pass Crossing Minimization (`TopologyForge.Rendering`)**:
  * `TopologyHtmlRenderer` (`ITopologyHtmlRenderer`) & `TopologyRenderingOptions`:
    * **Map script as an embedded resource**: the page's JavaScript lives in `TopologyForge/Rendering/topology-map.js`, compiled into the assembly as `TopologyForge.Rendering.topology-map.js`, read once and copied into every page after the `TOPOLOGY_DATA` payload. The generated HTML file stays self-contained.
    * **Activity-Based Pagination & Chunked Loading (`DefaultPageSize = 100`)**:
      * Prioritized server ordering (locally audited nodes, connection degree, and observation density) with automatic preservation of connected hosts on the current page.
      * Integrated pagination toolbar: `◀ Prev`, `Next ▶`, `+100 Servers`, and `Show All`.
    * **5-Pass Crossing Minimization Engine**:
      1. **Initial 360° concentric rings or staggered central columns** placed around locally audited nodes.
      2. **Alternating 8-pass Barycenter / Weighted Median sweep** to pull interconnected nodes into alignment.
      3. **Exact 2-Opt Optimizer (`optimizeNodePositions2Opt`)** minimizing true geometric edge intersections and arrow-to-box collisions.
      4. **Repulsive Force Relaxation (`applyForceRepulsionPolish`)** carving unobstructed visibility corridors with zero box overlaps.
      5. **Uniform 4-border anchor distribution (`computeAllEdgeAnchors`) and obstacle-aware corridor routing (`findCorridorWaypoint`)** recalculated dynamically in real-time, including during mouse dragging.
    * **4 Layout Modes (`TopologyLayoutMode`)**:
      * `Layered`: Concentric rings around audited hub(s) or tiered bipartite columns.
      * `ForceDirectedOrganic`: Physical spring-electrical simulation (Hooke attraction weighted by flow observations + Coulomb repulsion + box clearance); audited servers stay fixed and are spread on a circle so their boxes never overlap.
      * `Circular`: Uniform 360-degree perimeter ring.
      * `Grid`: Balanced multi-column matrix.
    * **6 Server Sorting Strategies (`TopologyNodeSortMethod`)**:
      * `BarycentricSugiyama`: Multi-pass barycenter/median heuristic with 2-Opt refinement.
      * `FunctionalTier`: 4-tier architectural stratification (Clients ➔ Gateways/Ingress ➔ Applications ➔ Databases/Infra).
      * `SubnetCluster`: Contiguous spatial clustering by IP CIDR subnets with visual boundary enclosures; columns start after their widest subnet and 2-Opt swaps stay within a subnet, so enclosures never overlap.
      * `ConnectionDegree`: Traffic density sorting by descending connection count.
      * `Alphabetical`: Alphabetical ordering by hostname.
      * `SubnetIp`: Hierarchical IP address octet sorting.
    * **Spatial Hash Grid Acceleration (`SpatialHashGrid`)**:
      * 2D spatial grid partitioning segment-segment crossings and box-edge collisions into localized buckets, accelerating 2-Opt layout optimization on 150+ nodes.
    * **3 Edge Routing Algorithms (`TopologyEdgeRoutingMode`)**:
      * `BezierCurved`: Cubic Bézier curves respecting boundary normal vectors with corridor detour and multi-edge fanout.
      * `OrthogonalCorridors`: 90-degree orthogonal corridors with lane-offset anti-collision spacing; a detour around a blocking box runs through the waypoint row or column, whichever the detour follows.
      * `DirectStraight`: Straight border-to-border vectors with parallel spacing.
    * **Arrow Label Placement (`placeEdgeLabels`)**:
      * Each `IN` / `OUT :port/protocol` label sits at the middle of its arrow; when that spot is taken, it moves up or down one row at a time (up to 12 shifts) until it no longer overlaps another label. Labels of the selected server are placed first.
    * **Border Anchoring & Directional Terminals (`IN` / `OUT`)**:
      * Equidistant border anchor distribution across all four sides of each server.
      * High-contrast chromatic differentiation: outbound flows (`OUT` in cyan `#38bdf8`) vs. inbound flows (`IN` in emerald `#10b981`).
    * **Spacing Profiles & Live Sliders (`TopologySpacingProfile`)**:
      * Presets: `Compact`, `Balanced`, `Spacious` (for maps of more than 100 servers) with real-time scaling sliders for node spacing (`0.65x` to `2.4x`) and edge separation (`6px` to `46px`).
    * **Ergonomic Filter Controls & "Apply Filters" Button**:
      * Dedicated `Apply Filters` trigger and `[Enter]` shortcut on text search and port fields to eliminate laggy keystroke recalculations on dense maps.
      * Text search keeps the matching servers together with their direct neighbours and the flows between them; it also matches the port, process and service target of each flow, so an outbound client process can be found.
      * Port fields accept single ports and ranges (`"443, 8000-8080"`), like `NetworkPortRange`.
      * Header counters (servers, service ports, flows) report the whole filtered result, across all pages.
      * Single-click `Reset` action, which also refreshes the displayed CLI command.
    * **Collapsible Sidebar (`◀ Sidebar`), Accordion Controls & Zero Scrollbar Drift**:
      * Dedicated `◀ Sidebar` button (or `[B]` key) to instantly collapse/expand the control panel, granting 100% viewport space to the SVG canvas.
      * Interactive accordion sections (`1. Layout`, `2. Filters`, `3. CLI Command`, `4. Inspector`), eliminating vertical window scrolling.
    * **Live CLI Reproduction (`CLI Command`)**:
      * Interactive code box reproducing the command that renders the current view (filters, layout, sort, routing, spacing). Its syntax comes from `TopologyRenderingOptions.CliCommandFormat` (`TopologyCliCommandFormat`: `Prefix`, `LayoutOption`, `SortOption`, `RoutingOption`, `SpacingOption`); the default values produce the NetFlowAudit 1.x syntax `NetFlowAudit.Cli --map-only ... --map-layout ...`.
      * One-click `📋 Copy Command` clipboard button.
    * **Standalone Vector SVG Export (`Export SVG`)**:
      * One-click vector extraction exporting the active topology view (including custom user drag positions and active filters) into a standalone `network_topology_map.svg` file.
      * Automatic bounding box (`viewBox`) calculation with balanced padding and embedded dark canvas background (`#0b0f19`) for seamless embedding in Word, PowerPoint, Visio, Draw.io, or Inkscape.
    * **Dashboard Ergonomics & Orphan Pruning**:
      * **Header-Aligned Sidebar Toggle**: Integrated into the top KPI banner directly beside *Servers*, avoiding any section heading overlap.
      * **Auto-Pruning of Orphan Nodes**: Nodes losing all active links and listeners after filtering are automatically pruned from the canvas and live KPI metrics.

---

## 📋 Maintenance Protocol for AI Agents

All enhancements to TopologyForge must comply with:
1. **Backward Compatibility:** Preserve all existing public contracts. Use overloads or optional parameters.
2. **Code Cleanliness:** Zero warnings (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), zero pragmas/suppressions.
3. **Testing Standards:** Test naming convention `TargetMethod_ScenarioOrCondition_ExpectedResult`.
4. **Documentation:** Update both `FRAMEWORK.md` and `FRAMEWORK.fr.md` immediately upon introducing or modifying capabilities.
5. **Validation:** Full pass of `./verify.ps1`.
