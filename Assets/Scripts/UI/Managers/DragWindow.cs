using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DragWindow : MonoBehaviour, IDragHandler, IPointerDownHandler {

    // Windows-style dragging: the window body may slide off-screen, but the
    // close button always stays fully visible so it can never become
    // unclickable. If no close button is found we fall back to keeping a
    // sliver of the drag handle visible.
    private const float FallbackMinHandleVisible = 20f;

    [SerializeField] private RectTransform dragRectTransform;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform closeButton; // optional; auto-detected when null

    private void Awake() {
        if (dragRectTransform == null) {
            dragRectTransform = transform.parent.GetComponent<RectTransform>();
        }

        if (canvas == null) {
            Transform testCanvasTransform = transform.parent;
            while (testCanvasTransform != null) {
                canvas = testCanvasTransform.GetComponent<Canvas>();
                if (canvas != null) {
                    break;
                }
                testCanvasTransform = testCanvasTransform.parent;
            }
        }

        if (closeButton == null)
            closeButton = FindCloseButton();
    }

    public void OnDrag(PointerEventData eventData) {
        // Only the left mouse button may drag the window. The UI input module runs
        // drag processing for the right and middle buttons too, so without this check
        // a middle-click on the drag handle would slide the window around.
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        dragRectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        ClampCloseButtonToViewport();
    }

    public void OnPointerDown(PointerEventData eventData) {
        dragRectTransform.SetAsLastSibling();
        Window window = transform.parent != null ? transform.parent.GetComponentInParent<Window>() : null;
        window?.BringToFront();
    }

    // Finds the close button under the window: primarily a button conventionally
    // named close / X (as used by the game's screens), with a fallback to a
    // button whose inspector-wired onClick method looks like a close handler.
    private RectTransform FindCloseButton()
    {
        if (dragRectTransform == null) return null;
        foreach (Button button in dragRectTransform.GetComponentsInChildren<Button>(true))
        {
            if (IsCloseButton(button))
                return button.transform as RectTransform;
        }
        return null;
    }

    private static bool IsCloseButton(Button button)
    {
        string name = button.gameObject.name;

        // 1. Conventional naming — this is how the game's close buttons are named
        if (name.IndexOf("close", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name == "X" || name == "x")
            return true;

        // 2. Inspector-wired onClick whose method looks like a close handler
        //    (e.g. CloseWindow / CloseButton). Deliberately checks the METHOD
        //    name and not just "target is a Window", so buttons wired to other
        //    methods on the Window subclass (Spouse/Liege, tab buttons, ...)
        //    are not mistaken for the close button.
        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            string method = button.onClick.GetPersistentMethodName(i);
            if (!string.IsNullOrEmpty(method)
                && method.IndexOf("close", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    // Keeps the close button fully inside the viewport so it is always
    // clickable, while the rest of the window may slide partially off-screen.
    private void ClampCloseButtonToViewport()
    {
        if (dragRectTransform == null) return;

        // Clamp against the canvas root: that is the visible screen area for
        // both Screen-Space-Overlay and Screen-Space-Camera canvases.
        RectTransform viewport = canvas != null
            ? canvas.GetComponent<RectTransform>()
            : dragRectTransform.parent as RectTransform;
        if (viewport == null) return;

        if (closeButton == null)
        {
            ClampHandleSliver(viewport);
            return;
        }

        Vector3[] viewportCorners = new Vector3[4];
        Vector3[] buttonCorners = new Vector3[4];
        viewport.GetWorldCorners(viewportCorners);
        closeButton.GetWorldCorners(buttonCorners);

        // World-space bounding boxes (works for Overlay and Camera canvases)
        float vpLeft   = viewportCorners[0].x, vpBottom = viewportCorners[0].y;
        float vpRight  = viewportCorners[2].x, vpTop    = viewportCorners[2].y;
        float btnLeft  = buttonCorners[0].x,  btnBottom = buttonCorners[0].y;
        float btnRight = buttonCorners[2].x,  btnTop    = buttonCorners[2].y;

        float btnWidth  = btnRight - btnLeft;
        float btnHeight = btnTop - btnBottom;
        float vpWidth   = vpRight - vpLeft;
        float vpHeight  = vpTop - vpBottom;

        Vector3 correction = Vector3.zero;

        if (btnWidth <= vpWidth)
        {
            if (btnLeft < vpLeft)        correction.x += vpLeft - btnLeft;
            else if (btnRight > vpRight) correction.x -= btnRight - vpRight;
        }
        if (btnHeight <= vpHeight)
        {
            if (btnBottom < vpBottom)    correction.y += vpBottom - btnBottom;
            else if (btnTop > vpTop)     correction.y -= btnTop - vpTop;
        }

        if (correction != Vector3.zero)
            dragRectTransform.position += correction;
    }

    // Fallback when no close button could be found: keep at least a small part
    // of the drag handle on-screen so the window can still be grabbed.
    private void ClampHandleSliver(RectTransform viewport)
    {
        RectTransform handle = transform as RectTransform;
        if (handle == null) return;

        Vector3[] viewportCorners = new Vector3[4];
        Vector3[] handleCorners = new Vector3[4];
        viewport.GetWorldCorners(viewportCorners);
        handle.GetWorldCorners(handleCorners);

        float vpLeft   = viewportCorners[0].x, vpBottom = viewportCorners[0].y;
        float vpRight  = viewportCorners[2].x, vpTop    = viewportCorners[2].y;
        float hLeft    = handleCorners[0].x,   hBottom  = handleCorners[0].y;
        float hRight   = handleCorners[2].x,   hTop     = handleCorners[2].y;

        float handleWidth  = hRight - hLeft;
        float handleHeight = hTop - hBottom;
        float vpWidth      = vpRight - vpLeft;
        float vpHeight     = vpTop - vpBottom;

        float minX = Mathf.Min(FallbackMinHandleVisible, handleWidth * 0.5f);
        float minY = Mathf.Min(FallbackMinHandleVisible, handleHeight * 0.5f);

        Vector3 correction = Vector3.zero;

        if (handleWidth <= vpWidth)
        {
            if (hLeft < vpLeft + minX)        correction.x += (vpLeft + minX) - hLeft;
            else if (hRight > vpRight - minX) correction.x -= hRight - (vpRight - minX);
        }
        if (handleHeight <= vpHeight)
        {
            if (hBottom < vpBottom + minY)    correction.y += (vpBottom + minY) - hBottom;
            else if (hTop > vpTop - minY)     correction.y -= hTop - (vpTop - minY);
        }

        if (correction != Vector3.zero)
            dragRectTransform.position += correction;
    }
}