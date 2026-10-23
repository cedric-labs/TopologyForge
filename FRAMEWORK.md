# TopologyForge

Framework partagé .NET de modélisation topologique, de filtrage déclaratif (inclusions/exclusions) et de génération de cartographies réseau interactives autonomes (Single-File HTML hors-ligne sans dépendance externe).

## 🚀 Capacités Clés

* **Modélisation Topologique Immuable (`TopologyForge.Models`)** :
  * `NetworkPortRange` : Value Object de plage de ports réseau validée (`0..65535`), avec plage éphémère IANA par défaut (`49152-65535`) et parsing d'expressions (`"80, 443, 8000-8080"`).
  * `TopologyFlowRecord` : Enregistrement de flux ou d'écoute normalisé.
  * `TopologyServerNode`, `ServerListenerDescriptor`, `TopologyServiceEdge`, `NetworkTopologyGraph` : Graphe consolidé des serveurs (locaux et distants), de leurs ports de service non éphémères et de leurs liens directionnels.
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
* **Rendu Cartographique HTML5 Autonome (`TopologyForge.Rendering`)** :
  * `TopologyHtmlRenderer` (`ITopologyHtmlRenderer`) : Génère un fichier `.html` unique et 100 % hors-ligne (zéro lien CDN) embarquant un moteur de graphe SVG interactif (zoom/pan, sélection et mise en surbrillance des dépendances d'un serveur, filtres dynamiques de ports/hôtes en temps réel et choix de disposition hiérarchique, anneau ou grille).
