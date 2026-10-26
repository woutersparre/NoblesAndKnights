using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DelaunatorSharp.Unity;

public class RegionManager
{
    private readonly RegionSettings settings;
    
    // Static buffers for FrontierSearch to eliminate allocations
    static readonly List<Region> groupBuffer = new();
    static readonly List<Region> frontierBuffer = new();
    static readonly HashSet<Region> processedBuffer = new();

    // Frontier expansion data structures for province optimization
    private struct FrontierCandidate
    {
        public MapCell Cell;
        public float Score;
    }

    // Static buffers for province frontier expansion to eliminate allocations
    static readonly Dictionary<Province, List<FrontierCandidate>> provinceFrontierBuffer = new();
    static readonly HashSet<MapCell> frontierProcessedBuffer = new();

    public RegionManager(RegionSettings settings)
    {
        this.settings = settings;
    }



    // Generate all regions with focus on sophisticated province generation
    public List<MapCell> landCells;
    public void GenerateAllRegions(List<MapCell> cells, out List<Province> provinces, out List<Duchy> duchies, out List<Kingdom> kingdoms, out List<Empire> empires)
    {
        landCells = cells;
        if (settings.enableDebugOutput)
            Debug.Log("Starting region formation...");

        // Clear static buffers for this generation cycle
        groupBuffer.Clear();
        frontierBuffer.Clear();
        processedBuffer.Clear();

        // Generate provinces with sophisticated expansion
        GenerateProvinces(out provinces);

        // Frontier search for higher tier regions
        GenerateDuchies(provinces, out duchies);
        GenerateKingdoms(duchies, out kingdoms);
        GenerateEmpires(kingdoms, out empires);

        // Assign colors to higher-tier regions AFTER the full hierarchy exists,
        // so neighbor detection is complete and reliable.
        AssignHigherTierRegionColors(duchies, kingdoms, empires);

        if (settings.enableDebugOutput)
            Debug.Log("Region formation completed");
    }
    private void GenerateProvinces(out List<Province> provinces)
    {
        provinces = new List<Province>();

        if (landCells.Count == 0)
        {
            Debug.LogWarning("No land cells found for province generation");
            return;
        }

        HashSet<MapCell> availableCells = new HashSet<MapCell>(landCells);

        // Lloyd's relaxation loop (first pass = 0, so total passes = lloydRelaxationPasses + 1)
        for (int lloydPass = 0; lloydPass <= settings.province.lloydRelaxationPasses; lloydPass++)
        {
            if (settings.enableDebugOutput && lloydPass == 0)
                Debug.Log($"Starting province generation (initial pass)");
            else if (settings.enableDebugOutput)
                Debug.Log($"Lloyd's relaxation pass {lloydPass}/{settings.province.lloydRelaxationPasses}");

            // Clear existing assignments for relaxation passes (not the first pass)
            List<MapCell> seedPoints = GetSeedPoints(lloydPass, availableCells, provinces);
            
            // Optimized frontier-based expansion
            ExpandProvincesUsingFrontier(provinces, availableCells);
        }

        // Finalize provinces
        foreach (var province in provinces)
        {
            province.SetupProvince();
        }

        if (settings.enableDebugOutput)
        {
            Debug.Log($"Generated {provinces.Count} provinces");
            foreach (var province in provinces)
            {
                Debug.Log($"Province '{province.Name}': {province.MapCells.Count} cells");
            }
        }
    }

