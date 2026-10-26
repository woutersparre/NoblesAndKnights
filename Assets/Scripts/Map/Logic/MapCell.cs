using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class MapCell
{
    public Vector2[] Polygon; // polygon clipped to map bounds used for mesh

    [System.NonSerialized]
    public Province Province = null; // local province
    [System.NonSerialized]
    public MapCell[] Neighbors = new MapCell[0];

    public GameObject GameObject;
    public Mesh Mesh;
    public MeshRenderer Renderer;

    public Vector2 Center;

    // Bounding-box extents of the straight (unnoised) clipped polygon. Used for
    // playable-cell eligibility so the choice of edge style cannot change which
    // cells become provinces.
    public Vector2 StraightExtents;

    public bool enabled = true;

    // Terrain type for this cell
    public TerrainType TerrainType = TerrainType.Grassland;

    // Unified border system - this is the source of truth for all border/neighbor data
    public List<CellBorder> Borders = new List<CellBorder>();

    // Pre-computed corners for water cells (derived from borders)
    public List<Vector2> Corners = new List<Vector2>();

    // Utility methods
    public bool IsWaterCell => TerrainType == TerrainType.Ocean || TerrainType == TerrainType.LargeLake;
    public bool IsMountainCell => TerrainType == TerrainType.Mountain;

// Get province borders (borders with different provinces on either side)
    public List<CellBorder> GetProvinceBorders()
    {
        return Borders.Where(b => b.IsProvinceBorder).ToList();
    }

    // Get accurate shared border length with another province
    public float GetProvinceBorderLength(Province otherProvince)
    {
        float totalLength = 0f;
        foreach (var border in Borders)
        {
            if (border.GetBorderingProvince(this) == otherProvince)
            {
                totalLength += border.Length;
            }
        }
        return totalLength;
    }

    // Get all neighboring provinces
    public List<Province> GetNeighboringProvinces()
    {
        var neighboringProvinces = new HashSet<Province>();
        foreach (var border in Borders)
        {
            var borderingProvince = border.GetBorderingProvince(this);
            if (borderingProvince != null)
            {
                neighboringProvinces.Add(borderingProvince);
            }
        }
        return neighboringProvinces.ToList();
    }
    

}

[System.Serializable]
public enum BorderType
{
    None = 0,           // No special properties
    Shore = 1 << 0,     // Water-to-land border (shoreline)
    Water = 1 << 1,     // Water-to-water border (between water bodies)
    River = 1 << 2,     // River crosses this border (can be combined)
    Mountain = 1 << 3,   // Mountain terrain border
    Blocked = 1 << 4,    // Too small or map edge (impassable)
    Normal = 1 << 5      // Regular passable land border
}

[System.Serializable]
public class BorderFlag
{
    // Helper methods for bitmask operations
    public static bool HasFlag(BorderType flags, BorderType flag) => (flags & flag) != 0;
    public static BorderType SetFlag(BorderType flags, BorderType flag) => flags | flag;
    public static BorderType ClearFlag(BorderType flags, BorderType flag) => flags & ~flag;
    public static BorderType ToggleFlag(BorderType flags, BorderType flag) => flags ^ flag;
    
    // Multi-flag operations
    public static bool HasAnyFlag(BorderType flags, BorderType checkFlags) => (flags & checkFlags) != 0;
    public static bool HasAllFlags(BorderType flags, BorderType checkFlags) => (flags & checkFlags) == checkFlags;
    public static BorderType SetFlags(BorderType flags, BorderType newFlags) => flags | newFlags;
    public static BorderType ClearFlags(BorderType flags, BorderType clearFlags) => flags & ~clearFlags;
    
    // Common border state checks
    public static bool IsWaterBorder(BorderType flags) => HasAnyFlag(flags, BorderType.Shore | BorderType.Water);
    public static bool IsLandBorder(BorderType flags) => HasFlag(flags, BorderType.Normal);
    public static bool IsPassable(BorderType flags) => !HasFlag(flags, BorderType.Blocked);
}
[System.Serializable]
public class CellBorder
{
    [Header("Border Data")]
    public Vector2 Start;
    public Vector2 End;
    [HideInInspector] public Vector2 Midpoint;
    public List<Vector2> NoisyPoints; // Hidden to prevent serialization crashes

    //[Header("Connected Cells")]
    [System.NonSerialized] public MapCell CellA;           // First cell sharing this border
    [System.NonSerialized] public MapCell CellB;           // Second cell sharing this border (null for map edge)

    [HideInInspector]public BorderType Flags = BorderType.Normal;
    
    public bool IsPassable => BorderFlag.IsPassable(Flags); 
    
