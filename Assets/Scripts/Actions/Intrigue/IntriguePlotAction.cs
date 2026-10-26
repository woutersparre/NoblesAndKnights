using UnityEngine;

/// <summary>
/// Placeholder plot action used by the Intrigue screen. Each instance represents one
/// CK2-style plot/scheme (assassinate, fabricate claim, ...) as a registered action
/// in the existing action system. These are intentionally lightweight: they declare
/// the intrigue requirement, time cost and risk, but do not yet resolve against a
/// specific character target. Targeting, backers and plot power come in later phases.
/// </summary>
public class IntriguePlotAction : BaseAction
{
    private readonly string actionName;
    private readonly string actionDescription;
    private readonly int requiredIntrigue;
    private readonly float wealthCost;
    private readonly float timeDays;
    private readonly float riskChance;

    public IntriguePlotAction(string name, string description,
        int requiredIntrigue = 0, float wealthCost = 0f,
        float timeDays = 7f, float riskChance = 0.25f)
    {
        actionName = name;
        actionDescription = description;
        this.requiredIntrigue = requiredIntrigue;
        this.wealthCost = wealthCost;
        this.timeDays = timeDays;
        this.riskChance = riskChance;
    }

    public override string Name => actionName;
    public override string Description => actionDescription;
    public override ActionType Type => ActionType.Intrigue;
    public override bool IsRepeatable => true;
    public override DiscoveryVisibility Visibility => DiscoveryVisibility.Any;

    public override ActionCost GetCost(Character character, Location location)
    {
        return new ActionCost
        {
            WealthCost = wealthCost,
            RequiredIntrigue = requiredIntrigue,
            TimeCostDays = Mathf.Max(1, Mathf.RoundToInt(timeDays))
        };
    }

    public override ActionReward GetReward(Character character, Location location)
    {
        return new ActionReward
        {
            IntrigueExp = 2f,
            ReknownGain = 1f
        };
    }

    public override ActionRisk GetRisk(Character character, Location location)
    {
        return new ActionRisk
        {
            HasRisk = riskChance > 0f,
            FailureChance = Mathf.Clamp01(riskChance),
            ReknownLoss = 5f,
            WealthLoss = wealthCost
        };
    }
}