    // Optimized frontier-based province expansion
    private void ExpandProvincesUsingFrontier(List<Province> provinces, HashSet<MapCell> availableCells)
    {
        // Clear static buffers for this expansion cycle
        provinceFrontierBuffer.Clear();
        frontierProcessedBuffer.Clear();

        // Initialize frontiers for all provinces
        InitializeProvinceFrontiers(provinces, availableCells);

        // Fair round-robin expansion
        int expansionRound = 0;
        bool stillExpanding = true;

        while (stillExpanding && availableCells.Count > 0)
        {
            stillExpanding = false;
            expansionRound++;

            if (settings.enableDebugOutput && expansionRound % 20 == 0)
                Debug.Log($"Frontier expansion round {expansionRound}, {availableCells.Count} cells remaining");

            // Round-robin through provinces
            foreach (var province in provinces)
            {
                if (availableCells.Count == 0) break;

                // Get best scored candidate from this province's frontier
                var bestCandidate = GetBestFrontierCandidate(province, availableCells);

                if (bestCandidate.HasValue)
                {
                    // Assign cell to province
                    var cell = bestCandidate.Value.Cell;
                    cell.Province = province;
                    province.MapCells.Add(cell);
                    availableCells.Remove(cell);
                    frontierProcessedBuffer.Add(cell);

                    // Update this province's frontier with new candidates
                    UpdateProvinceFrontier(province, cell, availableCells);

                    stillExpanding = true;
                }
            }
        }
    }

    // Initialize frontier candidates for all provinces
    private void InitializeProvinceFrontiers(List<Province> provinces, HashSet<MapCell> availableCells)
    {
        foreach (var province in provinces)
        {
            var frontier = new List<FrontierCandidate>();

            // Add initial border cells to frontier
            foreach (var cell in province.MapCells)
            {
                foreach (var neighbor in cell.Neighbors)
                {
                    // Check if neighbor is available for expansion
                    if (availableCells.Contains(neighbor) && HasPassableBorder(cell, neighbor))
                    {
                        var candidate = new FrontierCandidate
                        {
                            Cell = neighbor,
                            Score = CalculateExpansionScore(neighbor, province)
                        };
                        frontier.Add(candidate);
                    }
                }
            }

            provinceFrontierBuffer[province] = frontier;
        }
    }

    // Get the best available frontier candidate for a province
    private FrontierCandidate? GetBestFrontierCandidate(Province province, HashSet<MapCell> availableCells)
    {
        var frontier = provinceFrontierBuffer[province];
        FrontierCandidate best = default;
        float bestScore = float.MinValue;
        bool foundCandidate = false;

        // Find best available candidate
        for (int i = 0; i < frontier.Count; i++)
        {
            var candidate = frontier[i];
            // availability check
            if (availableCells.Contains(candidate.Cell) && candidate.Score > bestScore)
            {
                best = candidate;
                bestScore = candidate.Score;
                foundCandidate = true;
            }
        }

        if (foundCandidate)
        {
            return best;
        }
        else return null;
    }

    // Update a province's frontier after claiming a cell
    private void UpdateProvinceFrontier(Province province, MapCell claimedCell, HashSet<MapCell> availableCells)
    {
        var frontier = provinceFrontierBuffer[province];

        // Remove claimed cell from this province's frontier (if it was there)
        for (int i = frontier.Count - 1; i >= 0; i--)
        {
            if (frontier[i].Cell == claimedCell)
            {
                frontier.RemoveAt(i);
                break;
            }
        }

        // Add new frontier candidates from the newly claimed cell
        foreach (var neighbor in claimedCell.Neighbors)
        {
            // Check if neighbor is available and not already processed
            if (availableCells.Contains(neighbor) && 
                !frontierProcessedBuffer.Contains(neighbor) && 
                HasPassableBorder(claimedCell, neighbor))
            {
                // Check if this neighbor is already in the frontier
                bool alreadyInFrontier = false;
                for (int i = 0; i < frontier.Count; i++)
                {
                    if (frontier[i].Cell == neighbor)
                    {
                        alreadyInFrontier = true;
                        break;
                    }
                }

                if (!alreadyInFrontier)
                {
                    var candidate = new FrontierCandidate
                    {
                        Cell = neighbor,
                        Score = CalculateExpansionScore(neighbor, province)
                    };
                    frontier.Add(candidate);
                }
            }
        }
    }

