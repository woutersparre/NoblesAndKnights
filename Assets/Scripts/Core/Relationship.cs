[System.Serializable]
public class Relationship
{
    public bool CreateTarget = false;
    public CharacterSO targetSO;
    public Character TargetCharacter; // The character this relationship points to.
    public RelationshipType Type;    // The type of relationship (e.g., friend, rival, spouse).
    public int Strength;             // A value representing the relationship's strength (-100 to 100, for example).
    public string History;           // Optional: Notes or history of the relationship.

    public Relationship(Character target, RelationshipType type, int strength = 0)
    {
        TargetCharacter = target;
        Type = type;
        Strength = strength;
    }
    public void UpdateTargetFromSO()
    {
        if (targetSO != null)
        {
            TargetCharacter = MapManager.instance.characters.allCharacters.Find(c => c.Name == targetSO.CharacterName);
            TargetCharacter ??= new(SO: targetSO);
        }
    }
}

public enum RelationshipType
{
    Friend,
    Rival,
    Spouse,
    Father, 
    Mother,
    Daughter,
    Son
}