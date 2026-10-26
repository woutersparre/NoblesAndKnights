using System.Collections.Generic;
using UnityEngine;

public enum CouncilRole
{
    None,
    Diplomat,
    Marshall,
    Steward,
    Spymaster,
    CourtPriest
}

public static class CouncilRoleInfo
{
    public static Stats.StatType GetStatType(CouncilRole role)
    {
        switch (role)
        {
            case CouncilRole.Diplomat:     return Stats.StatType.Diplomacy;
            case CouncilRole.Marshall:     return Stats.StatType.Martial;
            case CouncilRole.Steward:      return Stats.StatType.Stewardship;
            case CouncilRole.Spymaster:    return Stats.StatType.Intrigue;
            case CouncilRole.CourtPriest:  return Stats.StatType.Learning;
            default:                       return Stats.StatType.Diplomacy;
        }
    }

    public static string GetDisplayName(CouncilRole role)
    {
        switch (role)
        {
            case CouncilRole.Diplomat:     return "Diplomat";
            case CouncilRole.Marshall:     return "Marshall";
            case CouncilRole.Steward:      return "Steward";
            case CouncilRole.Spymaster:    return "Spymaster";
            case CouncilRole.CourtPriest:  return "Court Priest";
            default:                       return "Empty Slot";
        }
    }

    public static CouncilRole[] AllRoles => new CouncilRole[]
    {
        CouncilRole.Diplomat,
        CouncilRole.Marshall,
        CouncilRole.Steward,
        CouncilRole.Spymaster,
        CouncilRole.CourtPriest
    };

    // Roles the ruler may assign. Per-role unlock conditions will live here.
    public static List<CouncilRole> GetUnlockedRoles(Character ruler)
    {
        List<CouncilRole> unlocked = new List<CouncilRole>();
        foreach (CouncilRole role in AllRoles)
        {
            if (IsUnlocked(role, ruler))
                unlocked.Add(role);
        }
        return unlocked;
    }

    public static bool IsUnlocked(CouncilRole role, Character ruler)
    {
        return role != CouncilRole.None;
    }

    // Whether a candidate may fill the role. Refine later with stats/traits/status.
    public static bool IsEligible(CouncilRole role, Character ruler, Character candidate)
    {
        if (role == CouncilRole.None || candidate == null || candidate == ruler) return false;
        return true;
    }

    // Whether the ruler already has a councilor appointed to this role
    public static bool IsRoleInUse(CouncilRole role, Character ruler)
    {
        if (ruler == null || ruler.Council == null) return false;
        foreach (var position in ruler.Council)
        {
            if (position.Role == role)
                return true;
        }
        return false;
    }

    // The placeholder deployment action available for a role. Deploys the councilor
    // to a chosen map location; it stays active until the timer runs out or it is cancelled.
    public static List<CouncilRoleAction> GetPlaceholderActions(CouncilRole role)
    {
        switch (role)
        {
            case CouncilRole.Diplomat:
                return new List<CouncilRoleAction>
                {
                    new CouncilRoleAction("Oversee Diplomatic Mission", "Deploy the diplomat to oversee a diplomatic mission.", role, true, 15)
                };

            case CouncilRole.Marshall:
                return new List<CouncilRoleAction>
                {
                    new CouncilRoleAction("Command Local Garrison", "Deploy the marshall to command a local garrison.", role, true, 20)
                };

            case CouncilRole.Steward:
                return new List<CouncilRoleAction>
                {
                    new CouncilRoleAction("Oversee Development", "Deploy the steward to oversee local development.", role, true, 25)
                };

            case CouncilRole.Spymaster:
                return new List<CouncilRoleAction>
                {
                    new CouncilRoleAction("Run Local Spy Network", "Deploy the spymaster to run a local spy network.", role, true, 12)
                };

            case CouncilRole.CourtPriest:
                return new List<CouncilRoleAction>
                {
                    new CouncilRoleAction("Minister at Pilgrimage Site", "Deploy the court priest to minister at a pilgrimage site.", role, true, 18)
                };

            default:
                return new List<CouncilRoleAction>();
        }
    }

    // Tint used for the map indicator of a deployed councilor.
    public static Color GetRoleColor(CouncilRole role)
    {
        switch (role)
        {
            case CouncilRole.Diplomat:    return new Color(0.20f, 0.70f, 1.00f); // blue
            case CouncilRole.Marshall:    return new Color(0.90f, 0.25f, 0.20f); // red
            case CouncilRole.Steward:     return new Color(0.85f, 0.75f, 0.20f); // gold
            case CouncilRole.Spymaster:   return new Color(0.55f, 0.35f, 0.90f); // purple
            case CouncilRole.CourtPriest: return new Color(0.95f, 0.60f, 0.30f); // orange
            default:                      return Color.white;
        }
    }
}
