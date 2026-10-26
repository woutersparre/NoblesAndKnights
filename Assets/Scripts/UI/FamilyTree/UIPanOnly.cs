using UnityEngine;
using UnityEngine.EventSystems;

// Drag-to-pan for the family tree viewport. There is no zoom: the tree is always
// shown at 100%, and the user slides the Content rect underneath a masked
// viewport. Sitting on the viewport (rather than the window root) means a drag
// that starts over the background is captured here, while dragging from the
// window header still moves the whole window through DragWindow.
public class UIPanOnly : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public RectTransform content;
    public RectTransform viewport;

    // How much of the tree must stay visible at the edges while panning, so the
    // content can never be dragged completely off screen.
    public float keepVisible = 80f;

    private Vector2 startContentPosition;
    private Vector2 startPointer;
    private bool dragging;

    public bool IsDragging => dragging;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (content == null) return;

        // Suppress the portrait's click: if a pan starts on a portrait we must not
        // also open that character's screen when the button is released.
        eventData.eligibleForClick = false;

        dragging = true;
        startContentPosition = content.anchoredPosition;
        startPointer = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (content == null || !dragging) return;

        Vector2 delta = (eventData.position - startPointer) / CanvasScale();
        content.anchoredPosition = Clamp(startContentPosition + delta);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
    }

    private float CanvasScale()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
    }

    private Vector2 Clamp(Vector2 position)
    {
        if (viewport == null || content == null) return position;

        Vector2 viewSize = viewport.rect.size;
        Vector2 contentSize = content.rect.size;

        // Layout may not have run yet on the very first frame; skip clamping then.
        if (viewSize.x < 1f || viewSize.y < 1f) return position;

        float limitX = Mathf.Max(0f, (contentSize.x + viewSize.x) * 0.5f - keepVisible);
        float limitY = Mathf.Max(0f, (contentSize.y + viewSize.y) * 0.5f - keepVisible);

        position.x = Mathf.Clamp(position.x, -limitX, limitX);
        position.y = Mathf.Clamp(position.y, -limitY, limitY);
        return position;
    }
}
