using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Interface for all actions that can be performed by characters at locations
/// Provides a generic framework for costs, rewards, risks, and conditions
/// </summary>
public interface IAction
{
    // Basic action information
    string Name { get; }
    string Description { get; }
    Sprite Icon { get; }
    ActionType Type { get; }
    
    // Target and context
    LocationType[] ValidLocationTypes { get; }
    bool RequiresRuler { get; }
    bool IsRepeatable { get; }
    DiscoveryVisibility Visibility { get; }
    
    // Cost validation and application
    bool CanPerform(Character character, Location location);
    ActionCost GetCost(Character character, Location location);
    void ApplyCost(Character character, Location location);
    
    // Reward and risk systems
    ActionReward GetReward(Character character, Location location);
    ActionRisk GetRisk(Character character, Location location);
    
    // Execution
    ActionResult Execute(Character character, Location location);
    
    // Called by the manager after a result is determined (success or failure)
    void OnExecuteComplete(Character character, Location location, ActionResult result);
    
    // Conditions and availability
    bool IsAvailable(Character character, Location location);
    bool HasCooldown(Character character, Location location);
    int GetCooldownDays(Character character, Location location);
    
    // Events
    System.Action<Character, Location, ActionResult> OnActionCompleted { get; set; }
    System.Action<Character, Location> OnActionFailed { get; set; }
}
/// <summary>
/// Types of actions for categorization and filtering
/// </summary>
public enum ActionType
{
    Travel,         // Movement between locations
    Exploration,    // Exploring caves, ruins, etc.
    Diplomacy,      // Political actions
    Military,       // Combat and warfare
    Economic,       // Trade and resource management
    Religious,      // Piety-based actions
    Intrigue,       // Espionage and plots
    Construction,   // Building and improvements
    Social,         // Personal relationships
    Learning,       // Research and education
    Custom          // Specialized actions
}

/// <summary>
/// A bitmask of discovery states in which an action is visible to a character.
/// Each action declares which state(s) of a location unlock it.
/// </summary>
[System.Flags]
public enum DiscoveryVisibility
{
    Unvisited = 1 << 0, // visible when the location is unvisited
    Visited   = 1 << 1, // visible when the location has been visited
    Explored  = 1 << 2, // visible when the location has been explored

    Any        = Unvisited | Visited | Explored,
    NotExplored = Unvisited | Visited
}
/// <summary>
/// Represents the cost structure of an action
/// </summary>
[System.Serializable]
public class ActionCost
{
    public float WealthCost = 0f;
    public float ReknownCost = 0f;
    public float PietyCost = 0f;
    
    // Skill requirements that must be met
    public int RequiredDiplomacy = 0;
    public int RequiredMartial = 0;
    public int RequiredStewardship = 0;
    public int RequiredIntrigue = 0;
    public int RequiredLearning = 0;
    
    // Time cost in days
    public int TimeCostDays = 0;
    
    // Custom requirements
    public List<string> RequiredTraits = new List<string>();
    public List<string> RequiredTitles = new List<string>();
    
    public bool CanAfford(Character character)
    {
        // Check resource costs
        if (character.Wealth < WealthCost) return false;
        if (character.Reknown < ReknownCost) return false;
        if (character.Piety < PietyCost) return false;
        
        // Check skill requirements
        if (character.Stats.Diplomacy < RequiredDiplomacy) return false;
        if (character.Stats.Martial < RequiredMartial) return false;
        if (character.Stats.Stewardship < RequiredStewardship) return false;
        if (character.Stats.Intrigue < RequiredIntrigue) return false;
        if (character.Stats.Learning < RequiredLearning) return false;
        
        // Check trait requirements
        foreach (string requiredTrait in RequiredTraits)
        {
            if (!character.HasTrait(Resources.Load<Trait>($"Traits/{requiredTrait}")))
                return false;
        }
        
        // Check title requirements
        foreach (string requiredTitle in RequiredTitles)
        {
            bool hasTitle = character.Titles.Exists(t => t.Name == requiredTitle);
            if (!hasTitle) return false;
        }
        
        return true;
    }
    
