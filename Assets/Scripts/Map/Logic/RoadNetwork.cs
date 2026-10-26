using System.Collections.Generic;
using UnityEngine;

public class RoadNetwork : MonoBehaviour
{
    [System.Serializable]
    public class RoadConnection
    {
        public Location LocationA;
        public Location LocationB;
        public List<MapCell> Path;
        public GameObject RoadObject;
        public List<LineRenderer> LineRenderers = new List<LineRenderer>();
        public float RoadWidth = 2f;
        public Color RoadColor = new Color(0.5f, 0.3f, 0.1f, 1f); // brown
    }

    [Header("Road Generation Settings")]
    [SerializeField] private float maxRoadDistance = 50f;
    [SerializeField] private int maxConnectionsPerLocation = 4;
    [SerializeField] private float roadWidth = 2f;
    [SerializeField] private Color roadColor = Color.gray;
    [SerializeField] private bool enableCurvedRoads = true;
    [SerializeField] private float curveIntensity = 0.3f;

    [Header("Network Topology")]
    [Tooltip("Cost multiplier for moving through an already-built road cell while generating " +
             "(< 1 makes later roads merge onto established corridors instead of carving parallel ones)")]
    [SerializeField] private float corridorReuseMultiplier = 0.6f;

    [Header("Rendering")]
    [Tooltip("Draw order of road lines. 0 (default) keeps the existing z-based stacking " +
             "(roads above rivers); raise it to draw roads over other sorted visuals")]
    [SerializeField] private int roadSortingOrder = 0;
    [Tooltip("Draw a hub marker where three or more roads diverge")]
    [SerializeField] private bool showJunctionMarkers = true;
    [Tooltip("Radius of the junction hub disc (roads are roadWidth wide)")]
    [SerializeField] private float junctionMarkerRadius = 0.75f;

    [Header("Materials")]
    [Tooltip("Texture applied to the road LineRenderers. Leave empty for a plain color.")]
    public Texture roadTexture;

    private readonly List<RoadConnection> roads = new List<RoadConnection>();
    private readonly List<GameObject> junctionMarkers = new List<GameObject>();
    private Material sharedRoadMaterial;

    // Reference-keyed caches for province adjacency checks, rebuilt every generation.
    private Dictionary<(Province, Province), bool> provinceConnectionCache =
        new Dictionary<(Province, Province), bool>();

    public event System.Action OnRoadsChanged;

    public IReadOnlyList<RoadConnection> Roads => roads;

    public void GenerateRoadNetwork()
    {
        ClearExistingRoads();
        provinceConnectionCache.Clear();

        var locations = FindAllLocations();
        if (locations.Count < 2) return;

        // Connections come back spanning-tree first, then extra links, so trunk roads claim
        // their corridors before branch roads path onto them.
        var connections = DetermineRoadConnections(locations);
        var drawnCells = new HashSet<MapCell>();
        var corridorCells = new HashSet<MapCell>();

        foreach (var connection in connections)
        {
            var path = BuildLandPath(connection.Item1, connection.Item2, corridorCells);
            if (path == null || path.Count < 2) continue;

            var road = CreateRoad(connection.Item1, connection.Item2, path, ExtractUndrawnRuns(path, drawnCells));
            if (road == null) continue;

            foreach (var cell in path)
            {
                drawnCells.Add(cell);
                corridorCells.Add(cell);
            }
        }

        RebuildJunctionMarkers();
        OnRoadsChanged?.Invoke();
    }

    public HashSet<MapCell> GetRoadCells()
    {
        var roadCells = new HashSet<MapCell>();
        foreach (var road in roads)
        {
            if (road.Path == null) continue;
            foreach (var cell in road.Path)
            {
                roadCells.Add(cell);
            }
        }
        return roadCells;
    }

