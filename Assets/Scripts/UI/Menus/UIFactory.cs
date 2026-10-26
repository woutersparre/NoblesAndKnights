using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Runtime helpers for building the menus procedurally. The main menu, world
// generation screen and character select overlay are constructed in code rather
// than authored as prefabs, so they work without any editor-authored assets.
// Unity's own DefaultControls factories are used underneath, which keeps the
// underlying control hierarchies (slider, toggle, scroll view) correct.
public static class UIFactory
{
    public static readonly Color BackdropColor   = new Color(0.05f, 0.06f, 0.08f, 1f);
    public static readonly Color PanelColor      = new Color(0.09f, 0.10f, 0.13f, 0.98f);
    public static readonly Color PanelAltColor   = new Color(0.13f, 0.14f, 0.18f, 1f);
    public static readonly Color AccentColor     = new Color(0.78f, 0.63f, 0.32f, 1f);
    public static readonly Color TextColor       = new Color(0.93f, 0.91f, 0.86f, 1f);
    public static readonly Color TextDimColor    = new Color(0.66f, 0.66f, 0.68f, 1f);
    public static readonly Color ButtonColor     = new Color(0.18f, 0.20f, 0.25f, 1f);
    public static readonly Color ButtonHoverColor= new Color(0.27f, 0.30f, 0.37f, 1f);
    public static readonly Color ButtonDownColor = new Color(0.12f, 0.13f, 0.16f, 1f);
    public static readonly Color SelectedRowColor= new Color(0.35f, 0.29f, 0.15f, 1f);
    public static readonly Color RowColor        = new Color(0.14f, 0.15f, 0.19f, 1f);
    public static readonly Color RowAltColor     = new Color(0.11f, 0.12f, 0.15f, 1f);

    private static readonly DefaultControls.Resources Resources = new DefaultControls.Resources();
    private static readonly TMP_DefaultControls.Resources TmpResources = new TMP_DefaultControls.Resources();

    public static Canvas CreateCanvas(string name, int sortOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    // Guarantees a usable EventSystem. The project's UI is driven by the new
    // Input System (activeInputHandler = 1), so the pointer module must be the
    // Input System one rather than the legacy StandaloneInputModule.
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var module = go.GetComponent<InputSystemUIInputModule>();
        try
        {
            module.AssignDefaultActions();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[UIFactory] Could not assign default UI input actions: " + e.Message);
        }
    }

    // The world scene supplies its own camera; the menu scene does not, so build a
    // plain one so the game view has something to render behind the overlay canvas.
    public static Camera CreateCamera(Color background)
    {
        var go = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.orthographic = true;
        cam.cullingMask = -1; // render the default layer (empty scene -> solid colour backdrop)
        TryAddUrpCameraData(go);
        return cam;
    }

    private static void TryAddUrpCameraData(GameObject cameraGo)
    {
        try
        {
            Type t = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (t != null && cameraGo.GetComponent(t) == null)
                cameraGo.AddComponent(t);
        }
        catch
        {
            // URP not installed or already handled: a plain camera is fine for a UI-only scene.
        }
    }

