using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Trait", menuName = "Scriptable Objects/Trait")]
public class Trait : ScriptableObject
{
    public Sprite Icon;
    public GameObject Prefab;
    // The value of the modifier that affects the character's stats (e.g., health, strength, etc.)
    public List<Modifier> modifiers = new List<Modifier>();
    public string Description;
    public int OpinionModifier;
    public Trait OppositeTrait;
    public int OpinionOfOppositeTrait;

    // Dormant: reserved for health-affecting traits (Stressed, Wounded, Strong...).
    // No system reads this yet; the natural-death roll will include it once trait
    // health modifiers are introduced.
    public float healthModifier = 0f;
}




public enum ModifierType { Diplomacy, Martial, Stewardship, Intrigue, Learning ,CombatStrengh }

[System.Serializable]
public class Modifier
{
    public float modifierValue; 
    public ModifierType modifierType;
}
