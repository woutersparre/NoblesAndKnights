using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// After the world has been generated, the player picks which character to play
// as. Shown as a full-screen overlay over the freshly generated map.
//
// The list defaults to landed rulers (characters holding at least one title);
// a toggle reveals every character, which can be a few thousand entries, so the
// rows are created in batches across frames to keep the UI responsive.
public static class CharacterSelectUI
{
    private const float RowHeight = 76f;
    private const int RowsPerFrame = 40;

    public static IEnumerator Run(List<Character> characters, Action<Character> onConfirmed)
    {
        bool confirmed = false;
        bool showAll = false;
        bool rebuild = true;
        Character selected = null;

        var rowBackgrounds = new List<Image>();
        var rowCharacters = new List<Character>();

        UIFactory.EnsureEventSystem();
        Canvas canvas = UIFactory.CreateCanvas("CharacterSelectCanvas", 400);
        RectTransform root = (RectTransform)canvas.transform;

        Image backdrop = UIFactory.CreateImage(root, "Backdrop", new Color(0.03f, 0.04f, 0.06f, 0.96f));
        UIFactory.Stretch(backdrop.rectTransform);

        TextMeshProUGUI title = UIFactory.CreateText(root, "Title", "Choose Your Ruler",
            54f, TextAlignmentOptions.Center, UIFactory.TextColor);
        Top(title.rectTransform, 30f, 70f);

        TextMeshProUGUI subtitle = UIFactory.CreateText(root, "Subtitle",
            "You will play as the character you select.",
            24f, TextAlignmentOptions.Center, UIFactory.TextDimColor);
        Top(subtitle.rectTransform, 104f, 36f);

        // Filter bar -------------------------------------------------------
        var filterBar = UIFactory.NewRect(root, "FilterBar");
        filterBar.anchorMin = new Vector2(0f, 1f);
        filterBar.anchorMax = new Vector2(1f, 1f);
        filterBar.pivot = new Vector2(0.5f, 1f);
        filterBar.sizeDelta = new Vector2(-280f, 48f);
        filterBar.anchoredPosition = new Vector2(0f, -150f);

        TextMeshProUGUI countLabel = UIFactory.CreateText(filterBar, "Count", "", 22f,
            TextAlignmentOptions.Left, UIFactory.TextDimColor);
        var countRect = countLabel.rectTransform;
        countRect.anchorMin = new Vector2(0f, 0.5f);
        countRect.anchorMax = new Vector2(0f, 0.5f);
        countRect.pivot = new Vector2(0f, 0.5f);
        countRect.anchoredPosition = Vector2.zero;
        countRect.sizeDelta = new Vector2(700f, 40f);

        TextMeshProUGUI showAllLabel = UIFactory.CreateText(filterBar, "ShowAllLabel",
            "Show all characters", 22f, TextAlignmentOptions.Right, UIFactory.TextColor);
        var showAllLabelRect = showAllLabel.rectTransform;
        showAllLabelRect.anchorMin = new Vector2(1f, 0.5f);
        showAllLabelRect.anchorMax = new Vector2(1f, 0.5f);
        showAllLabelRect.pivot = new Vector2(1f, 0.5f);
        showAllLabelRect.anchoredPosition = new Vector2(-44f, 0f);
        showAllLabelRect.sizeDelta = new Vector2(320f, 40f);

        Toggle showAllToggle = UIFactory.CreateToggle(filterBar, false, v =>
        {
            showAll = v;
            rebuild = true;
        });
        var toggleRect = (RectTransform)showAllToggle.transform;
        toggleRect.anchorMin = new Vector2(1f, 0.5f);
        toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(0f, 0f);
        toggleRect.sizeDelta = new Vector2(32f, 32f);

        // List -------------------------------------------------------------
        ScrollRect scroll = UIFactory.CreateScrollView(root, out RectTransform content);
        var scrollRect = (RectTransform)scroll.transform;
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(140f, 120f);
        scrollRect.offsetMax = new Vector2(-140f, -210f);

        // Confirm bar ------------------------------------------------------
        Image confirmBar = UIFactory.CreateImage(root, "ConfirmBar", UIFactory.PanelColor);
        var confirmRect = confirmBar.rectTransform;
        confirmRect.anchorMin = new Vector2(0f, 0f);
        confirmRect.anchorMax = new Vector2(1f, 0f);
        confirmRect.pivot = new Vector2(0.5f, 0f);
        confirmRect.sizeDelta = new Vector2(0f, 96f);
        confirmRect.anchoredPosition = new Vector2(0f, 0f);

        TextMeshProUGUI summary = UIFactory.CreateText(confirmBar.transform, "Summary",
            "No ruler selected", 26f, TextAlignmentOptions.Left, UIFactory.TextColor);
        var summaryRect = summary.rectTransform;
        summaryRect.anchorMin = new Vector2(0f, 0.5f);
        summaryRect.anchorMax = new Vector2(0f, 0.5f);
        summaryRect.pivot = new Vector2(0f, 0.5f);
        summaryRect.anchoredPosition = new Vector2(40f, 0f);
        summaryRect.sizeDelta = new Vector2(1000f, 60f);

        Button confirmButton = UIFactory.CreateButton(confirmBar.transform, "Play as this ruler", () => confirmed = true);
        var confirmButtonRect = (RectTransform)confirmButton.transform;
        confirmButtonRect.anchorMin = new Vector2(1f, 0.5f);
        confirmButtonRect.anchorMax = new Vector2(1f, 0.5f);
        confirmButtonRect.pivot = new Vector2(1f, 0.5f);
        confirmButtonRect.anchoredPosition = new Vector2(-40f, 0f);
        confirmButtonRect.sizeDelta = new Vector2(340f, 60f);
        confirmButton.interactable = false;

        // Helpers ----------------------------------------------------------
        List<Character> BuildFiltered()
        {
            var list = new List<Character>();
            foreach (Character c in characters)
            {
                if (c == null || c.IsTemporary)
                    continue;
                if (!showAll && c.Titles.Count == 0)
                    continue;
                list.Add(c);
            }

            list.Sort((a, b) =>
            {
                int rankA = a.MainTitle != null ? (int)a.MainTitle.Rank : -1;
                int rankB = b.MainTitle != null ? (int)b.MainTitle.Rank : -1;
                if (rankA != rankB) return rankB.CompareTo(rankA);
                return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            });
            return list;
        }

        void Select(Character c)
        {
            selected = c;
            for (int i = 0; i < rowCharacters.Count; i++)
            {
                if (rowBackgrounds[i] == null) continue;
                bool isSelected = ReferenceEquals(rowCharacters[i], c);
                rowBackgrounds[i].color = isSelected
                    ? UIFactory.SelectedRowColor
                    : (i % 2 == 0 ? UIFactory.RowColor : UIFactory.RowAltColor);
            }

            summary.text = c != null ? "Selected: " + c.Name : "No ruler selected";
            if (confirmButton != null)
                confirmButton.interactable = c != null;
        }

        void CreateRow(Character c, int index)
        {
            RectTransform row = UIFactory.NewRect(content, "Row");
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, RowHeight - 4f);
            row.anchoredPosition = new Vector2(0f, -index * RowHeight);

            Image bg = UIFactory.CreateImage(row, "Bg", index % 2 == 0 ? UIFactory.RowColor : UIFactory.RowAltColor);
            UIFactory.Stretch(bg.rectTransform);

            Button button = bg.gameObject.AddComponent<Button>();
            StyleRowButton(button);
            Character captured = c;
            button.onClick.AddListener(() => Select(captured));

            Image avatar = UIFactory.CreateImage(row, "Avatar", UIFactory.PanelAltColor);
            var avatarRect = avatar.rectTransform;
            avatarRect.anchorMin = new Vector2(0f, 0.5f);
            avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.pivot = new Vector2(0f, 0.5f);
            avatarRect.anchoredPosition = new Vector2(10f, 0f);
            avatarRect.sizeDelta = new Vector2(56f, 56f);
            avatar.raycastTarget = false;
            if (c.Portrait != null)
            {
                avatar.sprite = c.Portrait;
                avatar.color = Color.white;
                avatar.preserveAspect = true;
            }

            TextMeshProUGUI nameText = UIFactory.CreateText(row, "Name", c.Name ?? "Unknown",
                26f, TextAlignmentOptions.Left, UIFactory.TextColor);
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0.5f);
            nameRect.anchorMax = new Vector2(0f, 0.5f);
            nameRect.pivot = new Vector2(0f, 0.5f);
            nameRect.anchoredPosition = new Vector2(80f, 15f);
            nameRect.sizeDelta = new Vector2(820f, 32f);

