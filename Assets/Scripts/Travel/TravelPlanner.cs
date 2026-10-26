using System.Collections.Generic;
using UnityEngine;

// Land-only pathfinding for physical travel. Routes characters through MapCell centers using the
// shared MapPathfinder (same graph and terrain policy as road generation, so routes never diverge
// from traversable land). Opportunistically prefers cells that are part of an existing road corridor
// (slightly cheaper G-cost).
public class TravelPlanner : MonoBehaviour
{
    public static TravelPlanner instance;

    [Header("Pathfinding")]
    [Tooltip("Multiplier applied to the cost of moving through road cells (< 1 makes roads preferred, 1 = neutral)")]
    public float roadCostMultiplier = 0.85f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    // Returns a list of world-space waypoints (Vector2) from origin to destination.
    // Falls back to a straight line if no land-only path exists.
    public List<Vector2> PlanPath(Location origin, Location destination)
    {
        if (origin?.MapCell == null || destination?.MapCell == null)
        {
            Debug.LogWarning("TravelPlanner: origin or destination has no MapCell; using straight line");
            return new List<Vector2> { origin.Position, destination.Position };
        }

        MapCell startCell = origin.MapCell;
        MapCell endCell = destination.MapCell;

        var roadCells = MapManager.RoadNetwork?.GetRoadCells() ?? new HashSet<MapCell>();

        List<MapCell> cellPath = MapPathfinder.FindPath(startCell, endCell, MapPathfinder.IsPassableTerrain,
            MapPathfinder.PreferredCellCost(roadCells, roadCostMultiplier));
        if (cellPath == null || cellPath.Count < 2)
        {
            Debug.LogWarning($"TravelPlanner: no land-only path from {origin.Name} to {destination.Name}; using straight line");
            return new List<Vector2> { origin.Position, destination.Position };
        }

        // Transform cell path into waypoints (replace last cell center with exact destination position)
        var waypoints = new List<Vector2>();
        for (int i = 0; i < cellPath.Count; i++)
        {
            if (i == cellPath.Count - 1)
                waypoints.Add(destination.Position);
            else
                waypoints.Add(cellPath[i].Center);
        }
        return waypoints;
    }

    // Routes from an arbitrary world position (e.g. a pawn mid-journey) to a destination location.
    public List<Vector2> PlanPathFrom(Vector2 start, Location destination)
    {
        if (destination?.MapCell == null)
            return new List<Vector2> { start, destination.Position };

        MapCell endCell = destination.MapCell;
        MapCell startCell = MapManager.instance != null ? MapManager.instance.GetCellAt(start) : null;
        if (startCell == null)
            startCell = FindNearestPassableCell(start);
        if (startCell == null)
            return new List<Vector2> { start, destination.Position };

        var roadCells = MapManager.RoadNetwork?.GetRoadCells() ?? new HashSet<MapCell>();

        List<MapCell> cellPath = MapPathfinder.FindPath(startCell, endCell, MapPathfinder.IsPassableTerrain,
            MapPathfinder.PreferredCellCost(roadCells, roadCostMultiplier));
        if (cellPath == null || cellPath.Count < 2)
            return new List<Vector2> { start, destination.Position };

        // Start exactly at the pawn's position, then follow the cell path to the destination
        var waypoints = new List<Vector2> { start };
        for (int i = 1; i < cellPath.Count; i++)
        {
            if (i == cellPath.Count - 1)
                waypoints.Add(destination.Position);
            else
                waypoints.Add(cellPath[i].Center);
        }
        return waypoints;
    }

    private MapCell FindNearestPassableCell(Vector2 pos)
    {
        MapCell best = null;
        float bestDist = float.MaxValue;
        foreach (var cell in MapManager.instance.map.mapCells)
        {
            if (!MapPathfinder.IsPassableTerrain(cell))
                continue;
            float dist = Vector2.Distance(cell.Center, pos);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = cell;
            }
        }
        return best;
    }
}