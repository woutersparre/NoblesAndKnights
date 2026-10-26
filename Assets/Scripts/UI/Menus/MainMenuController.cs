using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Entry point for the main menu scene. Builds the title screen and the world
// generation settings screen procedurally, then hands the chosen parameters to
// GameSession and loads the game scene.
public class MainMenuController : MonoBehaviour
{
    public const string MenuSceneName = "MainMenu";
    public const string GameSceneName = "NewMapTest";

    private WorldGenConfig _config;
    private Canvas _canvas;
    private GameObject _mainPanel;
    private GameObject _worldGenPanel;

    // Runs once when the runtime starts. Only builds the menu when the menu scene
    // is the active one, so playing the game scene directly still works.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != MenuSceneName)
            return;

        var go = new GameObject("MainMenuController");
        go.AddComponent<MainMenuController>();
    }

    private void Awake()
    {
        _config = WorldGenConfig.CreateDefault();

        UIFactory.CreateCamera(UIFactory.BackdropColor);
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("MainMenuCanvas", 100);

        BuildMainPanel();
        BuildWorldGenPanel();
        ShowMain();
    }

    // ---------------------------------------------------------------- panels

    private void ShowMain()
    {
        if (_mainPanel != null) _mainPanel.SetActive(true);
        if (_worldGenPanel != null) _worldGenPanel.SetActive(false);
    }

    private void ShowWorldGen()
    {
        if (_mainPanel != null) _mainPanel.SetActive(false);
        if (_worldGenPanel != null) _worldGenPanel.SetActive(true);
    }

    private void BuildMainPanel()
    {
        _mainPanel = new GameObject("MainPanel", typeof(RectTransform));
        var root = (RectTransform)_mainPanel.transform;
        root.SetParent(_canvas.transform, false);
        UIFactory.Stretch(root);

        Image background = UIFactory.CreateImage(root, "Background", UIFactory.BackdropColor);
        UIFactory.Stretch(background.rectTransform);

        TextMeshProUGUI title = UIFactory.CreateText(root, "Title", "Nobles & Knights",
            96f, TextAlignmentOptions.Center, UIFactory.AccentColor);
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -140f);
        titleRect.sizeDelta = new Vector2(1400f, 120f);

        TextMeshProUGUI subtitle = UIFactory.CreateText(root, "Subtitle",
            "Forge a world, then choose the ruler whose story you will tell.",
            28f, TextAlignmentOptions.Center, UIFactory.TextDimColor);
        var subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0.5f, 1f);
        subtitleRect.anchorMax = new Vector2(0.5f, 1f);
        subtitleRect.pivot = new Vector2(0.5f, 1f);
        subtitleRect.anchoredPosition = new Vector2(0f, -280f);
        subtitleRect.sizeDelta = new Vector2(1400f, 50f);

        RectTransform buttons = UIFactory.NewRect(root, "Buttons");
        buttons.anchorMin = buttons.anchorMax = new Vector2(0.5f, 0.5f);
        buttons.pivot = new Vector2(0.5f, 0.5f);
        buttons.anchoredPosition = new Vector2(0f, -40f);
        buttons.sizeDelta = new Vector2(460f, 248f);

        Button newGame = UIFactory.CreateButton(buttons, "New Game", ShowWorldGen, 30f);
        PlaceStacked((RectTransform)newGame.transform, 0);

        Button continueButton = UIFactory.CreateButton(buttons, "Continue (no saves yet)", null, 26f);
        continueButton.interactable = false;
        PlaceStacked((RectTransform)continueButton.transform, 1);

        Button quit = UIFactory.CreateButton(buttons, "Quit", QuitGame, 30f);
        PlaceStacked((RectTransform)quit.transform, 2);

        TextMeshProUGUI footer = UIFactory.CreateText(root, "Footer",
            "Early build — menus are generated at runtime.",
            20f, TextAlignmentOptions.Center, UIFactory.TextDimColor);
        var footerRect = footer.rectTransform;
        footerRect.anchorMin = new Vector2(0.5f, 0f);
        footerRect.anchorMax = new Vector2(0.5f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 30f);
        footerRect.sizeDelta = new Vector2(1400f, 40f);
    }

    private void BuildWorldGenPanel()
    {
        if (_worldGenPanel != null)
            Destroy(_worldGenPanel);

        _worldGenPanel = new GameObject("WorldGenPanel", typeof(RectTransform));
        var root = (RectTransform)_worldGenPanel.transform;
        root.SetParent(_canvas.transform, false);
        UIFactory.Stretch(root);

        Image background = UIFactory.CreateImage(root, "Background", UIFactory.BackdropColor);
        UIFactory.Stretch(background.rectTransform);

        TextMeshProUGUI title = UIFactory.CreateText(root, "Title", "Create World",
            64f, TextAlignmentOptions.Center, UIFactory.AccentColor);
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -30f);
        titleRect.sizeDelta = new Vector2(1200f, 80f);

        // Preset row
        RectTransform presets = UIFactory.NewRect(root, "Presets");
        presets.anchorMin = new Vector2(0.5f, 1f);
        presets.anchorMax = new Vector2(0.5f, 1f);
        presets.pivot = new Vector2(0.5f, 1f);
        presets.anchoredPosition = new Vector2(0f, -110f);
        presets.sizeDelta = new Vector2(1200f, 50f);

        string[] presetNames = { "Small", "Medium", "Large", "Archipelago", "Continent" };
        for (int i = 0; i < presetNames.Length; i++)
        {
            string preset = presetNames[i];
            Button presetButton = UIFactory.CreateButton(presets, preset, () => { ApplyPreset(preset); BuildWorldGenPanel(); ShowWorldGen(); }, 22f);
            var rt = (RectTransform)presetButton.transform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(i * 228f, 0f);
            rt.sizeDelta = new Vector2(210f, 48f);
        }

        // Parameter list
        ScrollRect scroll = UIFactory.CreateScrollView(root, out RectTransform content);
        var scrollRect = (RectTransform)scroll.transform;
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(180f, 120f);
        scrollRect.offsetMax = new Vector2(-180f, -180f);

        float cursor = 0f;
        AddToggleRow(content, ref cursor, "Generate island (surround land with ocean)", _config.GenerateIsland,
            v => _config.GenerateIsland = v);
        AddSliderRow(content, ref cursor, "Map size", 100f, 1200f, _config.MapSize, true, null,
            v => _config.MapSize = v);
        AddToggleRow(content, ref cursor, "Cells from circumcenters", _config.CellsFromCircumcenters,
            v => _config.CellsFromCircumcenters = v);

        AddSectionHeader(content, ref cursor, "Water level");
        AddSliderRow(content, ref cursor, "Sea level", 0f, 0.6f, _config.SeaLevel, false, "0.00", v => _config.SeaLevel = v);
        AddSliderRow(content, ref cursor, "Polar ice threshold", 0.5f, 1f, _config.PolarThreshold, false, "0.00", v => _config.PolarThreshold = v);

        AddSectionHeader(content, ref cursor, "Mountains");
        AddSliderRow(content, ref cursor, "Mountain threshold", 0.3f, 0.95f, _config.MountainThreshold, false, "0.00", v => _config.MountainThreshold = v);
        AddSliderRow(content, ref cursor, "Elevation scale (smaller = larger ranges)", 0.01f, 0.1f, _config.ElevationScale, false, "0.000", v => _config.ElevationScale = v);
        AddSliderRow(content, ref cursor, "Mountain coherence", 0.1f, 0.5f, _config.MountainCoherence, false, "0.00", v => _config.MountainCoherence = v);

        AddSectionHeader(content, ref cursor, "Climate");
        AddSliderRow(content, ref cursor, "Moisture scale", 0.01f, 0.3f, _config.MoistureScale, false, "0.000", v => _config.MoistureScale = v);
        AddSliderRow(content, ref cursor, "Moisture level", 0f, 1f, _config.MoistureLevel, false, "0.00", v => _config.MoistureLevel = v);
        AddSliderRow(content, ref cursor, "Temperature scale", 0.01f, 0.2f, _config.TemperatureScale, false, "0.000", v => _config.TemperatureScale = v);

        content.sizeDelta = new Vector2(0f, cursor + 20f);
        content.anchoredPosition = Vector2.zero;

        // Bottom bar
        Image bottomBar = UIFactory.CreateImage(root, "BottomBar", UIFactory.PanelColor);
        var bottomRect = bottomBar.rectTransform;
        bottomRect.anchorMin = new Vector2(0f, 0f);
        bottomRect.anchorMax = new Vector2(1f, 0f);
        bottomRect.pivot = new Vector2(0.5f, 0f);
        bottomRect.sizeDelta = new Vector2(0f, 96f);
        bottomRect.anchoredPosition = Vector2.zero;

        Button back = UIFactory.CreateButton(bottomBar.transform, "Back", ShowMain, 26f);
        var backRect = (RectTransform)back.transform;
        backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 0.5f);
        backRect.pivot = new Vector2(0f, 0.5f);
        backRect.anchoredPosition = new Vector2(40f, 0f);
        backRect.sizeDelta = new Vector2(220f, 60f);

        Button generate = UIFactory.CreateButton(bottomBar.transform, "Generate World", GenerateWorld, 30f);
        var generateRect = (RectTransform)generate.transform;
        generateRect.anchorMin = generateRect.anchorMax = new Vector2(1f, 0.5f);
        generateRect.pivot = new Vector2(1f, 0.5f);
        generateRect.anchoredPosition = new Vector2(-40f, 0f);
        generateRect.sizeDelta = new Vector2(360f, 60f);
    }

    // ------------------------------------------------------------------ rows

    private void AddToggleRow(RectTransform content, ref float cursor, string label, bool value, Action<bool> onChange)
    {
        const float height = 56f;

        RectTransform row = CreateRow(content, cursor, height);
        TextMeshProUGUI text = UIFactory.CreateText(row, "Label", label, 24f, TextAlignmentOptions.Left, UIFactory.TextColor);
        var textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0.5f);
        textRect.anchorMax = new Vector2(1f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(0f, -20f);
        textRect.offsetMax = new Vector2(-60f, 20f);

        Toggle toggle = UIFactory.CreateToggle(row, value, onChange);
        var toggleRect = (RectTransform)toggle.transform;
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(0f, 0f);
        toggleRect.sizeDelta = new Vector2(40f, 40f);

        cursor += height + 6f;
    }

    private void AddSliderRow(RectTransform content, ref float cursor, string label, float min, float max,
        float value, bool wholeNumbers, string format, Action<float> onChange)
    {
        const float height = 84f;

        RectTransform row = CreateRow(content, cursor, height);

        TextMeshProUGUI labelText = UIFactory.CreateText(row, "Label", label, 24f, TextAlignmentOptions.Left, UIFactory.TextColor);
        var labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.offsetMin = new Vector2(0f, -32f);
        labelRect.offsetMax = new Vector2(-140f, 0f);

        string Format(float v) => string.IsNullOrEmpty(format) ? Mathf.RoundToInt(v).ToString() : v.ToString(format);

        TextMeshProUGUI valueText = UIFactory.CreateText(row, "Value", Format(value), 24f, TextAlignmentOptions.Right, UIFactory.AccentColor);
        var valueRect = valueText.rectTransform;
        valueRect.anchorMin = new Vector2(1f, 1f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 1f);
        valueRect.anchoredPosition = new Vector2(0f, 0f);
        valueRect.sizeDelta = new Vector2(130f, 32f);

        Slider slider = UIFactory.CreateSlider(row, min, max, value, wholeNumbers, v =>
        {
            valueText.text = Format(v);
            onChange?.Invoke(v);
        });
        var sliderRect = (RectTransform)slider.transform;
        sliderRect.anchorMin = new Vector2(0f, 0f);
        sliderRect.anchorMax = new Vector2(1f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 10f);
        sliderRect.sizeDelta = new Vector2(0f, 30f);

        cursor += height + 6f;
    }

    private void AddSectionHeader(RectTransform content, ref float cursor, string title)
    {
        const float height = 46f;

        RectTransform row = CreateRow(content, cursor, height);
        TextMeshProUGUI text = UIFactory.CreateText(row, "Header", title, 26f, TextAlignmentOptions.Left, UIFactory.AccentColor);
        var textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0.5f);
        textRect.anchorMax = new Vector2(1f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(0f, -20f);
        textRect.offsetMax = new Vector2(0f, 20f);

        cursor += height + 10f;
    }

    private static RectTransform CreateRow(RectTransform content, float y, float height)
    {
        RectTransform row = UIFactory.NewRect(content, "Row");
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(0f, height);
        row.anchoredPosition = new Vector2(0f, -y);
        return row;
    }

    private static void PlaceStacked(RectTransform button, int index)
    {
        button.anchorMin = new Vector2(0f, 1f);
        button.anchorMax = new Vector2(1f, 1f);
        button.pivot = new Vector2(0.5f, 1f);
        button.sizeDelta = new Vector2(0f, 72f);
        button.anchoredPosition = new Vector2(0f, -index * 88f);
    }

    // -------------------------------------------------------------- actions

    private void ApplyPreset(string preset)
    {
        switch (preset)
        {
            case "Small":
                _config.MapSize = 200f;
                _config.GenerateIsland = false;
                break;
            case "Large":
                _config.MapSize = 450f;
                _config.GenerateIsland = false;
                break;
            case "Archipelago":
                _config.MapSize = 300f;
                _config.GenerateIsland = true;
                break;
            case "Continent":
                _config.MapSize = 360f;
                _config.GenerateIsland = false;
                break;
            default: // Medium
                _config.MapSize = 300f;
                _config.GenerateIsland = false;
                break;
        }
    }

    private void GenerateWorld()
    {
        GameSession.RequestWorld(_config);
        SceneManager.LoadScene(GameSceneName);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