    private List<MapCell> GetSeedPoints(int lloydPass, HashSet<MapCell> availableCells, List<Province> provinces)
    {
        List<MapCell> seedPoints = new();

        if (lloydPass == 0)
        {
            // Initial pass: use random Poisson seeds
            SelectRandomSeeds(availableCells, seedPoints);

            // Create provinces with seed points
            for (int i = 0; i < seedPoints.Count; i++)
            {
                var seed = seedPoints[i];
                Province province;
                // Initial pass: create new province
                province = new Province(new List<MapCell> { seed })
                {
                    MapColor = MapGenerator.RandomBrightColor()
                };
                provinces.Add(province);

                seed.Province = province;
                availableCells.Remove(seed);
            }

            if (settings.enableDebugOutput)
                Debug.Log($"Creating {seedPoints.Count} provinces from {landCells.Count} land cells)");
        }
        else
        {
            // Relaxation passes: use province centers as seeds
            availableCells = new HashSet<MapCell>(landCells);

            for (int i = 0; i < provinces.Count; i++)
            {
                var province = provinces[i];
                var centerCell = MapManager.instance.GetCellAt(province.Center);
                if (centerCell != null && !centerCell.IsWaterCell)
                {
                    seedPoints.Add(centerCell);
                }
            }

            provinces.ForEach(p => p.MapCells.Clear());
            landCells.ForEach(c => c.Province = null);

            for (int i = 0; i < seedPoints.Count; i++)
            {
                provinces[i].MapCells.Add(seedPoints[i]);
                seedPoints[i].Province = provinces[i];
                availableCells.Remove(seedPoints[i]);
            }

            if (settings.enableDebugOutput)
                Debug.Log($"Generated {seedPoints.Count} center seed cells from {provinces.Count} provinces");
        }
        return seedPoints;
    }
    private List<Vector2> GeneratePoissonPoints()
    {
        // Access MapSize from MapManager's NewMapGen instance
        float mapSize = MapManager.instance.map.MapSize - 60;
        float half = mapSize / 2f;

        var sampler = UniformPoissonDiskSampler.SampleRectangle(
            new Vector2(-half, -half),
            new Vector2(half, half),
            settings.province.minSeedDistance
        );

        return sampler.Select(p => new Vector2(p.x, p.y)).ToList();
    }

    private void SelectRandomSeeds(HashSet<MapCell> availableCells, List<MapCell> seedPoints)
    {
        var sampledPoints = GeneratePoissonPoints();
        
        foreach (var point in sampledPoints)
        {
            var cell = MapManager.instance.GetCellAt(point);
            if (cell != null && availableCells.Contains(cell) && !seedPoints.Contains(cell))
            {
                seedPoints.Add(cell);
            }
        }
    }

    private bool HasPassableBorder(MapCell cell1, MapCell cell2)
    {
        // Find border between these two cells
        foreach (var border in cell1.Borders)
        {
            if (cell2 == border.GetOtherCell(cell1))
            {
                // Check if border is passable (not blocked)
                return BorderFlag.IsPassable(border.Flags);
            }
        }
        
        // If no border found, assume not passable
        return false;
    }

    private float CalculateExpansionScore(MapCell cell, Province province)
    {
        float score = 100f; // Base score
        
        // Terrain-based scoring
        switch (cell.TerrainType)
        {
            case TerrainType.Mountain:
                score *= settings.province.mountainExpansionPenalty;
                break;
        }
        
        // Penalty for crossing rivers
        if (HasAnyRiverBorder(cell, province))
        {
            score *= settings.province.riverPenalty;
        }


        // Bonus for neighbor amount
        int provinceNeighbors = 0;
        var neighbors = cell.Neighbors;
        for (int i = 0; i < neighbors.Length; i++)
        {
            if (neighbors[i].Province == province)
                provinceNeighbors++;
        }
        if( provinceNeighbors > 1)
        {
            score *= 1 + ((provinceNeighbors - 1) * settings.province.neighborBonus);
        }
        
        return score;
    }

    private bool HasAnyRiverBorder(MapCell cell, Province province)
    {
        // Check if cell has any river borders with the target province
        foreach (var neighbor in cell.Neighbors)
        {
            if (neighbor.Province == province && HasRiverBorder(cell, neighbor))
            {
                return true;
            }
        }
        return false;
    }

    private bool HasRiverBorder(MapCell cell1, MapCell cell2)
    {
        // Find the border between these two cells and check for river flag
        foreach (var border in cell1.Borders)
        {
            if (cell2 == border.GetOtherCell(cell1))
            {
                return BorderFlag.HasFlag(border.Flags, BorderType.River);
            }
        }
        return false;
    }