    public void ApplyCost(Character character)
    {
        character.Wealth -= WealthCost;
        character.Reknown -= ReknownCost;
        character.Piety -= PietyCost;
    }
}
/// <summary>
/// Represents the potential rewards of an action
/// </summary>
[System.Serializable]
public class ActionReward
{
    // Guaranteed rewards
    public float WealthGain = 0f;
    public float ReknownGain = 0f;
    public float PietyGain = 0f;
    
    // Skill experience
    public float DiplomacyExp = 0f;
    public float MartialExp = 0f;
    public float StewardshipExp = 0f;
    public float IntrigueExp = 0f;
    public float LearningExp = 0f;
    
    // World impact
    public float LocationPopulationChange = 0f;
    public float LocationMilitaryChange = 0f;
    public float LocationDefenseChange = 0f;
    
    // Items and discoveries
    public List<string> PotentialTraits = new List<string>();
    public List<string> PotentialItems = new List<string>();
    public List<ItemGrant> GrantedItems = new List<ItemGrant>();
    
    // Custom rewards
    public Dictionary<string, object> CustomRewards = new Dictionary<string, object>();
    
    public void ApplyReward(Character character, Location location)
    {
        // Apply resource gains
        character.Wealth += WealthGain;
        character.Reknown += ReknownGain;
        character.Piety += PietyGain;
        
        // Apply skill experience (you might want to implement a skill progression system)
        // For now, these could modify stats directly
        
        // Apply location changes
        if (location != null)
        {
            location.Population += LocationPopulationChange;
            location.MilitaryStrength += LocationMilitaryChange;
            location.DefensiveStrength += LocationDefenseChange;
        }
        
        // Apply traits (simplified - you might want to make this more sophisticated)
        foreach (string traitName in PotentialTraits)
        {
            Trait trait = Resources.Load<Trait>($"Traits/{traitName}");
            if (trait != null && !character.HasTrait(trait))
            {
                character.Traits.Add(trait);
                character.ApplyStatusModifiers(); // Reapply modifiers with new trait
            }
        }

        // Apply items
        foreach (var grant in GrantedItems)
        {
            if (grant != null && !string.IsNullOrEmpty(grant.itemId) && grant.count > 0)
                character.GiveItem(grant.itemId, grant.count);
        }
    }
}
/// <summary>
/// Represents the risk structure of an action
/// </summary>
[System.Serializable]
public class ActionRisk
{
    public bool HasRisk = false;
    public float FailureChance = 0f; // 0-1, probability of failure
    public float InjuryChance = 0f;   // 0-1, probability of injury
    public float DeathChance = 0f;    // 0-1, probability of death (use carefully!)
    
    // Negative consequences
    public float WealthLoss = 0f;
    public float ReknownLoss = 0f;
    public float PietyLoss = 0f;
    
    // Status effects
    public List<string> NegativeTraits = new List<string>();
    public List<string> TemporaryStatuses = new List<string>();
    
    // Location damage
    public float LocationDamage = 0f;
    
    public bool RollSuccess()
    {
        return !HasRisk || Random.value > FailureChance;
    }
    
    public void ApplyFailure(Character character, Location location)
    {
        // Apply resource losses
        character.Wealth -= WealthLoss;
        character.Reknown -= ReknownLoss;
        character.Piety -= PietyLoss;
        
        // Apply negative traits
        foreach (string traitName in NegativeTraits)
        {
            Trait trait = Resources.Load<Trait>($"Traits/{traitName}");
            if (trait != null && !character.HasTrait(trait))
            {
                character.Traits.Add(trait);
                character.ApplyStatusModifiers();
            }
        }
        
        // Apply location damage
        if (location != null)
        {
            location.MilitaryStrength -= LocationDamage;
            location.Population -= LocationDamage * 0.5f; // Civilian casualties
        }
    }
}
/// <summary>
/// Result of executing an action
/// </summary>
[System.Serializable]
public class ActionResult
{
    public bool Success;
    public string Message;
    public ActionReward AppliedReward;
    public ActionRisk AppliedRisk;
    public float TimeTakenDays;
    public List<string> EventsTriggered = new List<string>();
    
    public ActionResult(bool success, string message = "")
    {
        Success = success;
        Message = message;
    }
}
