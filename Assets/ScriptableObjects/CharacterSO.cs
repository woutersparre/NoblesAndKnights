using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
[CreateAssetMenu(fileName = "Character", menuName = "Scriptable Objects/Character")]
public class CharacterSO : ScriptableObject
{
    public string CharacterName;
    public int age = 0;
    public Sprite Portrait;
    public CharacterSO Spouse;
    public float Wealth = 0;

    public Gender Gender; // Add a field to store the character's gender

    public Stats Stats = new Stats(4,4,4,4,4,4);
    public List<Trait> Traits = new();
    public List<Relationship> Relations = new();

}
