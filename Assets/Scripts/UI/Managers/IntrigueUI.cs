using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Intrigue screen (CK2-style). Sections: Status, My Plots, Suggested Plots,
/// Known Plots, Leverage, Defence. Plot lifecycle is still placeholder-level,
/// but the screen is interactive and mirrors the CK2 layout.
/// </summary>
public class IntrigueUI : MonoBehaviour
{
    public static IntrigueUI instance;

    [SerializeField] private Transform entriesList;

    // In-memory placeholder plot data (persists while the game runs).
    private class Plot
    {
        public string Type;
        public string Target;
        public int Power;
        public int Progress;
        public int Required;
    }

    private readonly List<Plot> myPlots = new List<Plot>();
    private readonly List<string> knownPlots = new List<string>();
    private readonly List<string> leverage = new List<string>();

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

    // Starts a plot targeting a specific character (used by the right-click
    // character action menu). Safe to call while the window is inactive.
    public void StartPlot(string type, Character target)
    {
        if (target == null || string.IsNullOrEmpty(type))
            return;

        int days = 21;
        if (type.Contains("Spy")) days = 7;
        else if (type.Contains("Seduce")) days = 28;
        else if (type.Contains("Poison") || type.Contains("Abduct")) days = 10;
        else if (type.Contains("Forge")) days = 45;

        myPlots.Add(new Plot { Type = type, Target = target.Name, Power = 0, Progress = 0, Required = days });
        lastStatus = $"Started plot: {type} against {target.Name}";

        if (gameObject.activeInHierarchy)
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

        // Status
        CreateHeader("Intrigue");
        if (!string.IsNullOrEmpty(lastStatus))
            CreateLabel("Last: " + lastStatus);
        CreateLabel($"Intrigue skill: {player.Stats.Intrigue}");
        CreateLabel($"Suspicion: 0 (placeholder)");
        CreateLabel($"Leverage held: {leverage.Count}");

        // My Plots
        CreateHeader("My Plots");
        if (myPlots.Count == 0)
        {
            CreateLabel("No active plots. Start one from Suggested Plots.");
        }
        else
        {
            foreach (var plot in new List<Plot>(myPlots))
            {
                CreateLabel($"{plot.Type}  ->  {plot.Target}");
                CreateLabel($"   Plot Power: {plot.Power}%   Progress: {plot.Progress}/{plot.Required} days");

                // Action row
                GameObject row = new GameObject("PlotActions", typeof(RectTransform));
                row.transform.SetParent(entriesList, false);
                RectTransform rowRect = (RectTransform)row.transform;
                rowRect.sizeDelta = new Vector2(0f, 34f);
                LayoutElement rowLe = row.AddComponent<LayoutElement>();
                rowLe.minHeight = 34f;
                rowLe.preferredHeight = 34f;
                HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 6f;
                hlg.childForceExpandWidth = false;

                Plot captured = plot;
                AddSmallButton(row.transform, "Invite", () => { captured.Power = Mathf.Min(100, captured.Power + 10); lastStatus = $"Invited a backer to {captured.Type}"; Refresh(); });
                AddSmallButton(row.transform, "Bribe", () =>
                {
                    if (player.Wealth >= 10f)
                    {
                        player.Wealth -= 10f;
                        captured.Power = Mathf.Min(100, captured.Power + 15);
                        lastStatus = $"Bribed a backer for {captured.Type}";
                    }
                    else
                    {
                        lastStatus = "Not enough wealth to bribe.";
                    }
                    Refresh();
                });
                AddSmallButton(row.transform, "Execute", () =>
                {
                    if (captured.Power >= 100)
                    {
                        myPlots.Remove(captured);
                        player.Reknown += 5f;
                        lastStatus = $"{captured.Type} executed successfully.";
                    }
                    else
                    {
                        lastStatus = $"{captured.Type} needs 100% plot power (currently {captured.Power}%).";
                    }
                    Refresh();
                });
                AddSmallButton(row.transform, "Cancel", () => { myPlots.Remove(captured); lastStatus = $"{captured.Type} cancelled."; Refresh(); });
            }
        }

        // Suggested Plots
        CreateHeader("Suggested Plots");
        var suggestions = new (string name, int req, int days)[]
        {
            ("Assassinate", 8, 21),
            ("Fabricate Claim", 6, 21),
            ("Seduce", 5, 28),
            ("Spy On", 4, 7),
            ("Abduct", 7, 10),
            ("Forge Document", 10, 45),
        };
        foreach (var s in suggestions)
        {
            CreateLabel($"{s.name}  (Intrigue {s.req}, ~{s.days} days)");
            GameObject row = new GameObject("SuggestedRow", typeof(RectTransform));
            row.transform.SetParent(entriesList, false);
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.sizeDelta = new Vector2(0f, 30f);
            LayoutElement rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 30f;
            rowLe.preferredHeight = 30f;
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;

            string name = s.name;
            int days = s.days;
            AddSmallButton(row.transform, "Start", () =>
            {
                myPlots.Add(new Plot { Type = name, Target = "Selected target", Power = 0, Progress = 0, Required = days });
                lastStatus = $"Started plot: {name}";
                Refresh();
            });
        }

        // Known Plots
        CreateHeader("Known Plots");
        if (knownPlots.Count == 0)
        {
            CreateLabel("No plots known to be targeting you.");
        }
        else
        {
            foreach (var kp in new List<string>(knownPlots))
            {
                CreateLabel($"- {kp}");
                GameObject row = new GameObject("KnownRow", typeof(RectTransform));
                row.transform.SetParent(entriesList, false);
                RectTransform rowRect = (RectTransform)row.transform;
                rowRect.sizeDelta = new Vector2(0f, 30f);
                LayoutElement rowLe = row.AddComponent<LayoutElement>();
                rowLe.minHeight = 30f;
                rowLe.preferredHeight = 30f;
                HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 6f;

                string captured = kp;
                AddSmallButton(row.transform, "Join", () => { lastStatus = $"Joined plot: {captured}"; Refresh(); });
                AddSmallButton(row.transform, "Expose", () => { knownPlots.Remove(captured); lastStatus = $"Exposed plot: {captured}"; Refresh(); });
            }
        }
        // Seed a sample known plot for legibility (once).
        if (knownPlots.Count == 0)
        {
            // keep empty by default
        }

        // Leverage
        CreateHeader("Leverage");
        if (leverage.Count == 0)
        {
            CreateLabel("No leverage held yet. Use Spy On / Compromise to gain secrets.");
        }
        else
        {
            foreach (var lev in new List<string>(leverage))
            {
                CreateLabel($"- {lev}");
                GameObject row = new GameObject("LevRow", typeof(RectTransform));
                row.transform.SetParent(entriesList, false);
                RectTransform rowRect = (RectTransform)row.transform;
                rowRect.sizeDelta = new Vector2(0f, 30f);
                LayoutElement rowLe = row.AddComponent<LayoutElement>();
                rowLe.minHeight = 30f;
                rowLe.preferredHeight = 30f;
                HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 6f;

                string captured = lev;
                AddSmallButton(row.transform, "Blackmail", () => { lastStatus = $"Blackmailed using: {captured}"; player.Wealth += 20f; leverage.Remove(captured); Refresh(); });
            }
        }
        AddSmallButton(entriesList, "Gain Sample Leverage", () => { leverage.Add("Secret of a rival (source: servant)"); lastStatus = "Gained leverage."; Refresh(); });

        // Defence
        CreateHeader("Defence");
        CreateLabel("Suspicion breakdown: none (placeholder)");
        AddSmallButton(entriesList, "Discover Plots", () => { lastStatus = "Searched for plots against you."; Refresh(); });

        // Close
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