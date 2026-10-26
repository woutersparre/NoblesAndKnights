using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Central manager for all actions in the game
// Handles registration, execution, and cooldowns
public class ActionManager : MonoBehaviour
{
    public static ActionManager instance;

    // Registered actions
    private Dictionary<string, IAction> registeredActions = new Dictionary<string, IAction>();

    // Active cooldowns - tracks when actions can be used again
    private Dictionary<string, Dictionary<string, int>> actionCooldowns = new Dictionary<string, Dictionary<string, int>>();

    // One-time usage tracking
    private HashSet<string> usedOneTimeActions = new HashSet<string>();

    // Events
    public System.Action<Character, Location, ActionResult> OnAnyActionCompleted;
    public System.Action<Character, Location> OnAnyActionFailed;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        // Subscribe to time events. Queues the listener if this Awake runs before
        // TimeManager's, instead of dropping cooldown ticking for the session.
        TimeManager.SubscribeDayPassed(UpdateCooldowns);
    }

    // Register an action with the manager
    public void RegisterAction(IAction action)
    {
        if (action == null)
        {
            //Debug.LogError("Cannot register null action!");
            return;
        }

        registeredActions[action.Name] = action;

        // Hook up action events
        action.OnActionCompleted += (character, location, result) =>
        {
            OnAnyActionCompleted?.Invoke(character, location, result);
        };

        action.OnActionFailed += (character, location) =>
        {
            OnAnyActionFailed?.Invoke(character, location);
        };

        //Debug.Log($"Registered action: {action.Name}");
    }

    // Get all available actions for a character at a location
    public List<IAction> GetAvailableActions(Character character, Location location)
    {
        List<IAction> availableActions = new List<IAction>();
        DiscoveryVisibility locationVisibility = ToVisibility(location.GetDiscoveryState(character));

        foreach (IAction action in registeredActions.Values)
        {
            if (IsActionAvailable(action, character, location) && action.Visibility.HasFlag(locationVisibility))
            {
                availableActions.Add(action);
            }
        }

        return availableActions;
    }

    // Maps a DiscoveryState to its corresponding DiscoveryVisibility flag
    public static DiscoveryVisibility ToVisibility(DiscoveryState state)
    {
        return state switch
        {
            DiscoveryState.Unvisited => DiscoveryVisibility.Unvisited,
            DiscoveryState.Visited => DiscoveryVisibility.Visited,
            DiscoveryState.Explored => DiscoveryVisibility.Explored,
            _ => DiscoveryVisibility.Unvisited
        };
    }

    // Check if an action is available for a character at a location
    public bool IsActionAvailable(IAction action, Character character, Location location)
    {
        if (action == null || character == null || location == null)
            return false;

        // Check if location type is valid (empty list means valid everywhere)
        if (action.ValidLocationTypes.Length > 0 && !action.ValidLocationTypes.Contains(location.Type))
            return false;

        // Check ruler requirement
        if (action.RequiresRuler && location.Ruler != character)
            return false;

        // Check if action is available (custom conditions)
        if (!action.IsAvailable(character, location))
            return false;

        // Check cooldowns
        if (HasCooldown(action, character, location))
            return false;

        // Check one-time usage
        if (!action.IsRepeatable && IsOneTimeActionUsed(action, character, location))
            return false;

        // Check if character can afford the action
        if (!action.CanPerform(character, location))
            return false;

        return true;
    }

    // Execute an action
    public ActionResult ExecuteAction(string actionName, Character character, Location location)
    {
        if (!registeredActions.TryGetValue(actionName, out IAction action))
        {
            Debug.LogError($"Action {actionName} not found!");
            return new ActionResult(false, $"Action {actionName} not found!");
        }

        return ExecuteAction(action, character, location);
    }

    // Execute an action
    public ActionResult ExecuteAction(IAction action, Character character, Location location)
    {
        if (!IsActionAvailable(action, character, location))
        {
            string reason = GetUnavailabilityReason(action, character, location);
            Debug.LogWarning($"Cannot execute {action.Name}: {reason}");
            action.OnActionFailed?.Invoke(character, location);
            return new ActionResult(false, reason);
        }

        // Apply costs
        ActionCost cost = action.GetCost(character, location);
        cost.ApplyCost(character);

        // Roll for success if there are risks
        ActionRisk risk = action.GetRisk(character, location);
        bool success = risk.RollSuccess();

        ActionResult result;
        if (success)
        {
            // Apply rewards
            ActionReward reward = action.GetReward(character, location);
            reward.ApplyReward(character, location);

            result = new ActionResult(true, $"Successfully completed {action.Name}")
            {
                AppliedReward = reward,
                AppliedRisk = risk,
                TimeTakenDays = cost.TimeCostDays
            };

            // Trigger completion event
            action.OnActionCompleted?.Invoke(character, location, result);

            // Mark one-time actions as used
            if (!action.IsRepeatable)
            {
                MarkOneTimeActionUsed(action, character, location);
            }
        }
        else
        {
            // Apply failure consequences
            risk.ApplyFailure(character, location);

            result = new ActionResult(false, $"Failed to complete {action.Name}")
            {
                AppliedRisk = risk,
                TimeTakenDays = cost.TimeCostDays
            };

            // Trigger failure event
            action.OnActionFailed?.Invoke(character, location);
        }

        // Give the action a chance to react to the finished execution
        action.OnExecuteComplete(character, location, result);

        // Set cooldown
        if (action.IsRepeatable)
        {
            SetCooldown(action, character, location);
        }

        Debug.Log($"Action {action.Name} executed for {character.Name} at {location.Name}. Success: {result.Success}");

        return result;
    }

    // Get the reason why an action is not available
    public string GetUnavailabilityReason(IAction action, Character character, Location location)
    {
        if (action == null) return "Action is null";
        if (character == null) return "Character is null";
        if (location == null) return "Location is null";

        if (action.ValidLocationTypes.Length > 0 && !action.ValidLocationTypes.Contains(location.Type))
            return $"Action not available at {location.Type} locations";

        if (action.RequiresRuler && location.Ruler != character)
            return "Only the ruler can perform this action";

        if (!action.IsAvailable(character, location))
            return "Action conditions not met";

        if (HasCooldown(action, character, location))
            return $"Action on cooldown for {GetRemainingCooldownDays(action, character, location)} days";

        if (!action.IsRepeatable && IsOneTimeActionUsed(action, character, location))
            return "Action can only be used once";

        if (!action.CanPerform(character, location))
            return "Cannot afford this action";

        return "Unknown reason";
    }

    // Cooldown management
    private bool HasCooldown(IAction action, Character character, Location location)
    {
        string key = GetCooldownKey(action, character, location);

        if (actionCooldowns.ContainsKey(key))
        {
            return actionCooldowns[key].Count > 0;
        }

        return false;
    }

    private string GetCooldownKey(IAction action, Character character, Location location)
    {
        return $"{action.Name}_{character.UniqueId}_{location.UniqueId}";
    }

    private void SetCooldown(IAction action, Character character, Location location)
    {
        int cooldownDays = action.GetCooldownDays(character, location);
        if (cooldownDays <= 0) return;

        string key = GetCooldownKey(action, character, location);

        if (!actionCooldowns.ContainsKey(key))
        {
            actionCooldowns[key] = new Dictionary<string, int>();
        }

        actionCooldowns[key]["cooldown"] = cooldownDays;
    }

    private void UpdateCooldowns()
    {
        List<string> keysToRemove = new List<string>();

        foreach (var kvp in actionCooldowns)
        {
            string key = kvp.Key;
            var cooldowns = kvp.Value;

            List<string> cooldownKeysToRemove = new List<string>();

            foreach (var cooldownKvp in cooldowns)
            {
                string cooldownKey = cooldownKvp.Key;
                int remainingDays = cooldownKvp.Value;

                if (remainingDays > 0)
                {
                    cooldowns[cooldownKey] = remainingDays - 1;
                }
                else
                {
                    cooldownKeysToRemove.Add(cooldownKey);
                }
            }

            foreach (string cooldownKey in cooldownKeysToRemove)
            {
                cooldowns.Remove(cooldownKey);
            }

            if (cooldowns.Count == 0)
            {
                keysToRemove.Add(key);
            }
        }

        foreach (string key in keysToRemove)
        {
            actionCooldowns.Remove(key);
        }
    }

    private int GetRemainingCooldownDays(IAction action, Character character, Location location)
    {
        string key = GetCooldownKey(action, character, location);

        if (actionCooldowns.ContainsKey(key) && actionCooldowns[key].ContainsKey("cooldown"))
        {
            return actionCooldowns[key]["cooldown"];
        }

        return 0;
    }

    private bool IsOneTimeActionUsed(IAction action, Character character, Location location)
    {
        string key = GetOneTimeActionKey(action, character, location);
        return usedOneTimeActions.Contains(key);
    }

    private void MarkOneTimeActionUsed(IAction action, Character character, Location location)
    {
        string key = GetOneTimeActionKey(action, character, location);
        usedOneTimeActions.Add(key);
    }

    private string GetOneTimeActionKey(IAction action, Character character, Location location)
    {
        return $"{action.Name}_ONETIME_{character.UniqueId}_{location.UniqueId}";
    }

    // Get all registered actions (for debugging/initialization)
    public List<IAction> GetAllActions()
    {
        return new List<IAction>(registeredActions.Values);
    }
}
