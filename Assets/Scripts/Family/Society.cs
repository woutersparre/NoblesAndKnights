using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Society
{
    public string Name;
    public string Description;
    public Character Leader; // placeholder, assigned lazily

    public static List<Society> AllSocieties = new();

    public Society(string name, string description = "")
    {
        AllSocieties.Add(this);
        Name = name;
        Description = description;
    }
}