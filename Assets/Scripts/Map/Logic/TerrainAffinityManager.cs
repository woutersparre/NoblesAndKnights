using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TerrainAffinityManager : MonoBehaviour
{
    [Header("🌍🏞️⚔️ Predefined Terrain Affinity Configurations")]
    [SerializeField] private bool usePredefinedAffinities = true;
    [SerializeField] private TerrainAffinityPreset selectedPreset = TerrainAffinityPreset.HistoricalRealism;
    
    [Header("🎛️ Custom Terrain-Culture Affinity Matrix")]
    [SerializeField] private List<CultureTerrainAffinityOverride> customCultureAffinities = new List<CultureTerrainAffinityOverride>();
    [SerializeField] private List<ReligionTerrainAffinityOverride> customReligionAffinities = new List<ReligionTerrainAffinityOverride>();

    public enum TerrainAffinityPreset
    {
        HistoricalRealism,      // Realistic historical patterns
        FantasyMythic,        // Mythical/legendary affinities
        GameBalance,          // Competitive gameplay balance
        ExtremeDiversity,      // Maximum cultural mixing
        GeographicDeterminism   // Geography heavily determines outcomes
    }

    [System.Serializable]
    public class CultureTerrainAffinityOverride
    {
        public Culture culture;
        public TerrainType terrain;
        public float affinityBonus; // -1.0 to +1.0
        [TextArea(3, 5)] public string description;
    }

    [System.Serializable]
    public class ReligionTerrainAffinityOverride
    {
        public Religion religion;
        public TerrainType terrain;
        public float affinityBonus; // -1.0 to +1.0
        [TextArea(3, 5)] public string description;
    }

    private Dictionary<(Culture, TerrainType), float> cultureAffinityMatrix;
    private Dictionary<(Religion, TerrainType), float> religionAffinityMatrix;


    // Public method to call later when data is ready
    public void InitializeWhenReady()
    {
        if (Culture.AllCultures?.Count > 0 && Religion.AllReligions?.Count > 0)
        {
            //Debug.Log("TerrainAffinityManager initializing with " + Culture.AllCultures.Count + " cultures and " + Religion.AllReligions.Count + " religions");
            InitializeAffinityMatrices();
            ApplyPresetConfiguration();
        }
        else
        {
            Debug.LogWarning("TerrainAffinityManager: Still waiting for culture/religion data");
        }
    }

    private void InitializeAffinityMatrices()
    {
        cultureAffinityMatrix = new Dictionary<(Culture, TerrainType), float>();
        religionAffinityMatrix = new Dictionary<(Religion, TerrainType), float>();
    }

    private void ApplyPresetConfiguration()
    {
        if (!usePredefinedAffinities) return;

        switch (selectedPreset)
        {
            case TerrainAffinityPreset.HistoricalRealism:
                ApplyHistoricalRealismPreset();
                break;
            case TerrainAffinityPreset.FantasyMythic:
                ApplyFantasyMythicPreset();
                break;
            case TerrainAffinityPreset.GameBalance:
                ApplyGameBalancePreset();
                break;
            case TerrainAffinityPreset.ExtremeDiversity:
                ApplyExtremeDiversityPreset();
                break;
            case TerrainAffinityPreset.GeographicDeterminism:
                ApplyGeographicDeterminismPreset();
                break;
        }

        // Apply custom overrides
        ApplyCustomAffinities();
    }

    private void ApplyHistoricalRealismPreset()
    {
        // Realistic historical terrain-culture relationships
        var cultureAffinities = new List<(Culture, TerrainType, float)>
        {
            // Desert Tribes thrive in arid environments
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Desert Tribes"), TerrainType.Desert, 0.4f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Desert Tribes"), TerrainType.Savanna, 0.2f),
            
            // Northern Clans adapted to cold
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Northern Clans"), TerrainType.Tundra, 0.4f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Northern Clans"), TerrainType.Taiga, 0.3f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Northern Clans"), TerrainType.Mountain, 0.2f),
            
            // Forest Kin excel in wooded areas
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Forest Kin"), TerrainType.Forest, 0.4f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Forest Kin"), TerrainType.Rainforest, 0.3f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Forest Kin"), TerrainType.Wetland, 0.2f),
            
            // Warrior Nations prefer plains and coasts
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Warrior Nation"), TerrainType.Grassland, 0.3f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Warrior Nation"), TerrainType.Savanna, 0.2f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Warrior Nation"), TerrainType.Ocean, 0.3f),
            
            // Maritime adaptations
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Northern Clans"), TerrainType.Ocean, 0.1f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Warrior Nation"), TerrainType.Ocean, 0.2f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Desert Tribes"), TerrainType.Ocean, 0.1f)
        };

        foreach (var (culture, terrain, bonus) in cultureAffinities)
        {
            if (culture != null)
                cultureAffinityMatrix[(culture, terrain)] = bonus;
        }

        // Religious affinities for historical realism
        var religionAffinities = new List<(Religion, TerrainType, float)>
        {
            // Sun Worship thrives where sun dominates
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Sun Worship"), TerrainType.Desert, 0.4f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Sun Worship"), TerrainType.Savanna, 0.3f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Sun Worship"), TerrainType.Mountain, 0.3f),
            
            // Moon Cult prefers coasts and forests
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Moon Cult"), TerrainType.Ocean, 0.2f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Moon Cult"), TerrainType.Forest, 0.3f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Moon Cult"), TerrainType.Wetland, 0.2f),
            
            // Druidic Order is nature-based
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Druidic Order"), TerrainType.Forest, 0.5f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Druidic Order"), TerrainType.Rainforest, 0.4f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Druidic Order"), TerrainType.Wetland, 0.3f),
            
            // Blood Pact favors conflict zones
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Blood Pact"), TerrainType.Mountain, 0.3f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Blood Pact"), TerrainType.Grassland, 0.2f)
        };

        foreach (var (religion, terrain, bonus) in religionAffinities)
        {
            if (religion != null)
                religionAffinityMatrix[(religion, terrain)] = bonus;
        }
    }

    private void ApplyFantasyMythicPreset()
    {
        // Fantasy/mythological terrain-culture relationships
        var cultureAffinities = new List<(Culture, TerrainType, float)>
        {
            // Desert Tribes have ancient magical knowledge
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Desert Tribes"), TerrainType.Desert, 0.6f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Desert Tribes"), TerrainType.Mountain, 0.4f), // Ancient ruins
            
            // Northern Clans connected to spirits
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Northern Clans"), TerrainType.Tundra, 0.5f), // Spirit realms
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Northern Clans"), TerrainType.Taiga, 0.3f),
            
            // Forest Kin are mystical
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Forest Kin"), TerrainType.Forest, 0.6f), // Enchanted forests
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Forest Kin"), TerrainType.Rainforest, 0.5f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Forest Kin"), TerrainType.Wetland, 0.4f),
            
            // Warrior Nations have divine favor
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Warrior Nation"), TerrainType.Mountain, 0.4f), // Blessed mountains
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Warrior Nation"), TerrainType.Grassland, 0.3f)
        };

        foreach (var (culture, terrain, bonus) in cultureAffinities)
        {
            if (culture != null)
                cultureAffinityMatrix[(culture, terrain)] = bonus;
        }

        // Fantasy religious affinities
        var religionAffinities = new List<(Religion, TerrainType, float)>
        {
            // Sun Worship is strongest in fantasy
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Sun Worship"), TerrainType.Desert, 0.6f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Sun Worship"), TerrainType.Mountain, 0.5f),
            
            // Moon Cult controls magic
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Moon Cult"), TerrainType.Forest, 0.5f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Moon Cult"), TerrainType.Wetland, 0.4f),
            
            // Druidic Order controls nature magic
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Druidic Order"), TerrainType.Rainforest, 0.6f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Druidic Order"), TerrainType.Forest, 0.5f),
            
            // Blood Pact has dark magic
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Blood Pact"), TerrainType.Mountain, 0.4f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Blood Pact"), TerrainType.Wetland, 0.3f)
        };

        foreach (var (religion, terrain, bonus) in religionAffinities)
        {
            if (religion != null)
                religionAffinityMatrix[(religion, terrain)] = bonus;
        }
    }

    private void ApplyGameBalancePreset()
    {
        // Balanced for competitive gameplay
        var cultureAffinities = new List<(Culture, TerrainType, float)>
        {
            // More balanced affinities
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Desert Tribes"), TerrainType.Desert, 0.3f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Northern Clans"), TerrainType.Tundra, 0.3f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Forest Kin"), TerrainType.Forest, 0.3f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Warrior Nation"), TerrainType.Grassland, 0.3f),
            
            // Cross-terrain competence
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Desert Tribes"), TerrainType.Grassland, 0.1f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Northern Clans"), TerrainType.Forest, 0.1f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Forest Kin"), TerrainType.Mountain, 0.1f),
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Warrior Nation"), TerrainType.Desert, 0.1f)
        };

        foreach (var (culture, terrain, bonus) in cultureAffinities)
        {
            if (culture != null)
                cultureAffinityMatrix[(culture, terrain)] = bonus;
        }

        // Religious balance
        var religionAffinities = new List<(Religion, TerrainType, float)>
        {
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Sun Worship"), TerrainType.Desert, 0.3f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Moon Cult"), TerrainType.Forest, 0.3f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Druidic Order"), TerrainType.Rainforest, 0.3f),
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Blood Pact"), TerrainType.Mountain, 0.3f)
        };

        foreach (var (religion, terrain, bonus) in religionAffinities)
        {
            if (religion != null)
                religionAffinityMatrix[(religion, terrain)] = bonus;
        }
    }

    private void ApplyExtremeDiversityPreset()
    {
        // Maximum cultural mixing and adaptation
        var allTerrains = System.Enum.GetValues(typeof(TerrainType)).Cast<TerrainType>();
        var allCultures = Culture.AllCultures;
        var allReligions = Religion.AllReligions;

        // High baseline affinity for all terrain-culture combinations
        foreach (var culture in allCultures)
        {
            foreach (var terrain in allTerrains)
            {
                cultureAffinityMatrix[(culture, terrain)] = 0.2f; // All cultures moderately good everywhere
            }
        }

        // High baseline affinity for all terrain-religion combinations
        foreach (var religion in allReligions)
        {
            foreach (var terrain in allTerrains)
            {
                religionAffinityMatrix[(religion, terrain)] = 0.2f;
            }
        }
    }

    private void ApplyGeographicDeterminismPreset()
    {
        // Geography has extreme influence on culture/religion
        var cultureAffinities = new List<(Culture, TerrainType, float)>
        {
            // Extreme specialization
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Desert Tribes"), TerrainType.Desert, 0.6f), // Very high
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Desert Tribes"), TerrainType.Grassland, -0.4f), // Very poor
            
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Northern Clans"), TerrainType.Tundra, 0.6f), // Very high
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Northern Clans"), TerrainType.Forest, -0.3f), // Poor
            
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Forest Kin"), TerrainType.Forest, 0.6f), // Very high
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Forest Kin"), TerrainType.Desert, -0.5f), // Very poor
            
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Warrior Nation"), TerrainType.Grassland, 0.5f), // High
            (Culture.AllCultures.FirstOrDefault(c => c.Name == "Warrior Nation"), TerrainType.Mountain, 0.2f) // Moderate
        };

        foreach (var (culture, terrain, bonus) in cultureAffinities)
        {
            if (culture != null)
                cultureAffinityMatrix[(culture, terrain)] = bonus;
        }

        // Religious geographic determinism
        var religionAffinities = new List<(Religion, TerrainType, float)>
        {
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Sun Worship"), TerrainType.Desert, 0.6f), // Very high
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Sun Worship"), TerrainType.Forest, -0.4f), // Very poor
            
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Moon Cult"), TerrainType.Ocean, 0.5f), // High
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Moon Cult"), TerrainType.Mountain, -0.3f), // Poor
            
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Druidic Order"), TerrainType.Rainforest, 0.6f), // Very high
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Druidic Order"), TerrainType.Desert, -0.5f), // Very poor
            
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Blood Pact"), TerrainType.Mountain, 0.5f), // High
            (Religion.AllReligions.FirstOrDefault(r => r.Name == "Blood Pact"), TerrainType.Forest, -0.2f) // Poor
        };

        foreach (var (religion, terrain, bonus) in religionAffinities)
        {
            if (religion != null)
                religionAffinityMatrix[(religion, terrain)] = bonus;
        }
    }

    private void ApplyCustomAffinities()
    {
        // Apply custom culture terrain overrides
        foreach (var override_ in customCultureAffinities)
        {
            if (override_.culture != null)
                cultureAffinityMatrix[(override_.culture, override_.terrain)] = override_.affinityBonus;
        }

        // Apply custom religion terrain overrides
        foreach (var override_ in customReligionAffinities)
        {
            if (override_.religion != null)
                religionAffinityMatrix[(override_.religion, override_.terrain)] = override_.affinityBonus;
        }
    }

    public float GetCultureTerrainAffinity(Culture culture, TerrainType terrain)
    {
        if (cultureAffinityMatrix == null) return 0f;
        if (culture == null) return 0f;
        
        if (cultureAffinityMatrix.TryGetValue((culture, terrain), out float affinity))
            return affinity;
        
        return 0f; // Default neutral if not found
    }

    public float GetReligionTerrainAffinity(Religion religion, TerrainType terrain)
    {
        if (religionAffinityMatrix == null) return 0f;
        if (religion == null) return 0f;
        
        if (religionAffinityMatrix.TryGetValue((religion, terrain), out float affinity))
            return affinity;
        
        return 0f; // Default neutral if not found
    }
}