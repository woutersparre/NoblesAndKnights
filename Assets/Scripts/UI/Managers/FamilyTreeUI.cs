using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The family tree window. Opens on top of the character screen (via the Family
// button), rebuilds a generational tree around a focus character from the live
// Character graph, spawns one portrait node per character and draws the
// connectors between them. Reuses CharacterPortraitUI so every node keeps the
// usual hover tooltip and click-through to the character screen.
public class FamilyTreeUI : Window<Character>
{
    [Header("Wiring")]
    public RectTransform viewport;
    public RectTransform content;
    public RectTransform toolbarPanel;
    public TMP_Text titleText;

    [Header("Node visuals")]
    public Sprite nodeFrameSprite;
    public Sprite focusFrameSprite;
    public Sprite buttonSprite;
    public TMP_FontAsset nodeFont;

    [Header("Scope")]
    public int ancestorDepth = 3;
    public int descendantDepth = 3;
    public bool includeSpouses = true;

    [Header("Colours")]
    public Color nodeFrameColor = new Color(0.86f, 0.80f, 0.64f, 1f);
    public Color focusFrameColor = new Color(1f, 0.84f, 0.30f, 1f);
    public Color deadTint = new Color(0.52f, 0.52f, 0.56f, 1f);
    public Color buttonColor = new Color(0.35f, 0.28f, 0.20f, 1f);
    public Color spouseLineColor = new Color(0.82f, 0.36f, 0.38f, 0.95f);
    public Color descentLineColor = new Color(0.78f, 0.71f, 0.55f, 0.95f);

    private const float PortraitWidth = 85f;
    private const float PortraitHeight = 120f;

    private FamilyTreeConnector connector;
    private readonly List<GameObject> spawnedNodes = new List<GameObject>();
    private readonly Dictionary<Character, RectTransform> nodeRects = new Dictionary<Character, RectTransform>();
    private Character focusCharacter;
    private bool toolbarBuilt;

    // Called by GameUIManager when the window is created; subsequent calls with a
    // different character simply rebuild the tree in place.
    public void UpdateUI(Character character)
    {
        Target = character;
        focusCharacter = character;

        if (titleText != null)
            titleText.text = character != null ? "Family Tree \u2013 " + character.Name : "Family Tree";

        BuildToolbar();
        ClearTree();

        if (character == null)
            return;

        FamilyTreeGraph graph = FamilyTreeBuilder.Build(character, ancestorDepth, descendantDepth, includeSpouses);
        TreeLayout layout = FamilyTreeLayout.Compute(graph, spouseLineColor, descentLineColor);

        if (content != null)
        {
            content.sizeDelta = layout.Size;
            content.anchoredPosition = Vector2.zero;
        }

        EnsureConnector();
        if (connector != null)
            connector.SetLines(layout.Lines);

        foreach (PlacedNode node in layout.Nodes)
            SpawnNode(node);

        CenterOnFocus();
    }

    // Toolbar buttons are built once from code so the prefab stays simple.
    private void BuildToolbar()
    {
        if (toolbarBuilt || toolbarPanel == null) return;
        toolbarBuilt = true;

        CreateToolbarButton("Center", Vector2.zero, CenterOnFocus);
    }

    private void CreateToolbarButton(string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonGo = new GameObject("Button_" + label, typeof(RectTransform));
        buttonGo.transform.SetParent(toolbarPanel, false);

        RectTransform rect = (RectTransform)buttonGo.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(170f, 32f);
        rect.anchoredPosition = position;

        Image background = buttonGo.AddComponent<Image>();
        if (buttonSprite != null) background.sprite = buttonSprite;
        background.color = buttonColor;
        background.type = Image.Type.Sliced;
        background.raycastTarget = true;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(action);

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(buttonGo.transform, false);

        RectTransform labelRect = (RectTransform)labelGo.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = labelGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        if (nodeFont != null) text.font = nodeFont;
        text.fontSize = 15f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.92f, 0.87f, 0.72f, 1f);
        text.raycastTarget = false;
    }

    private void ClearTree()
    {
        for (int i = 0; i < spawnedNodes.Count; i++)
        {
            if (spawnedNodes[i] != null)
                Destroy(spawnedNodes[i]);
        }
        spawnedNodes.Clear();
        nodeRects.Clear();

        if (connector != null)
            connector.SetLines(null);
    }

    private void EnsureConnector()
    {
        if (content == null) return;

        if (connector == null)
            connector = content.GetComponent<FamilyTreeConnector>();

        if (connector == null)
            connector = content.gameObject.AddComponent<FamilyTreeConnector>();

        connector.raycastTarget = false;
    }

    private void SpawnNode(PlacedNode node)
    {
        if (node.Character == null || content == null) return;

        GameObject nodeGo = new GameObject("Node_" + node.Character.UniqueId, typeof(RectTransform));
        nodeGo.transform.SetParent(content, false);

        RectTransform nodeRect = (RectTransform)nodeGo.transform;
        nodeRect.anchorMin = nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
        nodeRect.pivot = new Vector2(0.5f, 0.5f);
        nodeRect.sizeDelta = new Vector2(FamilyTreeLayout.NodeWidth, FamilyTreeLayout.NodeHeight);
        nodeRect.anchoredPosition = node.Position;

        // Frame behind the portrait; the focused character gets a gold one.
        Sprite frameSprite = node.IsFocus && focusFrameSprite != null ? focusFrameSprite : nodeFrameSprite;
        if (frameSprite != null)
        {
            Image frame = nodeGo.AddComponent<Image>();
            frame.sprite = frameSprite;
            frame.color = node.IsFocus ? focusFrameColor : nodeFrameColor;
            frame.raycastTarget = false;
        }

        // Portrait widget: a plain Image plus the same button/tooltip components
        // the CharacterPortrait prefab carries, wired up the same way.
        GameObject portraitGo = new GameObject("Portrait", typeof(RectTransform));
        portraitGo.transform.SetParent(nodeGo.transform, false);

        RectTransform portraitRect = (RectTransform)portraitGo.transform;
        portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0.5f, 1f);
        portraitRect.pivot = new Vector2(0.5f, 1f);
        portraitRect.sizeDelta = new Vector2(PortraitWidth, PortraitHeight);
        portraitRect.anchoredPosition = new Vector2(0f, -8f);

        Image portraitImage = portraitGo.AddComponent<Image>();
        portraitImage.raycastTarget = true;

        Button button = portraitGo.AddComponent<Button>();
        button.targetGraphic = portraitImage;

        InfoPopup infoPopup = portraitGo.AddComponent<InfoPopup>();
        CharacterPortraitUI portraitUI = portraitGo.AddComponent<CharacterPortraitUI>();
        portraitUI.Portrait = portraitImage;
        portraitUI.Button = button;
        portraitUI.infoPopup = infoPopup;
        portraitUI.Setup(node.Character, node.Relation);

        if (!node.Character.IsAlive)
        {
            portraitImage.color = deadTint;
            AddDeathMarker(nodeGo);
        }

        // Name plate under the portrait.
        GameObject nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(nodeGo.transform, false);

        RectTransform nameRect = (RectTransform)nameGo.transform;
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.sizeDelta = new Vector2(0f, 30f);
        nameRect.anchoredPosition = new Vector2(0f, 6f);

        TextMeshProUGUI nameText = nameGo.AddComponent<TextMeshProUGUI>();
        nameText.text = node.Character.Name;
        if (nodeFont != null) nameText.font = nodeFont;
        nameText.fontSize = 13f;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.raycastTarget = false;
        nameText.color = node.Character.IsAlive
            ? new Color(0.94f, 0.91f, 0.83f, 1f)
            : new Color(0.70f, 0.68f, 0.66f, 1f);

        spawnedNodes.Add(nodeGo);
        nodeRects[node.Character] = nodeRect;
    }

    // No skull art exists in the project yet, so a deceased character is marked
    // with a desaturated portrait plus a small dark dagger overlay.
    private void AddDeathMarker(GameObject nodeGo)
    {
        GameObject markerGo = new GameObject("DeathMarker", typeof(RectTransform));
        markerGo.transform.SetParent(nodeGo.transform, false);

        RectTransform markerRect = (RectTransform)markerGo.transform;
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 1f);
        markerRect.pivot = new Vector2(0.5f, 1f);
        markerRect.sizeDelta = new Vector2(30f, 30f);
        markerRect.anchoredPosition = new Vector2(30f, -12f);

        TextMeshProUGUI marker = markerGo.AddComponent<TextMeshProUGUI>();
        marker.text = "\u2020"; // dagger
        if (nodeFont != null) marker.font = nodeFont;
        marker.fontSize = 26f;
        marker.fontStyle = FontStyles.Bold;
        marker.alignment = TextAlignmentOptions.Center;
        marker.color = new Color(0.08f, 0.08f, 0.09f, 0.9f);
        marker.raycastTarget = false;
    }

    // Puts the focus character in the middle of the viewport. Also the window's
    // initial view, so "Center" undoes any panning the user has done.
    public void CenterOnFocus()
    {
        if (content == null) return;

        if (focusCharacter != null && nodeRects.TryGetValue(focusCharacter, out RectTransform nodeRect))
            content.anchoredPosition = -nodeRect.anchoredPosition;
        else
            content.anchoredPosition = Vector2.zero;
    }
}
