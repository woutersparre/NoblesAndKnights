using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Utility class for province operations using unified border system
public static class ProvinceBorderUtils
{
    // Matching tolerance for edge chaining, deduplication and loop building.
    // Cell polygon vertices are snapped to a 0.001 grid (see CleanPolygon), so
    // shared vertices match at exactly zero distance while real edges are at
    // least 0.001 long. A tolerance of 0.0001 (well below the grid) therefore
    // matches shared vertices without confusing them for tiny real edges.
    private const float EdgeTolerance = 0.0001f;

    // Create province border loops efficiently using pre-computed borders
    public static List<List<Vector2>> CreateProvinceBorderLoops(Province province)
    {
        if (province?.MapCells == null || province.MapCells.Count == 0)
            return new List<List<Vector2>>();

        var edges = new List<(Vector2, Vector2)>();
        var processedBorders = new HashSet<CellBorder>();

        // Collect unique province borders from all cells in one pass.
        // Include a cell's own map-edge border (CellB == null) so that provinces
        // touching the map edge get their border loop closed along the world boundary.
        // IsProvinceBorder is intentionally left untouched for gameplay/adjacency uses.
        foreach (var cell in province.MapCells)
        {
            foreach (var border in cell.Borders)
            {
                bool isMapEdgeBorder = border.CellB == null;
                if (!(border.IsProvinceBorder || isMapEdgeBorder))
                    continue;

                // Deduplicate by border instance (a border is shared by both cells).
                if (!processedBorders.Add(border))
                    continue;

                // Use the (possibly noisy) curve for rendering instead of the straight
                // Start->End segment, so cell border noise actually shows up.
                var noisyPoints = (border.NoisyPoints != null && border.NoisyPoints.Count >= 2)
                    ? border.NoisyPoints
                    : new List<Vector2> { border.Start, border.End };

                for (int i = 0; i < noisyPoints.Count - 1; i++)
                {
                    // Skip degenerate segments.
                    if (Vector2.Distance(noisyPoints[i], noisyPoints[i + 1]) < 0.000001f)
                        continue;
                    edges.Add((noisyPoints[i], noisyPoints[i + 1]));
                }
            }
        }

        if (edges.Count == 0)
        {
            Debug.LogWarning($"Province {province.Name} has no province borders - cannot create border loops!");
            return new List<List<Vector2>>();
        }

        // Build loops directly using Vector2 edges (avoid unnecessary conversions)
        var loops = BuildLoops2D(edges);

        return loops;
    }

    // Optimized 2D loop building to avoid Vector3 conversions
    private static List<List<Vector2>> BuildLoops2D(List<(Vector2, Vector2)> edges)
    {
        var unused = new List<(Vector2, Vector2)>(edges);
        var loops = new List<List<Vector2>>();

        while (unused.Count > 0)
        {
            var start = unused[0];
            unused.RemoveAt(0);

            var loop = new List<Vector2> { start.Item1, start.Item2 };
            Vector2 current = start.Item2;
            bool closed = false;

            while (!closed && unused.Count > 0)
            {
                int index = unused.FindIndex(e => 
                    Vector2.Distance(e.Item1, current) < EdgeTolerance || 
                    Vector2.Distance(e.Item2, current) < EdgeTolerance);
                
                if (index == -1) break;

                var edge = unused[index];
                unused.RemoveAt(index);

                Vector2 next = Vector2.Distance(edge.Item1, current) < EdgeTolerance ? edge.Item2 : edge.Item1;
                
                if (Vector2.Distance(next, loop[0]) < EdgeTolerance)
                {
                    loop.Add(loop[0]); // Close the loop
                    closed = true;
                }
                else
                {
                    loop.Add(next);
                    current = next;
                }
            }

            if (loop.Count > 2)
            {
                loops.Add(loop);
            }
        }

        return loops;
    }

    // Calculate accurate shared border length between two provinces (optimized)
    public static float CalculateSharedBorderLength(Province province1, Province province2)
    {
        if (province1?.MapCells == null || province2?.MapCells == null)
            return 0f;

        float totalLength = 0f;
        var processedEdges = new HashSet<(Vector2, Vector2)>(new VectorPairComparer());

        // Check all cells from the smaller province for efficiency
        var cellsToCheck = province1.MapCells.Count <= province2.MapCells.Count ? province1.MapCells : province2.MapCells;
        var targetProvince = province1.MapCells.Count <= province2.MapCells.Count ? province2 : province1;

        foreach (var cell in cellsToCheck)
        {
            foreach (var border in cell.Borders)
            {
                if (!border.IsProvinceBorder) continue;
                
                var edgeKey = (border.Start, border.End);
                if (!processedEdges.Add(edgeKey)) continue;

                var borderingProvince = border.GetBorderingProvince(cell);
                if (borderingProvince == targetProvince)
                {
                    totalLength += border.Length;
                }
            }
        }

        return totalLength;
    }