    // Simple length calculation
    public float Length => Vector2.Distance(Start, End);

    public bool Highlighted;        // For UI highlighting/selection
    
    // Province-specific data
    public bool IsProvinceBorder => CellA?.Province != CellB?.Province && CellB != null;
    public Province GetBorderingProvince(MapCell fromCell)
    {
        var otherCell = GetOtherCell(fromCell);
        return otherCell?.Province;
    }


    // Constructor for internal borders (shared between two cells)
    public CellBorder(MapCell cellA, MapCell cellB, Vector2 start, Vector2 end, List<Vector2> noisyPoints = null)
    {
        CellA = cellA;
        CellB = cellB;
        Start = start;
        End = end;
        Midpoint = (start + end) * 0.5f;
        NoisyPoints = noisyPoints ?? new List<Vector2> { start, end };

        // Auto-detect border type based on terrain
        DetectBorderType();
    }

    // Constructor for edge borders (only one cell)
    public CellBorder(MapCell cell, Vector2 start, Vector2 end, List<Vector2> noisyPoints = null)
    {
        CellA = cell;
        CellB = null; // Edge of map
        Start = start;
        End = end;
        Midpoint = (start + end) * 0.5f;
        NoisyPoints = noisyPoints ?? new List<Vector2> { start, end };

        DetectBorderType();
    }

    // Get the other cell sharing this border
    public MapCell GetOtherCell(MapCell cell)
    {
        if (CellA == cell) return CellB;
        if (CellB == cell) return CellA;
        return null;
    }

    // Check if a point is on this noisy border
    public bool ContainsPoint(Vector2 point, float tolerance = 0.1f)
    {
        foreach (var noisyPoint in NoisyPoints)
        {
            if (Vector2.Distance(noisyPoint, point) < tolerance)
                return true;
        }

        // Also check the main line
        float distToLine = PointToLineDistance(point, Start, End);
        if (distToLine > tolerance) return false;

        return Vector2.Distance(Start, point) + Vector2.Distance(point, End) <= Length + tolerance;
    }

    // Auto-detect border type based on cell terrain types (supports multiple flags)
    private void DetectBorderType()
    {
        BorderType flags = BorderType.None;
        
        if (CellB == null)
        {
            // Map edge - always blocked
            flags = BorderFlag.SetFlag(flags, BorderType.Blocked);
        }
        else
        {
            // Check for water-to-land border (shoreline)
            if (CellA.IsWaterCell != CellB.IsWaterCell)
            {
                flags = BorderFlag.SetFlag(flags, BorderType.Shore);
            }
            // Check for water-to-water border
            else if (CellA.IsWaterCell && CellB.IsWaterCell)
            {
                flags = BorderFlag.SetFlag(flags, BorderType.Water);
            }
            
            // Check for mountain terrain border
            if (CellA.IsMountainCell && CellB.IsMountainCell)
            {
                flags = BorderFlag.SetFlag(flags, BorderType.Mountain);
            }
            
            // Check if border is too small to be passable
            if (Length < 0.3f) // minimumBorderLength from NewMapGen
            {
                flags = BorderFlag.SetFlag(flags, BorderType.Blocked);
            }
            else
            {
                // Normal passable land border (only if not already water-related)
                if (!CellA.IsWaterCell && !CellB.IsWaterCell)
                {
                    flags = BorderFlag.SetFlag(flags, BorderType.Normal);
                }
            }
        }
        
        Flags = flags;
    }

    // Add a flag to existing flags (preserves current state)
    public void AddFlag(BorderType flag)
    {
        Flags = BorderFlag.SetFlag(Flags, flag);
    }
    
    // Remove a specific flag from existing flags
    public void RemoveFlag(BorderType flag)
    {
        Flags = BorderFlag.ClearFlag(Flags, flag);
    }
    
    // Check if border has ANY of the specified flags (OR operation)
    public bool HasAnyFlag(BorderType flags)
    {
        return (Flags & flags) != 0;
    }
    
    // Check if border has ALL of the specified flags (AND operation)
    public bool HasAllFlags(BorderType flags)
    {
        return (Flags & flags) == flags;
    }
    
    private float PointToLineDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        Vector2 line = lineEnd - lineStart;
        float len = line.magnitude;
        if (len == 0) return Vector2.Distance(point, lineStart);

        Vector2 lineNormalized = line.normalized;
        Vector2 v = point - lineStart;
        float d = Vector2.Dot(v, lineNormalized);

        Vector2 closestPoint = lineStart + lineNormalized * Mathf.Clamp(d, 0f, len);
        return Vector2.Distance(point, closestPoint);
    }
    

}


