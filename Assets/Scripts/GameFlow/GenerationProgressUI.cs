using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Full-screen loading overlay shown while the world is being generated. Built
// procedurally and torn down when generation finishes.
public static class GenerationProgressUI
{
    private const float BarWidth = 720f;

    private static GameObject _root;
    private static TextMeshProUGUI _status;
    private static RectTransform _fill;

    public static void Show(string message)
    {
        if (_root != null)
        {
            Set(0f, message);
            return;
        }

        UIFactory.EnsureEventSystem();
        Canvas canvas = UIFactory.CreateCanvas("GenerationCanvas", 500);
        _root = canvas.gameObject;

        // Opaque backdrop that also swallows input while generating.
        Image backdrop = UIFactory.CreateImage(canvas.transform, "Backdrop", new Color(0.04f, 0.05f, 0.07f, 1f));
        UIFactory.Stretch(backdrop.rectTransform);

        TextMeshProUGUI title = UIFactory.CreateText(canvas.transform, "Title", "Forging the World",
            56f, TextAlignmentOptions.Center, UIFactory.TextColor);
        Center(title.rectTransform, new Vector2(0f, 130f), new Vector2(1200f, 80f));

        _status = UIFactory.CreateText(canvas.transform, "Status", message,
            26f, TextAlignmentOptions.Center, UIFactory.TextDimColor);
        Center(_status.rectTransform, new Vector2(0f, 30f), new Vector2(1200f, 40f));

        Image barBackground = UIFactory.CreateImage(canvas.transform, "BarBackground", UIFactory.PanelAltColor);
        Center(barBackground.rectTransform, new Vector2(0f, -60f), new Vector2(BarWidth, 26f));

        Image fill = UIFactory.CreateImage(barBackground.transform, "Fill", UIFactory.AccentColor);
        _fill = fill.rectTransform;
        _fill.anchorMin = new Vector2(0f, 0f);
        _fill.anchorMax = new Vector2(0f, 1f);
        _fill.pivot = new Vector2(0f, 0.5f);
        _fill.anchoredPosition = Vector2.zero;
        _fill.sizeDelta = new Vector2(0f, 0f);

        Set(0f, message);
    }

    public static void Set(float progress, string message)
    {
        if (_status != null && !string.IsNullOrEmpty(message))
            _status.text = message;

        if (_fill != null)
            _fill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(progress), 0f);
    }

    public static void Close()
    {
        if (_root != null)
            UnityEngine.Object.Destroy(_root);
        _root = null;
        _status = null;
        _fill = null;
    }

    private static void Center(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }
}
