using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[System.Serializable]
public class Province : Region
{
    public List<MapCell> MapCells = new();

    // Backing field for neighbor management
    private readonly List<Region> _neighboringRegions = new();

    public Religion Religion { get; private set; }
    public Culture Culture { get; private set; }
    // In Province class
    public override Vector2 Center =>
        (MapCells != null && MapCells.Count > 0)
        ? new Vector2(
            MapCells.Average(c => c.Center.x),
            MapCells.Average(c => c.Center.y)
          )
        : Vector2.zero;


    public override List<Region> NeighboringRegions => _neighboringRegions;

    public new Location Capital => Locations.Count > 0 ? Locations[0] : null;
    public List<Location> Locations { get; private set; } = new();

    // Lord of the province's capital; also holds titles for generated settlements
    private Character _lord;

    public Province(List <MapCell> mapCells) : base(Rank.Province, null)
    {
        MapCells = mapCells;
        foreach (var cell in MapCells)
            cell.Province = this;
    }
    public override void CreateBorderLoops()
    {
        BorderLoops.Clear();

        if (MapCells.Count == 0)
        {
            Debug.LogWarning("Province has no map cells.");
            return;
        }
        // Use unified border system for efficient border loop creation
        var loops2D = ProvinceBorderUtils.CreateProvinceBorderLoops(this);
        
        // Convert to Vector3 for compatibility with existing system
        BorderLoops = loops2D.Select(loop => loop.Select(p => (Vector3)p).ToList()).ToList();
    }

    public void SetupProvince()
    {
        CreateBorderLoops();
        SetColor(MapColor);
        AssignReligionAndCulture();
        SetNeighbors();
        if (!CreateCapitalHoldingAndRuler())
        {
            Debug.LogError($"Failed to create capital for province {Name}");
            return;
        }
        
        CreateExtraLocations();
        CreateExtraHoldings();
    }

    private void AssignReligionAndCulture()
    {
        Religion = GetRandomItemSafe(Religion.AllReligions);
        Culture = GetRandomItemSafe(Culture.AllCultures);
    }

    void SetNeighbors()
    {
        foreach (var neighbor in ProvinceBorderUtils.GetBorderingProvinces(this))
        {
            AddNeighbor(neighbor);
        }
    }

    private bool CreateCapitalHoldingAndRuler()
    {
        try 
        {
            // Validate MapCells exists
            if (MapCells == null || MapCells.Count == 0)
            {
                Debug.LogError($"Province {Name} has no map cells to place capital");
                return false;
            }
            
            //create a new character to rule over the new castle
            Character character;
            try
            {
                character = new Character(gender: (Gender)Random.Range(0, 2), Married: true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Exception creating character for province {Name}: {e.Message}");
                return false;
            }
            
            if (character == null)
            {
                Debug.LogError($"Failed to create character for province {Name}");
                return false;
            }

            // Inherit the province's culture and religion so rulers (and their
            // descendants) reflect the region they rule rather than the global default.
            character.Culture = Culture ?? character.Culture;
            character.Religion = Religion ?? character.Religion;

            //create holding of type castle
            var capitalLocation = new Location(LocationType.Castle, this, MapCells[0], character); //temp assign cell 0 as capital
            Locations.Add(capitalLocation);

            //create location title for the castle
            var castleTitle = new HoldingTitle(capitalLocation.Name + " Castle", character, capitalLocation);
            character.AddTitle(castleTitle);

            //create province title
            var regionTitle = new RegionTitle(Name, character, this, TitleRank.Province, new List<Title> { castleTitle });
            character.AddTitle(regionTitle);

            _lord = character;
            
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error creating capital for province {Name}: {e.Message}");
            return false;
        }
    }

    private void CreateExtraLocations()
    {
        //Debug.Log("extra extra " + MapCells.Count);
        // Create zones on cells using probability-based generation
        foreach (var cell in MapCells)
        {
            if (Random.Range(0,10) > 6)
            {
                var locationType = Location.GetLocationTypeForTerrain(cell.TerrainType, cell);
                if (locationType.HasValue) // Only create location if one was generated
                {
                    Location location = new Location(locationType.Value, this, cell);
                    Locations.Add(location);
                }
            }
        }
    }

    private void CreateExtraHoldings()
    {
        int settlementCount = GetSettlementCount();
        if (settlementCount <= 0) return;

        for (int i = 0; i < settlementCount; i++)
        {
            MapCell cell = PickSettlementCell();
            if (cell == null) break;

            float roll = Random.value;
            if (roll < 0.55f || _lord == null)
            {
                // Village (zone, no ruler required)
                Locations.Add(new Location(LocationType.Village, this, cell));
            }
            else
            {
                // Church / City (holding, requires a ruler -> the province lord holds the title)
                LocationType type = roll < 0.90f ? LocationType.Church : LocationType.City;
                Location location = new Location(type, this, cell, _lord);
                var holdingTitle = new HoldingTitle(location.Name + " " + type, _lord, location);
                _lord.AddTitle(holdingTitle);
                Locations.Add(location);
            }
        }
    }

    private int GetSettlementCount()
    {
        if (MapCells.Count >= 20) return 3;
        if (MapCells.Count >= 10) return 2;
        return 1;
    }

    private MapCell PickSettlementCell()
    {
        var usedCells = new HashSet<MapCell>(Locations.Select(l => l.MapCell));
        var candidates = MapCells.Where(c => !usedCells.Contains(c)).ToList();
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    public float GetPopulation()
    {
        float totalPop = 0f;
        foreach (var loc in Locations)
            totalPop += loc.Population;
        return totalPop;
    }

    // Explicitly manage neighbors
    public void AddNeighbor(Province neighbor)
    {
        if (!_neighboringRegions.Contains(neighbor))
        {
            _neighboringRegions.Add(neighbor);
            if (!neighbor._neighboringRegions.Contains(this))
                neighbor._neighboringRegions.Add(this);
        }
    }


    public void SetColor(Color color)
    {
        foreach (var cell in MapCells)
        {
            if (cell.Renderer != null)
            {
                // Use PropertyBlock for better performance than material instances
                var propBlock = new MaterialPropertyBlock();
                cell.Renderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", color);
                cell.Renderer.SetPropertyBlock(propBlock);
            }
        }
    }

    private static T GetRandomItemSafe<T>(List<T> list)
    {
        return (list != null && list.Count > 0) ? list[Random.Range(0, list.Count)] : default;
    }

    public override string ToString()
    {
        return Capital.Name; // This is what Unity will display in the Inspector
    }
}