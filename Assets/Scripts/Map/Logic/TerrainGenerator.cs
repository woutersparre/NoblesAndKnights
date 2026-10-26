using UnityEngine;
using System.Collections.Generic;

public enum TerrainType
    {
        // Whittaker-inspired basic biomes
        Ocean,          // Water (saltwater)
        LargeLake,      // Large freshwater body (non-traversable like ocean)
        Ice,            // Frozen/ice caps
        Tundra,          // Cold treeless
        Taiga,           // Cold forest
        Desert,          // Hot dry
        Savanna,         // Hot seasonal
        Grassland,        // Temperate grass
        Forest,           // Temperate forest
        Rainforest,       // Tropical wet
        Mountain,         // High elevation
        Wetland          // Marsh/swamp
    }

    [System.Serializable]
    public class TerrainData
    {
        public TerrainType terrainType;
        public Color color;

        public TerrainData(TerrainType type, Color col)
        {
            terrainType = type;
            color = col;
        }
    }

    public class TerrainGenerator
    {
        [System.Serializable]
        public class TerrainSettings
        {
            [Header("Water Level Parameters")]
            [Tooltip("Sea level threshold - below this becomes ocean")]
            public float seaLevel = 0.15f;
            
            [Tooltip("Polar ice cap threshold for highest elevations at cold temperatures")]
            public float polarThreshold = 0.85f;
            
            [Header("Mountain Formation Parameters")]
            [Tooltip("Minimum elevation for mountain terrain")]
            public float mountainThreshold = 0.7f;
            
            [Tooltip("Elevation noise scale - SMALLER = LARGER mountain ranges")]
            [Range(0.01f, 0.1f)]
            public float elevationScale = 0.05f;
            
            [Tooltip("Mountain range coherence - higher creates more connected mountains")]
            [Range(0.1f, 0.5f)]
            public float mountainCoherence = 0.3f;
            
            [Header("Climate Parameters")]
            [Tooltip("Moisture noise scale")]
            public float moistureScale = 0.08f;
            [Tooltip("General Moisture multiplier")]
            public float moistureLevel = 0.7f;
            [Tooltip("Temperature noise scale")]
            public float temperatureScale = 0.06f;
            

        }

        private static readonly Dictionary<TerrainType, TerrainData> terrainLookup = new Dictionary<TerrainType, TerrainData>
        {
            // Basic terrain definitions following Whittaker logic
            { TerrainType.Ocean, new TerrainData(TerrainType.Ocean, new Color(0.1f, 0.3f, 0.6f, 1f))},
            { TerrainType.LargeLake, new TerrainData(TerrainType.LargeLake, new Color(0.2f, 0.4f, 0.7f, 1f)) },
            { TerrainType.Ice, new TerrainData(TerrainType.Ice, new Color(0.9f, 0.95f, 1.0f, 1f)) },
            { TerrainType.Tundra, new TerrainData(TerrainType.Tundra, new Color(0.6f, 0.7f, 0.5f, 1f)) },
            { TerrainType.Taiga, new TerrainData(TerrainType.Taiga, new Color(0.2f, 0.4f, 0.3f, 1f)) },
            { TerrainType.Desert, new TerrainData(TerrainType.Desert, new Color(0.8f, 0.7f, 0.5f, 1f)) },
            { TerrainType.Savanna, new TerrainData(TerrainType.Savanna, new Color(0.7f, 0.6f, 0.3f, 1f)) },
            { TerrainType.Grassland, new TerrainData(TerrainType.Grassland, new Color(0.4f, 0.6f, 0.3f, 1f)) },
            { TerrainType.Forest, new TerrainData(TerrainType.Forest, new Color(0.2f, 0.5f, 0.2f, 1f)) },
            { TerrainType.Rainforest, new TerrainData(TerrainType.Rainforest, new Color(0.1f, 0.4f, 0.1f, 1f)) },
            { TerrainType.Mountain, new TerrainData(TerrainType.Mountain, new Color(0.5f, 0.5f, 0.5f, 1f)) },
            { TerrainType.Wetland, new TerrainData(TerrainType.Wetland, new Color(0.3f, 0.5f, 0.4f, 1f)) }
        };

        private TerrainSettings settings;
        
        // Random offsets for unique terrain patterns each generation
        private readonly Vector2 randomElevationOffset;
        private readonly Vector2 randomMoistureOffset;
        private readonly Vector2 randomTemperatureOffset;

        public TerrainGenerator(TerrainSettings terrainSettings)
        {
            settings = terrainSettings;
            
            // Generate random offsets for unique terrain patterns each generation
            randomElevationOffset = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            randomMoistureOffset = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));
            randomTemperatureOffset = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));
        }

        public TerrainType GenerateTerrain(Vector2 position)
        {
            // Generate Whittaker-style parameters
            float elevation = GetElevationAt(position);
            float moisture = GetMoistureAt(position);
            float temperature = GetTemperatureAt(position);

            return DetermineWhittakerBiome(elevation, moisture, temperature);
        }

        public float GetElevationAt(Vector2 position)
        {
            // Primary elevation noise
            float primaryElevation = Mathf.PerlinNoise(
                position.x * settings.elevationScale + randomElevationOffset.x,
                position.y * settings.elevationScale + randomElevationOffset.y
            );
            
            // Secondary noise for mountain coherence (creates connected mountain ranges)
            float mountainNoise = Mathf.PerlinNoise(
                position.x * settings.elevationScale * 0.5f + randomElevationOffset.x * 2f,
                position.y * settings.elevationScale * 0.5f + randomElevationOffset.y * 2f
            );
            
            // Blend primary and mountain noise using coherence parameter
            float blendedElevation = Mathf.Lerp(primaryElevation, mountainNoise, settings.mountainCoherence);
            
            // Normalize to 0-1 range
            return Mathf.Clamp01(blendedElevation);
        }

        public float GetMoistureAt(Vector2 position)
        {
            // Simple Perlin noise for moisture with less range
            float baseMoisture = Mathf.PerlinNoise(
                position.x * settings.moistureScale + randomMoistureOffset.x,
                position.y * settings.moistureScale + randomMoistureOffset.y
            );
            
            // Normalize to 0-1 range, but bias toward dryness
            return Mathf.Clamp01(baseMoisture * settings.moistureLevel);
        }

        private float GetTemperatureAt(Vector2 position)
        {
            // Temperature decreases with latitude (Y position)
            float latitudeTemperature = 1f - Mathf.Abs(position.y) / (MapManager.instance ? MapManager.instance.map.MapSize / 2f : 100f);
            
            // Add noise variation
            float noiseVariation = Mathf.PerlinNoise(
                position.x * settings.temperatureScale + randomTemperatureOffset.x,
                position.y * settings.temperatureScale + randomTemperatureOffset.y
            );
            
            // Combine latitude base with noise
            float temperature = latitudeTemperature * 0.7f + Mathf.Clamp01(noiseVariation) * 0.3f;
            
            return Mathf.Clamp01(temperature);
        }

        private TerrainType DetermineWhittakerBiome(float elevation, float moisture, float temperature)
        {
            // Ocean check - lowest elevations
            if (elevation < settings.seaLevel)
                return TerrainType.Ocean;

            // Polar/ice check - highest latitudes with low temperature
            if (temperature < 0.1f)
            {
                if (elevation > settings.polarThreshold)
                    return TerrainType.Ice;
                return TerrainType.Tundra;
            }

            // Mountain check - highest elevations
            if (elevation > settings.mountainThreshold)
                return TerrainType.Mountain;

            // Whittaker-style biome determination
            if (temperature < 0.3f)
            {
                // Cold climates
                if (moisture < 0.3f) return TerrainType.Tundra;
                return TerrainType.Taiga;
            }
            else if (temperature < 0.75f)
            {
                // Temperate climates
                if (moisture < 0.3f) return TerrainType.Grassland;
                if (moisture < 0.7f) return TerrainType.Forest;
                return TerrainType.Wetland;
            }
            else
            {
                // Hot climates
                if (moisture < 0.3f) return TerrainType.Desert;
                if (moisture < 0.7f) return TerrainType.Savanna;
                return TerrainType.Rainforest;
            }
        }

        public static TerrainData GetTerrainData(TerrainType terrainType)
        {
            return terrainLookup.TryGetValue(terrainType, out var data) ? data : terrainLookup[TerrainType.Grassland];
        }
    }