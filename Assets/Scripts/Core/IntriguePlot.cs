using System.Collections.Generic;

/// <summary>
/// Placeholder data model for a CK2-style plot/scheme. Intentionally minimal:
/// fields are public and default-safe so they can be added to Character later
/// without breaking saves. Targeting, backers and resolution arrive in later phases.
/// </summary>
[System.Serializable]
public class IntriguePlot
{
    public string Name;
    public Character Plotter;
    public Character Target;
    public ActionType Type = ActionType.Intrigue;

    public List<Character> Backers = new List<Character>();
    public float PlotPower = 0f;
    public float ProgressDays = 0f;
    public float RequiredDays = 14f;
    public float Suspicion = 0f;

    public bool IsActive = false;
    public bool IsReady = false;
    public bool IsDiscovered = false;
    public bool IsComplete = false;

    public IntriguePlot() { }

    public IntriguePlot(string name, Character plotter, Character target)
    {
        Name = name;
        Plotter = plotter;
        Target = target;
        IsActive = true;
    }
}

/// <summary>
/// Placeholder for a piece of leverage/secret held about a character. Source records
/// how the leverage was gained (friend, servant, rival, creditor, physician, lover).
/// </summary>
[System.Serializable]
public class Secret
{
    public Character Owner;
    public Character KnownBy;
    public string Description;
    public string Source; // e.g. "friend", "servant", "rival", "creditor", "physician", "lover"
    public float Strength = 1f;

    public Secret() { }

    public Secret(Character owner, Character knownBy, string description, string source)
    {
        Owner = owner;
        KnownBy = knownBy;
        Description = description;
        Source = source;
    }
}