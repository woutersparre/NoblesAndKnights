using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// CK2-style right-click action menu for a character. Built at runtime so it
/// does not depend on any editor-authored prefab. Shows a small floating list
/// of actions (gift, sway, plots, ...) that operate on the chosen target.
/// </summary>
public static class CharacterActionMenu
{
    private static GameObject currentBackdrop;

    public static void Show(Character target, Vector2 screenPosition)
    {
        Hide();
        if (target == null)
            return;

        Transform parent = null;
        if (GameUIManager.instance != null)
            parent = GameUIManager.instance.screensTransform;
        if (parent == null)
        {
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas != null) parent = canvas.transform;
        }
        if (parent == null)
            return;

        // Full-screen transparent catcher: clicking anywhere outside the menu
        // closes it.
        GameObject backdrop = new GameObject("CharacterActionMenuBackdrop",
            typeof(RectTransform), typeof(Image), typeof(Button));
        backdrop.transform.SetParent(parent, false);
        RectTransform backdropRt = (RectTransform)backdrop.transform;
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;

        Image backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = new Color(0f, 0f, 0f, 0.18f);

        Button backdropButton = backdrop.GetComponent<Button>();
        backdropButton.onClick.AddListener(Hide);

        // Floating panel.
        GameObject panel = new GameObject("CharacterActionMenu",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(backdrop.transform, false);
        RectTransform panelRt = (RectTransform)panel.transform;
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);

        VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = panel.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Position the panel at the click, clamped to the screen.
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(backdropRt, screenPosition, null, out local))
            local = screenPosition;

        float estWidth = 230f;
        float estHeight = 34f * 12f;
        local.x = Mathf.Clamp(local.x, -backdropRt.rect.width * 0.5f + estWidth * 0.5f + 8f,
            backdropRt.rect.width * 0.5f - estWidth * 0.5f - 8f);
        local.y = Mathf.Clamp(local.y, -backdropRt.rect.height * 0.5f + estHeight * 0.5f + 8f,
            backdropRt.rect.height * 0.5f - estHeight * 0.5f - 8f);
        panelRt.anchoredPosition = local;

        Character player = (MapManager.instance != null && MapManager.instance.characters != null)
            ? MapManager.instance.characters.Player : null;

        // Title row.
        TMP_Text title = UIFactory.CreateText(panel.transform, "Title", target.Name, 16f,
            TextAlignmentOptions.Left, UIFactory.AccentColor);
        LayoutElement titleLe = title.gameObject.AddComponent<LayoutElement>();
        titleLe.minWidth = 210f;
        titleLe.preferredWidth = 210f;
        titleLe.minHeight = 22f;
        titleLe.preferredHeight = 22f;

        AddItem(panel.transform, "View Character", () =>
        {
            GameUIManager.NewCharacterUI(target);
        });

        AddItem(panel.transform, "Send Gift (25 gold)", () =>
        {
            if (player == null || player == target)
                return;
            if (player.Wealth < 25f)
            {
                MessageManager.instance?.Send("Not enough wealth to send a gift.");
                return;
            }
            player.Wealth -= 25f;
            target.Wealth += 25f;
            target.AddTimedModifier(player, "Received gift", 20, 5);
            MessageManager.instance?.Send("{0} sent a gift to {1}.", player.Name, target.Name);
        });

        AddItem(panel.transform, "Sway (10 gold)", () =>
        {
            if (player == null || player == target)
                return;
            if (player.Wealth < 10f)
            {
                MessageManager.instance?.Send("Not enough wealth to sway.");
                return;
            }
            player.Wealth -= 10f;
            target.AddTimedModifier(player, "Swayed", 15, 10);
            MessageManager.instance?.Send("{0} is swaying {1}.", player.Name, target.Name);
        });

        Action<string> startPlot = (plotType) =>
        {
            if (target == null) return;
            IntrigueUI ui = IntrigueUI.instance;
            if (ui == null)
                ui = UnityEngine.Object.FindFirstObjectByType<IntrigueUI>();
            if (ui != null)
                ui.StartPlot(plotType, target);
            if (GameUIManager.instance != null)
                GameUIManager.instance.OpenIntrigueScreen();
            MessageManager.instance?.Send("Started plot: {0} against {1}.", plotType, target.Name);
        };

        AddItem(panel.transform, "Plot to Kill", () => startPlot("Assassinate"));
        AddItem(panel.transform, "Fabricate Claim", () => startPlot("Fabricate Claim"));
        AddItem(panel.transform, "Seduce", () => startPlot("Seduce"));
        AddItem(panel.transform, "Spy On", () => startPlot("Spy On"));
        AddItem(panel.transform, "Poison", () => startPlot("Poison"));
        AddItem(panel.transform, "Abduct", () => startPlot("Abduct"));

        AddItem(panel.transform, "Cancel", () => { });

        currentBackdrop = backdrop;
    }

    public static void Hide()
    {
        if (currentBackdrop != null)
        {
            UnityEngine.Object.Destroy(currentBackdrop);
            currentBackdrop = null;
        }
    }

    private static void AddItem(Transform parent, string label, Action onClick)
    {
        Button btn = UIFactory.CreateButton(parent, label, onClick, 15f);
        RectTransform rt = (RectTransform)btn.transform;
        rt.sizeDelta = new Vector2(218f, 30f);
        LayoutElement le = btn.gameObject.GetComponent<LayoutElement>();
        if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
        le.minWidth = 218f;
        le.preferredWidth = 218f;
        le.minHeight = 30f;
        le.preferredHeight = 30f;
    }
}
