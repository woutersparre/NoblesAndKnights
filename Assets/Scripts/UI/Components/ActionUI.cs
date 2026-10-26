using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// UI component for displaying and executing actions
// Integrates with the existing Window system
public class ActionUI : MonoBehaviour
{
[Header("Action UI Components")]
    public GameObject actionsScreen;
    public GameObject actionEntryPrefab;

    List<IAction> availableActions;
    private Location currentLocation;
    private Character currentPlayer;
    private IAction pendingAction;
    private bool wasTraveling;

    public void UpdateActions(Location location)
    {
        currentLocation = location;
        currentPlayer = MapManager.instance.characters.Player;
        wasTraveling = currentPlayer != null && currentPlayer.IsTraveling;

        availableActions = ActionManager.instance.GetAvailableActions(currentPlayer, location);

        // Clear previous action buttons (in case of update)
        foreach (Transform child in actionsScreen.transform)
        {
            Destroy(child.gameObject);
        }

        // Populate with new actions
        foreach (IAction action in availableActions)
        {
            ActionEntry actionEntry = Instantiate(actionEntryPrefab, actionsScreen.transform).GetComponent<ActionEntry>();
            
            // Get action data
            ActionCost cost = action.GetCost(currentPlayer, location);
            ActionReward reward = action.GetReward(currentPlayer, location);
            ActionRisk risk = action.GetRisk(currentPlayer, location);
            
            // Travel actions display their destination in the button label
            string actionName = action is TravelAction ? $"Travel to {location.Name}" : action.Name;
            
            // Update entry with all data
            actionEntry.UpdateEntry(
                actionName, 
                action.Description, 
                FormatCostText(cost), 
                FormatRewardText(reward), 
                FormatRiskText(risk)
            );
            
            actionEntry.Button.gameObject.SetActive(true);
            actionEntry.Button.onClick.AddListener(() => ActionClicked(action));
        }
    }

    private void Update()
    {
        if (currentPlayer == null || currentPlayer.IsTraveling)
        {
            wasTraveling = currentPlayer != null && currentPlayer.IsTraveling;
            return;
        }

        bool justArrived = wasTraveling;
        wasTraveling = false;

        IAction action = pendingAction;
        pendingAction = null;

        // TravelAction already handled moving us here; only re-execute real actions
        if (action != null && !(action is TravelAction))
        {
            ActionManager.instance.ExecuteAction(action, currentPlayer, currentLocation);
        }

        // Whether we arrived to run an action or the travel here finished some other way,
        // refresh so newly unlocked actions appear.
        if (justArrived || action != null)
        {
            UpdateActions(currentLocation);
        }
    }

    private string FormatCostText(ActionCost cost)
    {
        var parts = new List<string>();
        
        if (cost.WealthCost > 0) parts.Add($"Wealth: {cost.WealthCost}");
        if (cost.ReknownCost > 0) parts.Add($"Reknown: {cost.ReknownCost}");
        if (cost.PietyCost > 0) parts.Add($"Piety: {cost.PietyCost}");
        if (cost.TimeCostDays > 0) parts.Add($"Time: {cost.TimeCostDays} days");
        
        if (cost.RequiredDiplomacy > 0) parts.Add($"Diplomacy: {cost.RequiredDiplomacy}");
        if (cost.RequiredMartial > 0) parts.Add($"Martial: {cost.RequiredMartial}");
        if (cost.RequiredStewardship > 0) parts.Add($"Stewardship: {cost.RequiredStewardship}");
        if (cost.RequiredIntrigue > 0) parts.Add($"Intrigue: {cost.RequiredIntrigue}");
        if (cost.RequiredLearning > 0) parts.Add($"Learning: {cost.RequiredLearning}");
        
        return parts.Count > 0 ? string.Join(", ", parts) : "Free";
    }

private string FormatRewardText(ActionReward reward)
    {
        var parts = new List<string>();

        // Include all reward factors (easiest implementation)
        if (reward.WealthGain > 0) parts.Add($"Wealth: +{reward.WealthGain:F1}");
        if (reward.ReknownGain > 0) parts.Add($"Reknown: +{reward.ReknownGain:F1}");
        if (reward.PietyGain > 0) parts.Add($"Piety: +{reward.PietyGain:F1}");

        if (reward.DiplomacyExp > 0) parts.Add($"Diplomacy Exp: +{reward.DiplomacyExp:F1}");
        if (reward.MartialExp > 0) parts.Add($"Martial Exp: +{reward.MartialExp:F1}");
        if (reward.StewardshipExp > 0) parts.Add($"Stewardship Exp: +{reward.StewardshipExp:F1}");
        if (reward.IntrigueExp > 0) parts.Add($"Intrigue Exp: +{reward.IntrigueExp:F1}");
        if (reward.LearningExp > 0) parts.Add($"Learning Exp: +{reward.LearningExp:F1}");
        
        if (reward.PotentialTraits.Count > 0) parts.Add($"Potential traits: {reward.PotentialTraits.Count}");
        
        return parts.Count > 0 ? string.Join(", ", parts) : "No rewards";
    }

    private string FormatRiskText(ActionRisk risk)
    {
        if (!risk.HasRisk)
            return "No risk";
            
        var parts = new List<string>();
        
        // Include all risk factors (easiest implementation)
        if (risk.FailureChance > 0) parts.Add($"Failure chance: {risk.FailureChance:P0}");
        if (risk.InjuryChance > 0) parts.Add($"Injury chance: {risk.InjuryChance:P0}");
        if (risk.DeathChance > 0) parts.Add($"Death chance: {risk.DeathChance:P0}");
        
        if (risk.WealthLoss > 0) parts.Add($"Wealth loss: {risk.WealthLoss}");
        if (risk.ReknownLoss > 0) parts.Add($"Reknown loss: {risk.ReknownLoss}");
        if (risk.PietyLoss > 0) parts.Add($"Piety loss: {risk.PietyLoss}");
        
        if (risk.NegativeTraits.Count > 0) parts.Add($"Negative traits: {risk.NegativeTraits.Count}");
        
        return parts.Count > 0 ? string.Join(", ", parts) : "Unknown risk";
    }

    private void ActionClicked(IAction action)
    {
        // A traveling character is never physically "at" a location, even if CurrentLocation still points at the origin
        if (!currentPlayer.IsTraveling && currentPlayer.CurrentLocation == currentLocation)
        {
            // Already at location — execute immediately
            ActionManager.instance.ExecuteAction(action, currentPlayer, currentLocation);
            UpdateActions(currentLocation); // refresh in case execution unlocked new actions
        }
        else
        {
            // Need to travel first — arrival will trigger execution
            pendingAction = action;
            TravelManager.instance.RequestTravel(currentPlayer, currentLocation);
        }
    }
}