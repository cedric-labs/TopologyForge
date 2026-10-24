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
* **Rendu Cartographique HTML5 Autonome & Routage Anti-Croisement en 5 Passes (`TopologyForge.Rendering`)** :
  * `TopologyHtmlRenderer` (`ITopologyHtmlRenderer`) & `TopologyRenderingOptions` :
    * **Pagination intelligente par activité & Chargement par tranche (`DefaultPageSize = 100`)** :
      * Classement prioritaire (serveurs audités locaux, degré de connexion et volume d'observations) avec préservation automatique des serveurs locaux reliés à la page courante.
      * Barre de pagination intégrée : `◀ Prev`, `Next ▶`, `+100 Servers` et `Show All`.
    * **Pipeline anti-croisement en 5 passes** :
      1. **Placement initial 360° en anneaux concentriques ou colonnes centrées en quinconce** autour des serveurs audités.
      2. **Balayage alterné Barycentre / Médiane pondérée (8 passes)** pour aligner les nœuds interconnectés.
      3. **Optimiseur 2-Opt exact (`optimizeNodePositions2Opt`)** guidé par le décompte géométrique réel des intersections de segments et des collisions flèche/boîte.
      4. **Relaxation répulsive (`applyForceRepulsionPolish`)** ouvrant des couloirs de visibilité sans chevauchement de boîtes.
      5. **Distribution angulaire uniforme des points d'attache sur les 4 bordures (`computeAllEdgeAnchors`) et routage par couloirs multi-obstacles (`findCorridorWaypoint`)** recalculés en temps réel y compris lors du déplacement manuel à la souris.
    * **4 méthodes de tri des serveurs (`TopologyNodeSortMethod`)** :
      * `BarycentricSugiyama` : Heuristique barycentrique/médiane multi-passes + optimisation 2-Opt.
      * `ConnectionDegree` : Tri par densité de trafic (nombre de connexions décroissant).
      * `Alphabetical` : Tri alphabétique par nom d'hôte.
      * `SubnetIp` : Tri logique par sous-réseau et octets d'adresse IP.
    * **3 modes de routage des flèches (`TopologyEdgeRoutingMode`)** :
      * `BezierCurved` : Courbes de Bézier cubiques orientées selon les normales aux bordures avec contournement par couloirs + éventail sur liens multiples.
      * `OrthogonalCorridors` : Couloirs orthogonaux à 90° avec pistes décalées anti-chevauchement.
      * `DirectStraight` : Lignes directes de bordure à bordure avec écartement parallèle.
    * **Ancrage sur bordures & Pointes directionnelles (`IN` / `OUT`)** :
      * Répartition régulière et tri géométrique des points d'attache sur les 4 côtés de chaque serveur.
      * Distinction visuelle et chromatique des flux sortants (`OUT` en bleu cyan `#38bdf8`) et entrants (`IN` en vert émeraude `#10b981`).
    * **Profils et curseurs d'écartement (`TopologySpacingProfile`)** :
      * Profils `Compact`, `Balanced`, `Spacious` (adapté aux parcs > 100 serveurs) et curseurs temps réel d'écartement des serveurs (`0.65x` à `2.4x`) et d'écartement des flèches (`6px` à `46px`).
    * **Contrôles de filtrage ergonomiques & Bouton "Apply Filters"** :
      * Bouton `Apply Filters` dédié et déclenchement par validation `[Enter]` sur les champs de recherche textuelle et de ports, supprimant les recalculs intempestifs frappe par frappe pour une fluidité maximale sur les parcs denses.
      * Bouton `Reset` pour réinitialiser les filtres en un clic.
    * **Barre latérale rétractable (`◀ Sidebar`), Sections accordéon & Élimination de l'ascenseur** :
      * Bouton `◀ Sidebar` (ou touche `[B]`) permettant de masquer/afficher instantanément le panneau latéral pour allouer 100 % de l'écran au canevas SVG.
      * Sections d'options cliquables en accordéon (`1. Layout`, `2. Filters`, `3. CLI Command`, `4. Inspector`), supprimant tout défilement vertical parasite.
    * **Reproduction CLI instantanée (`CLI Command`)** :
      * Encadré dynamique générant la ligne de commande exacte `NetFlowAudit.Cli --map-only ...` correspondant fidèlement aux filtres et algorithmes actuellement actifs dans la page.
      * Bouton `📋 Copy Command` pour copier la syntaxe dans le presse-papier en un clic.
    * **Export Vectoriel SVG Autonome (`Export SVG`)** :
      * Bouton intégré dans la barre d'outils permettant d'extraire la vue courante (avec filtres, sélections et déplacements manuels) sous forme de fichier vectoriel autonome `network_topology_map.svg`.
      * Calcul automatique du cadre englobant (`viewBox`) avec marges équilibrées et fond sombre vectoriel intégré (`#0b0f19`) pour insertion directe dans Microsoft Word, PowerPoint, Visio, Draw.io ou Inkscape sans perte de netteté.
