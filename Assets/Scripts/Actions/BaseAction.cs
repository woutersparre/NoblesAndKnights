using UnityEngine;

/// <summary>
/// Base class for actions that provides default implementations
/// Makes it easier to create new actions by only requiring override of specific methods
/// </summary>
public abstract class BaseAction : IAction
{
    // Basic action information - subclasses must implement these
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual Sprite Icon => Resources.Load<Sprite>($"Icons/Actions/{Name}");
    public abstract ActionType Type { get; }
    
    // Target and context - subclasses can override
    public virtual LocationType[] ValidLocationTypes => new LocationType[0];
    public virtual bool RequiresRuler => false;
    public virtual bool IsRepeatable => true;
    public virtual DiscoveryVisibility Visibility => DiscoveryVisibility.Any;
    
    // Events
    public System.Action<Character, Location, ActionResult> OnActionCompleted { get; set; }
    public System.Action<Character, Location> OnActionFailed { get; set; }
    
    // Default implementations - subclasses can override these
    public virtual bool CanPerform(Character character, Location location)
    {
        ActionCost cost = GetCost(character, location);
        return cost.CanAfford(character);
    }
    
    public virtual ActionCost GetCost(Character character, Location location)
    {
        return new ActionCost(); // Free action by default
    }
    
    public virtual void ApplyCost(Character character, Location location)
    {
        ActionCost cost = GetCost(character, location);
        cost.ApplyCost(character);
    }
    
    public virtual ActionReward GetReward(Character character, Location location)
    {
        return new ActionReward(); // No reward by default
    }
    
    public virtual ActionRisk GetRisk(Character character, Location location)
    {
        return new ActionRisk { HasRisk = false }; // No risk by default
    }
    
    public virtual ActionResult Execute(Character character, Location location)
    {
        if (!IsAvailable(character, location))
        {
            return new ActionResult(false, "Action not available");
        }
        
        ActionCost cost = GetCost(character, location);
        ActionReward reward = GetReward(character, location);
        ActionRisk risk = GetRisk(character, location);
        
        // Apply costs
        ApplyCost(character, location);
        
        // Roll for success if there are risks
        bool success = risk.RollSuccess();
        
        ActionResult result;
        if (success)
        {
            // Apply rewards
            reward.ApplyReward(character, location);
            
            result = new ActionResult(true, $"Successfully completed {Name}")
            {
                AppliedReward = reward,
                AppliedRisk = risk,
                TimeTakenDays = cost.TimeCostDays
            };
            
            OnActionCompleted?.Invoke(character, location, result);
        }
        else
        {
            // Apply failure consequences
            risk.ApplyFailure(character, location);
            
            result = new ActionResult(false, $"Failed to complete {Name}")
            {
                AppliedRisk = risk,
                TimeTakenDays = cost.TimeCostDays
            };
            
            OnActionFailed?.Invoke(character, location);
        }
        
        return result;
    }
    
    public virtual bool IsAvailable(Character character, Location location)
    {
        if (character == null || location == null)
            return false;
            
        // Check location type (empty list means valid everywhere)
        if (ValidLocationTypes.Length > 0)
        {
            bool locationTypeValid = false;
            foreach (LocationType validType in ValidLocationTypes)
            {
                if (validType == location.Type)
                {
                    locationTypeValid = true;
                    break;
                }
            }
            if (!locationTypeValid)
                return false;
        }
            
        // Check ruler requirement
        if (RequiresRuler && location.Ruler != character)
            return false;
            
        // Check if character can afford it
        if (!CanPerform(character, location))
            return false;
            
        return true;
    }
    
    public virtual void OnExecuteComplete(Character character, Location location, ActionResult result)
    {
        // Override to react to a finished execution
    }
    
    public virtual bool HasCooldown(Character character, Location location)
    {
        return false; // No cooldown by default
    }
    
    public virtual int GetCooldownDays(Character character, Location location)
    {
        return 0; // No cooldown by default
    }
}
