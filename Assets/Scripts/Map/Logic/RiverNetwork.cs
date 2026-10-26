using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RiverNetwork : MonoBehaviour
{
    [System.Serializable]
    public class RiverConnection
    {
        public MapCell Source;
        public MapCell Destination;
        public List<Vector2> EdgePoints; // Points along cell borders
        public Vector2 StartPoint; // River source at cell border corner
        public Vector2 EndPoint; // River mouth at cell border corner
        public LineRenderer LineRenderer;
        public float RiverWidth = 1.5f;
        public Color RiverColor = new Color(0.2f, 0.4f, 0.8f, 0.8f); // Blue
        public List<CellBorder> Borders = new List<CellBorder>(); // Borders used by this river
    }

    [Header("River Generation Settings")]
    [SerializeField] private int maxRiversPerMap = 15;
    [SerializeField] private float riverWidth = 1.5f;
    [SerializeField] private Color riverColor = new Color(0.2f, 0.4f, 0.8f, 0.8f);
    [SerializeField] private float minRiverLength = 10f;
    [SerializeField] private float maxRiverLength = 100f;
    [SerializeField] private float riverGenerationChance = 0.6f; // Chance per mountain to generate a river
    [SerializeField] private float minDistanceBetweenRivers = 8f; // Minimum distance between river sources
    [SerializeField] private float mountainCompetitionRadius = 15f; // Radius for mountain competition (clusters compete for water)


    [Header("Materials")]
    [SerializeField] private Material riverMaterial;


    private List<RiverConnection> rivers = new List<RiverConnection>();

    public void GenerateRiverNetwork()
    {
        var mountainCells = FindMountainCells();
        var oceanCells = FindOceanCells();

        if (mountainCells.Count == 0 || oceanCells.Count == 0)
        {
            Debug.LogWarning("RiverNetwork: No mountains or oceans found for river generation");
            return;
        }

        int riverCount = 0;
        var usedMountains = new HashSet<MapCell>();
        var allRiverCells = new List<MapCell>(); // Track all cells that have rivers

        // Filter mountains by elevation and apply chance-based generation
        var eligibleMountains = mountainCells
            .Where(mountain => IsMountainEligibleForRiver(mountain))
            .OrderBy(x => Random.value)
            .ToList();

        foreach (var mountain in eligibleMountains)
        {
            if (riverCount >= maxRiversPerMap) break;
            if (usedMountains.Contains(mountain)) continue;

            // Apply chance-based river generation
            if (Random.value > riverGenerationChance) continue;

            var nearestOcean = FindNearestOcean(mountain, oceanCells);
            if (nearestOcean == null) continue;

            var riverPath = FindRiverBorderPath(mountain, nearestOcean);
            if (riverPath != null && riverPath.Count > 2)
            {
                float riverLength = CalculateBorderPathLength(riverPath);
                if (riverLength >= minRiverLength && riverLength <= maxRiverLength)
                {
                    CreateRiver(mountain, nearestOcean, riverPath);
                    usedMountains.Add(mountain);
                    riverCount++;
                }
            }
        }

        // Mark all river borders after generating all rivers
        MarkAllRiverBorders();

        //Debug.Log($"RiverNetwork: Generated {rivers.Count} rivers from {eligibleMountains.Count} eligible mountain sources out of {mountainCells.Count} total mountains");
    }

    // Mark river borders for all generated rivers
    private void MarkAllRiverBorders()
    {
        foreach (var river in rivers)
        {
            foreach (var border in river.Borders)
            {
                border.AddFlag(BorderType.River);
            }
        }
    }


    private List<MapCell> FindMountainCells()
    {
        var mountainCells = new List<MapCell>();

        if (MapManager.instance != null && MapManager.instance.map != null)
        {
            foreach (var cell in MapManager.instance.map.mapCells)
            {
                if (cell.TerrainType == TerrainType.Mountain)
                {
                    mountainCells.Add(cell);
                }
            }
        }

        return mountainCells;
    }

    private List<MapCell> FindOceanCells()
    {
        var oceanCells = new List<MapCell>();

        if (MapManager.instance != null && MapManager.instance.map != null)
        {
            foreach (var cell in MapManager.instance.map.mapCells)
            {
                if (cell.TerrainType == TerrainType.Ocean)
                {
                    oceanCells.Add(cell);
                }
            }
        }

        return oceanCells;
    }

    private bool IsMountainEligibleForRiver(MapCell mountain)
    {
        // Check distance from existing river sources
        if (rivers.Count > 0)
        {
            foreach (var river in rivers)
            {
                float distanceFromSource = Vector2.Distance(mountain.Center, river.Source.Center);
                if (distanceFromSource < minDistanceBetweenRivers)
                {
                    return false; // Too close to existing river source
                }
            }
        }

        // Check mountain competition (high-density mountain areas have reduced chance)
        var nearbyMountains = FindNearbyMountains(mountain, mountainCompetitionRadius);
        if (nearbyMountains.Count > 3)
        {
            // In mountain clusters, reduce chance based on competition
            float competitionPenalty = (nearbyMountains.Count - 3) * 0.1f;
            if (Random.value < competitionPenalty)
            {
                return false;
            }
        }

        return true;
    }

    private List<MapCell> FindNearbyMountains(MapCell mountain, float radius)
    {
        var nearbyMountains = new List<MapCell>();

        if (MapManager.instance != null && MapManager.instance.map != null)
        {
            foreach (var cell in MapManager.instance.map.mapCells)
            {
                if (cell.TerrainType == TerrainType.Mountain && cell != mountain)
                {
                    float distance = Vector2.Distance(mountain.Center, cell.Center);
                    if (distance <= radius)
                    {
                        nearbyMountains.Add(cell);
                    }
                }
            }
        }

        return nearbyMountains;
    }

    private List<Vector2> FindRiverBorderPath(MapCell start, MapCell end)
    {
        if (start == null || end == null) return null;

        // Build a graph of border points and edges
        var borderGraph = BuildBorderGraph(start, end);
        if (borderGraph.Count == 0) return null;

        // A* pathfinding on border graph
        var startPoint = FindBestBorderPoint(start, borderGraph);

        // For water cells, don't specify an end point - let pathfinding stop when it hits water
        List<Vector2> path;
        if (end.TerrainType == TerrainType.Ocean || end.TerrainType == TerrainType.LargeLake)
        {
            path = FindPathToWater(startPoint, end, borderGraph, start);
        }
        else
        {
            var endPoint = FindBestBorderPoint(end, borderGraph);
            path = FindPathOnBorderGraph(startPoint, endPoint, borderGraph, start, end);
        }

        // Check if path actually reaches the water cell
        if (path == null || path.Count < 2)
        {
            return null; // Path doesn't actually reach water
        }

        return path;
    }

    private Dictionary<Vector2, List<Vector2>> BuildBorderGraph(MapCell start, MapCell end)
    {
        var graph = new Dictionary<Vector2, List<Vector2>>();

        // Get all relevant cells (start, end, and their neighbors)
        var relevantCells = new HashSet<MapCell>();
        var queue = new Queue<MapCell>();

        relevantCells.Add(start);
        relevantCells.Add(end);
        queue.Enqueue(start);
        queue.Enqueue(end);

        // Expand to include neighboring cells for connectivity
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in current.Neighbors)
            {
                if (!relevantCells.Contains(neighbor) &&
                    neighbor.TerrainType != TerrainType.LargeLake)
                {
                    relevantCells.Add(neighbor);

                    // Only add neighbors if they're on a reasonable path
                    var distToStart = Vector2.Distance(neighbor.Center, start.Center);
                    var distToEnd = Vector2.Distance(neighbor.Center, end.Center);
                    var totalDist = distToStart + distToEnd;
                    var directDist = Vector2.Distance(start.Center, end.Center);

                    if (totalDist <= directDist * 2.0f) // Don't wander too far off
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // OPTIMIZED: Build graph from pre-computed CellBorder data with length tracking
        foreach (var cell in relevantCells)
        {
            foreach (var border in cell.Borders)
            {
                var otherCell = border.GetOtherCell(cell);
                if (otherCell == null || !relevantCells.Contains(otherCell))
                    continue;

                // Use pre-computed border data - no expensive edge detection
                Vector2 edgeStart = border.Start;
                Vector2 edgeEnd = border.End;
                Vector2 midpoint = border.Midpoint;

                // Add edges to graph
                if (!graph.ContainsKey(edgeStart))
                    graph[edgeStart] = new List<Vector2>();
                if (!graph.ContainsKey(edgeEnd))
                    graph[edgeEnd] = new List<Vector2>();
                if (!graph.ContainsKey(midpoint))
                    graph[midpoint] = new List<Vector2>();

                // Connect the border points
                graph[edgeStart].Add(edgeEnd);
                graph[edgeStart].Add(midpoint);
                graph[edgeEnd].Add(edgeStart);
                graph[edgeEnd].Add(midpoint);
                graph[midpoint].Add(edgeStart);
                graph[midpoint].Add(edgeEnd);
            }
        }

        return graph;
    }

    // Cache for border costs to avoid repeated calculations during pathfinding
    private Dictionary<(Vector2, Vector2), float> _borderCostCache = new Dictionary<(Vector2, Vector2), float>();

    private float GetBorderCost(Vector2 pointA, Vector2 pointB, MapCell start, MapCell end)
    {
        var key = (pointA, pointB);
        if (_borderCostCache.TryGetValue(key, out float cost))
            return cost;

        // Find the border that contains these points
        var relevantCells = new HashSet<MapCell> { start, end };
        var queue = new Queue<MapCell>(relevantCells);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in current.Neighbors)
            {
                if (!relevantCells.Contains(neighbor) && neighbor.TerrainType != TerrainType.LargeLake)
                {
                    var distToStart = Vector2.Distance(neighbor.Center, start.Center);
                    var distToEnd = Vector2.Distance(neighbor.Center, end.Center);
                    var totalDist = distToStart + distToEnd;
                    var directDist = Vector2.Distance(start.Center, end.Center);

                    if (totalDist <= directDist * 2.0f)
                    {
                        relevantCells.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        foreach (var cell in relevantCells)
        {
            foreach (var border in cell.Borders)
            {
                if ((border.ContainsPoint(pointA, 0.1f) && border.ContainsPoint(pointB, 0.1f)) ||
                    (Vector2.Distance(pointA, border.Start) < 0.1f && Vector2.Distance(pointB, border.End) < 0.1f) ||
                    (Vector2.Distance(pointA, border.End) < 0.1f && Vector2.Distance(pointB, border.Start) < 0.1f))
                {
                    _borderCostCache[key] = border.Length;
                    _borderCostCache[(pointB, pointA)] = border.Length; // Cache both directions
                    return border.Length;
                }

                // Check midpoint connections (use half border length)
                if ((Vector2.Distance(pointA, border.Midpoint) < 0.1f && (border.ContainsPoint(pointB, 0.1f) || Vector2.Distance(pointB, border.Start) < 0.1f || Vector2.Distance(pointB, border.End) < 0.1f)) ||
                    (Vector2.Distance(pointB, border.Midpoint) < 0.1f && (border.ContainsPoint(pointA, 0.1f) || Vector2.Distance(pointA, border.Start) < 0.1f || Vector2.Distance(pointA, border.End) < 0.1f)))
                {
                    _borderCostCache[key] = border.Length * 0.5f;
                    _borderCostCache[(pointB, pointA)] = border.Length * 0.5f;
                    return border.Length * 0.5f;
                }
            }
        }

        // Fallback to direct distance
        _borderCostCache[key] = Vector2.Distance(pointA, pointB);
        _borderCostCache[(pointB, pointA)] = Vector2.Distance(pointA, pointB);
        return Vector2.Distance(pointA, pointB);
    }

    private Vector2 FindBestBorderPoint(MapCell cell, Dictionary<Vector2, List<Vector2>> graph)
    {
        if (cell.Polygon == null || graph.Count == 0)
            return cell.Center;

        // For water cells (ocean/lake), we don't need a specific end point anymore
        // The pathfinding will stop when it hits the water border
        if (cell.TerrainType == TerrainType.Ocean || cell.TerrainType == TerrainType.LargeLake)
        {
            return cell.Center; // This won't actually be used for water cells
        }

        // OPTIMIZED: Use pre-computed CellBorder data instead of graph search
        Vector2 bestPoint = cell.Center;
        float bestScore = float.MaxValue;

        // Check all border points (start, end, midpoints) - much faster than graph iteration
        foreach (var border in cell.Borders)
        {
            // Check start point
            if (PointOnCellBorder(border.Start, cell))
            {
                float score = Vector2.Distance(border.Start, cell.Center);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPoint = border.Start;
                }
            }

            // Check end point
            if (PointOnCellBorder(border.End, cell))
            {
                float score = Vector2.Distance(border.End, cell.Center);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPoint = border.End;
                }
            }

            // Check midpoint
            if (PointOnCellBorder(border.Midpoint, cell))
            {
                float score = Vector2.Distance(border.Midpoint, cell.Center);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPoint = border.Midpoint;
                }
            }
        }

        return bestPoint;
    }
    private bool PointOnCellBorder(Vector2 point, MapCell cell)
    {
        // OPTIMIZED: Use pre-computed CellBorder data instead of polygon iteration
        foreach (var border in cell.Borders)
        {
            if (border.ContainsPoint(point, 0.1f))
                return true;
        }
        return false;
    }
    private List<Vector2> FindPathToWater(Vector2 start, MapCell waterCell, Dictionary<Vector2, List<Vector2>> graph, MapCell originalStart)
    {
        if (!graph.ContainsKey(start))
            return new List<Vector2> { start };

        var openSet = new List<Vector2> { start };
        var closedSet = new HashSet<Vector2>();
        var cameFrom = new Dictionary<Vector2, Vector2>();
        var gScore = new Dictionary<Vector2, float> { [start] = 0 };
        var fScore = new Dictionary<Vector2, float> { [start] = Vector2.Distance(start, waterCell.Center) };

        while (openSet.Count > 0)
        {
            // Find node with lowest fScore
            Vector2 current = openSet.OrderBy(n => fScore.GetValueOrDefault(n, float.MaxValue)).First();

            // Check if this point is on the water cell border - if so, we're done!
            if (PointOnCellBorder(current, waterCell))
            {
                return ReconstructBorderPath(cameFrom, current);
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (var neighbor in graph[current])
            {
                if (closedSet.Contains(neighbor)) continue;

                float tentativeGScore = gScore[current] + GetBorderCost(current, neighbor, originalStart, waterCell);

                if (!openSet.Contains(neighbor))
                {
                    openSet.Add(neighbor);
                }
                else if (tentativeGScore >= gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = gScore[neighbor] + Vector2.Distance(neighbor, waterCell.Center);
            }
        }

        return new List<Vector2> { start }; // Fallback - just start point
    }

    private List<Vector2> FindPathOnBorderGraph(Vector2 start, Vector2 end, Dictionary<Vector2, List<Vector2>> graph, MapCell originalStart, MapCell originalEnd)
    {
        if (!graph.ContainsKey(start) || !graph.ContainsKey(end))
            return new List<Vector2> { start, end };

        var openSet = new List<Vector2> { start };
        var closedSet = new HashSet<Vector2>();
        var cameFrom = new Dictionary<Vector2, Vector2>();
        var gScore = new Dictionary<Vector2, float> { [start] = 0 };
        var fScore = new Dictionary<Vector2, float> { [start] = Vector2.Distance(start, end) };

        while (openSet.Count > 0)
        {
            // Find node with lowest fScore
            Vector2 current = openSet.OrderBy(n => fScore.GetValueOrDefault(n, float.MaxValue)).First();

            if (current == end)
            {
                return ReconstructBorderPath(cameFrom, current);
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (var neighbor in graph[current])
            {
                if (closedSet.Contains(neighbor)) continue;

                float tentativeGScore = gScore[current] + GetBorderCost(current, neighbor, originalStart, originalEnd);

                if (!openSet.Contains(neighbor))
                {
                    openSet.Add(neighbor);
                }
                else if (tentativeGScore >= gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = gScore[neighbor] + Vector2.Distance(neighbor, end);
            }
        }

        return new List<Vector2> { start, end }; // Fallback direct path
    }

    private List<Vector2> ReconstructBorderPath(Dictionary<Vector2, Vector2> cameFrom, Vector2 current)
    {
        var path = new List<Vector2> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }


    private string GetCellId(MapCell cell)
    {
        // Use the cell's center position as a unique identifier
        return $"{cell.Center.x:F1}_{cell.Center.y:F1}";
    }


    private float CalculateBorderPathLength(List<Vector2> path)
    {
        if (path.Count < 2) return 0f;

        float length = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            length += Vector2.Distance(path[i], path[i + 1]);
        }
        return length;
    }

    private List<CellBorder> GetBordersFromPath(List<Vector2> borderPath, MapCell source, MapCell destination)
    {
        var riverBorders = new List<CellBorder>();
        var processedBorders = new HashSet<CellBorder>();

        // Get all relevant cells for border search
        var relevantCells = new HashSet<MapCell>();
        var queue = new Queue<MapCell>();

        relevantCells.Add(source);
        relevantCells.Add(destination);
        queue.Enqueue(source);
        queue.Enqueue(destination);

        // Expand to include neighboring cells for connectivity
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in current.Neighbors)
            {
                if (!relevantCells.Contains(neighbor) &&
                    neighbor.TerrainType != TerrainType.LargeLake)
                {
                    relevantCells.Add(neighbor);

                    // Only add neighbors if they're on a reasonable path
                    var distToStart = Vector2.Distance(neighbor.Center, source.Center);
                    var distToEnd = Vector2.Distance(neighbor.Center, destination.Center);
                    var totalDist = distToStart + distToEnd;
                    var directDist = Vector2.Distance(source.Center, destination.Center);

                    if (totalDist <= directDist * 2.0f) // Don't wander too far off
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // For each segment in the path, find the corresponding border
        for (int i = 0; i < borderPath.Count - 1; i++)
        {
            Vector2 pointA = borderPath[i];
            Vector2 pointB = borderPath[i + 1];

            foreach (var cell in relevantCells)
            {
                foreach (var border in cell.Borders)
                {
                    if (processedBorders.Contains(border)) continue;

                    // Check if this border contains both points of the path segment
                    if ((border.ContainsPoint(pointA, 0.1f) && border.ContainsPoint(pointB, 0.1f)) ||
                        (Vector2.Distance(pointA, border.Start) < 0.1f && Vector2.Distance(pointB, border.End) < 0.1f) ||
                        (Vector2.Distance(pointA, border.End) < 0.1f && Vector2.Distance(pointB, border.Start) < 0.1f) ||
                        // Check midpoint connections
                        (Vector2.Distance(pointA, border.Midpoint) < 0.1f && (border.ContainsPoint(pointB, 0.1f) || Vector2.Distance(pointB, border.Start) < 0.1f || Vector2.Distance(pointB, border.End) < 0.1f)) ||
                        (Vector2.Distance(pointB, border.Midpoint) < 0.1f && (border.ContainsPoint(pointA, 0.1f) || Vector2.Distance(pointA, border.Start) < 0.1f || Vector2.Distance(pointA, border.End) < 0.1f)))
                    {
                        riverBorders.Add(border);
                        processedBorders.Add(border);
                    }
                }
            }
        }

        return riverBorders;
    }

    private void CreateRiver(MapCell source, MapCell destination, List<Vector2> borderPath)
    {
        if (borderPath.Count < 2) return;

        // Get the CellBorder objects used by this river path
        var riverBorders = GetBordersFromPath(borderPath, source, destination);

        var riverGO = new GameObject($"River_{GetCellId(source)}_to_{GetCellId(destination)}");
        riverGO.transform.SetParent(transform);

        var lineRenderer = riverGO.AddComponent<LineRenderer>();

        // Configure LineRenderer
        lineRenderer.material = riverMaterial ? riverMaterial : new Material(Shader.Find("Sprites/Default"));
        lineRenderer.material.color = riverColor;
        lineRenderer.startWidth = riverWidth;
        lineRenderer.endWidth = riverWidth * 0.7f; // Slightly narrower at mouth
        lineRenderer.useWorldSpace = true;
        lineRenderer.numCapVertices = 8;
        lineRenderer.numCornerVertices = 8;

        // Create final river path
        var positions = CreateFinalBorderPath(borderPath);

        lineRenderer.positionCount = positions.Length;
        lineRenderer.SetPositions(positions);

        var river = new RiverConnection
        {
            Source = source,
            Destination = destination,
            EdgePoints = positions.Skip(1).Take(positions.Length - 2).Select(v => (Vector2)v).ToList(),
            StartPoint = positions.First(),
            EndPoint = positions.Last(),
            LineRenderer = lineRenderer,
            RiverWidth = riverWidth,
            RiverColor = riverColor,
            Borders = riverBorders
        };

        rivers.Add(river);
    }

    private Vector3[] CreateFinalBorderPath(List<Vector2> borderPath)
    {
        var positions = new List<Vector3>();

        for (int i = 0; i < borderPath.Count; i++)
        {
            Vector2 current = borderPath[i];
            positions.Add(new Vector3(current.x, current.y, -0.15f));
        }

        return positions.ToArray();
    }
    private MapCell FindNearestOcean(MapCell cell, List<MapCell> oceanCells)
    {
        if (cell == null || oceanCells == null || oceanCells.Count == 0)
            return null;

        MapCell nearestOcean = null;
        float minDistance = float.MaxValue;

        foreach (var ocean in oceanCells)
        {
            float distance = Vector2.Distance(cell.Center, ocean.Center);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestOcean = ocean;
            }
        }

        return nearestOcean;
    }
    public List<RiverConnection> GetRivers()
    {
        return rivers;
    }
}