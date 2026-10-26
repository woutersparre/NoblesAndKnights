using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Religion
{
    public string Name;
    public string Deity;
    public List<Location> HolySites = new List<Location>(); // Make this public
    public bool AllowsPolygamy;
    public bool IsOrganized;
    public static List<Religion> AllReligions = new();
    
    [Header("⚡ Religious Spread Properties")]
    public bool isMissionary = false; // Actively seeks converts
    public bool isMilitant = false; // Uses force for conversion
    public bool isPeaceful = false; // Resists forced conversion
    public bool isExclusive = false; // No syncretism allowed
    public float conversionStrength = 1.0f; // Base conversion power
    public float holySiteInfluenceRadius = 50f; // Influence from holy sites
    
    [Header("🏞️ Terrain Religious Affinity")]
    public bool prefersMountains = false; // Thrives in high elevations
    public bool prefersForests = false; // Sacred groves and nature
    public bool prefersDeserts = false; // Desert spirituality
    public bool prefersCoasts = false; // Maritime religious practices
    public bool prefersPlains = false; // Open field worship
    
    [Header("🌍 Dynamic Properties")]
    [SerializeReference] public List<Religion> parentReligions = new List<Religion>(); // Religious ancestors
    [SerializeReference] public List<Religion> heresies = new List<Religion>(); // Religious variants
    [SerializeReference] public List<Religion> schisms = new List<Religion>(); // Religious splits
    [SerializeReference] public Dictionary<Religion, float> religiousRelations = new Dictionary<Religion, float>(); // Inter-religious relations
    public float age = 0f; // Years since founding
    public bool hasReformed = false; // Has undergone reformation
    
    [Header("⚔️ Religious Conflict Properties")]
    public bool isCrusading = false; // Engages in holy wars
    public bool isPersecuting = false; // Actively persecutes other religions
    public float religiousTolerance = 0.5f; // 0 = intolerant, 1 = very tolerant
    [SerializeReference] public List<Religion> enemyReligions = new List<Religion>(); // Religious enemies
    
    [Header("🛐 Spiritual Properties")]
    public bool hasAfterlife = false; // Belief in afterlife affects conversion
    public bool hasMiracles = false; // Miracle claims increase conversion
    public bool hasProphecy = false; // Prophetic claims increase influence
    public float spiritualAuthority = 1.0f; // Religious authority level

    public Religion(string name, string deity = "God", List<Location> holySites = null, bool allowsPolygamy = false, bool isOrganized = true)
    {
        AllReligions.Add(this);
        Name = name;
        Deity = deity;
        HolySites = holySites ?? new List<Location>();
        AllowsPolygamy = allowsPolygamy;
        IsOrganized = isOrganized;
        
        // Set default traits based on religion type
        SetDefaultTraits();
    }
    
    private void SetDefaultTraits()
    {
        switch (Name)
        {
            case "Sun Worship":
                prefersDeserts = true;
                prefersMountains = true;
                prefersCoasts = true;
                isMissionary = true;
                hasAfterlife = true;
                hasMiracles = true;
                conversionStrength = 1.2f;
                break;
            case "Moon Cult":
                prefersCoasts = true;
                prefersForests = true;
                isPeaceful = true;
                hasProphecy = true;
                spiritualAuthority = 1.3f;
                religiousTolerance = 0.7f;
                break;
            case "Druidic Order":
                prefersForests = true;
                prefersPlains = true;
                isPeaceful = true;
                isExclusive = true;
                hasMiracles = true;
                conversionStrength = 0.8f;
                break;
            case "Blood Pact":
                prefersMountains = true;
                isMilitant = true;
                isCrusading = true;
                isPersecuting = true;
                religiousTolerance = 0.1f;
                conversionStrength = 1.5f;
                break;
        }
    }
    
    public float GetTerrainCompatibility(TerrainType terrain)
    {
        float compatibility = 0f;
        
        if (prefersMountains && terrain == TerrainType.Mountain)
            compatibility += 0.4f;
        if (prefersForests && (terrain == TerrainType.Forest || terrain == TerrainType.Rainforest))
            compatibility += 0.4f;
        if (prefersDeserts && terrain == TerrainType.Desert)
            compatibility += 0.4f;
        if (prefersCoasts && IsCoastalTerrain(terrain))
            compatibility += 0.3f;
        if (prefersPlains && (terrain == TerrainType.Grassland || terrain == TerrainType.Savanna))
            compatibility += 0.3f;
        
        // Penalties for incompatible terrain
        if (prefersForests && terrain == TerrainType.Desert)
            compatibility -= 0.5f;
        if (prefersDeserts && terrain == TerrainType.Forest)
            compatibility -= 0.5f;
        if (prefersMountains && terrain == TerrainType.Wetland)
            compatibility -= 0.3f;
        
        return Mathf.Clamp(compatibility, -0.6f, 0.6f);
    }
    
    private bool IsCoastalTerrain(TerrainType terrain)
    {
        return terrain == TerrainType.Ocean || terrain == TerrainType.LargeLake;
    }
    
    public float GetReligiousRelation(Religion other)
    {
        // Check predefined relations
        if (religiousRelations.TryGetValue(other, out float relation))
            return relation;
        
        // Enemy religions
        if (enemyReligions.Contains(other))
            return -1.0f; // Hostile
        
        // Parent/child relationships
        if (parentReligions.Contains(other) || schisms.Contains(other))
            return 0.6f; // Related but different
        
        // Heresies
        if (heresies.Contains(other))
            return -0.8f; // Very hostile to heresies
        
        // Same deity family
        if (IsDeityRelated(other))
            return 0.4f; // Moderately compatible
        
        // Exclusive religions
        if (isExclusive || other.isExclusive)
            return -0.5f; // Low tolerance
        
        return 0.1f; // Default neutral
    }
    
    private bool IsDeityRelated(Religion other)
    {
        // Check if deities are from same pantheon/family
        return (Deity.Contains("Sun") && other.Deity.Contains("Sun")) ||
               (Deity.Contains("Moon") && other.Deity.Contains("Moon")) ||
               (Deity.Contains("Nature") && other.Deity.Contains("Nature")) ||
               (Deity.Contains("Blood") && other.Deity.Contains("Blood"));
    }
    
    public void CreateHeresy(string heresyName, Vector2 location)
    {
        var heresy = new Religion(heresyName, $"{Deity} (Heresy)");
        heresy.parentReligions.Add(this);
        heresies.Add(heresy);
        
        // Heresies inherit some traits but modify others
        heresy.isMilitant = isMilitant && Random.value < 0.7f;
        heresy.isMissionary = isMissionary && Random.value < 0.5f;
        heresy.conversionStrength = conversionStrength * 0.8f;
        
        Debug.Log($"Heresy created: {heresyName} from {Name} at {location}");
    }
    
    public void CreateSchism(string schismName, Vector2 location)
    {
        var schism = new Religion(schismName, Deity);
        schism.parentReligions.Add(this);
        schisms.Add(schism);
        
        // Schisms maintain most traits but have different interpretations
        schism.isMilitant = isMilitant;
        schism.isMissionary = isMissionary;
        schism.conversionStrength = conversionStrength;
        schism.religiousTolerance = religiousTolerance * 0.8f; // Less tolerant
        
        // Add to enemy religions (mutual hostility)
        enemyReligions.Add(schism);
        schism.enemyReligions.Add(this);
        
        Debug.Log($"Schism created: {schismName} from {Name} at {location}");
    }
    
    public void ReformReligion(string reformationAspect)
    {
        hasReformed = true;
        
        // Create reformed version
        var reformedReligion = new Religion($"{Name} (Reformed)", Deity);
        reformedReligion.parentReligions.Add(this);
        
        // Reformed religions often become more tolerant or change practices
        reformedReligion.religiousTolerance = religiousTolerance * 1.5f;
        reformedReligion.isPeaceful = isPeaceful || Random.value < 0.3f;
        reformedReligion.conversionStrength = conversionStrength * 1.2f;
        
        Debug.Log($"Religion reformed: {Name} → {reformedReligion.Name}");
    }
    
    public void DeclareHolyWar(Religion target, Vector2 epicenter)
    {
        if (!isCrusading) return;
        
        enemyReligions.Add(target);
        target.enemyReligions.Add(this);
        
        // Create holy war event
        Debug.Log($"Holy War declared: {Name} vs {target.Name} from {epicenter}");
    }
    
    public float CalculateConversionPower(Province province, TerrainType terrain)
    {
        float power = conversionStrength;
        
        // Terrain affinity
        power *= (1f + GetTerrainCompatibility(terrain));
        
        // Holy site proximity
        if (HolySites.Count > 0)
        {
            float nearestHolySiteDistance = HolySites
                .Select(site => Vector2.Distance(site.Position, province.Center))
                .Min();
            
            if (nearestHolySiteDistance < holySiteInfluenceRadius)
            {
                power *= (1f + (1f - nearestHolySiteDistance / holySiteInfluenceRadius));
            }
        }
        
        // Spiritual authority
        power *= spiritualAuthority;
        
        // Organized religion bonus
        if (IsOrganized)
            power *= 1.2f;
        
        // Missionary bonus
        if (isMissionary)
            power *= 1.5f;
        
        return power;
    }
}