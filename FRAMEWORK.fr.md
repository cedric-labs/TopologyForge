<p align="right">
  <a href="FRAMEWORK.md">English</a> | <b>Français</b>
</p>

# TopologyForge — Spécification & Documentation IA du Framework

Ce document est le manifeste technique destiné à l'agent IA et aux développeurs. Il récapitule l'ensemble des capacités, composants et contrats publics exposés par **TopologyForge**.

---

## 🎯 Périmètre & Responsabilité

TopologyForge est le framework partagé .NET de **modélisation topologique, de filtrage déclaratif (inclusions/exclusions) et de génération de cartographies réseau interactives autonomes** (Single-File HTML hors-ligne sans dépendance externe).

---

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
    * **Script de la carte en ressource embarquée** : le JavaScript de la page est dans `TopologyForge/Rendering/topology-map.js`, compilé dans l'assembly sous le nom `TopologyForge.Rendering.topology-map.js`, lu une seule fois et copié dans chaque page après la charge utile `TOPOLOGY_DATA`. Le fichier HTML généré reste autonome.
    * **Pagination intelligente par activité & Chargement par tranche (`DefaultPageSize = 100`)** :
      * Classement prioritaire (serveurs audités locaux, degré de connexion et volume d'observations) avec préservation automatique des serveurs locaux reliés à la page courante.
      * Barre de pagination intégrée : `◀ Prev`, `Next ▶`, `+100 Servers` et `Show All`.
    * **Pipeline anti-croisement en 5 passes** :
      1. **Placement initial 360° en anneaux concentriques ou colonnes centrées en quinconce** autour des serveurs audités.
      2. **Balayage alterné Barycentre / Médiane pondérée (8 passes)** pour aligner les nœuds interconnectés.
      3. **Optimiseur 2-Opt exact (`optimizeNodePositions2Opt`)** guidé par le décompte géométrique réel des intersections de segments et des collisions flèche/boîte.
      4. **Relaxation répulsive (`applyForceRepulsionPolish`)** ouvrant des couloirs de visibilité sans chevauchement de boîtes.
      5. **Distribution angulaire uniforme des points d'attache sur les 4 bordures (`computeAllEdgeAnchors`) et routage par couloirs multi-obstacles (`findCorridorWaypoint`)** recalculés en temps réel y compris lors du déplacement manuel à la souris.
    * **4 modes de disposition géométrique (`TopologyLayoutMode`)** :
      * `Layered` : Anneaux concentriques 360° autour des serveurs audités ou colonnes étagées en quinconce.
      * `ForceDirectedOrganic` : Simulation physique par forces (attraction de Hooke pondérée par l'activité des flux + répulsion de Coulomb + anti-chevauchement) ; les serveurs audités restent fixes, répartis sur un cercle pour que leurs boîtes ne se chevauchent jamais.
      * `Circular` : Anneau périmétrique uniforme à 360°.
      * `Grid` : Matrice multi-colonnes équilibrée.
    * **6 méthodes de tri des serveurs (`TopologyNodeSortMethod`)** :
      * `BarycentricSugiyama` : Heuristique barycentrique/médiane multi-passes + optimisation 2-Opt.
      * `FunctionalTier` : Stratification architecturale en 4 couches (Clients ➔ Passerelles/Ingress ➔ Applications ➔ Bases de données/Infra).
      * `SubnetCluster` : Regroupement spatial contigu par sous-réseaux IP CIDR avec enveloppes visuelles englobantes ; chaque colonne commence après son sous-réseau le plus large et l'optimiseur 2-Opt n'échange que des serveurs d'un même sous-réseau, si bien que les enveloppes ne se chevauchent jamais.
      * `ConnectionDegree` : Tri par densité de trafic (nombre de connexions décroissant).
      * `Alphabetical` : Tri alphabétique par nom d'hôte.
      * `SubnetIp` : Tri logique par sous-réseau et octets d'adresse IP.
    * **Accélération par grille spatiale hachée (`SpatialHashGrid`)** :
      * Partitionnement 2D indexant les intersections de segments et collisions boîtes/flèches par cellules locales, accélérant l'optimiseur 2-Opt sur les cartes de plus de 150 serveurs.
    * **3 modes de routage des flèches (`TopologyEdgeRoutingMode`)** :
      * `BezierCurved` : Courbes de Bézier cubiques orientées selon les normales aux bordures avec contournement par couloirs + éventail sur liens multiples.
      * `OrthogonalCorridors` : Couloirs orthogonaux à 90° avec pistes décalées anti-chevauchement ; un détour autour d'une boîte passe par la ligne ou la colonne du point de passage, selon le sens du détour.
      * `DirectStraight` : Lignes directes de bordure à bordure avec écartement parallèle.
    * **Placement des étiquettes de flèches (`placeEdgeLabels`)** :
      * Chaque étiquette `IN` / `OUT :port/protocole` se place au milieu de sa flèche ; si la place est prise, elle monte ou descend d'une demi-ligne à la fois (24 décalages au plus, soit six lignes de chaque côté) jusqu'à ne chevaucher ni une autre étiquette ni une boîte de serveur (dessinées au-dessus des étiquettes, elles les masqueraient). Les étiquettes du serveur sélectionné sont placées en premier.
      * Toutes les mises en page gardent au moins 42 px (trois lignes d'étiquettes) entre deux boîtes de serveurs empilées, même avec le profil d'espacement `Compact`, pour laisser de la place aux étiquettes des flèches qui les relient.
    * **Ancrage sur bordures & Pointes directionnelles (`IN` / `OUT`)** :
      * Répartition régulière et tri géométrique des points d'attache sur les 4 côtés de chaque serveur.
      * Distinction visuelle et chromatique des flux sortants (`OUT` en bleu cyan `#38bdf8`) et entrants (`IN` en vert émeraude `#10b981`).
    * **Profils et curseurs d'écartement (`TopologySpacingProfile`)** :
      * Profils `Compact`, `Balanced`, `Spacious` (adapté aux cartes de plus de 100 serveurs) et curseurs temps réel d'écartement des serveurs (`0.65x` à `2.4x`) et d'écartement des flèches (`6px` à `46px`).
    * **Contrôles de filtrage ergonomiques & Bouton "Apply Filters"** :
      * Bouton `Apply Filters` dédié et déclenchement par validation `[Enter]` sur les champs de recherche textuelle et de ports, supprimant les recalculs intempestifs frappe par frappe pour une fluidité maximale sur les cartes denses.
      * La recherche textuelle garde les serveurs trouvés avec leurs voisins directs et les flux qui les relient ; elle porte aussi sur le port, le processus et la cible de service de chaque flux, ce qui permet de retrouver un processus client sortant.
      * Les champs de ports acceptent des ports isolés et des plages (`"443, 8000-8080"`), comme `NetworkPortRange`.
      * Les compteurs d'en-tête (serveurs, ports de service, flux) portent sur tout le résultat filtré, toutes pages confondues.
      * Bouton `Reset` pour réinitialiser les filtres en un clic, qui met aussi à jour la commande CLI affichée.
    * **Barre latérale rétractable (`◀ Sidebar`), Sections accordéon & Élimination de l'ascenseur** :
      * Bouton `◀ Sidebar` (ou touche `[B]`) permettant de masquer/afficher instantanément le panneau latéral pour allouer 100 % de l'écran au canevas SVG.
      * Sections d'options cliquables en accordéon (`1. Layout`, `2. Filters`, `3. CLI Command`, `4. Inspector`), supprimant tout défilement vertical parasite.
    * **Reproduction CLI instantanée (`CLI Command`)** :
      * Encadré affichant la commande qui reproduit la vue courante (filtres, disposition, tri, routage, écartement). Sa syntaxe vient de `TopologyRenderingOptions.CliCommandFormat` (`TopologyCliCommandFormat` : `Prefix`, `LayoutOption`, `SortOption`, `RoutingOption`, `SpacingOption`) ; les valeurs par défaut produisent la syntaxe NetFlowAudit 1.x `NetFlowAudit.Cli --map-only ... --map-layout ...`.
      * Bouton `📋 Copy Command` pour copier la syntaxe dans le presse-papier en un clic.
    * **Export Vectoriel SVG Autonome (`Export SVG`)** :
      * Bouton intégré dans la barre d'outils permettant d'extraire la vue courante (avec filtres, sélections et déplacements manuels) sous forme de fichier vectoriel autonome `network_topology_map.svg`.
      * Calcul automatique du cadre englobant (`viewBox`) avec marges équilibrées et fond sombre vectoriel intégré (`#0b0f19`) pour insertion directe dans Microsoft Word, PowerPoint, Visio, Draw.io ou Inkscape sans perte de netteté.
    * **Ergonomie du Tableau de Bord & Filtrage Dynamique Précis** :
      * **Bouton Sidebar repositionné** : Intégré directement dans la barre d'en-tête KPI à gauche de l'indicateur *Servers*, libérant totalement les titres de sections sans aucun chevauchement.
      * **Sidebar compacte sans ascenseur par défaut** : Échelle et paddings verticaux calibrés pour afficher 100 % des options d'un coup d'œil sans barre de défilement vertical.
      * **Élimination des serveurs orphelins** : Lors de l'exclusion ou de l'inclusion de ports (ou de directions de flux), les serveurs ne conservant aucun port actif et aucune liaison connectée sont automatiquement masqués de la carte et du décompte KPI en temps réel.

---

## 📋 Protocole de Maintenance par l'Agent IA

Toute modification de TopologyForge doit respecter :
1. **Rétrocompatibilité :** Conserver tous les contrats publics existants. Utiliser des surcharges ou des paramètres optionnels.
2. **Qualité :** Zéro warning (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), zéro pragma/suppress.
3. **Tests :** Nommage `MethodeCible_ScenarioOuCondition_ResultatAttendu`.
4. **Mise à jour :** Compléter immédiatement ce fichier `FRAMEWORK.md` en cas d'ajout d'une nouvelle capacité.
5. **Validation :** Valider par `./verify.ps1`.
