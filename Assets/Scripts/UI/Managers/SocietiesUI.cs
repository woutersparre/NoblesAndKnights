using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SocietiesUI : MonoBehaviour
{
    public static SocietiesUI instance;

    [SerializeField] private Transform entriesList;

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
            return;

        if (player.Society == null)
            BuildJoinView(player);
        else
            BuildInfoView(player);
    }

    private void BuildJoinView(Character player)
    {
        if (Society.AllSocieties == null || Society.AllSocieties.Count == 0)
        {
            CreateLabel("No societies available.");
            return;
        }

        CreateLabel("Choose a society to join:");

        foreach (Society society in Society.AllSocieties)
        {
            GameObject row = new GameObject("SocietyRow", typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(entriesList, false);
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.sizeDelta = new Vector2(0f, 44f);

            Image bg = row.GetComponent<Image>();
            bg.color = UIFactory.RowColor;
            LayoutElement le = row.AddComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;

            TextMeshProUGUI label = UIFactory.CreateText(row.transform, "Label", society.Name ?? "Unknown", 20f, TextAlignmentOptions.Left, UIFactory.TextColor);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(12f, 4f);
            labelRect.offsetMax = new Vector2(-100f, -4f);

            Society captured = society;
            Button btn = row.GetComponent<Button>();
            btn.onClick.AddListener(() => Join(captured));

            TextMeshProUGUI btnLabel = UIFactory.CreateText(row.transform, "JoinLabel", "Join", 18f, TextAlignmentOptions.Center, UIFactory.TextColor);
            RectTransform btnLabelRect = btnLabel.rectTransform;
            btnLabelRect.anchorMin = new Vector2(1f, 0.5f);
            btnLabelRect.anchorMax = new Vector2(1f, 0.5f);
            btnLabelRect.pivot = new Vector2(1f, 0.5f);
            btnLabelRect.anchoredPosition = new Vector2(-8f, 0f);
            btnLabelRect.sizeDelta = new Vector2(80f, 30f);
        }
    }

    private void Join(Society society)
    {
        Character player = Player;
        if (player == null || society == null) return;

        player.Society = society;
        player.SocietyRank = 1;
        player.SocietyScore = 0f;

        if (society.Leader == null)
        {
            if (MapManager.instance != null && MapManager.instance.characters != null)
            {
                var rulers = MapManager.instance.characters.allCharacters.FindAll(c => c != null && !c.IsTemporary && c.Titles.Count > 0);
                if (rulers.Count > 0)
                    society.Leader = rulers[UnityEngine.Random.Range(0, rulers.Count)];
            }
            if (society.Leader == null)
                society.Leader = player;
        }

        Refresh();
    }

    private void BuildInfoView(Character player)
    {
        Society society = player.Society;

        CreateLabel($"Society: {(society != null ? society.Name : "Unknown")}");
        CreateLabel($"Description: {(society != null ? society.Description : "")}");
        string leaderName = society != null && society.Leader != null ? society.Leader.Name : "Unknown";
        CreateLabel($"Leader: {leaderName}");
        CreateLabel($"Your rank: {Mathf.Max(1, player.SocietyRank)}");
        CreateLabel($"Your score: {player.SocietyScore:F0}");
        CreateLabel("Powers / Missions: (placeholder)");

        Button leaveBtn = UIFactory.CreateButton(entriesList, "Leave Society", () => Leave(), 20f);
        RectTransform leaveRect = (RectTransform)leaveBtn.transform;
        leaveRect.sizeDelta = new Vector2(200f, 40f);
        LayoutElement le = leaveBtn.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 40f;
    }

    private void Leave()
    {
        Character player = Player;
        if (player == null) return;
        player.Society = null;
        player.SocietyRank = 0;
        player.SocietyScore = 0f;
        Refresh();
    }

    private void CreateLabel(string text)
    {
        TextMeshProUGUI label = UIFactory.CreateText(entriesList, "Label", text, 20f, TextAlignmentOptions.Left, UIFactory.TextColor);
        RectTransform rect = label.rectTransform;
        rect.sizeDelta = new Vector2(0f, 30f);
        LayoutElement le = label.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 30f;
        le.preferredHeight = 30f;
    }
}