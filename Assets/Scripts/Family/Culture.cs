using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Culture
{
    public string Name;
    public string Language;
    public List<string> Traditions = new();
    public List<string> CommonNames = new();
    public static List<Culture> AllCultures = new();
    
    [Header("🌍 Geographic Expansion Properties")]
    public bool isMaritime = false; // Spreads well by sea/coast
    public bool isMountainAdapted = false; // Thrives in mountains
    public bool isDesertAdapted = false; // Tolerates arid conditions
    public bool isForestDwelling = false; // Prefers forest environments
    public float culturalInfluenceRadius = 25f; // Base spread distance
    public float resistanceToAssimilation = 1.0f; // How hard to absorb this culture
    
    [Header("⚡ Dynamic Properties")]
    [SerializeReference] public List<Culture> parentCultures = new List<Culture>(); // Cultural ancestors
    [SerializeReference] public List<Culture> derivedCultures = new List<Culture>(); // Cultural variants
    [SerializeReference] public Dictionary<Culture, float> relationModifiers = new Dictionary<Culture, float>(); // Cultural compatibility
    public float age = 0f; // Years since culture formation
    public bool hasReformed = false; // Has undergone major changes
    
    [Header("🎯 Cultural Traits")]
    public bool isMilitaristic = false; // More likely to conquer/spread
    public bool isTradeOriented = false; // Spreads via trade routes
    public bool isReligiouslyDevout = false; // Strong religious cohesion
    public bool isIsolationist = false; // Resists foreign influence
    public float innovationLevel = 1.0f; // Affects technological/economic spread

    public Culture(string name, string language)
    {
        AllCultures.Add(this);
        Name = name;
        Language = language;
        
        // Set default traits based on culture type
        SetDefaultTraits();
    }
    
    private void SetDefaultTraits()
    {
        switch (Name)
        {
            case "Desert Tribes":
                isDesertAdapted = true;
                isMaritime = false;
                isMilitaristic = true;
                resistanceToAssimilation = 1.5f;
                break;
            case "Northern Clans":
                isMountainAdapted = true;
                isMilitaristic = true;
                resistanceToAssimilation = 1.3f;
                break;
            case "Forest Kin":
                isForestDwelling = true;
                isReligiouslyDevout = true;
                isIsolationist = true;
                resistanceToAssimilation = 1.4f;
                break;
            case "Warrior Nation":
                isMilitaristic = true;
                isTradeOriented = false;
                innovationLevel = 1.2f;
                break;
        }
    }
    
    public float GetTerrainCompatibility(TerrainType terrain)
    {
        if (!isForestDwelling && (terrain == TerrainType.Forest || terrain == TerrainType.Rainforest))
            return -0.4f; // 40% penalty
        
        if (!isDesertAdapted && terrain == TerrainType.Desert)
            return -0.5f; // 50% penalty
        
        if (!isMountainAdapted && terrain == TerrainType.Mountain)
            return -0.3f; // 30% penalty
        
        if (isMaritime && IsCoastalTerrain(terrain))
            return 0.3f; // 30% bonus
        
        return 0f; // Neutral
    }
    
    private bool IsCoastalTerrain(TerrainType terrain)
    {
        return terrain == TerrainType.Ocean || terrain == TerrainType.LargeLake;
    }
    
    public float GetCulturalCompatibility(Culture other)
    {
        // Check relation modifiers
        if (relationModifiers.TryGetValue(other, out float modifier))
            return modifier;
        
        // Parent/child relationships
        if (parentCultures.Contains(other) || derivedCultures.Contains(other))
            return 0.7f; // 70% compatibility
        
        // Same language family
        if (IsLanguageRelated(other))
            return 0.5f; // 50% compatibility
        
        return 0.1f; // 10% default compatibility
    }
    
    private bool IsLanguageRelated(Culture other)
    {
        // Simplified language relation check
        return (Language.Contains("Norse") && other.Language.Contains("Norse")) ||
               (Language.Contains("Saharan") && other.Language.Contains("Saharan")) ||
               (Language.Contains("Elvish") && other.Language.Contains("Elvish"));
    }
    
    public void AddCulturalInfluence(Culture source, float strength)
    {
        // Gradual cultural assimilation or blending
        if (source == this) return;
        
        float assimilationChance = strength / resistanceToAssimilation;
        
        if (Random.value < assimilationChance)
        {
            // Create cultural blend or complete assimilation
            if (Random.value < 0.3f) // 30% chance of blending
            {
                // Create hybrid culture
                CreateDerivedCulture(source, this);
            }
            else // 70% chance of assimilation
            {
                // Adopt some traits from source culture
                AdoptTraitsFrom(source);
            }
        }
    }
    
    private void CreateDerivedCulture(Culture parent1, Culture parent2)
    {
        string blendedName = $"{parent1.Name}-{parent2.Name}";
        string blendedLanguage = $"{parent1.Language}/{parent2.Language}";
        
        var derivedCulture = new Culture(blendedName, blendedLanguage);
        derivedCulture.parentCultures.Add(parent1);
        derivedCulture.parentCultures.Add(parent2);
        
        parent1.derivedCultures.Add(derivedCulture);
        parent2.derivedCultures.Add(derivedCulture);
        
        Debug.Log($"New blended culture formed: {blendedName}");
    }
    
    private void AdoptTraitsFrom(Culture source)
    {
        // Gradually adopt traits from dominant culture
        if (source.isMilitaristic && !isMilitaristic && Random.value < 0.1f)
        {
            isMilitaristic = true;
            Traditions.Add("Adopted martial traditions");
        }
        
        if (source.isTradeOriented && !isTradeOriented && Random.value < 0.15f)
        {
            isTradeOriented = true;
            Traditions.Add("Embraced trade practices");
        }
        
        if (Random.value < 0.05f)
        {
            // Adopt some language elements
            Language = $"{Language} (influenced by {source.Language})";
        }
    }
    
    public void ReformCulture(string newAspect)
    {
        hasReformed = true;
        Traditions.Add($"Reformed: {newAspect}");
        
        // Create derived culture representing reformed version
        var reformedCulture = new Culture($"{Name} (Reformed)", Language);
        reformedCulture.parentCultures.Add(this);
        derivedCultures.Add(reformedCulture);
        
        Debug.Log($"Culture reformed: {Name} → {reformedCulture.Name}");
    }
}
