using UnityEngine;

// Unified sprite management system for locations
// Handles both map sprites (for world rendering) and UI portraits (for interface display)
[CreateAssetMenu(fileName = "LocationSpriteManager", menuName = "Scriptable Objects/LocationSpriteManager")]
public class LocationSpriteManager : ScriptableObject
{
    [System.Serializable]
    public class LocationSpriteEntry
    {
        public LocationType locationType;
        [Header("Map Sprites")]
        [Tooltip("Sprites shown on the game map (randomly selected)")]
        public Sprite[] mapSprites;
        [Header("UI Portraits")]
        [Tooltip("Portraits shown in UI panels and character titles")]
        public Sprite[] uiPortraits;
    }
    
    [Header("Location Sprite Data")]
    public LocationSpriteEntry[] locationSprites;
    
    // Cache for fast lookups
    private System.Collections.Generic.Dictionary<LocationType, LocationSpriteEntry> _spriteCache;
    
    private void OnEnable()
    {
        BuildSpriteCache();
    }
    
    // Builds a dictionary for fast sprite lookups
    private void BuildSpriteCache()
    {
        _spriteCache = new System.Collections.Generic.Dictionary<LocationType, LocationSpriteEntry>();
        
        foreach (var entry in locationSprites)
        {
            if (!_spriteCache.ContainsKey(entry.locationType))
            {
                _spriteCache[entry.locationType] = entry;
            }
            else
            {
                Debug.LogWarning($"Duplicate LocationType entry found: {entry.locationType}");
            }
        }
    }
    
    // Gets a random map sprite for the specified location type
    public Sprite GetRandomMapSprite(LocationType locationType)
    {
        if (_spriteCache.TryGetValue(locationType, out LocationSpriteEntry entry))
        {
            if (entry.mapSprites != null && entry.mapSprites.Length > 0)
            {
                return entry.mapSprites[Random.Range(0, entry.mapSprites.Length)];
            }
        }
        
        //Debug.LogWarning($"No map sprites found for location type: {locationType}");
        return null;
    }
    
    // Gets a random UI portrait for the specified location type
    public Sprite GetRandomUIPortrait(LocationType locationType)
    {
        if (_spriteCache.TryGetValue(locationType, out LocationSpriteEntry entry))
        {
            if (entry.uiPortraits != null && entry.uiPortraits.Length > 0)
            {
                return entry.uiPortraits[Random.Range(0, entry.uiPortraits.Length)];
            }
        }
        
        Debug.LogWarning($"No UI portraits found for location type: {locationType}");
        return null;
    }
    
}