    private void GenerateHigherTierRegions<TSub, TParent>(List<TSub> subRegions, out List<TParent> parentRegions) 
        where TSub : Region 
        where TParent : Region
    {
        parentRegions = new List<TParent>();
        
        if (subRegions.Count <= 1) return;
        
        var availableSubRegions = new List<TSub>(subRegions);
        
        // Calculate parent count based on region type and settings
        int parentCount;
        if (typeof(TParent) == typeof(Duchy))
        {
            int targetSubRegionsPerDuchy = (settings.duchy.minSubRegions + settings.duchy.maxSubRegions) / 2;
            parentCount = Mathf.Max(1, subRegions.Count / targetSubRegionsPerDuchy);
        }
        else if (typeof(TParent) == typeof(Kingdom))
        {
            int targetSubRegionsPerKingdom = (settings.kingdom.minSubRegions + settings.kingdom.maxSubRegions) / 2;
            parentCount = Mathf.Max(1, subRegions.Count / targetSubRegionsPerKingdom);
        }
        else if (typeof(TParent) == typeof(Empire))
        {
            int targetSubRegionsPerEmpire = (settings.empire.minSubRegions + settings.empire.maxSubRegions) / 2;
            parentCount = Mathf.Max(1, subRegions.Count / targetSubRegionsPerEmpire);
        }
        else
        {
            parentCount = Mathf.Max(1, subRegions.Count / 3); // Fallback
        }
        
        if (settings.enableDebugOutput)
        {
            string regionType = typeof(TParent).Name;
            Debug.Log($"Generating {parentCount} {regionType.ToLower()}s from {subRegions.Count} {typeof(TSub).Name.ToLower()}s");
        }
        for (int i = 0; i < parentCount && availableSubRegions.Any(); i++)
        {
            var seedSubRegion = availableSubRegions.First();
            // Determine max group size based on region type
            int maxGroupSize;
            if (typeof(TParent) == typeof(Duchy))
                maxGroupSize = settings.duchy.maxSubRegions;
            else if (typeof(TParent) == typeof(Kingdom))
                maxGroupSize = settings.kingdom.maxSubRegions;
            else if (typeof(TParent) == typeof(Empire))
                maxGroupSize = settings.empire.maxSubRegions;
            else
                maxGroupSize = 3; // Fallback
                
            var groupedSubRegions = FrontierSearch(seedSubRegion, availableSubRegions, maxGroupSize);
            
            TParent parentRegion;
            
            // Create the appropriate parent region based on type
            if (typeof(TParent) == typeof(Duchy))
            {
                parentRegion = (TParent)(object)new Duchy(groupedSubRegions.Cast<Province>().ToList());
            }
            else if (typeof(TParent) == typeof(Kingdom))
            {
                parentRegion = (TParent)(object)new Kingdom(groupedSubRegions.Cast<Duchy>().ToList());
            }
            else if (typeof(TParent) == typeof(Empire))
            {
                parentRegion = (TParent)(object)new Empire(groupedSubRegions.Cast<Kingdom>().ToList());
            }
            else
            {
                throw new System.NotSupportedException($"Unsupported parent region type: {typeof(TParent).Name}");
            }
            parentRegions.Add(parentRegion);
            
            foreach (var subRegion in groupedSubRegions)
            {
                availableSubRegions.Remove(subRegion);
            }
        }
        
        // Assign any remaining sub-regions to the closest neighboring parent region
        if (availableSubRegions.Any() && settings.enableDebugOutput)
        {
            Debug.Log($"Assigning {availableSubRegions.Count} remaining {typeof(TSub).Name.ToLower()}s to closest {typeof(TParent).Name.ToLower()}");
        }
        
        foreach (var subRegion in availableSubRegions)
        {
            if (parentRegions.Count == 0) break;

            var bestParent = parentRegions
                .OrderByDescending(parent => parent.SubRegions.Count(member => member.NeighboringRegions.Contains(subRegion)))
                .First();

            bestParent.SubRegions.Add(subRegion);
            subRegion.ParentRegion = bestParent;
        }
    }

