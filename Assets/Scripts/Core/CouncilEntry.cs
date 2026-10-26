using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class CouncilEntry : MonoBehaviour
{
    [SerializeField]
    public CouncilPosition position;
    public TMP_Text AppointedCharName, PositionName, HolderStatus;
    public Button AppointButton;
    public Image Portrait;
    [SerializeField] private Transform actionsContainer;

    private System.Collections.Generic.List<GameObject> actionButtons = new();

    public void Initialize(CouncilPosition pos)
    {
        position = pos;
        UpdateEntry();
    }

    public void UpdateEntry()
    {
        PositionName.text = position.PositionName;

        CharacterPortraitUI portraitUI = GetPortraitUI();

        if (position.Appointed == null || position.Role == CouncilRole.None)
        {
            AppointedCharName.text = "Position Empty";
            HolderStatus.text = "";
            if (portraitUI != null) portraitUI.Clear();
            else if (Portrait != null) Portrait.sprite = null;
            BuildActions();
            return;
        }

        AppointedCharName.text = position.Appointed.Name;
        if (portraitUI != null) portraitUI.Setup(position.Appointed, position.PositionName);
        else if (Portrait != null) Portrait.sprite = position.Appointed.Portrait;

        if (position.IsWorking)
        {
            string location = position.ActionLocation != null ? position.ActionLocation.Name : "somewhere";
            string days = position.ActiveAction != null && position.ActiveAction.DurationDays > 0 ? $" ({position.WorkingDaysLeft}d)" : " (until cancelled)";
            HolderStatus.text = $"{position.ActiveAction.Name} at {location}{days}";
        }
        else
        {
            HolderStatus.text = "Idle";
        }

        BuildActions();
    }

    // Rebuilds the actions row with the deployment action for this slot's role.
    // The baked-in empty icon slots in the prefab are cleared at runtime.
    void BuildActions()
    {
        ClearActionButtons();

        if (actionsContainer == null || position.Appointed == null)
            return;

        foreach (CouncilRoleAction action in CouncilRoleInfo.GetPlaceholderActions(position.Role))
        {
            CreateActionButton(action);
        }
    }

    private CharacterPortraitUI GetPortraitUI()
    {
        return Portrait != null ? Portrait.GetComponent<CharacterPortraitUI>() : null;
    }

    void CreateActionButton(CouncilRoleAction action)
    {
        bool running = position.IsRunning(action);
        string buttonText = running ? $"{action.Name} ({position.WorkingDaysLeft}d)" : action.Name;

        GameObject go = new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(InfoPopup));
        go.transform.SetParent(actionsContainer, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 28);

        Image img = go.GetComponent<Image>();
        img.color = running ? new Color(0.55f, 0.25f, 0.15f, 1f) : new Color(0.37f, 0.37f, 0.30f, 1f);

        GameObject labelGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        labelGo.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        labelGo.GetComponent<RectTransform>().anchorMax = Vector2.one;
        labelGo.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        labelGo.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        TMP_Text labelText = labelGo.GetComponent<TextMeshProUGUI>();
        labelText.text = buttonText;
        labelText.fontSize = 14;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => ActionClicked(action));

        InfoPopup popup = go.GetComponent<InfoPopup>();
        popup.Header = action.Name;
        popup.Body = BuildActionTooltip(action, running);

        actionButtons.Add(go);
    }

    private string BuildActionTooltip(CouncilRoleAction action, bool running)
    {
        StringBuilder b = new StringBuilder();

        if (!string.IsNullOrEmpty(action.Description))
            b.AppendLine(action.Description);

        b.Append("Role: ").AppendLine(CouncilRoleInfo.GetDisplayName(action.Role));
        b.Append("Duration: ").Append(action.DurationDays > 0 ? action.DurationDays + " days" : "until cancelled");

        if (running)
            b.Append("\nIn progress: ").Append(position.WorkingDaysLeft).Append(" days remaining");

        return b.ToString();
    }

    void ActionClicked(CouncilRoleAction action)
    {
        Debug.Log($"[CouncilEntry] ActionClicked: {action.Name} on {position.PositionName} (IsDeployment={action.IsDeployment}, IsWorking={position.IsWorking}).");

        if (action.IsDeployment)
        {
            if (position.IsWorking)
                CouncilDeployer.CancelDeployment(position);
            else
                CouncilDeployer.StartDeploy(position, action);
            return;
        }

        MessageManager.instance?.Send($"{action.Name} is not implemented yet.");
    }

    void ClearActionButtons()
    {
        foreach (var btn in actionButtons)
        {
            if (btn != null)
                Destroy(btn);
        }
        actionButtons.Clear();

        // Remove any baked-in children lingering in the container (icon slots from the prefab).
        if (actionsContainer != null)
        {
            foreach (Transform child in actionsContainer)
            {
                if (child != null)
                    Destroy(child.gameObject);
            }
        }
    }

    private int GetStatValue(Character c, Stats.StatType stat)
    {
        switch (stat)
        {
            case Stats.StatType.Diplomacy:    return c.Stats.Diplomacy;
            case Stats.StatType.Martial:      return c.Stats.Martial;
            case Stats.StatType.Stewardship:  return c.Stats.Stewardship;
            case Stats.StatType.Intrigue:     return c.Stats.Intrigue;
            case Stats.StatType.Learning:     return c.Stats.Learning;
            default:                          return 0;
        }
    }
}