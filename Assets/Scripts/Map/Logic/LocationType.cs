using UnityEngine;

// Unified location type system that consolidates Holdings and Zones
// Holdings are settlements with rulers (Castle, City, Church)
// Zones are terrain features without rulers (Ruin, Cave, etc.)
public enum LocationType
{
    // Holdings - settlements that can have rulers and generate taxes
    Castle,
    City, 
    Church,
    
    // Zones - terrain features and points of interest
    Ruin,
    Cave,
    Oasis,
    Village,
    Temple,
    Mine,
    Camp,
    Lake
}

// Extension methods for LocationType to provide categorization and helper functions
public static class LocationTypeExtensions
{
    // Returns true if this location type is a Holding (settlement with ruler)
    public static bool IsHolding(this LocationType type)
    {
        return type switch
        {
            LocationType.Castle => true,
            LocationType.City => true,
            LocationType.Church => true,
            _ => false
        };
    }
    
    // Returns true if this location type is a Zone (terrain feature)
    public static bool IsZone(this LocationType type)
    {
        return !type.IsHolding();
    }
    
    // Gets the default population for this location type
    public static float GetDefaultPopulation(this LocationType type)
    {
        return type switch
        {
            LocationType.Castle => 1000f,
            LocationType.City => 2000f,
            LocationType.Church => 500f,
            LocationType.Village => 100f,
            _ => 50f // Default for zones
        };
    }
    
    // Gets whether this location type requires a ruler
    public static bool RequiresRuler(this LocationType type)
    {
        return type.IsHolding();
    }
    
    // Gets whether this location type can have improvements
    public static bool CanHaveImprovements(this LocationType type)
    {
        return type.IsHolding();
    }
    
    // Gets whether this location type can be developed
    public static bool IsDevelopable(this LocationType type)
    {
        return type switch
        {
            LocationType.Ruin => true,
            LocationType.Village => true,
            _ => false
        };
    }
}