    private void GenerateDuchies(List<Province> provinces, out List<Duchy> duchies)
    {
        GenerateHigherTierRegions<Province, Duchy>(provinces, out duchies);
    }

    private void GenerateKingdoms(List<Duchy> duchies, out List<Kingdom> kingdoms)
    {
        GenerateHigherTierRegions<Duchy, Kingdom>(duchies, out kingdoms);
    }

    private void GenerateEmpires(List<Kingdom> kingdoms, out List<Empire> empires)
    {
        GenerateHigherTierRegions<Kingdom, Empire>(kingdoms, out empires);
    }

    private List<T> FrontierSearch<T>(T seed, List<T> availableItems, int maxGroupSize) where T : Region
    {
        // Clear and reuse static buffers to eliminate allocations
        groupBuffer.Clear();
        frontierBuffer.Clear();
        processedBuffer.Clear();
        
        // Cast to Region for buffer usage
        groupBuffer.Add(seed);
        frontierBuffer.Add(seed);
        processedBuffer.Add(seed);
        
        while (frontierBuffer.Any() && groupBuffer.Count < maxGroupSize)
        {
            var current = (T)frontierBuffer.First();
            frontierBuffer.Remove(current);
            
            // Find neighbors using the current item's NeighboringRegions
            var neighbors = FindNeighbors(current, availableItems);
            
            foreach (var neighbor in neighbors)
            {
                if (!processedBuffer.Contains(neighbor) && groupBuffer.Count < maxGroupSize)
                {
                    groupBuffer.Add(neighbor);
                    frontierBuffer.Add(neighbor);
                    processedBuffer.Add(neighbor);
                    availableItems.Remove(neighbor);
                }
            }
        }
        
        // Create result list from buffer and cast back to type T
        var result = new List<T>(groupBuffer.Count);
        foreach (var item in groupBuffer)
        {
            result.Add((T)item);
        }
        
        return result;
    }

    private List<T> FindNeighbors<T>(T item, List<T> availableItems) where T : Region
    {
        // Find actual neighboring regions that are still available
        var neighboringRegions = item.NeighboringRegions
            .Where(neighbor => availableItems.Contains(neighbor))
            .Cast<T>()
            .ToList();
            
        return neighboringRegions;
    }

    // Assign distinct, neighbor-safe colors to higher-tier regions. Must run AFTER the
    // full hierarchy (including ParentRegion links) exists, since Duchy/Kingdom/Empire
    // neighbor detection relies on fully-wired parent links.
    private void AssignHigherTierRegionColors(List<Duchy> duchies, List<Kingdom> kingdoms, List<Empire> empires)
    {
        AssignNeighborDistinctColors(duchies.Cast<Region>().ToList());
        AssignNeighborDistinctColors(kingdoms.Cast<Region>().ToList());
        AssignNeighborDistinctColors(empires.Cast<Region>().ToList());
    }

    private void AssignNeighborDistinctColors(List<Region> regions)
    {
        foreach (var region in regions)
        {
            Color mapColor = MapGenerator.RandomBrightColor();
            int guard = 0;
            while (HasSimilarNeighborColor(region, mapColor) && guard++ < 50)
                mapColor = MapGenerator.RandomBrightColor();
            region.MapColor = mapColor;
        }
    }

    private bool HasSimilarNeighborColor(Region region, Color color)
    {
        foreach (var neighbor in region.NeighboringRegions)
        {
            if (neighbor.MapColor != Color.white && MapGenerator.AreColorsSimilarHSV(neighbor.MapColor, color))
                return true;
        }
        return false;
    }

