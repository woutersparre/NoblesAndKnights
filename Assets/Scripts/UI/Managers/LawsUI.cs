using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Laws screen (CK2-style). Interactive placeholder: each law can be cycled
/// through its options; changes are stored in-memory and reported as status.
/// </summary>
public class LawsUI : MonoBehaviour
{
    public static LawsUI instance;

    [SerializeField] private Transform entriesList;

    private class LawDef
    {
        public string Name;
        public string Group;
        public string[] Options;
        public int Current;
    }

    private readonly List<LawDef> laws = new List<LawDef>
    {
        new LawDef { Name = "Crown Authority", Group = "Crown", Options = new[] { "Autonomous Vassals", "Low", "Medium", "High", "Absolute" }, Current = 2 },
        new LawDef { Name = "Investiture", Group = "Crown", Options = new[] { "Free Investiture", "Papal Investiture" }, Current = 0 },
        new LawDef { Name = "Vice Royalty", Group = "Crown", Options = new[] { "Disabled", "Enabled" }, Current = 0 },
        new LawDef { Name = "Centralization", Group = "Crown", Options = new[] { "Decentralized", "Centralized" }, Current = 0 },
        new LawDef { Name = "Feudal Tax", Group = "Realm", Options = new[] { "None", "Low", "Normal", "High", "Extortionate" }, Current = 2 },
        new LawDef { Name = "City Tax", Group = "Realm", Options = new[] { "None", "Low", "Normal", "High", "Extortionate" }, Current = 2 },
        new LawDef { Name = "Temple Tax", Group = "Realm", Options = new[] { "None", "Low", "Normal", "High", "Extortionate" }, Current = 2 },
        new LawDef { Name = "Levy Size", Group = "Realm", Options = new[] { "None", "Small", "Normal", "Large", "Obligatory Service" }, Current = 2 },
        new LawDef { Name = "Succession Type", Group = "Succession", Options = new[] { "Gavelkind", "Elective", "Primogeniture", "Ultimogeniture" }, Current = 2 },
        new LawDef { Name = "Gender Law", Group = "Succession", Options = new[] { "Agnatic", "Agnatic-Cognatic", "Absolute Cognatic", "Enatic" }, Current = 1 },
    };

    private string lastStatus = "";

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (entriesList == null)
            entriesList = transform.Find("Body/List");

        HideWindow();
    }

    public void HideWindow()
    {
        gameObject.SetActive(false);
    }

    public void Setup()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void CloseButton()
    {
        HideWindow();
    }

    private static Character Player
    {
        get
        {
            if (MapManager.instance == null || MapManager.instance.characters == null)
                return null;
            return MapManager.instance.characters.Player;
        }
    }

    private void Refresh()
    {
        if (entriesList == null) return;

        foreach (Transform child in entriesList)
            Destroy(child.gameObject);

        Character player = Player;
        if (player == null)
        {
            CreateLabel("No player character.");
            return;
        }

        CreateHeader("Laws");
        if (!string.IsNullOrEmpty(lastStatus))
            CreateLabel("Last: " + lastStatus);

        string currentGroup = null;
        foreach (var law in laws)
        {
            if (law.Group != currentGroup)
            {
                currentGroup = law.Group;
                CreateHeader(currentGroup + " Laws");
            }

            GameObject row = new GameObject("LawRow", typeof(RectTransform));
            row.transform.SetParent(entriesList, false);
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.sizeDelta = new Vector2(0f, 40f);
            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 40f;
            rowLe.preferredHeight = 40f;
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childForceExpandWidth = false;

            TextMeshProUGUI rowLabel = UIFactory.CreateText(row.transform, "Label", $"{law.Name}: {law.Options[law.Current]}", 18f, TextAlignmentOptions.Left, UIFactory.TextColor);
            RectTransform labelRect = rowLabel.rectTransform;
            labelRect.sizeDelta = new Vector2(260f, 28f);
            LayoutElement labelLe = rowLabel.gameObject.AddComponent<LayoutElement>();
            labelLe.minWidth = 260f;
            labelLe.preferredWidth = 260f;
            labelLe.minHeight = 28f;
            labelLe.preferredHeight = 28f;

            LawDef captured = law;
            TextMeshProUGUI capturedLabel = rowLabel;
            Slider slider = UIFactory.CreateSlider(row.transform, 0f, law.Options.Length - 1f, law.Current, true, (v) =>
            {
                int idx = Mathf.RoundToInt(v);
                if (idx < 0 || idx >= captured.Options.Length) return;
                if (captured.Current == idx) return;
                captured.Current = idx;
                capturedLabel.text = $"{captured.Name}: {captured.Options[captured.Current]}";
                lastStatus = $"{captured.Name} changed to {captured.Options[captured.Current]}.";
            });
            RectTransform sliderRect = (RectTransform)slider.transform;
            sliderRect.sizeDelta = new Vector2(220f, 24f);
            LayoutElement sliderLe = slider.gameObject.GetComponent<LayoutElement>();
            if (sliderLe == null) sliderLe = slider.gameObject.AddComponent<LayoutElement>();
            sliderLe.minWidth = 220f;
            sliderLe.preferredWidth = 220f;
            sliderLe.minHeight = 24f;
            sliderLe.preferredHeight = 24f;
        }

        Button closeBtn = UIFactory.CreateButton(entriesList, "Close", CloseButton, 20f);
        RectTransform closeRect = (RectTransform)closeBtn.transform;
        closeRect.sizeDelta = new Vector2(200f, 40f);
        LayoutElement closeLe = closeBtn.gameObject.AddComponent<LayoutElement>();
        closeLe.minHeight = 40f;
        closeLe.preferredHeight = 40f;
    }

    private void CreateRowLabel(Transform parent, string text, float width)
    {
        TextMeshProUGUI label = UIFactory.CreateText(parent, "Label", text, 18f, TextAlignmentOptions.Left, UIFactory.TextColor);
        RectTransform rect = label.rectTransform;
        rect.sizeDelta = new Vector2(width, 28f);
        LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
        le.minWidth = width;
        le.preferredWidth = width;
        le.minHeight = 28f;
        le.preferredHeight = 28f;
    }

    private void AddSmallButton(Transform parent, string label, Action onClick)
    {
        Button btn = UIFactory.CreateButton(parent, label, onClick, 16f);
        RectTransform rt = (RectTransform)btn.transform;
        rt.sizeDelta = new Vector2(90f, 26f);
        LayoutElement le = btn.gameObject.GetComponent<LayoutElement>();
        if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
        le.minWidth = 90f;
        le.preferredWidth = 90f;
        le.minHeight = 26f;
        le.preferredHeight = 26f;
    }

    private void CreateHeader(string text)
    {
        TextMeshProUGUI label = UIFactory.CreateText(entriesList, "Header", text, 22f, TextAlignmentOptions.Left, UIFactory.AccentColor);
        RectTransform rect = label.rectTransform;
        rect.sizeDelta = new Vector2(0f, 32f);
        LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 32f;
        le.preferredHeight = 32f;
    }

    private void CreateLabel(string text)
    {
        TextMeshProUGUI label = UIFactory.CreateText(entriesList, "Label", text, 18f, TextAlignmentOptions.Left, UIFactory.TextColor);
        RectTransform rect = label.rectTransform;
        rect.sizeDelta = new Vector2(0f, 24f);
        LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 24f;
        le.preferredHeight = 24f;
    }
}