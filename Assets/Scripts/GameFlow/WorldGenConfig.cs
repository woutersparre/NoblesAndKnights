using System;
using UnityEngine;

// Serializable set of parameters used to generate a new world. The main menu
// fills one of these in, stores it on GameSession, and the game scene applies it
// to the MapManager before generation starts.
//
// The defaults mirror the values currently serialized on the NewMapTest scene so
// that a world generated from the menu matches the world the scene produces when
// played directly.
[Serializable]
public class WorldGenConfig
{
    public bool GenerateIsland = false;
    public float MapSize = 300f;
    public float CellSize = 10f;
    public bool CellsFromCircumcenters = false;

    // Cell edge shape choice. Drives the whole geometry pipeline (mesh, picking,
    // borders and border loops all follow the same processed polygon).
    public bool NoisedCellEdges = true;

    public float SeaLevel = 0.25f;
    public float PolarThreshold = 0.85f;

    public float MountainThreshold = 0.7f;
    public float ElevationScale = 0.05f;
    public float MountainCoherence = 0.3f;

    public float MoistureScale = 0.08f;
    public float MoistureLevel = 0.7f;
    public float TemperatureScale = 0.06f;

    public static WorldGenConfig CreateDefault() => new WorldGenConfig();

    public WorldGenConfig Clone() => (WorldGenConfig)MemberwiseClone();

    // Pushes these values onto the live map generation objects.
    public void Apply(MapManager manager)
    {
        if (manager == null)
            return;

        manager.GenerateIsland = GenerateIsland;

        if (manager.map == null)
            manager.map = new NewMapGen();

        manager.map.MapSize = Mathf.Max(50f, MapSize);
        manager.map.CellSize = Mathf.Max(1f, CellSize);
        manager.map.CellsFromCircumCenters = CellsFromCircumcenters;
        manager.map.EdgeStyle = NoisedCellEdges ? CellEdgeStyle.Noised : CellEdgeStyle.Straight;

        if (manager.map.terrainSettings == null)
            manager.map.terrainSettings = new TerrainGenerator.TerrainSettings();

        TerrainGenerator.TerrainSettings terrain = manager.map.terrainSettings;
        terrain.seaLevel = Mathf.Clamp(SeaLevel, 0f, 0.9f);
        terrain.polarThreshold = Mathf.Clamp(PolarThreshold, 0.5f, 1f);
        terrain.mountainThreshold = Mathf.Clamp(MountainThreshold, 0.3f, 0.95f);
        terrain.elevationScale = Mathf.Clamp(ElevationScale, 0.01f, 0.1f);
        terrain.mountainCoherence = Mathf.Clamp(MountainCoherence, 0.1f, 0.5f);
        terrain.moistureScale = Mathf.Max(0.001f, MoistureScale);
        terrain.moistureLevel = Mathf.Clamp(MoistureLevel, 0f, 1f);
        terrain.temperatureScale = Mathf.Max(0.001f, TemperatureScale);
    }

    // Reads the values currently on the map generation objects (used so the menu
    // can start from whatever the scene is configured with).
    public void CaptureFrom(MapManager manager)
    {
        if (manager == null || manager.map == null)
            return;

        GenerateIsland = manager.GenerateIsland;
        MapSize = manager.map.MapSize;
        CellSize = manager.map.CellSize;
        CellsFromCircumcenters = manager.map.CellsFromCircumCenters;
        NoisedCellEdges = manager.map.EdgeStyle == CellEdgeStyle.Noised;

        TerrainGenerator.TerrainSettings terrain = manager.map.terrainSettings;
        if (terrain == null)
            return;

        SeaLevel = terrain.seaLevel;
        PolarThreshold = terrain.polarThreshold;
        MountainThreshold = terrain.mountainThreshold;
        ElevationScale = terrain.elevationScale;
        MountainCoherence = terrain.mountainCoherence;
        MoistureScale = terrain.moistureScale;
        MoistureLevel = terrain.moistureLevel;
        TemperatureScale = terrain.temperatureScale;
    }
}