            string titleName = c.MainTitle != null ? c.MainTitle.Name : "Landless";
            string culture = c.Culture != null ? c.Culture.Name : "Unknown";
            TextMeshProUGUI infoText = UIFactory.CreateText(row, "Info",
                $"{titleName}   •   Age {c.Age}   •   {culture}",
                19f, TextAlignmentOptions.Left, UIFactory.TextDimColor);
            var infoRect = infoText.rectTransform;
            infoRect.anchorMin = new Vector2(0f, 0.5f);
            infoRect.anchorMax = new Vector2(0f, 0.5f);
            infoRect.pivot = new Vector2(0f, 0.5f);
            infoRect.anchoredPosition = new Vector2(80f, -16f);
            infoRect.sizeDelta = new Vector2(820f, 28f);

            Stats s = c.Stats;
            TextMeshProUGUI statsText = UIFactory.CreateText(row, "Stats",
                $"Dip {s.Diplomacy}   Mar {s.Martial}   Ste {s.Stewardship}   Int {s.Intrigue}   Lea {s.Learning}",
                18f, TextAlignmentOptions.Right, UIFactory.TextColor);
            var statsRect = statsText.rectTransform;
            statsRect.anchorMin = new Vector2(1f, 0.5f);
            statsRect.anchorMax = new Vector2(1f, 0.5f);
            statsRect.pivot = new Vector2(1f, 0.5f);
            statsRect.anchoredPosition = new Vector2(-16f, 0f);
            statsRect.sizeDelta = new Vector2(360f, 60f);

