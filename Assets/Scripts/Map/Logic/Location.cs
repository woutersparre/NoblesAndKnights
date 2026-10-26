using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using System;

/// <summary>
/// Tracks how much a character knows about a location
/// </summary>
public enum DiscoveryState
{
    Unvisited,
    Visited,
    Explored
}

[System.Serializable]
public class Location
{
    private static int nextLocationId = 1;
    public int UniqueId { get; private set; }
    
    public string Name;
    [System.NonSerialized]
    public Character Ruler;
    public LocationType Type;
    public Sprite Image; // UI portrait sprite
    [System.NonSerialized]
    public Province Province;
    [System.NonSerialized]
    public MapCell MapCell;
    
    [Header("Location Stats")]
    public float MilitaryStrength;
    public float DefensiveStrength;
    public float Population;
    public float FortificationStrength;
    public float AvailableLevy;
    
    [Header("Development")]
    public bool Developable;
    
    [SerializeReference]
    public List<Improvement> Improvements = new();

    public UnityEvent OnUpgradeFinish = new();
    
    // Per-character discovery state tracking
    private Dictionary<int, DiscoveryState> characterDiscoveryState = new Dictionary<int, DiscoveryState>();
    
    // Properties for backwards compatibility and convenience
    public bool IsHolding => Type.IsHolding();
    public bool IsZone => Type.IsZone();
    public Vector2 Position => MapCell?.Center ?? (Province?.Center ?? Vector2.zero);

    // Discovery state methods
    public DiscoveryState GetDiscoveryState(Character character)
    {
        if (character == null) return DiscoveryState.Unvisited;
        if (characterDiscoveryState.TryGetValue(character.UniqueId, out DiscoveryState state))
            return state;
        return DiscoveryState.Unvisited;
    }

    public void Visit(Character character)
    {
        if (character == null) return;
        int id = character.UniqueId;
        if (!characterDiscoveryState.ContainsKey(id) || characterDiscoveryState[id] < DiscoveryState.Visited)
        {
            characterDiscoveryState[id] = DiscoveryState.Visited;
        }
    }

    public void Explore(Character character)
    {
        if (character == null) return;
        int id = character.UniqueId;
        characterDiscoveryState[id] = DiscoveryState.Explored;
    }
    
    // Creates a new location with the specified type and parameters
    public Location(LocationType type, Province province, MapCell cell, Character ruler = null, float? population = null)
    {
        UniqueId = nextLocationId++;
        
        Type = type;
        Province = province;
        MapCell = cell;
        
        // Validate ruler requirement
        if (Type.RequiresRuler() && ruler == null)
        {
            throw new ArgumentException($"Location type {type} requires a ruler but none was provided");
        }
        
        Ruler = ruler;
        Population = population ?? Type.GetDefaultPopulation();
        Name = GenerateName();
        
        // Initialize improvements for holdings
        if (Type.CanHaveImprovements())
        {
            InitializeImprovements();
        }
        
        // Initialize developable status
        Developable = Type.IsDevelopable();
        
        AvailableLevy = MilitaryStrength;

        // Subscribe to time events. TimeManager.SubscribeDayPassed queues the
        // listener when the manager has not been created yet, so a location built
        // during map setup still gets its levy tick and build countdown instead of
        // silently missing every day for the rest of the session.
        TimeManager.SubscribeDayPassed(OnDayPassed);
    }

    // Single entry point for the daily tick: levy recovery and build countdowns
    // both hang off it so they can never end up subscribed separately (or not at
    // all, as happened when TimeManager.instance was still null here).
    private void OnDayPassed()
    {
        ReplenishLevy();
        TickConstruction();
    }
    
    // Generates a name based on the location type
    private string GenerateName()
    {
        if (Type.IsZone() && Type != LocationType.Village)
        {
            // Zones use their type name (settlements get proper names)
            return Type.ToString();
        }
        else
        {
            // Holdings get random location names
            if (Names.randomLocationNames != null && Names.randomLocationNames.Count > 0)
            {
                return Names.randomLocationNames[UnityEngine.Random.Range(0, Names.randomLocationNames.Count)];
            }
            else
            {
                return Type.ToString();
            }
        }
    }
    
    // Initializes default improvements for holdings. Most start unbuilt (level 0);
    // the player pays ConstructionCost to raise them to level 1, then upgrades them
    // past that through the same button. Castle walls are the exception.
    protected virtual void InitializeImprovements()
    {
        if (Type.CanHaveImprovements())
        {
            Improvements.Add(new Barracks(0));

            // A castle is a fortification, so its walls are already standing instead
            // of being something the player raises from nothing. City and Church
            // start with none and have to build them.
            Improvements.Add(new Walls(Type == LocationType.Castle ? 1 : 0));

            // Existed as a class but was never added anywhere, so its BuildTime of 3
            // and its population bonus could never show up in the game.
            Improvements.Add(new HuntingLodge(0));

            ApplyAllImprovementEffects();
        }
    }
    