    // Get all provinces that border a given province
    public static List<Province> GetBorderingProvinces(Province province)
    {
        if (province?.MapCells == null) return new List<Province>();

        var borderingProvinces = new HashSet<Province>();
        var processedEdges = new HashSet<(Vector2, Vector2)>(new VectorPairComparer());

        foreach (var cell in province.MapCells)
        {
            foreach (var border in cell.Borders)
            {
                if (!border.IsProvinceBorder) continue;
                
                // Process each unique edge only once
                if (!processedEdges.Add((border.Start, border.End))) continue;

                var borderingProvince = border.GetBorderingProvince(cell);
                if (borderingProvince != null && borderingProvince != province)
                {
                    borderingProvinces.Add(borderingProvince);
                }
            }
        }

        return borderingProvinces.ToList();
    }

    // Highlight province borders for visualization (optimized)
    public static void HighlightProvinceBorders(Province province, bool highlight = true)
    {
        if (province?.MapCells == null) return;

        foreach (var cell in province.MapCells)
        {
            foreach (var border in cell.Borders)
            {
                if (border.IsProvinceBorder)
                {
                    border.Highlighted = highlight;
                }
            }
        }
    }

    // Get all borders that have a specific flag for a province
    public static List<CellBorder> GetProvinceBordersByType(Province province, BorderType type)
    {
        var borders = new HashSet<CellBorder>();

        if (province?.MapCells == null) return borders.ToList();

        foreach (var cell in province.MapCells)
        {
            foreach (var border in cell.Borders)
            {
                if (border.HasAnyFlag(type) && border.IsProvinceBorder)
                {
                    borders.Add(border);
                }
            }
        }

        return borders.ToList();
    }
    
    // Get all borders with multiple flags (e.g., Water AND Normal)
    public static List<CellBorder> GetProvinceBordersByFlags(Province province, BorderType requiredFlags)
    {
        var borders = new HashSet<CellBorder>();

        if (province?.MapCells == null) return borders.ToList();

        foreach (var cell in province.MapCells)
        {
            foreach (var border in cell.Borders)
            {
                if (border.HasAllFlags(requiredFlags) && border.IsProvinceBorder)
                {
                    borders.Add(border);
                }
            }
        }

        return borders.ToList();
    }


    // Get border cells of a province
    public static List<MapCell> GetBorderCells(Province province)
    {
        if (province?.MapCells == null)
            return new List<MapCell>();

        return province.MapCells
            .Where(cell => cell.Neighbors.Any(n => n.Province != null && n.Province != province))
            .ToList();
    }
}

// Custom comparer for Vector2 pairs to handle edge deduplication efficiently
public class VectorPairComparer : IEqualityComparer<(Vector2, Vector2)>
{
    private const float Tolerance = 0.0001f;

    public bool Equals((Vector2, Vector2) x, (Vector2, Vector2) y)
    {
        return (Vector2.Distance(x.Item1, y.Item1) < Tolerance && Vector2.Distance(x.Item2, y.Item2) < Tolerance) ||
                (Vector2.Distance(x.Item1, y.Item2) < Tolerance && Vector2.Distance(x.Item2, y.Item1) < Tolerance);
    }

    public int GetHashCode((Vector2, Vector2) obj)
    {
        // Create order-independent hash by combining min/max coordinates
        float minX = Mathf.Min(obj.Item1.x, obj.Item2.x);
        float maxX = Mathf.Max(obj.Item1.x, obj.Item2.x);
        float minY = Mathf.Min(obj.Item1.y, obj.Item2.y);
        float maxY = Mathf.Max(obj.Item1.y, obj.Item2.y);
            
        // Simple hash combination that works in Unity
        int hash = 17;
        hash = hash * 31 + minX.GetHashCode();
        hash = hash * 31 + maxX.GetHashCode();
        hash = hash * 31 + minY.GetHashCode();
        hash = hash * 31 + maxY.GetHashCode();
        return hash;
    }
}