            rowBackgrounds.Add(bg);
            rowCharacters.Add(c);
        }

        IEnumerator BuildRows()
        {
            foreach (Image bg in rowBackgrounds)
                if (bg != null) UnityEngine.Object.Destroy(bg.transform.parent.gameObject);
            rowBackgrounds.Clear();
            rowCharacters.Clear();

            List<Character> filtered = BuildFiltered();
            content.sizeDelta = new Vector2(0f, filtered.Count * RowHeight);
            content.anchoredPosition = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;

            countLabel.text = showAll
                ? $"{filtered.Count} characters"
                : $"{filtered.Count} landed rulers  (enable \"Show all characters\" to see everyone)";

            for (int i = 0; i < filtered.Count; i++)
            {
                CreateRow(filtered[i], i);
                if (i % RowsPerFrame == RowsPerFrame - 1)
                    yield return null;
            }

            if (selected != null && filtered.Contains(selected))
                Select(selected);
            else if (filtered.Count > 0)
                Select(filtered[0]);
            else
                Select(null);
        }

        while (!confirmed)
        {
            if (rebuild)
            {
                rebuild = false;
                yield return BuildRows();
            }
            yield return null;
        }

        UnityEngine.Object.Destroy(canvas.gameObject);
        onConfirmed?.Invoke(selected);
    }

    private static void Top(RectTransform rt, float topOffset, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-160f, height);
        rt.anchoredPosition = new Vector2(0f, -topOffset);
    }

    private static void StyleRowButton(Button button)
    {
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }
}
