using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class InfoUI : MonoBehaviour
{
    public Canvas parentCanvas; // Reference to the parent Canvas
    public Vector2 offset; // Offset from the mouse position
    public static InfoUI instance;
    public TMP_Text HeaderText, BodyText;

    void Update()
    {
        if (Mouse.current == null || parentCanvas == null) return;

        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        RectTransform rect = transform as RectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                Mouse.current.position.ReadValue(),
                parentCanvas.worldCamera,
                out Vector2 mouse))
            return;

        // rect.size, not sizeDelta: the ContentSizeFitter writes sizeDelta, and the
        // canvas rect's sizeDelta is 0 for an overlay canvas, so the old bounds
        // check was comparing against a zero-size canvas and always flipped.
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 size = rect.rect.size;

        // mouse is relative to the canvas pivot; a centre-anchored child is
        // positioned relative to the canvas rect centre. Bridge the two.
        mouse.x += (0.5f - canvasRect.pivot.x) * canvasSize.x;
        mouse.y += (0.5f - canvasRect.pivot.y) * canvasSize.y;

        // Distance from the rect's pivot to its lower-left edge, so the tooltip's
        // corner sits at cursor + offset instead of straddling the cursor.
        Vector2 lowerLeft = new Vector2(rect.pivot.x * size.x, rect.pivot.y * size.y);
        Vector2 upperRight = new Vector2((1f - rect.pivot.x) * size.x, (1f - rect.pivot.y) * size.y);

        Vector2 pos = mouse + offset + lowerLeft;

        // Clamp rather than flip. Flipping re-decides every frame against a size that
        // the fitter updates after Update(), so the tooltip oscillated between the
        // two sides and flickered; clamping is monotonic and self-correcting.
        Vector2 min = -canvasSize * 0.5f;
        Vector2 max = canvasSize * 0.5f;

        if (pos.x < min.x + lowerLeft.x) pos.x = min.x + lowerLeft.x;
        if (pos.x > max.x - upperRight.x) pos.x = max.x - upperRight.x;
        if (pos.y < min.y + lowerLeft.y) pos.y = min.y + lowerLeft.y;
        if (pos.y > max.y - upperRight.y) pos.y = max.y - upperRight.y;

        rect.localPosition = pos;
    }

    public void Awake()
    {
        instance = this;
        gameObject.SetActive(false);
        parentCanvas = transform.parent.GetComponent<Canvas>();

        // Update() positions the tooltip against the canvas rect centre, which is
        // only true for a centre anchor. The prefab ships bottom-left anchored.
        RectTransform rect = transform as RectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);

        // The tooltip must never take the raycast. Its background and text are
        // raycast targets by default, so appearing under the cursor fires
        // OnPointerExit on the widget being hovered, which hides the tooltip,
        // which hands the raycast straight back -- flicker on repeat. Done in code
        // (not the prefab) so it holds for every instance of this panel.
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }
}