    // Splits a path into contiguous runs of cells that have not been drawn yet, so shared
    // corridors are only rendered once (by the road that claimed them first). Each run is
    // extended one cell into the neighbouring drawn corridor on both sides: the rendered line
    // then ends on the exact vertex the shared corridor passes through, so roads join at cell
    // centers instead of dangling one cell short of the junction/city.
    private List<List<MapCell>> ExtractUndrawnRuns(List<MapCell> path, HashSet<MapCell> drawnCells)
    {
        var runs = new List<List<MapCell>>();
        List<MapCell> currentRun = null;
        MapCell lastDrawn = null;

        foreach (var cell in path)
        {
            if (drawnCells.Contains(cell))
            {
                if (currentRun != null)
                {
                    currentRun.Add(cell); // bridge the run onto the shared corridor
                    runs.Add(currentRun);
                    currentRun = null;
                }
                lastDrawn = cell;
                continue;
            }

            if (currentRun == null)
            {
                currentRun = new List<MapCell>();
                if (lastDrawn != null)
                    currentRun.Add(lastDrawn); // bridge from the shared corridor into this run
            }
            currentRun.Add(cell);
        }

        if (currentRun != null)
            runs.Add(currentRun);

        return runs;
    }

    public RoadConnection AddRoad(Location locationA, Location locationB)
    {
        var roadCells = GetRoadCells();
        var path = BuildLandPath(locationA, locationB, roadCells);
        if (path == null || path.Count < 2) return null;

        var road = CreateRoad(locationA, locationB, path, ExtractUndrawnRuns(path, roadCells));
        if (road != null)
        {
            RebuildJunctionMarkers();
            OnRoadsChanged?.Invoke();
        }
        return road;
    }

    public bool RemoveRoad(RoadConnection road)
    {
        if (road == null || !roads.Contains(road)) return false;

        DestroyRoadObject(road);
        roads.Remove(road);
        RebuildRoadRenderers();
        RebuildJunctionMarkers();
        OnRoadsChanged?.Invoke();
        return true;
    }

    public bool RemoveRoad(Location locationA, Location locationB)
    {
        RoadConnection match = roads.Find(r =>
            (r.LocationA == locationA && r.LocationB == locationB) ||
            (r.LocationA == locationB && r.LocationB == locationA));
        return RemoveRoad(match);
    }

