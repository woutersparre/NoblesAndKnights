using UnityEngine;

[System.Serializable]
public class CouncilPosition
{
    // The ruler this post belongs to. Set once at creation by
    // CharacterManager.CreateCouncil and never reassigned, so a position can never
    // be orphaned. Lets council UI and NPC AI resolve the owner without going
    // through CharacterManager.Player.
    public Character Owner;

    public Character Appointed;
    public CouncilRole Role;

    // Current deployment task (if any). ActionLocation mirrors the runtime-world pattern
    // of Character.CurrentLocation and is not persisted.
    public CouncilRoleAction ActiveAction;
    [System.NonSerialized]
    public Location ActionLocation;
    public int WorkingDaysLeft;

    public string PositionName => CouncilRoleInfo.GetDisplayName(Role);
    public Stats.StatType RelatedStat => CouncilRoleInfo.GetStatType(Role);

    public bool IsWorking => ActiveAction != null && ActionLocation != null;

    public CouncilPosition(CouncilRole role = CouncilRole.None, Character owner = null)
    {
        Role = role;
        Owner = owner;
    }

    public void BeginDeployment(CouncilRoleAction action, Location location)
    {
        ActiveAction = action;
        ActionLocation = location;
        WorkingDaysLeft = action != null ? action.DurationDays : 0;
    }

    public void EndDeployment()
    {
        ActiveAction = null;
        ActionLocation = null;
        WorkingDaysLeft = 0;
    }

    // True when the given action is the one this councilor is currently deployed on.
    public bool IsRunning(CouncilRoleAction action)
    {
        return IsWorking && ActiveAction != null && ActiveAction.Is(action);
    }
}