    // Build du jure titles for higher-tier regions and wire the liege/vassal hierarchy.
    // The ruler of each region's capital province is promoted to hold the region title,
    // mirroring the "a duke also rules the county of his capital" pattern.
    public void CreateHigherTierTitles(List<Duchy> duchies, List<Kingdom> kingdoms, List<Empire> empires)
    {
        foreach (var duchy in duchies)
        {
            Character duke = GetHolder(duchy);
            if (duke == null)
            {
                Debug.LogWarning($"Skipping duchy title for '{duchy.Name}': no capital ruler found");
                continue;
            }

            var duchyTitle = new RegionTitle(duchy.Name, duke, duchy, TitleRank.Duchy, new List<Title>());
            duke.AddTitle(duchyTitle);

            foreach (var province in duchy.GetProvinces())
            {
                Character ruler = province.Capital?.Ruler;
                if (ruler == null) continue;

                var provinceTitle = FindRegionTitle(ruler, province);
                if (provinceTitle == null) continue;

                duchyTitle.SubTitles.Add(provinceTitle);
                provinceTitle.Liege = duchyTitle;

                if (ruler != duke)
                {
                    ruler.Liege = duke;
                    if (!duke.Vassals.Contains(ruler))
                        duke.Vassals.Add(ruler);
                }
            }
        }

        foreach (var kingdom in kingdoms)
        {
            Character king = GetHolder(GetCapitalDuchy(kingdom));
            if (king == null)
            {
                Debug.LogWarning($"Skipping kingdom title for '{kingdom.Name}': no capital duchy ruler found");
                continue;
            }

            var kingdomTitle = new RegionTitle(kingdom.Name, king, kingdom, TitleRank.Kingdom, new List<Title>());
            king.AddTitle(kingdomTitle);

            foreach (var duchy in kingdom.SubRegions.Cast<Duchy>())
            {
                Character duke = GetHolder(duchy);
                if (duke == null) continue;

                var duchyTitle = FindRegionTitle(duke, duchy);
                if (duchyTitle == null) continue;

                kingdomTitle.SubTitles.Add(duchyTitle);
                duchyTitle.Liege = kingdomTitle;

                if (duke != king)
                {
                    duke.Liege = king;
                    if (!king.Vassals.Contains(duke))
                        king.Vassals.Add(duke);
                }
            }
        }

        foreach (var empire in empires)
        {
            Character emperor = GetHolder(GetCapitalKingdom(empire));
            if (emperor == null)
            {
                Debug.LogWarning($"Skipping empire title for '{empire.Name}': no capital kingdom ruler found");
                continue;
            }

            var empireTitle = new RegionTitle(empire.Name, emperor, empire, TitleRank.Empire, new List<Title>());
            emperor.AddTitle(empireTitle);

            foreach (var kingdom in empire.SubRegions.Cast<Kingdom>())
            {
                Character king = GetHolder(GetCapitalDuchy(kingdom));
                if (king == null) continue;

                var kingdomTitle = FindRegionTitle(king, kingdom);
                if (kingdomTitle == null) continue;

                empireTitle.SubTitles.Add(kingdomTitle);
                kingdomTitle.Liege = empireTitle;

                if (king != emperor)
                {
                    king.Liege = emperor;
                    if (!emperor.Vassals.Contains(king))
                        emperor.Vassals.Add(king);
                }
            }
        }
    }

    // The character to promote for a region: ruler of its capital province.
    private Character GetHolder(Region region)
    {
        return region?.Capital?.Ruler;
    }

    // The duchy that contains a kingdom's capital province.
    private Duchy GetCapitalDuchy(Kingdom kingdom)
    {
        return kingdom.Capital?.Province?.ParentRegion as Duchy;
    }

    // The kingdom that contains an empire's capital province.
    private Kingdom GetCapitalKingdom(Empire empire)
    {
        return empire.Capital?.Province?.ParentRegion?.ParentRegion as Kingdom;
    }

    // Find the region title for a region via the ruler of its capital.
    // Valid for all tiers because higher-tier titles are promoted to the capital's ruler.
    public static RegionTitle FindRegionTitle(Region region)
    {
        Character holder = region?.Capital?.Ruler;
        return holder?.Titles.OfType<RegionTitle>().FirstOrDefault(t => t.AssociatedRegion == region);
    }

    private static RegionTitle FindRegionTitle(Character holder, Region region)
    {
        return holder.Titles.OfType<RegionTitle>().FirstOrDefault(t => t.AssociatedRegion == region);
    }

}