    public static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static void Stretch(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    public static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    public static TextMeshProUGUI CreateText(Transform parent, string name, string text,
        float fontSize, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    public static Button CreateButton(Transform parent, string label, Action onClick, float fontSize = 26f)
    {
        var go = DefaultControls.CreateButton(Resources);
        go.name = "Button";
        go.transform.SetParent(parent, false);
        RemoveLegacyText(go);

        var button = go.GetComponent<Button>();
        StyleSelectable(button);

        var text = CreateText(go.transform, "Label", label, fontSize, TextAlignmentOptions.Center, TextColor);
        Stretch(text.rectTransform, 6f);

        if (onClick != null)
            button.onClick.AddListener(() => onClick());
        return button;
    }

    public static Slider CreateSlider(Transform parent, float min, float max, float value,
        bool wholeNumbers, Action<float> onChange)
    {
        var go = DefaultControls.CreateSlider(Resources);
        go.name = "Slider";
        go.transform.SetParent(parent, false);

        var slider = go.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.value = value;

        var background = go.transform.Find("Background")?.GetComponent<Image>();
        if (background != null) { background.sprite = null; background.type = Image.Type.Simple; background.color = PanelAltColor; }

        var fill = go.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
        if (fill != null) { fill.sprite = null; fill.type = Image.Type.Simple; fill.color = AccentColor; }

        var handle = go.transform.Find("Handle Slide Area/Handle")?.GetComponent<Image>();
        if (handle != null) { handle.sprite = null; handle.type = Image.Type.Simple; handle.color = TextColor; }

        if (onChange != null)
            slider.onValueChanged.AddListener(v => onChange(v));
        return slider;
    }

    public static TMP_Dropdown CreateDropdown(Transform parent, string[] options, int value,
        Action<int> onChange)
    {
        var go = TMP_DefaultControls.CreateDropdown(TmpResources);
        go.name = "Dropdown";
        go.transform.SetParent(parent, false);

        var dropdown = go.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        dropdown.value = value;
        dropdown.RefreshShownValue();

        StyleDropdown(dropdown);
        return dropdown;
    }

    // Recolours the default dropdown hierarchy to match the dark panels. The
    // template (list popup) is a child of the dropdown and is hidden until
    // clicked, so it has to be styled up front too.
    private static void StyleDropdown(TMP_Dropdown dropdown)
    {
        var background = dropdown.GetComponent<Image>();
        if (background != null) { background.sprite = null; background.type = Image.Type.Simple; background.color = PanelAltColor; }

        StyleSelectable(dropdown);
        var colors = dropdown.colors;
        colors.normalColor = PanelAltColor;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonDownColor;
        colors.selectedColor = ButtonHoverColor;
        dropdown.colors = colors;

        if (dropdown.captionText != null)
        {
            dropdown.captionText.color = TextColor;
            dropdown.captionText.fontSize = 18f;
        }

        if (dropdown.itemText != null)
        {
            dropdown.itemText.color = TextColor;
            dropdown.itemText.fontSize = 18f;
        }

        Transform template = dropdown.template;
        if (template == null) return;

        var templateImage = template.GetComponent<Image>();
        if (templateImage != null) { templateImage.sprite = null; templateImage.type = Image.Type.Simple; templateImage.color = PanelColor; }

        Transform itemBackground = template.Find("Viewport/Content/Item/Item Background");
        if (itemBackground != null)
        {
            var itemImage = itemBackground.GetComponent<Image>();
            if (itemImage != null) { itemImage.sprite = null; itemImage.type = Image.Type.Simple; itemImage.color = RowColor; }
        }

        Transform itemCheckmark = template.Find("Viewport/Content/Item/Item Checkmark");
        if (itemCheckmark != null)
        {
            var checkImage = itemCheckmark.GetComponent<Image>();
            if (checkImage != null) { checkImage.sprite = null; checkImage.type = Image.Type.Simple; checkImage.color = AccentColor; }
        }
    }

    public static Toggle CreateToggle(Transform parent, bool value, Action<bool> onChange)
    {
        var go = DefaultControls.CreateToggle(Resources);
        go.name = "Toggle";
        go.transform.SetParent(parent, false);
        RemoveLegacyText(go);

        var toggle = go.GetComponent<Toggle>();
        toggle.isOn = value;

        var background = go.transform.Find("Background") as RectTransform;
        if (background != null)
        {
            background.anchorMin = background.anchorMax = new Vector2(0.5f, 0.5f);
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(32f, 32f);

            var bgImage = background.GetComponent<Image>();
            if (bgImage != null) { bgImage.sprite = null; bgImage.type = Image.Type.Simple; bgImage.color = PanelAltColor; }
        }

        var checkmark = go.transform.Find("Background/Checkmark") as RectTransform;
        if (checkmark != null)
        {
            checkmark.anchorMin = checkmark.anchorMax = new Vector2(0.5f, 0.5f);
            checkmark.pivot = new Vector2(0.5f, 0.5f);
            checkmark.anchoredPosition = Vector2.zero;
            checkmark.sizeDelta = new Vector2(20f, 20f);

            var checkImage = checkmark.GetComponent<Image>();
            if (checkImage != null) { checkImage.sprite = null; checkImage.type = Image.Type.Simple; checkImage.color = AccentColor; }
        }

        if (onChange != null)
            toggle.onValueChanged.AddListener(v => onChange(v));
        return toggle;
    }

    public static ScrollRect CreateScrollView(Transform parent, out RectTransform content)
    {
        var go = DefaultControls.CreateScrollView(Resources);
        go.name = "ScrollView";
        go.transform.SetParent(parent, false);

        var scroll = go.GetComponent<ScrollRect>();

        // Drop the scrollbars; the list is scrolled with the wheel/drag instead.
        var hbar = scroll.horizontalScrollbar;
        var vbar = scroll.verticalScrollbar;
        scroll.horizontalScrollbar = null;
        scroll.verticalScrollbar = null;
        if (hbar != null) UnityEngine.Object.Destroy(hbar.gameObject);
        if (vbar != null) UnityEngine.Object.Destroy(vbar.gameObject);

        var viewport = scroll.viewport;
        if (viewport != null)
        {
            var mask = viewport.GetComponent<Mask>();
            if (mask != null) UnityEngine.Object.Destroy(mask);

            var image = viewport.GetComponent<Image>();
            if (image != null) { image.color = new Color(0f, 0f, 0f, 0f); image.raycastTarget = false; }

            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();
        }

        var rootImage = go.GetComponent<Image>();
        if (rootImage != null) rootImage.color = new Color(0f, 0f, 0f, 0f);

        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        content = scroll.content;
        if (content != null)
        {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
        }
        return scroll;
    }

    private static void RemoveLegacyText(GameObject root)
    {
        var legacy = root.GetComponentInChildren<Text>(true);
        if (legacy != null && legacy.gameObject != root)
            UnityEngine.Object.Destroy(legacy.gameObject);
    }

    private static void StyleSelectable(Selectable selectable)
    {
        selectable.transition = Selectable.Transition.ColorTint;
        var colors = selectable.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonDownColor;
        colors.selectedColor = ButtonHoverColor;
        colors.disabledColor = new Color(0.10f, 0.10f, 0.12f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        selectable.colors = colors;
    }
}