    // Applies all improvement effects to this location
    private void ApplyAllImprovementEffects()
    {
        foreach (var improvement in Improvements)
        {
            improvement.ApplyEffect(this);
        }
    }
    
    // Gets the tax income for this location
    public float GetTax()
    {
        return Population * 0.01f;
    }

    // Raises the local levy: dispatches an ArmyFormation on a MapPawn from this location toward the player's capital.
    // The levy pool is consumed and replenishes over time (see ReplenishLevy).
    public void RaiseLocalLevy()
    {
        if (AvailableLevy < 1) return;

        if (TravelManager.instance == null)
        {
            Debug.LogError("Cannot raise levy: TravelManager.instance is null");
            return;
        }

        Location destination = ResolveCapitalLocation(MapManager.instance?.characters?.Player);
        if (destination == null)
        {
            Debug.LogError("Cannot raise levy: player has no capital holding");
            return;
        }
        if (destination == this)
        {
            Debug.LogWarning("Cannot raise levy: this location is already the player's capital");
            return;
        }

        var amount = AvailableLevy;
        AvailableLevy = 0;
        Character commander = Ruler ?? MapManager.instance?.characters?.Player;

        TravelManager.instance.RequestArmyTravel(this, destination, commander, amount);
    }

    // The character's main title can be a provincial (RegionTitle) or landed (HoldingTitle) title;
    // both expose the capital holding Location the character resides at.
    public static Location ResolveCapitalLocation(Character character)
    {
        if (character?.MainTitle == null) return null;
        return character.MainTitle switch
        {
            HoldingTitle ht => ht.AssociatedLocation,
            RegionTitle rt => rt.AssociatedRegion?.Capital,
            _ => null
        };
    }

    // Reports why an upgrade was refused. Both channels matter: the console for
    // development, the message log for the player, whose click would otherwise look
    // like it did nothing at all.
    public void Notify(string reason)
    {
        Debug.Log($"{Name}: {reason}");
        MessageManager.instance?.Send(reason);
    }

    // Upgrades an improvement at this location
    public void UpgradeImprovement(Improvement _improvement, Character upgrader)
    {
        if (_improvement == null)
        {
            Notify("Improvement is null!");
            return;
        }
        // Find the improvement in the list
        Improvement improvement = Improvements.Find(i => i.GetType() == _improvement.GetType());
        if (improvement == null)
        {
            Notify($"Improvement of type {_improvement.GetType().Name} not found in this location!");
            return;
        }

        // Check if character is the ruler or liege (Ruler can be null, which used to
        // throw here instead of telling the player anything)
        if (Ruler == null || (upgrader != Ruler && upgrader != Ruler.Liege))
        {
            Notify($"{upgrader.Name} is not the ruler or liege of {Name}!");
            return;
        }

        // One build at a time per improvement: the level only lands on completion,
        // so a second click would otherwise queue a second level for one price.
        if (improvement.IsUnderConstruction)
        {
            Notify($"{improvement.Name} at {Name} is already under construction ({improvement.ConstructionDaysLeft} days left)!");
            return;
        }

        // Check if the improvement can be upgraded
        if (improvement.Level >= improvement.MaxLevel)
        {
            Notify($"Improvement {improvement.Name} is already at max level!");
            return;
        }

        // Check if the character has enough money. The cost has to be read before
        // anything runs, and it depends on the level the improvement sits at now:
        // an unbuilt improvement is charged ConstructionCost by Upgrade(), anything
        // else the upgrade price, so the guard has to follow whichever branch will
        // be taken. Neither figure changes while a build is running, because the
        // level only lands on completion.
        float cost = improvement.Level == 0 ? improvement.ConstructionCost : improvement.GetUpgradeCost();
        if (upgrader.Wealth < cost)
        {
            Notify($"{upgrader.Name} does not have enough money to upgrade {improvement.Name}! ({cost:F0} needed, {upgrader.Wealth:F0} held)");
            return;
        }

        // Upgrade the improvement. The cost is deducted inside StartConstruction(),
        // reached through Improvement.Upgrade() -- deducting it here as well charged
        // the player twice, and left a window where the re-check inside Upgrade()
        // could fail after the first deduction and take the money without upgrading.
        //
        // Starting it refreshes the UI through OnUpgradeFinish, so the entry shows
        // the countdown rather than the new level.
        improvement.Upgrade(this, upgrader);
    }

    // Starts construction of an improvement at this location. The gold leaves here
    // and the level only lands when the BuildTime countdown reaches zero.
    public void StartConstruction(Improvement improvement, Character builder, float cost)
    {
        // Deduct the cost
        builder.Wealth -= cost;

        // BuildTime is authored in days; anything non-positive still costs a day so
        // the improvement is never in a half-finished state for a whole tick.
        improvement.ConstructionDaysLeft = Mathf.Max(1, Mathf.CeilToInt(improvement.BuildTime));

        Debug.Log($"{builder.Name} started {improvement.Name} at {Name} for {cost} wealth, finishing in {improvement.ConstructionDaysLeft} days.");

        OnUpgradeFinish.Invoke();
    }

