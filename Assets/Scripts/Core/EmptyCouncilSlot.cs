using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class EmptyCouncilSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text slotLabel;
    [SerializeField] private Button assignButton;
    [SerializeField] private GameObject roleListPanel;
    [SerializeField] private Transform roleListContainer;

    private CouncilPosition position;
    private List<GameObject> roleButtonObjs = new();

    public void Initialize(CouncilPosition pos)
    {
        position = pos;
        slotLabel.text = "Empty Slot";

        assignButton.onClick.RemoveAllListeners();
        assignButton.onClick.AddListener(ShowRoleList);

        if (roleListPanel != null)
            roleListPanel.SetActive(false);
    }

    void ShowRoleList()
    {
        ClearRoleButtons();

        if (roleListContainer == null)
        {
            Debug.LogWarning("EmptyCouncilSlot: roleListContainer not assigned, cannot show role list.");
            return;
        }

        Character ruler = position != null ? position.Owner : null;
        if (ruler == null)
        {
            Debug.LogWarning("EmptyCouncilSlot: slot has no owning ruler, cannot show role list.");
            return;
        }

        foreach (CouncilRole role in CouncilRoleInfo.GetUnlockedRoles(ruler))
        {
            if (CouncilRoleInfo.IsRoleInUse(role, ruler))
                continue;

            CreateRoleButton(role);
        }

        if (roleListPanel != null)
            roleListPanel.SetActive(true);
    }

    void CreateRoleButton(CouncilRole role)
    {
        string roleName = CouncilRoleInfo.GetDisplayName(role);

        GameObject go = new GameObject($"{roleName}Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(roleListContainer, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 40);

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.37f, 0.37f, 0.30f, 1f);

        GameObject labelGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        labelGo.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        labelGo.GetComponent<RectTransform>().anchorMax = Vector2.one;
        labelGo.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        labelGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        TMP_Text label = labelGo.GetComponent<TextMeshProUGUI>();
        if (slotLabel != null && slotLabel.font != null)
            label.font = slotLabel.font;
        label.text = roleName;
        label.fontSize = 24;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => OnRoleSelected(role));

        roleButtonObjs.Add(go);
    }

    void OnRoleSelected(CouncilRole role)
    {
        if (!CouncilService.ChooseRole(position?.Owner, position, role))
        {
            Debug.LogWarning($"EmptyCouncilSlot: cannot give this post the role of {CouncilRoleInfo.GetDisplayName(role)}.");
            return;
        }

        if (roleListPanel != null)
            roleListPanel.SetActive(false);

        CouncilUI.instance.SetupEntries();
    }

    void ClearRoleButtons()
    {
        foreach (var btn in roleButtonObjs)
        {
            if (btn != null)
                Destroy(btn);
        }
        roleButtonObjs.Clear();
    }
}