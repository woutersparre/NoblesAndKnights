using UnityEngine;

/// <summary>
/// Generic placeholder action for a location discovery tier. A single class backs a
/// per-location-type surface (explore) and deep action, configured via its constructor
/// so we don't need twenty near-identical action classes.
///
/// Surface actions: visible while the location is visited but not yet explored.
/// Deep actions:    visible only after the location has been explored.
/// </summary>
public class PlaceholderLocationAction : BaseAction
{
    private string actionName;
    private string actionDescription;
    private LocationType[] validTypes;
    private DiscoveryVisibility visibility;
    private ActionType actionType;
    private float wealthCost;
    private float rewardWealth;
    private float rewardRenown;
    private float riskChance;

    public override string Name => actionName;
    public override string Description => actionDescription;
    public override LocationType[] ValidLocationTypes => validTypes;
    public override DiscoveryVisibility Visibility => visibility;
    public override ActionType Type => actionType;

    public PlaceholderLocationAction(string name, string description, LocationType type, DiscoveryVisibility visibility,
        ActionType actionType, float wealthCost = 0f, float rewardWealth = 30f, float rewardRenown = 5f, float riskChance = 0.1f)
    {
        actionName = name;
        actionDescription = description;
        validTypes = new LocationType[] { type };
        this.visibility = visibility;
        this.actionType = actionType;
        this.wealthCost = wealthCost;
        this.rewardWealth = rewardWealth;
        this.rewardRenown = rewardRenown;
        this.riskChance = riskChance;
    }

    public override ActionCost GetCost(Character character, Location location)
    {
        return new ActionCost
        {
            WealthCost = wealthCost,
            TimeCostDays = 1
        };
    }

    public override ActionReward GetReward(Character character, Location location)
    {
        return new ActionReward
        {
            WealthGain = Random.Range(rewardWealth * 0.5f, rewardWealth),
            ReknownGain = rewardRenown
        };
    }

    public override ActionRisk GetRisk(Character character, Location location)
    {
        return new ActionRisk
        {
            HasRisk = riskChance > 0f,
            FailureChance = riskChance,
            WealthLoss = wealthCost
        };
    }

    public override void OnExecuteComplete(Character character, Location location, ActionResult result)
    {
        // A successful surface (explore) action reveals the deep action at this location.
        if (result.Success && location != null && visibility == DiscoveryVisibility.Visited)
        {
            location.Explore(character);
        }
    }
}

/// <summary>
/// Builds and exposes the surface (explore) and deep placeholder actions for every location type.
/// </summary>
public static class LocationActionFactory
{
    // Registers a surface (explore) and deep placeholder action for every location type.
    public static void AddAll(ActionManager manager)
    {
        // (Type, surfaceName, surfaceDesc, deepName, deepDesc, actionType)
        var defs = new (LocationType, string, string, string, string, ActionType)[]
        {
            (LocationType.Castle, "Survey the Court", "Inspect this castle's halls, garrison, and holdings.",
                "Uncover Court Secrets", "Dig into the castle's intrigue and covert bargains.", ActionType.Intrigue),
            (LocationType.City, "Tour the Market", "Walk the bustling market stalls and guild halls.",
                "Strike a Trade Deal", "Negotiate a profitable arrangement with the city's merchants.", ActionType.Economic),
            (LocationType.Church, "Visit the Sanctuary", "Pay respects within the sanctuary and its quiet cloisters.",
                "Unearth a Lost Relic", "Search the catacombs and reliquaries for a forgotten relic.", ActionType.Religious),
            (LocationType.Ruin, "Explore Ruins", "Explore the crumbling remnants and uncover their past.",
                "Excavate the Ruins", "Dig deep to recover valuable artifacts from beneath the rubble.", ActionType.Exploration),
            (LocationType.Cave, "Explore Cave", "Scout the cave's passages and chambers.",
                "Plunder the Cave Depths", "Venture into the mapped depths to carry away anything of value.", ActionType.Exploration),
            (LocationType.Oasis, "Scout the Oasis", "Survey the water, foliage, and travellers at this oasis.",
                "Find the Hidden Spring", "Search for a concealed spring or a traveller's buried cache.", ActionType.Exploration),
            (LocationType.Village, "Visit the Village", "Meet the villagers and learn of their daily life.",
                "Search the Storehouses", "Go through the village's granaries and storehouses for surplus.", ActionType.Economic),
            (LocationType.Temple, "Seek an Audience", "Request an audience with the temple's keepers.",
                "Read the Ancient Scripts", "Study the temple's fragile scrolls and carvings for lore.", ActionType.Learning),
            (LocationType.Mine, "Survey the Mine", "Inspect the mine's shafts, supports, and yield.",
                "Find the Rich Vein", "Prospect for a richer ore seam hidden in the depths.", ActionType.Economic),
            (LocationType.Camp, "Approach the Camp", "Approach the camp and gauge its occupants.",
                "Relieve the Watch", "Gain access to the camp's stores by way of the night watch.", ActionType.Intrigue),
            (LocationType.Lake, "Survey the Lake", "Scan the water's edge and surrounding shores.",
                "Drag the Lakebed", "Drag the lakebed in search of something worth salvaging.", ActionType.Exploration)
        };

        foreach (var (type, surfName, surfDesc, deepName, deepDesc, actType) in defs)
        {
            // Surface (explore) action - visible while visited but not explored; reveals the deep action on success
            manager.RegisterAction(new PlaceholderLocationAction(
                surfName, surfDesc, type, DiscoveryVisibility.Visited, actType));

            // Deep action - only visible once the location is explored
            manager.RegisterAction(new PlaceholderLocationAction(
                deepName, deepDesc, type, DiscoveryVisibility.Explored, actType, wealthCost: 5f, rewardWealth: 50f, rewardRenown: 8f, riskChance: 0.2f));
        }
    }
}
