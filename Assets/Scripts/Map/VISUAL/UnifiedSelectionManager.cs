using UnityEngine;
using System;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class UnifiedSelectionManager : MonoBehaviour
{
    public static UnifiedSelectionManager Instance { get; private set; }
    
    [Header("Map References")]
    public MapManager mapManager;
    
    [Header("Input Actions")]
    public InputActionReference SelectAction;
    
    [Header("Border Highlighting")]
    public Color highlightColor = Color.yellow;
    [Tooltip("Width of highlighted province borders")]
    [Range(0.01f, 1.0f)]
    public float highlightBorderWidth = 0.15f;
    [Tooltip("Width of regular (non-highlighted) region borders")]
    [Range(0.01f, 1.0f)]
    public float regularBorderWidth = 0.05f;
    
    // Selection Events
    public static event Action<Province> OnProvinceSelected;
    public static event Action<Province> OnProvinceUnselected;
    public static event Action<Location> OnLocationSelected;
    public static event Action<Location> OnLocationHovered;
    public static event Action<Location> OnLocationExited;
    
    private Location currentHoveredLocation;
    private Camera mainCamera;

    // One-shot map click routing used by feature modes (e.g. deploying a councilor).
    // When set, the next click anywhere on the map is reported to the callback with the
    // clicked Location (null if empty land was clicked) and the request is cleared.
    private static System.Action<Location> locationClickRequest;

    public static void RequestLocationClick(System.Action<Location> callback)
    {
        locationClickRequest = callback;
    }

    public static void CancelLocationClick()
    {
        locationClickRequest = null;
    }
    
    // Border highlighting state
    private Province highlightedProvince;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("UnifiedSelectionManager: No main camera found!");
        }
        
        // Initialize border highlighting system
        RegionBorderDrawer.InitializeHighlighting(highlightColor, Color.clear /* unused */, highlightBorderWidth, regularBorderWidth);
        
        // Subscribe to province selection events for highlighting
        OnProvinceSelected += HandleProvinceSelection;
    }
    
    private void Update()
    {
        if (!SelectAction.action.WasPressedThisFrame())
            return;
            
        // Ignore clicks over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;
            
        HandleSelection();
    }
    
    private void LateUpdate()
    {
        HandleHover();
    }
    
    private void HandleSelection()
    {
        Vector3 worldPosition = GetMouseWorldPosition();

        // Feature-mode routing: consume one click and hand the target to the requester.
        if (locationClickRequest != null)
        {
            Location target = GetLocationAtPosition();
            var callback = locationClickRequest;
            locationClickRequest = null;
            callback?.Invoke(target);
            return;
        }

        // First check for location selection (raycast)
        Location selectedLocation = GetLocationAtPosition();
        if (selectedLocation != null)
        {
            OnLocationSelected?.Invoke(selectedLocation);
            return;
        }
        
        // Then check for province selection (polygon)
        Province selectedProvince = mapManager?.GetProvinceAt(worldPosition);
        if (selectedProvince != null)
        {
            //Debug.Log($"Province selected: {selectedProvince.Name} (Capital: {selectedProvince.Capital?.Name ?? "NULL"})");
            OnProvinceSelected?.Invoke(selectedProvince);
            mapManager.MapModes?.HandleProvinceClick(selectedProvince);
        }
        else
        {
            // Clicked on empty land - unselect current province
            OnProvinceSelected?.Invoke(null);
        }
    }
    
    private void HandleHover()
    {
        Location hoveredLocation = GetLocationAtPosition();
        
        if (!ReferenceEquals(hoveredLocation, currentHoveredLocation))
        {
            // Exit previous location
            if (currentHoveredLocation != null)
            {
                OnLocationExited?.Invoke(currentHoveredLocation);
            }
            
            // Enter new location
            currentHoveredLocation = hoveredLocation;
            if (currentHoveredLocation != null)
            {
                OnLocationHovered?.Invoke(currentHoveredLocation);
            }
        }
    }
    
    private Location GetLocationAtPosition()
    {
        if (mainCamera == null) return null;
        
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector2 worldPosition = mainCamera.ScreenToWorldPoint(mousePosition);
        
        RaycastHit2D[] hits = Physics2D.RaycastAll(worldPosition, Vector2.zero);
        
        foreach (RaycastHit2D hit in hits)
        {
            LocationVisual locationVisual = hit.collider.GetComponent<LocationVisual>();
            if (locationVisual != null)
            {
                return locationVisual.location;
            }
        }
        
        return null;
    }
    
    private Vector3 GetMouseWorldPosition()
    {
        if (mainCamera == null) return Vector3.zero;
        
        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = -mainCamera.transform.position.z;
        
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenPos);
        worldPos.z = 0;
        return worldPos;
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        
        // Unsubscribe from events
        OnProvinceSelected -= HandleProvinceSelection;
        
        // Clear highlights without creating new GameObjects
        RegionBorderDrawer.ClearHighlights();
    }
    
    private void HandleProvinceSelection(Province province)
    {
        // Clear previous highlights
        if (highlightedProvince != null)
        {
            RegionBorderDrawer.ClearProvinceHighlight(highlightedProvince);
        }
        
        // Set new highlight
        highlightedProvince = province;
        if (province != null)
        {
            RegionBorderDrawer.HighlightProvince(province);
        }
        else
        {
            // Province unselected
            OnProvinceUnselected?.Invoke(null);
        }
    }

    // Public method to clear selection state (called when switching mapmodes)
    public void ClearSelection()
    {
        // Clear visual highlights
        if (highlightedProvince != null)
        {
            RegionBorderDrawer.ClearProvinceHighlight(highlightedProvince);
            highlightedProvince = null;
        }
        
        // Trigger unselection event
        OnProvinceUnselected?.Invoke(null);
    }
}