using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class MapModes
{
private readonly MapManager map;

    // The currently displayed map mode; used to route map clicks (e.g. opening region titles).
    public MapViewMode CurrentView { get; private set; } = MapViewMode.Province;

    // Cultures and religions belong to no Region, so their labels cannot live in
    // Region.text the way province/duchy/kingdom/empire tags do. They are owned
    // here instead: one tag per group, centred over that group's provinces,
    // created the first time a mode needs it and reused afterwards.
    private readonly Dictionary<Culture, TMP_Text> cultureTags = new();
    private readonly Dictionary<Religion, TMP_Text> religionTags = new();

    public MapModes(MapManager map)
    {
        this.map = map;
    }

    // Clear any active highlights when switching mapmodes
    private void ClearHighlights()
    {
        RegionBorderDrawer.ClearHighlights();
        
        // Also clear the selection manager state to ensure synchronization
        if (UnifiedSelectionManager.Instance != null)
        {
            UnifiedSelectionManager.Instance.ClearSelection();
        }
    }

    // Reset all cell colors to ensure proper color management when switching mapmodes
    private void ResetCellColors()
    {
        foreach (var province in map.provincesDuJure)
        {
            foreach (var cell in province.MapCells)
            {
                if (cell.Renderer != null)
                {
                    // Clear any existing PropertyBlock to ensure clean state
                    cell.Renderer.SetPropertyBlock(null);
                }
            }
        }
    }
    // ======================
    // GROUP NAME TAGS (cultures / religions)
    // ======================
    // Every mode except its own has to hide these: map.DisableAllText() only knows
    // about region tags, so without this a culture tag stays on screen once you
    // switch to realms or terrain.
    private void HideGroupTags()
    {
        HideTags(cultureTags);
        HideTags(religionTags);
    }

    private static void HideTags<TKey>(Dictionary<TKey, TMP_Text> tags) where TKey : class
    {
        foreach (TMP_Text tag in tags.Values)
        {
            if (tag != null)
                tag.enabled = false;
        }
    }

    // Enables one tag per group, each positioned over the centre of mass of its
    // own provinces rather than parked on an arbitrary province's label. The name
    // selector exists because Culture and Religion share no common type.
    private void ShowGroupTags<TKey>(Dictionary<TKey, TMP_Text> tags, IEnumerable<IGrouping<TKey, Province>> groups, Func<TKey, string> nameOf)
        where TKey : class
    {
        foreach (IGrouping<TKey, Province> group in groups)
        {
            if (group.Key == null) continue;

            Vector3 centre = GroupCentre(group);

            if (!tags.TryGetValue(group.Key, out TMP_Text tag) || tag == null)
            {
                tag = map.CreateNameTag(nameOf(group.Key), centre, nameOf(group.Key));
                if (tag == null)
                    return; // canvas or prefab missing, already reported
                tags[group.Key] = tag;
            }

            // Re-centred every time: provinces can change group through conquest or
            // conversion, so the label follows the territory it describes.
            tag.transform.position = centre;
            tag.enabled = true;
        }
    }

    private static Vector3 GroupCentre<TKey>(IGrouping<TKey, Province> group) where TKey : class
    {
        Vector2 sum = Vector2.zero;
        int count = 0;

        foreach (Province province in group)
        {
            sum += province.Center;
            count++;
        }

        return count > 0 ? sum / count : Vector2.zero;
    }

    // ======================
    // REALM MAP MODE
    // ======================
public void ShowRealms() // all independant rulers
    {
        CurrentView = MapViewMode.Realm;
        ClearHighlights();
        ResetCellColors();
        map.DisableAllText();
        HideGroupTags();
        RegionBorderDrawer.ClearAll();

        foreach (Character ruler in map.GetTopLevelRealms())
        {
            Color color = ruler.MainTitle.Color;

            foreach (Province province in ruler.GetAllRuledProvinces())
            {
                province.SetColor(color);
                RegionBorderDrawer.DrawBorder(province, Color.black, 0.75f);
            }
            //enable text if main title is a region
            if (ruler.MainTitle is RegionTitle rt && rt.AssociatedRegion != null)
                rt.AssociatedRegion.ShowOwnTag();

        }
    }

    // ======================
    // DE JURE MAP MODES
    // ======================
    public void ShowProvinces() { CurrentView = MapViewMode.Province; ShowRegions(map.provincesDuJure); }
    public void ShowDuJureDuchies() { CurrentView = MapViewMode.Duchy; ShowRegions(map.duchiesDuJure); }
    public void ShowDuJureKingdoms() { CurrentView = MapViewMode.Kingdom; ShowRegions(map.kingdomsDuJure); }
    public void ShowDuJureEmpires() { CurrentView = MapViewMode.Empire; ShowRegions(map.empiresDuJure); }

private void ShowRegions(IEnumerable<Region> regions)
    {
        ClearHighlights();
        ResetCellColors();
        map.DisableAllText();
        HideGroupTags();
        RegionBorderDrawer.ClearAll();

        foreach (Region region in regions)
        {
            foreach (Province province in region.GetProvinces())
                province.SetColor(region.MapColor);

            region.ShowOwnTag();

            RegionBorderDrawer.DrawBorder(region, Color.black, 0.75f);
        }

    }

    // ======================
    // TERRAIN MAP MODE
    // ======================
public void ShowTerrain()
    {
        CurrentView = MapViewMode.Terrain;
        ClearHighlights();
        ResetCellColors();
        map.DisableAllText();
        HideGroupTags();
        RegionBorderDrawer.ClearAll();

        foreach (Province province in map.provincesDuJure)
        {
            // Apply terrain colors to cells
            foreach (MapCell cell in province.MapCells)
            {
                if (cell.Renderer != null)
                {
                    Color terrainColor = TerrainGenerator.GetTerrainData(cell.TerrainType).color;
                    // Use PropertyBlock for better performance than material instances
                    var propBlock = new MaterialPropertyBlock();
                    cell.Renderer.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_Color", terrainColor);
                    cell.Renderer.SetPropertyBlock(propBlock);
                }
            }
        }
        
        // Draw all province borders after setting terrain colors to ensure proper rendering order
        foreach (Province province in map.provincesDuJure)
        {
            RegionBorderDrawer.DrawBorder(province, Color.black, 0.75f);
        }
    }

    // ======================
    // CULTURE MAP MODE
    // ======================
public void ShowCultures()
    {
        CurrentView = MapViewMode.Culture;
        ClearHighlights();
        ResetCellColors();
        map.DisableAllText();
        HideGroupTags();
        RegionBorderDrawer.ClearAll();

        var cultureGroups = map.provincesDuJure
            .Where(p => p.Culture != null)
            .GroupBy(p => p.Culture)
            .ToList();

        int colorIndex = 0;
        var cultureColors = new Dictionary<Culture, Color>();

        foreach (var group in cultureGroups)
        {
            Color cultureColor = GetDistinctColor(colorIndex++);
            cultureColors[group.Key] = cultureColor;

            foreach (Province province in group)
            {
                province.SetColor(cultureColor);
            }

            // Draw borders for all provinces of this culture
            RegionBorderDrawer.DrawBorderForProvinces(group.ToList(), Color.black, 0.75f);
        }

        // Cultures get tags of their own; the province tags stay off and keep their
        // own names, which used to be overwritten here and never restored.
        ShowGroupTags(cultureTags, cultureGroups, culture => culture.Name);
    }

    // ======================
    // RELIGION MAP MODE
    // ======================
public void ShowReligions()
    {
        CurrentView = MapViewMode.Religion;
        ClearHighlights();
        ResetCellColors();
        map.DisableAllText();
        HideGroupTags();
        RegionBorderDrawer.ClearAll();

        var religionGroups = map.provincesDuJure
            .Where(p => p.Religion != null)
            .GroupBy(p => p.Religion)
            .ToList();

        int colorIndex = 0;
        var religionColors = new Dictionary<Religion, Color>();

        foreach (var group in religionGroups)
        {
            Color religionColor = GetDistinctColor(colorIndex++);
            religionColors[group.Key] = religionColor;

            foreach (Province province in group)
            {
                province.SetColor(religionColor);
            }

            // Draw borders for all provinces of this religion
            RegionBorderDrawer.DrawBorderForProvinces(group.ToList(), Color.black, 0.75f);
        }

        // Religions get tags of their own; the province tags stay off and keep their
        // own names, which used to be overwritten here and never restored.
        ShowGroupTags(religionTags, religionGroups, religion => religion.Name);
    }

    private Color GetDistinctColor(int index)
    {
        // Generate visually distinct colors using golden ratio for better distribution
        float hue = (index * 0.618033988749895f) % 1.0f;
        return Color.HSVToRGB(hue, 0.7f, 0.9f);
    }

    // Click routing for de jure map modes: clicking any province resolves up to the
    // region matching the current view (duchy/kingdom/empire) and opens its title UI.
    public void HandleProvinceClick(Province province)
    {
        if (province == null || GameUIManager.instance == null)
            return;

        Region target = null;
        switch (CurrentView)
        {
            case MapViewMode.Duchy:   target = province.ParentRegion; break;
            case MapViewMode.Kingdom: target = province.ParentRegion?.ParentRegion; break;
            case MapViewMode.Empire:  target = province.ParentRegion?.ParentRegion?.ParentRegion; break;
            default: return; // Province/Realm/Terrain/Culture/Religion keep default behavior
        }
        if (target == null) return;

        Title regionTitle = RegionManager.FindRegionTitle(target);
        if (regionTitle != null)
            GameUIManager.NewTitleUI(regionTitle);
    }
}

public enum MapViewMode 
{ 
    Province, 
    Duchy, 
    Kingdom, 
    Empire, 
    Realm, 
    Vassals,
    Terrain,
    Culture,    // NEW
    Religion     // NEW
}