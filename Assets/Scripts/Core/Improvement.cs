using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public abstract class Improvement
{
    public string Name;
    public int Level = 0;
    public int MaxLevel;
    public float BaseCost;
    public float CostMultiplier;
    public float BaseEffect;
    public float EffectMultiplier;
    public float BuildTime;
    public float ConstructionCost;

    // Days left on the BuildTime countdown. 0 means nothing is being built here.
    // Level and the effect it grants only change when this reaches zero, so a build
    // runs over days like every other job instead of finishing on the click.
    public int ConstructionDaysLeft;

    public bool IsUnderConstruction => ConstructionDaysLeft > 0;

    public Improvement(string name, int maxLevel, float baseCost, float costMultiplier, float baseEffect, float effectMultiplier, float buildTime, float constructionCost, int startLevel = 0)
    {
        Name = name;
        MaxLevel = maxLevel;
        BaseCost = baseCost;
        CostMultiplier = costMultiplier;
        BaseEffect = baseEffect;
        EffectMultiplier = effectMultiplier;
        BuildTime = buildTime;
        ConstructionCost = constructionCost;
        Level = startLevel;
    }

    public bool CanUpgrade() => Level < MaxLevel;

    public float GetUpgradeCost() => BaseCost * CostMultiplier * (Level + 1);

    public float GetImprovementCost() => BaseCost * CostMultiplier * Level;

    public float GetCurrentEffect() => BaseEffect * EffectMultiplier * Level;

    // Effect the improvement would grant one level up, for previewing the next tier
    // before the player commits to it.
    public float GetNextEffect() => BaseEffect * EffectMultiplier * (Level + 1);

    // Which location stat ApplyEffect() feeds. Display-only, so the tooltip can say
    // what the numbers actually mean; empty when there is nothing useful to show.
    public virtual string EffectName => "";

    public void Upgrade(Location location, Character builder)
    {
        if (IsUnderConstruction)
        {
            location.Notify($"{Name} at {location.Name} is already under construction ({ConstructionDaysLeft} days left).");
            return;
        }

        if (Level == 0)
        {
            // If the improvement is unbuilt, start construction instead of upgrading.
            if (builder.Wealth >= ConstructionCost)
            {
                location.StartConstruction(this, builder, ConstructionCost);
            }
            else
            {
                location.Notify($"{builder.Name} does not have enough gold to construct {Name}!");
            }
        }
        else if (CanUpgrade())
        {
            // If the improvement exists, upgrade it. This is the only place the
            // upgrade is paid for, so it must use the same figure the caller guards
            // on (GetUpgradeCost) -- GetImprovementCost is one tier cheaper and made
            // Location.UpgradeImprovement charge the player twice.
            if (builder.Wealth >= GetUpgradeCost())
            {
                location.StartConstruction(this, builder, GetUpgradeCost());
            }
            else
            {
                location.Notify($"{builder.Name} does not have enough gold to upgrade {Name}!");
            }
        }
        else
        {
            location.Notify($"{Name} at {location.Name} is already at max level!");
        }
    }

    // Lands the level that was paid for. Called by Location when the countdown hits
    // zero, never from the button click, so the button reports a duration rather
    // than granting the level instantly.
    public void CompleteConstruction(Location location)
    {
        Level = Level == 0 ? 1 : Level + 1;
        ApplyEffect(location);

        Debug.Log($"{Name} reached level {Level} at {location.Name}.");
        MessageManager.instance?.Send("{0} at {1} is now level {2}.", Name, location.Name, Level);
    }


    public abstract void ApplyEffect(Location location);
    public virtual void PerformAction(Location location) { }
}




[System.Serializable]
public class Barracks : Improvement
{
    public Barracks(int startLevel = 0) : base("Barracks", 3, 100, 1.5f, 10, 1.2f, 5, 50, startLevel) { }

    public override string EffectName => "Military Strength";

    public override void ApplyEffect(Location location)
    {
        location.MilitaryStrength += GetCurrentEffect();
    }
}

[System.Serializable]
public class Walls : Improvement
{
    public Walls(int startLevel = 0) : base("Walls", 3, 150, 1.7f, 20, 1.3f, 7, 50, startLevel) { }

    public override string EffectName => "Defensive Strength";

    public override void ApplyEffect(Location location)
    {
        location.DefensiveStrength += GetCurrentEffect();

        // Flat rather than scaled, so it needs gating on being built: level 0 would
        // otherwise hand out fortification for a wall that isn't there.
        if (Level > 0)
            location.FortificationStrength += 2;
    }
}

[System.Serializable]
public class HuntingLodge : Improvement
{
    public HuntingLodge(int startLevel = 0) : base("Hunting Lodge", 2, 80, 1.4f, 5, 1.1f, 3, 50, startLevel) { }

    public override string EffectName => "Population";

    public override void ApplyEffect(Location location)
    {
        location.Population += GetCurrentEffect();
    }

    public override void PerformAction(Location location)
    {
        Debug.Log("You go on a hunt, gathering food and gaining prestige!");
    }
}
