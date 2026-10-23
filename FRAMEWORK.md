# TopologyForge

Framework partagé .NET de modélisation topologique, de filtrage déclaratif (inclusions/exclusions) et de génération de cartographies réseau interactives autonomes (Single-File HTML hors-ligne sans dépendance externe).

## 🚀 Capacités Clés

* **Modélisation Topologique Immuable (`TopologyForge.Models`)** :
  * `NetworkPortRange` : Value Object de plage de ports réseau validée (`0..65535`), avec plage éphémère IANA par défaut (`49152-65535`) et parsing d'expressions (`"80, 443, 8000-8080"`).
  * `TopologyFlowRecord` : Enregistrement de flux ou d'écoute normalisé.
  * `TopologyServerNode`, `ServerListenerDescriptor`, `TopologyServiceEdge` (avec `FlowDirection` entrant `IN` ou sortant `OUT`), `NetworkTopologyGraph` : Graphe consolidé des serveurs (locaux et distants), de leurs ports de service non éphémères et de leurs liens directionnels.
* **Filtrage Multicritère d'Inclusion & Exclusion (`TopologyForge.Filtering`)** :
  * `TopologyFilterOptions` & `TopologyFlowEvaluator` (`ITopologyFlowEvaluator`) :
    * Exclusion automatique des ports éphémères clients (`49152-65535`).
    * Inclusion et exclusion de ports ou plages de ports (`IncludedPorts`, `ExcludedPorts`).
    * Inclusion et exclusion de serveurs par motifs génériques (`IncludedHostPatterns`, `ExcludedHostPatterns`).
    * Inclusion et exclusion d'adresses IPv4/IPv6 ou sous-réseaux CIDR (`IncludedIpRanges`, `ExcludedIpRanges`) via `CidrSubnetMatcher`.
    * Inclusion et exclusion de processus/cibles (`IncludedProcesses`, `ExcludedProcesses`) et de directions (`IncludeListeners`, `IncludeInboundFlows`, `IncludeOutboundFlows`).
* **Agrégation & Déduplication (`TopologyForge.Builders`)** :
  * `TopologyGraphBuilder` (`ITopologyGraphBuilder`) : Regroupe les flux par serveur, associe uniquement les ports de service non éphémères aux serveurs hébergeant le service (port local en écoute/entrant, port distant en sortant) et fusionne les liens redondants en cumulant les compteurs d'observations.
* **Analyse CSV RFC 4180 (`TopologyForge.Parsers`)** :
  * `TopologyCsvParser` (`ITopologyCsvParser`) : Lecture asynchrone ou en mémoire de journaux CSV (dont `NetFlowAudit`) avec détection dynamique des colonnes.
* **Rendu Cartographique HTML5 Autonome & Routage Anti-Croisement (`TopologyForge.Rendering`)** :
  * `TopologyHtmlRenderer` (`ITopologyHtmlRenderer`) & `TopologyRenderingOptions` :
    * **4 méthodes de tri des serveurs (`TopologyNodeSortMethod`)** :
      * `BarycentricSugiyama` : Heuristique barycentrique multicouche alignant face à face les serveurs interconnectés pour minimiser les croisements de flèches.
      * `ConnectionDegree` : Tri par densité de trafic (nombre de connexions décroissant).
      * `Alphabetical` : Tri alphabétique par nom d'hôte.
      * `SubnetIp` : Tri logique par sous-réseau et octets d'adresse IP.
    * **3 modes de routage des flèches (`TopologyEdgeRoutingMode`)** :
      * `BezierCurved` : Courbes de Bézier cubiques avec détection et contournement automatique des boîtes de serveurs intermédiaires + éventail sur liens multiples.
      * `OrthogonalCorridors` : Couloirs orthogonaux à 90° avec pistes décalées anti-chevauchement.
      * `DirectStraight` : Lignes directes de bordure à bordure avec écartement parallèle.
    * **Ancrage sur bordures & Pointes directionnelles (`IN` / `OUT`)** :
      * Calcul géométrique d'intersection sur la bordure extérieure des boîtes serveurs (la pointe de flèche s'arrête exactement sur le bord du serveur cible).
      * Distinction visuelle et chromatique des flux sortants (`OUT` en bleu cyan `#38bdf8`) et entrants (`IN` en vert émeraude `#10b981`).
    * **Profils et curseurs d'écartement (`TopologySpacingProfile`)** :
      * Profils `Compact`, `Balanced`, `Spacious` (adapté aux parcs > 100 serveurs) et curseurs temps réel d'écartement des serveurs (`0.65x` à `2.4x`) et d'écartement des flèches (`6px` à `46px`).
