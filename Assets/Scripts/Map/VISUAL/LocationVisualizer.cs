using UnityEngine;
using System.Collections.Generic;

public class LocationVisualizer : MonoBehaviour
{
    [Header("Location Prefab")]
    [SerializeField] public GameObject locationPrefab;
    
    [Header("Sprite Data")]
    [SerializeField] public LocationSpriteManager spriteManager;
    
    [Header("Visual Settings")]
    [SerializeField] public float yOffset = 0.1f;
    
    private Dictionary<Location, GameObject> locationObjects = new Dictionary<Location, GameObject>();
    
    private void Start()
    {
        MapManager.OnMapGenerated.AddListener(VisualizeAllLocations);
    }
    
    private void OnDestroy()
    {
        MapManager.OnMapGenerated.RemoveListener(VisualizeAllLocations);
    }
    
    public void VisualizeAllLocations()
    {
        if (MapManager.instance == null)
        {
            Debug.LogError("MapManager.instance is null!");
            return;
        }
        
        foreach (var province in MapManager.instance.provincesDuJure)
        {
            VisualizeProvinceLocations(province);
        }
    }
    
    public void VisualizeProvinceLocations(Province province)
    {
        if (province == null || province.Locations == null) return;
        
        foreach (var location in province.Locations)
        {
            if (!locationObjects.ContainsKey(location))
            {
                CreateLocationVisual(location);
            }
        }
    }
    
    private GameObject CreateLocationVisual(Location location)
    {
        if (locationPrefab == null)
        {
            Debug.LogError("Location prefab is not assigned!");
            return null;
        }
        
        Vector3 position = location.MapCell.Center;
        position.y += yOffset;
        
        GameObject locationObject = Instantiate(locationPrefab, position, Quaternion.identity, transform);
        locationObject.name = location.Name;

// Set sprite based on location type
        SpriteRenderer spriteRenderer = locationObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteManager != null)
        {
            Sprite sprite = spriteManager.GetRandomMapSprite(location.Type);
            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
            else
            {
                //Debug.LogWarning($"No map sprite found for location type: {location.Type}");
            }
        }

        // Cache the UI portrait on the location data (used by ProvinceUI + Title icons)
        if (spriteManager != null && location.Image == null)
        {
            location.Image = spriteManager.GetRandomUIPortrait(location.Type);
        }
        
        // Ensure LocationVisual component exists 
        LocationVisual locationVisual = locationObject.GetComponent<LocationVisual>();
        if (locationVisual == null)
        {
            Debug.Log("forgot to add locationvisual adding it manually");
            locationVisual = locationObject.AddComponent<LocationVisual>();
        }

        //set parent location for the locationvisual component
        locationVisual.location = location;

        locationObjects[location] = locationObject;
        
        return locationObject;
    }
    

    
    public void UpdateLocationPosition(Location location)
    {
        if (locationObjects.TryGetValue(location, out GameObject locationObject))
        {
            Vector3 position = location.MapCell.Center;
            position.y += yOffset;
            locationObject.transform.position = position;
        }
    }
    
    public void RemoveLocationVisual(Location location)
    {
        if (locationObjects.TryGetValue(location, out GameObject locationObject))
        {
            if (locationObject != null)
            {
                DestroyImmediate(locationObject);
            }
            locationObjects.Remove(location);
        }
    }
    
    public void ClearAllLocations()
    {
        foreach (var kvp in locationObjects)
        {
            if (kvp.Value != null)
            {
                DestroyImmediate(kvp.Value);
            }
        }
        locationObjects.Clear();
    }
    
    public GameObject GetLocationVisual(Location location)
    {
        return locationObjects.TryGetValue(location, out GameObject visual) ? visual : null;
    }
}