    private void ClearExistingRoads()
    {
        foreach (var road in roads)
        {
            DestroyRoadObject(road);
        }
        roads.Clear();

        foreach (var marker in junctionMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        junctionMarkers.Clear();
    }

    private void DestroyRoadObject(RoadConnection road)
    {
        if (road == null) return;

        if (road.RoadObject != null)
            Destroy(road.RoadObject);
        if (road.LineRenderers != null)
            road.LineRenderers.Clear();
    }

    // Rebuilds visible lines for all remaining roads from their stored paths. Used after a
    // removal so corridors shared with the deleted road get claimed by another road.
    private void RebuildRoadRenderers()
    {
        var drawnCells = new HashSet<MapCell>();
        foreach (var road in roads)
        {
            RedrawRoad(road, ExtractUndrawnRuns(road.Path, drawnCells));
            foreach (var cell in road.Path)
            {
                drawnCells.Add(cell);
            }
        }
    }

    private void RedrawRoad(RoadConnection road, List<List<MapCell>> runs)
    {
        if (road.LineRenderers != null)
        {
            foreach (var lr in road.LineRenderers)
            {
                if (lr != null)
                    Destroy(lr.gameObject);
            }
            road.LineRenderers.Clear();
        }

        if (road.RoadObject != null)
            road.LineRenderers = CreateLineRenderers(road.RoadObject, runs);
    }

    // --- Junction markers ---------------------------------------------------------------

    // Places a hub marker at every cell where three or more distinct road directions meet
    // (cells that simply carry several roads side by side along a shared corridor do not
    // qualify; location cells are skipped because cities have their own marker).
    private void RebuildJunctionMarkers()
    {
        foreach (var marker in junctionMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        junctionMarkers.Clear();

        if (!showJunctionMarkers || roads.Count == 0) return;

        var directions = new Dictionary<MapCell, HashSet<(MapCell, MapCell)>>();
        foreach (var road in roads)
        {
            var path = road.Path;
            if (path == null || path.Count < 3) continue;

            // Road ends are location cells (covered by the city marker), so only interior cells.
            for (int i = 1; i < path.Count - 1; i++)
            {
                if (!directions.TryGetValue(path[i], out var set))
                    directions[path[i]] = set = new HashSet<(MapCell, MapCell)>();
                set.Add(CanonicalPair(path[i - 1], path[i + 1]));
            }
        }

        if (directions.Count == 0) return;

        var locationCells = new HashSet<MapCell>();
        foreach (var location in FindAllLocations())
        {
            if (location.MapCell != null) locationCells.Add(location.MapCell);
        }

        foreach (var kvp in directions)
        {
            if (kvp.Value.Count < 3) continue;
            if (locationCells.Contains(kvp.Key)) continue;
            junctionMarkers.Add(CreateJunctionMarker(kvp.Key.Center));
        }
    }

    // Order-independent representation of the pair of cells a road passes between, so roads
    // running side by side along a shared corridor map to the same key.
    private static (MapCell, MapCell) CanonicalPair(MapCell a, MapCell b)
    {
        if (a == null || b == null) return (a, b);
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a) <=
               System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(b)
            ? (a, b)
            : (b, a);
    }

    private GameObject CreateJunctionMarker(Vector2 center)
    {
        const int segments = 24;
        float radius = Mathf.Max(0.1f, junctionMarkerRadius);

        var markerGO = new GameObject("JunctionMarker");
        markerGO.transform.SetParent(transform, false);

        var lineRenderer = markerGO.AddComponent<LineRenderer>();
        lineRenderer.sharedMaterial = GetRoadMaterial();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true;
        // Width slightly over twice the radius so the ring reads as a filled hub disc.
        lineRenderer.startWidth = radius * 2.1f;
        lineRenderer.endWidth = radius * 2.1f;
        lineRenderer.numCapVertices = 4;
        lineRenderer.sortingOrder = roadSortingOrder;

        var positions = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            positions[i] = new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius,
                -0.1f); // same layer as roads
        }
        lineRenderer.positionCount = segments;
        lineRenderer.SetPositions(positions);
        return markerGO;
    }

    // --- Connection topology ------------------------------------------------------------

    private List<Location> FindAllLocations()
    {
        var locations = new List<Location>();

        if (MapManager.instance != null)
        {
            // Get only Holdings (Castles, Cities, Churches) from MapManager
            foreach (var province in MapManager.instance.provincesDuJure)
            {
                if (province.Locations != null)
                {
                    foreach (var location in province.Locations)
                    {
                        // Only add Holdings (Castles, Cities, Churches) in traversable terrain
                        if (location.IsHolding &&
                            location.MapCell.TerrainType != TerrainType.Mountain &&
                            location.MapCell.TerrainType != TerrainType.Ocean &&
                            location.MapCell.TerrainType != TerrainType.LargeLake)
                        {
                            locations.Add(location);
                        }
                    }
                }
            }
        }
        return locations;
    }

    // Picks which location pairs get a road, in priority order:
    //   1. a spanning tree over all reachable locations (every holding gets a road, degree cap
    //      respected where possible),
    //   2. any edges still needed to join leftover components (connectivity beats the cap),
    //   3. extra local links up to the degree cap for a denser, more redundant network.
    // Roads are then generated in that order, so trunk corridors are claimed first.
    private List<(Location, Location)> DetermineRoadConnections(List<Location> locations)
    {
        var result = new List<(Location, Location)>();
        int n = locations.Count;
        if (n < 2) return result;

        // Candidate edges: within range and between (adjacent) provinces.
        var edges = new List<(int a, int b, float weight)>();
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                float distance = Vector2.Distance(locations[i].MapCell.Center, locations[j].MapCell.Center);
                if (distance > maxRoadDistance) continue;
                if (!AreProvincesConnected(locations[i].Province, locations[j].Province)) continue;
                edges.Add((i, j, distance));
            }
        }
        edges.Sort((x, y) => x.weight.CompareTo(y.weight));

        var degree = new int[n];
        var linked = new HashSet<(int, int)>();
        var unionParent = new int[n];
        for (int i = 0; i < n; i++) unionParent[i] = i;

        int Find(int x)
        {
            while (unionParent[x] != x)
            {
                unionParent[x] = unionParent[unionParent[x]];
                x = unionParent[x];
            }
            return x;
        }

        void AddEdge(int i, int j)
        {
            linked.Add((i, j));
            degree[i]++;
            degree[j]++;
            unionParent[Find(i)] = Find(j);
            result.Add((locations[i], locations[j]));
        }

        // 1) Spanning tree, honouring the degree cap wherever possible.
        foreach (var (i, j, _) in edges)
        {
            if (Find(i) == Find(j)) continue;
            if (degree[i] >= maxConnectionsPerLocation || degree[j] >= maxConnectionsPerLocation) continue;
            AddEdge(i, j);
        }

        // 2) Join any leftover components; connectivity wins over the degree cap.
        foreach (var (i, j, _) in edges)
        {
            if (Find(i) == Find(j)) continue;
            AddEdge(i, j);
        }

        // 3) Extra local links up to the degree cap.
        foreach (var (i, j, _) in edges)
        {
            if (linked.Contains((i, j))) continue;
            if (degree[i] >= maxConnectionsPerLocation || degree[j] >= maxConnectionsPerLocation) continue;
            AddEdge(i, j);
        }

        return result;
    }

    private bool AreProvincesConnected(Province provinceA, Province provinceB)
    {
        if (provinceA == null || provinceB == null) return false;

        // Same province
        if (provinceA == provinceB) return true;

        if (provinceConnectionCache.TryGetValue((provinceA, provinceB), out bool cached))
            return cached;

        // Provinces are adjacent when any cell of A has a neighbour belonging to B
        // (O(1) reference checks instead of scanning B's cell list per neighbour).
        bool connected = false;
        if (provinceA.MapCells != null)
        {
            foreach (var cellA in provinceA.MapCells)
            {
                foreach (var neighbor in cellA.Neighbors)
                {
                    if (neighbor.Province == provinceB)
                    {
                        connected = true;
                        break;
                    }
                }
                if (connected) break;
            }
        }

        provinceConnectionCache[(provinceA, provinceB)] = connected;
        provinceConnectionCache[(provinceB, provinceA)] = connected;
        return connected;
    }

    // --- Path building -----------------------------------------------------------------

    private List<MapCell> BuildLandPath(Location start, Location end, ISet<MapCell> preferRoadCells)
    {
        if (start?.MapCell == null || end?.MapCell == null) return null;

        var startCell = start.MapCell;
        var endCell = end.MapCell;

        // Don't build roads to/from mountains, oceans, or large lakes
        if (!MapPathfinder.IsPassableTerrain(startCell) || !MapPathfinder.IsPassableTerrain(endCell))
            return null;

        // Bias the path onto corridors built by earlier roads so the network merges into
        // shared trunks instead of carving parallel strands across the map.
        var stepCost = MapPathfinder.PreferredCellCost(preferRoadCells, corridorReuseMultiplier);

        return MapPathfinder.FindPath(startCell, endCell, MapPathfinder.IsPassableTerrain, stepCost);
    }

    // --- Rendering ---------------------------------------------------------------------

    private RoadConnection CreateRoad(Location locationA, Location locationB, List<MapCell> path, List<List<MapCell>> runs)
    {
        if (path.Count < 2) return null;

        var roadGO = new GameObject($"Road_{locationA.Name}_to_{locationB.Name}");
        roadGO.transform.SetParent(transform);

        // A road whose corridor is entirely claimed by earlier roads has no lines of its own
        // to draw, but still exists as a connection (travel, removal, forest clearance).
        var lineRenderers = CreateLineRenderers(roadGO, runs);

        var road = new RoadConnection
        {
            LocationA = locationA,
            LocationB = locationB,
            Path = path,
            RoadObject = roadGO,
            LineRenderers = lineRenderers,
            RoadWidth = roadWidth,
            RoadColor = roadColor
        };

        roads.Add(road);
        return road;
    }

    private List<LineRenderer> CreateLineRenderers(GameObject parent, List<List<MapCell>> runs)
    {
        var lineRenderers = new List<LineRenderer>();
        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (run.Count < 2) continue;

            var lineGO = new GameObject($"Segment_{i}");
            lineGO.transform.SetParent(parent.transform, false);

            var lineRenderer = lineGO.AddComponent<LineRenderer>();

            // Configure LineRenderer
            lineRenderer.sharedMaterial = GetRoadMaterial();
            // Repeat the road texture along the line instead of stretching one copy over it.
            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCapVertices = 8;
            lineRenderer.numCornerVertices = 8;
            lineRenderer.sortingOrder = roadSortingOrder;

            // Create road path through cell centers
            var positions = new Vector3[run.Count];
            for (int j = 0; j < run.Count; j++)
            {
                positions[j] = new Vector3(run[j].Center.x, run[j].Center.y, -0.1f); // Slightly below other objects
            }

            // Apply curves if enabled
            if (enableCurvedRoads && run.Count > 2)
            {
                positions = CreateCurvedPath(positions, curveIntensity);
            }

            lineRenderer.positionCount = positions.Length;
            lineRenderer.SetPositions(positions);
            lineRenderers.Add(lineRenderer);
        }
        return lineRenderers;
    }

    // One shared material for every road (and junction marker): no per-line material
    // instances, and tinting roads never mutates the source asset.
    private Material GetRoadMaterial()
    {
        if (sharedRoadMaterial == null)
            sharedRoadMaterial = new Material(Shader.Find("Sprites/Default"));

        // Apply the road texture (kept on the one shared material, so no per-line instances).
        if (sharedRoadMaterial.mainTexture != roadTexture)
            sharedRoadMaterial.mainTexture = roadTexture;
        sharedRoadMaterial.color = roadColor;
        return sharedRoadMaterial;
    }

    // Smooths a polyline by inserting a bowed midpoint per segment. The bow uses centred
    // noise (bends both ways instead of always one side) and tapers to zero at the ends of
    // the run, so junctions and location approaches stay perfectly straight and every road
    // meeting at a cell center touches it exactly.
    private Vector3[] CreateCurvedPath(Vector3[] originalPath, float intensity)
    {
        int count = originalPath.Length;
        var curvedPath = new List<Vector3>(count * 2 - 1);

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 start = originalPath[i];
            Vector3 end = originalPath[i + 1];

            curvedPath.Add(start);

            if (count > 2)
            {
                Vector3 mid = Vector3.Lerp(start, end, 0.5f);

                // Zero at both ends of the run, full strength in the middle.
                float taper = Mathf.Sin((i + 0.5f) / (count - 1) * Mathf.PI);

                // Centred noise in [-1, 1] so roads bow in both directions.
                float noise = Mathf.PerlinNoise(start.x * 0.1f + 17.3f, start.y * 0.1f + 9.7f) * 2f - 1f;

                Vector3 direction = (end - start).normalized;
                Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0);

                mid += perpendicular * (noise * intensity * taper * Vector2.Distance(start, end));
                curvedPath.Add(mid);
            }
        }

        curvedPath.Add(originalPath[count - 1]);
        return curvedPath.ToArray();
    }
}