    // Counts down every build running here and finishes the ones that hit zero.
    private void TickConstruction()
    {
        bool changed = false;

        foreach (Improvement improvement in Improvements)
        {
            if (!improvement.IsUnderConstruction)
                continue;

            improvement.ConstructionDaysLeft--;

            // The UI displays the remaining days, so it needs a refresh on every day
            // a build is running, not only on the day it completes.
            changed = true;

            if (improvement.ConstructionDaysLeft <= 0)
                improvement.CompleteConstruction(this);
        }

        if (changed)
            OnUpgradeFinish.Invoke();
    }



    // Replenishes available levy over time
    private void ReplenishLevy()
    {
        if (AvailableLevy < MilitaryStrength)
        {
            AvailableLevy = Mathf.Min(AvailableLevy + 1, MilitaryStrength);
        }
    }

    // Get a string representation of this location
    public override string ToString()
    {
        return $"{Type} {Name} (Population: {Population}, Ruler: {Ruler?.Name ?? "None"})";
    }

    // Gets a random location type (for zone generation)
    public static LocationType GetRandomZoneType()
    {
        Array values = Enum.GetValues(typeof(LocationType));
        List<LocationType> zoneTypes = new List<LocationType>();
        
        foreach (LocationType type in values)
        {
            if (type.IsZone())
            {
                zoneTypes.Add(type);
            }
        }
        
        if (zoneTypes.Count == 0)
        {
            Debug.LogError("No zone types found in LocationType enum!");
            return LocationType.Ruin; // Fallback
        }
        
        return zoneTypes[UnityEngine.Random.Range(0, zoneTypes.Count)];
    }

    // Gets a location type appropriate for the given terrain
    public static LocationType? GetLocationTypeForTerrain(TerrainType terrainType, MapCell cell = null)
    {
        float randomChance = UnityEngine.Random.value;
        
        switch (terrainType)
        {
            case TerrainType.Desert:
                if (randomChance < 0.08f) return LocationType.Oasis; // 8% chance for oasis
                if (randomChance < 0.12f) return LocationType.Ruin; // 4% chance for ancient ruin
                if (randomChance < 0.14f) return LocationType.Camp; // 2% chance for nomad camp
                break;
                
            case TerrainType.Mountain:
                if (randomChance < 0.15f) return LocationType.Cave; // 15% chance for cave
                if (randomChance < 0.18f) return LocationType.Mine; // 3% chance for abandoned mine
                if (randomChance < 0.20f) return LocationType.Temple; // 2% chance for mountain temple
                break;
                
            case TerrainType.Forest:
                if (randomChance < 0.10f) return LocationType.Cave; // 10% chance for cave
                if (randomChance < 0.13f) return LocationType.Ruin; // 3% chance for ruins
                if (randomChance < 0.15f) return LocationType.Camp; // 2% chance for hunter camp
                break;
                
            case TerrainType.Ice:
                if (randomChance < 0.04f) return LocationType.Ruin; // 4% chance for shipwreck/underwater ruin
                break;
                
            case TerrainType.Tundra:
                if (randomChance < 0.08f) return LocationType.Camp; // 8% chance for nomadic camp
                if (randomChance < 0.12f) return LocationType.Ruin; // 4% chance for ancient ruins
                break;
                
            case TerrainType.Taiga:
                if (randomChance < 0.08f) return LocationType.Cave; // 8% chance for cave
                if (randomChance < 0.12f) return LocationType.Camp; // 4% chance for frontier camp
                if (randomChance < 0.14f) return LocationType.Ruin; // 2% chance for old ruin
                break;
                
            case TerrainType.Grassland:
                if (randomChance < 0.10f) return LocationType.Village; // 10% chance for village
                if (randomChance < 0.14f) return LocationType.Camp; // 4% chance for camp
                if (randomChance < 0.16f) return LocationType.Ruin; // 2% chance for ruin
                break;
                
            case TerrainType.Savanna:
                if (randomChance < 0.08f) return LocationType.Camp; // 8% chance for tribal camp
                if (randomChance < 0.10f) return LocationType.Mine; // 2% chance for shallow mine
                if (randomChance < 0.12f) return LocationType.Ruin; // 2% chance for ruin
                break;
                
            case TerrainType.Rainforest:
                if (randomChance < 0.06f) return LocationType.Oasis; // 6% chance for jungle pool
                if (randomChance < 0.08f) return LocationType.Temple; // 2% chance for temple
                if (randomChance < 0.10f) return LocationType.Cave; // 2% chance for cave
                if (randomChance < 0.12f) return LocationType.Mine; // 2% chance for mine
                break;
                
            case TerrainType.Wetland:
                if (randomChance < 0.08f) return LocationType.Oasis; // 8% chance for wetland spring
                if (randomChance < 0.10f) return LocationType.Ruin; // 2% chance for ruin
                if (randomChance < 0.12f) return LocationType.Camp; // 2% chance for camp
                break;
        }
        
        return null;
    }
}