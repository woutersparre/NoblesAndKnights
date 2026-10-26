using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Military screen (CK2-style). Lists moving armies, garrisons, and provides
/// levy-raising. Still placeholder-level for wars but uses real map data.
/// </summary>
public class MilitaryUI : MonoBehaviour
{
    public static MilitaryUI instance;

    [SerializeField] private Transform entriesList;

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

        CreateHeader("Military");
        if (!string.IsNullOrEmpty(lastStatus))
            CreateLabel("Last: " + lastStatus);

        // Moving armies
        CreateHeader("Moving Armies");
        MapPawn[] pawns = FindObjectsByType<MapPawn>(FindObjectsSortMode.None);
        int armyCount = 0;
        foreach (var pawn in pawns)
        {
            foreach (var army in pawn.Armies)
            {
                armyCount++;
                string dest = pawn.Destination != null ? pawn.Destination.Name : "?";
                CreateLabel($"- {army.Troops:F0} troops -> {dest} {(pawn.IsMoving ? "(marching)" : "(arrived)")}");
            }
            if (pawn.Characters.Count > 0 && pawn.Armies.Count == 0)
            {
                // character parties are not armies; skip
            }
        }
        if (armyCount == 0)
            CreateLabel("No armies currently on the move.");

        // Garrisons / owned locations
        CreateHeader("Garrisons");
        int locCount = 0;
        if (MapManager.instance != null && MapManager.instance.provincesDuJure != null)
        {
            foreach (var province in MapManager.instance.provincesDuJure)
            {
                if (province?.Locations == null) continue;
                foreach (var loc in province.Locations)
                {
                    if (loc == null || loc.Ruler != player) continue;
                    locCount++;
                    CreateLabel($"- {loc.Name}: Military {loc.MilitaryStrength:F0}, Defense {loc.DefensiveStrength:F0}, Levy {loc.AvailableLevy:F0}");
                }
            }
        }
        if (locCount == 0)
            CreateLabel("No holdings under your command.");

        // Actions
        CreateHeader("Actions");
        AddSmallButton(entriesList, "Raise All Levies", () =>
        {
            int raised = 0;
            if (MapManager.instance != null && MapManager.instance.provincesDuJure != null)
            {
                foreach (var province in MapManager.instance.provincesDuJure)
                {
                    if (province?.Locations == null) continue;
                    foreach (var loc in province.Locations)
                    {
                        if (loc != null && loc.Ruler == player && loc.AvailableLevy >= 1)
                        {
                            loc.RaiseLocalLevy();
                            raised++;
                        }
                    }
                }
            }
            lastStatus = raised > 0 ? $"Raised levies from {raised} holding(s)." : "No levies available to raise.";
            Refresh();
        });
        AddSmallButton(entriesList, "Disband All", () => { lastStatus = "Disband all (placeholder)."; Refresh(); });

        // Wars
        CreateHeader("Wars");
        CreateLabel("No active wars (placeholder).");
        AddSmallButton(entriesList, "Declare War (placeholder)", () => { lastStatus = "Declare war (placeholder)."; Refresh(); });

        Button closeBtn = UIFactory.CreateButton(entriesList, "Close", CloseButton, 20f);
        RectTransform closeRect = (RectTransform)closeBtn.transform;
        closeRect.sizeDelta = new Vector2(200f, 40f);
        LayoutElement closeLe = closeBtn.gameObject.AddComponent<LayoutElement>();
        closeLe.minHeight = 40f;
        closeLe.preferredHeight = 40f;
    }

    private void AddSmallButton(Transform parent, string label, Action onClick)
    {
        Button btn = UIFactory.CreateButton(parent, label, onClick, 16f);
        RectTransform rt = (RectTransform)btn.transform;
        rt.sizeDelta = new Vector2(150f, 26f);
        LayoutElement le = btn.gameObject.GetComponent<LayoutElement>();
        if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
        le.minWidth = 150f;
        le.preferredWidth = 150f;
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