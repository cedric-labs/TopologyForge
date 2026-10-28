# TopologyForge

Self-contained offline HTML5/SVG network topology engine for .NET with multi-pass crossing minimization, corridor routing, dynamic interactive filtering, and SVG vector export.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Documentation: English](https://img.shields.io/badge/Docs-English-blue.svg)](FRAMEWORK.md)
[![Documentation: Français](https://img.shields.io/badge/Docs-Français-green.svg)](FRAMEWORK.fr.md)

> 📖 **Framework Documentation**: [English Specification](FRAMEWORK.md) | [Spécification en français](FRAMEWORK.fr.md)

---

## Key Capabilities

### 1. Immutable Topology Graph Model (`TopologyForge.Models`)
* **`NetworkPortRange`**: Validated port range Value Object (`0..65535`) with default IANA ephemeral port exclusion (`49152-65535`) and range parsing (`"80, 443, 8000-8080"`).
* **`NetworkTopologyGraph`**: Consolidated immutable graph structure organizing audited and remote servers, service listener ports, directional flow edges (`IN` / `OUT`), and connection metrics.

### 2. Multi-Criteria Filtering Engine (`TopologyForge.Filtering`)
* **`TopologyFlowEvaluator`**: Declarative filtering by:
  * Included and excluded port numbers and ranges.
  * Included and excluded hostnames and wildcard expressions.
  * IPv4 and IPv6 subnets via CIDR bitwise matching (`CidrSubnetMatcher`).
  * Process names, targets, and flow directions.

### 3. 5-Pass Anti-Crossing & Obstacle Avoidance Pipeline (`TopologyForge.Rendering`)
1. **Initial 360-degree Concentric Placement**: Positions nodes evenly around audited core servers.
2. **Alternating Barycenter / Weighted Median Sweeps (8 passes)**: Aligns connected neighbors.
3. **Exact 2-Opt Geometric Optimizer**: Prunes edge-edge intersections and edge-box collisions.
4. **Repulsive Force Relaxation Polish**: Guarantees clear visual corridors between server boxes.
5. **Uniform Angular Edge Anchoring & Corridor Waypoint Routing**: Connects bounding box borders dynamically with curved Bezier or orthogonal corridors.

### 4. Interactive Standalone HTML5 Dashboard
* **100% Offline**: Zero external CDN scripts or stylesheet dependencies.
* **Dynamic Pruning**: Automatically prunes orphan servers when port filters eliminate all active flows.
* **Ergonomic Toolbar**: Built-in sidebar toggle integrated into top KPI metrics bar.
* **Vector Export**: Single-click SVG export cropped to visible layout boundaries.

---

## Installation

Add the package via the .NET CLI:

```powershell
dotnet add package TopologyForge --version 1.5.6
```

---

## Code Example

```csharp
using TopologyForge.Builders;
using TopologyForge.Filtering;
using TopologyForge.Rendering;

var evaluator = new TopologyFlowEvaluator(new TopologyFilterOptions());
var builder = new TopologyGraphBuilder(evaluator);
var graph = builder.BuildGraph(flowRecords);

var renderer = new TopologyHtmlRenderer();
await renderer.WriteHtmlFileAsync("network_topology.html", graph, "Home Lab Topology");
```

---

---

## Performance & Scalability Benchmarks

Automated benchmarks executed via `TopologyRenderingPerformanceTests` across synthetic network topologies:

| Servers ($N$) | Links ($Y$) | Payload Size | HTML Render Time (.NET) | Headless Browser Load & Render (Playwright) | Evaluation |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **10** | 25 | 74 KB | 4 ms | 125 ms | Instantaneous |
| **50** | 200 | 127 KB | < 1 ms | 147 ms | Instantaneous |
| **200** | 1,000 | 359 KB | 6 ms | 172 ms | Ultra-responsive |
| **1,000** | 5,000 | 1,533 KB | 11 ms | ~280 ms | Responsive |

* Environment: .NET 8.0 (Release), Playwright Chromium (Headless), Windows 11.
* All layout calculations, Barycenter sweeps, and SVG layer injections complete within milliseconds on the client side.

## License

This project is licensed under the [MIT License](LICENSE).
