using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class RegionBorderDrawer
{
    // Keep a list of line renderers per region now
    private static Dictionary<Region, List<LineRenderer>> renderers = new();
    
    // OPTIMIZED: Shared material instances to avoid thousands of allocations
    private static Material DefaultBorderMaterial;
    
    // Static highlighting settings and state
    private static Color HighlightColor;
    private static float HighlightBorderWidth;
    private static float RegularBorderWidth;
    private static Province HighlightedProvince = null;
    
    // Exposed border width settings for easy adjustment
    public static float DefaultBorderWidth = 0.05f;  // Default thin border
    
    // Initialize highlighting settings
    public static void InitializeHighlighting(Color highlightColor, Color waterHighlightColor, 
                                               float highlightBorderWidth, float regularBorderWidth)
    {
        HighlightColor = highlightColor;
        HighlightBorderWidth = highlightBorderWidth;
        RegularBorderWidth = regularBorderWidth;
        DefaultBorderWidth = regularBorderWidth;
    }
    
    // Highlight a province (no GameObject creation during cleanup)
    public static void HighlightProvince(Province province)
    {
        if (province == null) return;
        
        // Set highlight state
        ProvinceBorderUtils.HighlightProvinceBorders(province, true);
        HighlightedProvince = province;
        
        // Redraw the province with highlights
        DrawBorder(province, Color.black);
    }
    
    // Clear province highlight (no GameObject creation during cleanup)
    public static void ClearProvinceHighlight(Province province)
    {
        if (province == null) return;
        
        // Clear highlight state
        ProvinceBorderUtils.HighlightProvinceBorders(province, false);
        
        // Redraw the province without highlights
        if (HighlightedProvince == province)
        {
            HighlightedProvince = null;
            DrawBorder(province, Color.black);
        }
    }
    
    // Clear all highlights (safe for OnDestroy)
    public static void ClearHighlights()
    {
        if (HighlightedProvince != null)
        {
            // Clear highlight state only
            ProvinceBorderUtils.HighlightProvinceBorders(HighlightedProvince, false);
            HighlightedProvince = null;
            // Note: Don't redraw here to avoid GameObject creation during OnDestroy
        }
    }

    public static void DrawBorder(Region region, Color color, float width = -1f)
    {
        if (region.BorderLoops == null || region.BorderLoops.Count == 0)
            return;

        // Clear old LRs if any
        Clear(region);

        List<LineRenderer> lrs = new();

        foreach (var loop in region.BorderLoops)
        {
            if (loop.Count < 3) // Minimum 3 points for a valid polygon
            {
                Debug.LogWarning($"Skipping invalid border loop with {loop.Count} points for {region.Name}");
                continue;
            }

            // Check if this loop should be highlighted (for provinces)
            bool isHighlighted = false;
            Color actualColor = color;
            float actualWidth = width;
            
            if (region is Province province)
            {
                // Check if any borders in this province are highlighted
                var loopBorders = GetBordersInLoop(province, loop);
                isHighlighted = loopBorders.Any(b => b.Highlighted);
                
                if (isHighlighted)
                {
                    // Simplified: always use the same highlight color regardless of border types
                    actualColor = HighlightColor;
                    actualWidth = HighlightBorderWidth;
                }
            }

            // Use default width if not specified
            if (actualWidth < 0)
                actualWidth = DefaultBorderWidth;

            GameObject go = new GameObject($"BorderLoop_{region.Name}");
            go.transform.parent = MapManager.instance.transform;

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;

            // Only close the line (draw the segment from last point back to the first)
            // when the loop is genuinely closed. Otherwise a broken/partial chain would
            // get a false straight line drawn straight through the province interior.
            bool closed = loop.Count > 2 &&
                Vector3.Distance(loop[loop.Count - 1], loop[0]) < 0.01f;
            lr.loop = closed;

            lr.positionCount = loop.Count;

            lr.startWidth = actualWidth;
            lr.endWidth = actualWidth;
            lr.startColor = actualColor;
            lr.endColor = actualColor;

            if (DefaultBorderMaterial == null)
                DefaultBorderMaterial = new Material(Shader.Find("Sprites/Default"));

            lr.material = DefaultBorderMaterial;
            
            // Set rendering order - highlighted borders render on top
            if (isHighlighted)
            {
                lr.sortingOrder = 100;
                lr.sortingLayerName = "Default";
            }
            else
            {
                lr.sortingOrder = 2;
            }

            for (int i = 0; i < loop.Count; i++)
                lr.SetPosition(i, loop[i]);

            lrs.Add(lr);
        }

        renderers[region] = lrs;
    }

    // Get the borders that make up this loop (for checking highlight state)
    private static List<CellBorder> GetBordersInLoop(Province province, List<Vector3> loop)
    {
        var loopBorders = new List<CellBorder>();
        
        foreach (var cell in province.MapCells)
        {
            foreach (var border in cell.Borders)
            {
                // Check if this border is part of the loop
                if (BorderIsInLoop(border, loop))
                {
                    loopBorders.Add(border);
                }
            }
        }
        
        return loopBorders.Distinct().ToList();
    }

    // Check if a border is part of a loop by checking if both endpoints are in the loop
    private static bool BorderIsInLoop(CellBorder border, List<Vector3> loop)
    {
        bool startInLoop = loop.Any(p => Vector2.Distance((Vector2)p, border.Start) < 0.01f);
        bool endInLoop = loop.Any(p => Vector2.Distance((Vector2)p, border.End) < 0.01f);
        return startInLoop && endInLoop;
    }

    public static void ClearAll()
    {
        foreach (var lrs in renderers.Values)
        {
            foreach (var lr in lrs)
                if (lr != null)
                    GameObject.Destroy(lr.gameObject);
        }

        renderers.Clear();
    }

    public static void Clear(Region region)
    {
        if (!renderers.TryGetValue(region, out var lrs)) return;

        foreach (var lr in lrs)
            if (lr != null)
                GameObject.Destroy(lr.gameObject);

        renderers.Remove(region);
    }

    public static void DrawBorderForProvinces(List<Province> provinces, Color color, float width = -1f)
    {
        foreach (var province in provinces)
        {
            DrawBorder(province, color, width);
        }
    }
    
    // Public method to update default border width for future borders
    public static void SetDefaultBorderWidth(float width)
    {
        DefaultBorderWidth = width;
    }
    
    // Note: Highlighted border width now comes from BorderHighlighter.Instance.borderWidth
    // This method is deprecated - use BorderHighlighter's inspector setting instead
    
    // Method to redraw all current borders with new width (useful for runtime adjustments)
    public static void RedrawAllBorders(float newWidth = -1f)
    {
        var allRegions = new List<Region>();
        allRegions.AddRange(MapManager.instance.provincesDuJure);
        allRegions.AddRange(MapManager.instance.duchiesDuJure);
        allRegions.AddRange(MapManager.instance.kingdomsDuJure);
        allRegions.AddRange(MapManager.instance.empiresDuJure);
        
        foreach (var region in allRegions)
        {
            if (region.BorderLoops != null && region.BorderLoops.Count > 0)
            {
                Clear(region);
                DrawBorder(region, Color.black, newWidth);
            }
        }
    }
}
