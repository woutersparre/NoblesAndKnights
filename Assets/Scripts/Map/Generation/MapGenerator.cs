using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MapGenerator : MonoBehaviour
{
    /*
    public Color colorToIgnore;
    public Texture2D mapTexture; // Your map image
    public Transform ProvincesHolder;
    private bool[,] visited;
    public static MapGenerator instance;

    public Dictionary<Color, List<List<Vector2>>> provinces = new();
    MapManager map;
    private void Awake()
    {
        if (instance != null && instance != this) 
        { 
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public void GenerateEmpires()
    {
        List<List<Kingdom>> empireKingdoms = GroupRegions(
            map.kingdomsDuJure,
            kingdom => FilterNeighbors<Kingdom>(kingdom.NeighboringRegions),
            1,
            3
        );

        Debug.Log($"Generated {empireKingdoms.Count} empires.");

        foreach (var kingdomGroup in empireKingdoms)
        {
            Empire newEmpire = new Empire(
                new Color(Random.value, Random.value, Random.value),
                kingdomGroup,
                true
            );

            map.empiresDuJure.Add(newEmpire);
            foreach (Kingdom k in kingdomGroup)
            {
                k.ParentRegion = newEmpire;
            }
        }
    }

    public void GenerateKingdoms()
    {
        List<List<Duchy>> kingdomDuchies = GroupRegions(
            map.duchiesDuJure,
            duchy => FilterNeighbors<Duchy>(duchy.NeighboringRegions), // Manually filter neighbors
            2,
            4
        );

        //Debug.Log($"Generated {kingdomDuchies.Count} kingdoms.");

        foreach (var duchyGroup in kingdomDuchies)
        {
            Kingdom newKingdom = new Kingdom(
                new Color(Random.value, Random.value, Random.value),
                duchyGroup,
                true
            );

            map.kingdomsDuJure.Add(newKingdom);
            foreach (Duchy d in duchyGroup)
            {
                d.ParentRegion = newKingdom;
            }
        }
    }
    public void GenerateDuchies()
    {
        List<List<Province>> duchyProvinces = GroupRegions(
            map.provincesDuJure,
            province => FilterNeighbors<Province>(province.NeighboringRegions), // Manually filter neighbors
            3,
            5
        );

        foreach (var provinceGroup in duchyProvinces)
        {
            Duchy newDuchy = new(
                new Color(Random.value, Random.value, Random.value),
                provinceGroup,
                true
            );

            map.duchiesDuJure.Add(newDuchy);
            foreach (Province p in provinceGroup)
            {
                p.ParentRegion = newDuchy;
            }
        }
    }
    public void GenerateProvinces()
    {
        if (mapTexture == null)
        {
            Debug.LogError("Map Texture is missing!");
            return;
        }

        visited = new bool[mapTexture.width, mapTexture.height];

        // Loop through each pixel in the image
        for (int x = 0; x < mapTexture.width; x++)
        {
            for (int y = 0; y < mapTexture.height; y++)
            {
                // Skip if the pixel is ignored or already visited
                if (visited[x, y])
                    continue;

                Color pixelColor = mapTexture.GetPixel(x, y);

                // Ignore transparent or specified color pixels
                if (pixelColor == colorToIgnore || pixelColor.a < 1f)
                    continue;

                // Perform edge-following flood fill to find the boundary
                List<Vector2> boundaryPixels = new List<Vector2>();
                List<Color> colors = new();
                colors.Add(pixelColor);
                FollowBoundary(x, y, colors, boundaryPixels);

                if (boundaryPixels.Count > 0)
                {
                    // Save the boundary pixels for the province
                    if (!provinces.ContainsKey(pixelColor))
                        provinces[pixelColor] = new List<List<Vector2>>();

                    provinces[pixelColor].Add(boundaryPixels); // Add this boundary as a new region
                }
            }
        }

        // Create a province GameObject for each color
        foreach (var province in provinces)
        {
            CreateProvince(province.Key, province.Value);
        }
    }


    // Helper method to filter neighboring regions by type
    private List<T> FilterNeighbors<T>(List<Region> neighbors) where T : Region
    {
        List<T> filteredNeighbors = new List<T>();
        foreach (Region region in neighbors)
        {
            if (region is T castedRegion)
            {
                filteredNeighbors.Add(castedRegion);
            }
        }
        return filteredNeighbors;
    }

    List<List<T>> GroupRegions<T>(List<T> regions, System.Func<T, List<T>> getNeighbors, int minGroupSize = 3, int maxGroupSize = 5)
    {
        List<List<T>> groupedRegions = new List<List<T>>();
        HashSet<T> visitedRegions = new HashSet<T>();
        regions = regions.OrderBy(_ => UnityEngine.Random.value).ToList();
        // Initial grouping based on adjacency and group size
        foreach (var region in regions)
        {
            if (!visitedRegions.Contains(region))
            {
                List<T> regionGroup = new List<T>();
                VisitAdjacentRegions(region, visitedRegions, regionGroup, getNeighbors, maxGroupSize);
                groupedRegions.Add(regionGroup);
            }
        }
        HandleSmallGroups(groupedRegions, getNeighbors, minGroupSize);
        HandleLargeGroups(groupedRegions, getNeighbors, maxGroupSize);

        return groupedRegions;
    }


    void VisitAdjacentRegions<T>(T current, HashSet<T> visited, List<T> group, System.Func<T, List<T>> getNeighbors, int maxGroupSize)
    {
        Queue<T> toVisit = new Queue<T>();
        toVisit.Enqueue(current);

        while (toVisit.Count > 0 && group.Count < maxGroupSize)
        {
            T region = toVisit.Dequeue();

            if (visited.Contains(region)) continue;

            visited.Add(region);
            group.Add(region);

            // Get all unvisited neighbors
            List<T> neighbors = getNeighbors(region)
                .Where(n => !visited.Contains(n))
                .ToList();

            if (neighbors.Count == 0) continue;

            // Sort neighbors by how many of their neighbors are already in the group (descending order)
            neighbors = neighbors.OrderByDescending(n =>
                getNeighbors(n).Count(neighbor => group.Contains(neighbor))
            ).ToList();

            // Add neighbors to the queue in sorted order (prioritizing highly connected ones)
            foreach (var neighbor in neighbors)
            {
                if (group.Count < maxGroupSize)
                {
                    toVisit.Enqueue(neighbor);
                }
                else
                {
                    break;
                }
            }
        }
    }


    void HandleSmallGroups<T>(List<List<T>> groups,System.Func<T,List<T>> getNeighbors,int minGroupSize)
    {
        List<List<T>> toRemove = new();

        foreach (var group in groups)
        {
            if (group.Count < minGroupSize)
            {
                T singleRegion = group[0];
                List<T> neighbors = getNeighbors(singleRegion);

                // Find the group with the most shared borders
                List<T> bestNeighborGroup = null;
                int maxSharedBorders = 0;

                foreach (var neighbor in neighbors)
                {
                    var neighborGroup = groups.Find(g => g.Contains(neighbor));
                    if (neighborGroup != null && neighborGroup != group)
                    {
                        // Calculate how many members of the neighbor group are also neighbors of this small group
                        int sharedBorders = group.Sum(region =>
                            getNeighbors(region).Count(n => neighborGroup.Contains(n)));

                        if (sharedBorders > maxSharedBorders)
                        {
                            maxSharedBorders = sharedBorders;
                            bestNeighborGroup = neighborGroup;
                        }
                    }
                }

                // Merge with the best neighbor group
                if (bestNeighborGroup != null)
                {
                    bestNeighborGroup.AddRange(group);
                    toRemove.Add(group);
                }
            }
        }

        // Remove merged groups
        foreach (var group in toRemove)
        {
            groups.Remove(group);
        }
    }

    void HandleLargeGroups<T>(List<List<T>> groups,System.Func<T, List<T>> getNeighbors,int maxGroupSize)
    {
        //bool madeChanges;

        //do
        //{
            //madeChanges = false;

            foreach (var group in groups.ToList()) // Iterate over a copy to modify safely
            {
                if (group.Count <= maxGroupSize) continue;

                // Find the best region to transfer
                T regionToMove = default(T);
                List<T> bestNeighborGroup = null;
                float bestConnectionRatio = -1f;

                foreach (var region in group)
                {
                    List<T> neighbors = getNeighbors(region);

                    foreach (var neighbor in neighbors)
                    {
                        var neighborGroup = groups.Find(g => g.Contains(neighbor));
                        if (neighborGroup != null && neighborGroup != group)
                        {
                            int sharedBordersWithGroup = neighbors.Count(n => neighborGroup.Contains(n));
                            int totalNeighborsOfNeighborGroup = neighborGroup.Sum(r => getNeighbors(r).Count);

                            // Calculate connection ratio (how connected it is to our group compared to its own total connections)
                            float connectionRatio = (float)sharedBordersWithGroup / totalNeighborsOfNeighborGroup;

                            // Prioritize the group that has the strongest connection to us
                            if (connectionRatio > bestConnectionRatio)
                            {
                                bestNeighborGroup = neighborGroup;
                                regionToMove = region;
                                bestConnectionRatio = connectionRatio;
                            }
                        }
                    }
                }

                // Transfer region if a suitable neighbor is found
                if (!EqualityComparer<T>.Default.Equals(regionToMove, default(T)) && bestNeighborGroup != null)
                {
                    group.Remove(regionToMove);
                    bestNeighborGroup.Add(regionToMove);
                    //madeChanges = true;
                }
            }
        //} while (madeChanges); // Repeat until no more transfers are possible
    }




    void CreateProvince(Color color, List<List<Vector2>> boundaryPixelSets)
    {
        if (boundaryPixelSets.Count == 0)
        {
            Debug.LogWarning($"Province has no valid boundary regions, skipping.");
            return;
        }

        // Create a new GameObject for the province
        GameObject provinceObject = new("Province");
        provinceObject.transform.parent = ProvincesHolder;



        // Create the Province
        Province province = new();
        //Religion.AllReligions[Random.Range(0, Religion.AllReligions.Count)],Culture.AllCultures[Random.Range(0, Culture.AllCultures.Count)], color, _manager

        // Add multiple colliders and borders for each boundary
        foreach (var boundaryPixels in boundaryPixelSets)
        {
            if (boundaryPixels.Count < 3)
            {
                Debug.LogWarning($"Boundary has fewer than 3 points, skipping. Points: {boundaryPixels.Count}");
                continue; // We need at least 3 points for a valid polygon
            }

            // Add PolygonCollider2D for this boundary
            PolygonCollider2D collider = provinceObject.AddComponent<PolygonCollider2D>();
            collider.pathCount = 1;
            collider.SetPath(0, boundaryPixels.ToArray());

            _manager.AddMesh(collider.points, province.MapColor);
            _manager.AddColliderBorders(collider);
            _manager.Colliders.Add(collider);
        }
        // Neighbor assignment
        AssignNeighbors(province, boundaryPixelSets);

        Color mapColor = RandomBrightColor();
        //Debug.Log(province.NeighboringRegions.Count);
        foreach (Region region in province.NeighboringRegions)
        {
            while (AreColorsSimilarHSV(region.MapColor, mapColor))
            {
                mapColor = RandomBrightColor();
            }
        }
        province.MapColor = mapColor;
    }*/
    public static bool AreColorsSimilarHSV(Color c1, Color c2, float hueThreshold = 0.1f)
    {
        Color.RGBToHSV(c1, out float h1, out float s1, out float v1);
        Color.RGBToHSV(c2, out float h2, out float s2, out float v2);

        float hueDiff = Mathf.Abs(h1 - h2); // Hue difference (0-1)
        if (hueDiff > 0.5f) hueDiff = 1 - hueDiff; // Wrap around (red is close to purple)

        float satDiff = Mathf.Abs(s1 - s2);
        float valDiff = Mathf.Abs(v1 - v2);

        return hueDiff < hueThreshold && satDiff < 0.2f && valDiff < 0.2f;
    }

    static public Color RandomBrightColor()
    {
        float hue = Random.value; // 0 to 1 (full color spectrum)
        float saturation = 0.5f + Random.value * 0.5f; // Keep saturation high (80%-100%)
        float value = 0.3f + Random.value * 0.7f; // Keep brightness high (80%-100%)

        return Color.HSVToRGB(hue, saturation, value);
    }
}

