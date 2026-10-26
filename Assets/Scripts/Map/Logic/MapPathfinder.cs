using System.Collections.Generic;
using UnityEngine;

// Single source of truth for map-cell pathfinding. Policy-neutral: callers decide what is
// passable and how much a step costs. All consumers (RoadNetwork, TravelPlanner) search the
// same graph (MapCell.Neighbors), so land routes and travel routes can never diverge.
public static class MapPathfinder
{
    // Fraction of a step cost added per unit of turn. Resolves equal-cost ties in favour of
    // straight continuations, so generated roads (and travel routes) don't zigzag across the
    // grid. Kept tiny so it never overrides a genuinely cheaper route.
    private const float TurnPenaltyFactor = 0.005f;

    // Shared terrain policy for land routes: all traversable (non-water, non-mountain) terrain.
    public static bool IsPassableTerrain(MapCell cell)
    {
        return cell.TerrainType != TerrainType.Ocean &&
               cell.TerrainType != TerrainType.LargeLake &&
               cell.TerrainType != TerrainType.Mountain;
    }

    // Step-cost factory that discounts movement through preferred cells (e.g. existing road cells),
    // so paths physically converge onto and reuse those cells instead of carving parallel corridors.
    public static System.Func<MapCell, MapCell, float> PreferredCellCost(ISet<MapCell> preferred, float multiplier)
    {
        return (current, neighbor) =>
        {
            float cost = Vector2.Distance(current.Center, neighbor.Center);
            if (preferred != null && preferred.Contains(neighbor))
                cost *= multiplier;
            return cost;
        };
    }

    // Ordered list of MapCells from start to end, or null if no path exists.
    // isPassable defaults to the shared terrain policy; stepCost defaults to Euclidean distance.
    public static List<MapCell> FindPath(MapCell start, MapCell end,
        System.Func<MapCell, bool> isPassable = null,
        System.Func<MapCell, MapCell, float> stepCost = null)
    {
        if (start == null || end == null) return null;
        if (isPassable == null) isPassable = IsPassableTerrain;
        if (stepCost == null) stepCost = (a, b) => Vector2.Distance(a.Center, b.Center);

        var cameFrom = new Dictionary<MapCell, MapCell>();
        var gScore = new Dictionary<MapCell, float> { [start] = 0f };
        var closedSet = new HashSet<MapCell>();
        var openSet = new MinHeap();

        openSet.Push(Vector2.Distance(start.Center, end.Center), start);

        while (openSet.Count > 0)
        {
            MapCell current = openSet.PopMin();

            // Skip stale heap entries (a cell can be pushed multiple times when its g-score
            // improves; the best entry pops first and the rest are already settled).
            if (!closedSet.Add(current)) continue;

            if (current == end)
                return ReconstructPath(cameFrom, current);

            foreach (var neighbor in current.Neighbors)
            {
                if (closedSet.Contains(neighbor)) continue;
                if (!isPassable(neighbor)) continue;

                float step = stepCost(current, neighbor);

                // Slight extra cost for turning: picks the straighter of two equal-cost routes.
                if (cameFrom.TryGetValue(current, out var parent))
                {
                    Vector2 prevDir = current.Center - parent.Center;
                    Vector2 nextDir = neighbor.Center - current.Center;
                    float sqrMag = prevDir.sqrMagnitude * nextDir.sqrMagnitude;
                    if (sqrMag > 1e-9f)
                    {
                        float dot = Vector2.Dot(prevDir, nextDir) / Mathf.Sqrt(sqrMag);
                        step += (1f - dot) * TurnPenaltyFactor * step;
                    }
                }

                float tentativeGScore = gScore[current] + step;
                if (gScore.TryGetValue(neighbor, out float knownG) && tentativeGScore >= knownG)
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                openSet.Push(tentativeGScore + Vector2.Distance(neighbor.Center, end.Center), neighbor);
            }
        }

        return null; // No path found
    }

    private static List<MapCell> ReconstructPath(Dictionary<MapCell, MapCell> cameFrom, MapCell current)
    {
        var path = new List<MapCell> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    // Binary min-heap keyed by f-score. Much faster than scanning a List for the minimum on
    // large open sets, which matters for long road paths across big maps.
    private class MinHeap
    {
        private readonly List<(float key, MapCell cell)> heap = new List<(float, MapCell)>();

        public int Count => heap.Count;

        public void Push(float key, MapCell cell)
        {
            heap.Add((key, cell));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].key <= heap[i].key) break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        public MapCell PopMin()
        {
            MapCell top = heap[0].cell;
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            int i = 0;
            while (true)
            {
                int left = i * 2 + 1;
                int right = left + 1;
                int smallest = i;
                if (left < heap.Count && heap[left].key < heap[smallest].key) smallest = left;
                if (right < heap.Count && heap[right].key < heap[smallest].key) smallest = right;
                if (smallest == i) break;
                (heap[smallest], heap[i]) = (heap[i], heap[smallest]);
                i = smallest;
            }
            return top;
        }
    }
}
