using UnityEngine;

public class LocationVisual : MonoBehaviour
{
    [Header("Location Data")]
    public Location location;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool highlightOnHover = true;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private float highlightScale = 1.2f;
    
    private Vector3 originalScale;
    private Color originalColor;
    private MaterialPropertyBlock propertyBlock;
    private Renderer[] renderers;
    
    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalScale = transform.localScale;
        
        propertyBlock = new MaterialPropertyBlock();
        if (renderers.Length > 0)
        {
            // Get original color from material, not property block
            originalColor = renderers[0].material.color;
        }
        
        // Subscribe to unified selection events
        UnifiedSelectionManager.OnLocationSelected += HandleLocationSelected;
        UnifiedSelectionManager.OnLocationHovered += HandleLocationHovered;
        UnifiedSelectionManager.OnLocationExited += HandleLocationExited;
    }
    
    private void HandleLocationSelected(Location selectedLocation)
    {
        if (selectedLocation == location)
        {
            GameUIManager.NewLocationUI(location);
        }
    }
    
    private void HandleLocationHovered(Location hoveredLocation)
    {
        if (hoveredLocation == location && highlightOnHover)
        {
            HighlightLocation(true);
        }
    }
    
    private void HandleLocationExited(Location exitedLocation)
    {
        if (exitedLocation == location && highlightOnHover)
        {
            HighlightLocation(false);
        }
    }
    
    private void HighlightLocation(bool highlight)
    {
        transform.localScale = highlight ? originalScale * highlightScale : originalScale;
        propertyBlock.SetColor("_Color", highlight ? highlightColor : originalColor);
        
        foreach (var renderer in renderers)
        {
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from unified selection events (always unsubscribe from static events)
        UnifiedSelectionManager.OnLocationSelected -= HandleLocationSelected;
        UnifiedSelectionManager.OnLocationHovered -= HandleLocationHovered;
        UnifiedSelectionManager.OnLocationExited -= HandleLocationExited;
    }
}