using UnityEngine;

// A single placeholder action available on a council role slot.
// Only deployment actions are functional for now; the rest are no-ops.
[System.Serializable]
public class CouncilRoleAction
{
    public string Name;
    public string Description;
    public CouncilRole Role;
    public bool IsDeployment;
    public int DurationDays; // <= 0 means the deployment stays active until cancelled

    public CouncilRoleAction(string name, string description, CouncilRole role, bool isDeployment = false, int durationDays = 0)
    {
        Name = name;
        Description = description;
        Role = role;
        IsDeployment = isDeployment;
        DurationDays = durationDays;
    }

    public bool Is(CouncilRoleAction other)
    {
        return other != null && other.Name == Name;
    }
}