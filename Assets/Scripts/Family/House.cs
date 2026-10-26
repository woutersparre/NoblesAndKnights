using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class House
{
    public string Name;
    public string Motto;
    public string Sigil;
    [System.NonSerialized]
    public List<Character> Members = new();
    [System.NonSerialized]
    public Character Founder;
    [System.NonSerialized]
    public static List<House> AllHouses = new();

    public House(string name, string motto, string sigil, Character founder)
    {
        AllHouses.Add(this);
        Name = name;
        Motto = motto;
        Sigil = sigil;
        Founder = founder;
        Members.Add(founder);
    }

    public void AddMember(Character newMember)
    {
        if(Members.Contains(newMember)) return;
        Members.Add(newMember);
    }

}
