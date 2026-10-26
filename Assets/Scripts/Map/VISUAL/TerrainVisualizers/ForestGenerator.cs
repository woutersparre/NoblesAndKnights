using UnityEngine;
using System.Collections.Generic;

public class ForestGenerator : MonoBehaviour
{
    [System.Serializable]
    public class TerrainTreeDensity
    {
        public TerrainType terrainType;
        [Range(0f, 1f)] public float density = 1f; // 0 = no trees, 1 = full Poisson density
    }

    [Header("Tree Settings")]
    public Sprite[] treeSprites;             // Array of tree sprites from spritesheet
    public float treeSpacing = 1f;           // Minimum distance between trees (Poisson disk)
    public Vector2 treeScaleRange = new Vector2(0.8f, 1.2f); // Random scale
    public Material treeMaterial;            // Material for trees (should support GPU instancing)

    [Header("Tree Density by Terrain")]
    [Tooltip("Which terrain types get trees and how dense. Leave empty to use built-in defaults.")]
    public TerrainTreeDensity[] treeDensity;

    // Used when treeDensity is unconfigured so the map still looks right out of the box.
    private static readonly Dictionary<TerrainType, float> defaultDensity = new Dictionary<TerrainType, float>
    {
        { TerrainType.Forest, 1f },
        { TerrainType.Rainforest, 1f },
        { TerrainType.Taiga, 0.8f },
        { TerrainType.Wetland, 0.4f },
        { TerrainType.Grassland, 0.25f },
        { TerrainType.Savanna, 0.2f },
        { TerrainType.Mountain, 0.15f },
    };

    [Header("Road Clearance")]
    public float roadClearance = 3f;

    [Header("River Clearance")]
    public float riverClearance = 2f;

    private class ForestCellData
    {
        public Sprite Sprite;
        public readonly List<Vector2> Positions = new List<Vector2>();
        public readonly List<float> Scales = new List<float>();
    }

    private readonly Dictionary<MapCell, ForestCellData> forestData = new Dictionary<MapCell, ForestCellData>();
    private readonly Dictionary<MapCell, MeshFilter> forestMeshes = new Dictionary<MapCell, MeshFilter>();

    private void Awake()
    {
        MapManager.OnMapGenerated.AddListener(Skibidi);
    }

    private void OnDestroy()
    {
        if (MapManager.RoadNetwork != null)
            MapManager.RoadNetwork.OnRoadsChanged -= RebuildTreesForRoads;
    }

    void Skibidi()
    {
        if (MapManager.RoadNetwork != null)
            MapManager.RoadNetwork.OnRoadsChanged += RebuildTreesForRoads;

        foreach (MapCell cell in MapManager.instance.map.mapCells)
        {
            float density = GetTreeDensity(cell.TerrainType);
            if (density > 0f)
            {
                GenerateTrees(cell, density);
            }
        }
    }

    private float GetTreeDensity(TerrainType terrainType)
    {
        if (treeDensity != null && treeDensity.Length > 0)
        {
            // Explicitly configured table: anything not listed gets no trees.
            foreach (var entry in treeDensity)
            {
                if (entry.terrainType == terrainType)
                    return entry.density;
            }
            return 0f;
        }

        // Fallback to built-in defaults when the table hasn't been set up.
        return defaultDensity.TryGetValue(terrainType, out float density) ? density : 0f;
    }

    public void GenerateTrees(MapCell cell, float density = 1f)
    {
        if (treeSprites == null || treeSprites.Length == 0 || treeMaterial == null || cell.Polygon == null || cell.Polygon.Length < 3)
        {
            Debug.LogWarning("GenerateTrees: missing setup");
            return;
        }

        Sprite selectedTreeSprite = treeSprites[Random.Range(0, treeSprites.Length)];

        // Thin out the Poisson sampling for sparse terrain: expected point density
        // scales with 1/spacing^2, so spacing = base / sqrt(density) yields ~density fraction.
        float spacing = density >= 1f ? treeSpacing : treeSpacing / Mathf.Sqrt(density);

        List<Vector2> positions2D = DelaunatorSharp.Unity.UniformPoissonDiskSampler.SampleCircle(cell.Center, 8.5f, spacing);
        List<Vector2> validPositions = new List<Vector2>();
        foreach (Vector2 point in positions2D)
        {
            if (MapManager.PointInPolygon(point, cell.Polygon))
            {
                validPositions.Add(point);
            }
        }

        var data = new ForestCellData { Sprite = selectedTreeSprite };
        for (int i = 0; i < validPositions.Count; i++)
        {
            data.Positions.Add(validPositions[i]);
            data.Scales.Add(Random.Range(treeScaleRange.x, treeScaleRange.y));
        }
        forestData[cell] = data;

        GameObject forestCellGO = new GameObject($"Forest at {cell.GameObject.name}");
        forestCellGO.transform.SetParent(cell.GameObject.transform);

        MeshFilter cellMeshFilter = forestCellGO.AddComponent<MeshFilter>();
        MeshRenderer cellMeshRenderer = forestCellGO.AddComponent<MeshRenderer>();
        cellMeshRenderer.sharedMaterial = treeMaterial;

        forestMeshes[cell] = cellMeshFilter;

        BuildCellMesh(cell);
    }

    private void RebuildTreesForRoads()
    {
        if (MapManager.instance == null || MapManager.RoadNetwork == null || forestData.Count == 0)
            return;

        var affectedCells = new HashSet<MapCell>();
        foreach (var road in MapManager.RoadNetwork.Roads)
        {
            foreach (var cell in road.Path)
            {
                if (forestData.ContainsKey(cell))
                    affectedCells.Add(cell);
                foreach (var neighbor in cell.Neighbors)
                {
                    if (forestData.ContainsKey(neighbor))
                        affectedCells.Add(neighbor);
                }
            }
        }

        foreach (var cell in affectedCells)
            BuildCellMesh(cell);
    }

    private void BuildCellMesh(MapCell cell)
    {
        if (!forestData.TryGetValue(cell, out var data) || !forestMeshes.TryGetValue(cell, out var cellMeshFilter))
            return;

        var keptPositions = new List<Vector2>();
        var keptScales = new List<float>();
        var roads = MapManager.RoadNetwork.Roads;

        for (int i = 0; i < data.Positions.Count; i++)
        {
            bool blocked = false;
            foreach (var road in roads)
            {
                if (DistanceToRoad(data.Positions[i], road) < roadClearance)
                {
                    blocked = true;
                    break;
                }
            }
            if (!blocked && MapManager.RiverNetwork != null)
            {
                foreach (var river in MapManager.RiverNetwork.GetRivers())
                {
                    if (DistanceToPolyline(data.Positions[i], river.LineRenderer) < riverClearance)
                    {
                        blocked = true;
                        break;
                    }
                }
            }
            if (!blocked)
            {
                keptPositions.Add(data.Positions[i]);
                keptScales.Add(data.Scales[i]);
            }
        }

        int quadCount = keptPositions.Count;
        Vector3[] vertices = new Vector3[quadCount * 4];
        Vector2[] uvs = new Vector2[quadCount * 4];
        int[] triangles = new int[quadCount * 6];
        Color[] colors = new Color[quadCount * 4];

        float halfWidth = data.Sprite.bounds.size.x / 2f;
        float halfHeight = data.Sprite.bounds.size.y / 2f;

        Rect spriteRect = data.Sprite.textureRect;
        Texture2D spriteTexture = data.Sprite.texture;

        float uMin = spriteRect.x / spriteTexture.width;
        float uMax = (spriteRect.x + spriteRect.width) / spriteTexture.width;
        float vMin = spriteRect.y / spriteTexture.height;
        float vMax = (spriteRect.y + spriteRect.height) / spriteTexture.height;

        for (int i = 0; i < quadCount; i++)
        {
            Vector3 pos = new Vector3(keptPositions[i].x, keptPositions[i].y, 0f);
            float scale = keptScales[i];

            Vector3 right = new Vector3(halfWidth * scale, 0f, 0f);
            Vector3 up = new Vector3(0f, halfHeight * scale, 0f);

            int vIndex = i * 4;
            vertices[vIndex + 0] = pos - right - up;
            vertices[vIndex + 1] = pos - right + up;
            vertices[vIndex + 2] = pos + right + up;
            vertices[vIndex + 3] = pos + right - up;

            uvs[vIndex + 0] = new Vector2(uMin, vMin);
            uvs[vIndex + 1] = new Vector2(uMin, vMax);
            uvs[vIndex + 2] = new Vector2(uMax, vMax);
            uvs[vIndex + 3] = new Vector2(uMax, vMin);

            int tIndex = i * 6;
            triangles[tIndex + 0] = vIndex + 0;
            triangles[tIndex + 1] = vIndex + 1;
            triangles[tIndex + 2] = vIndex + 2;
            triangles[tIndex + 3] = vIndex + 2;
            triangles[tIndex + 4] = vIndex + 3;
            triangles[tIndex + 5] = vIndex + 0;

            colors[vIndex + 0] = Color.white;
            colors[vIndex + 1] = Color.white;
            colors[vIndex + 2] = Color.white;
            colors[vIndex + 3] = Color.white;
        }

        Mesh mesh = new Mesh { name = "ForestCellMesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.RecalculateBounds();

        if (cellMeshFilter.mesh != null)
            Destroy(cellMeshFilter.mesh);
        cellMeshFilter.mesh = mesh;
    }

    private float DistanceToRoad(Vector2 point, RoadNetwork.RoadConnection road)
    {
        if (road.LineRenderers == null) return float.MaxValue;

        float minDist = float.MaxValue;
        foreach (var lineRenderer in road.LineRenderers)
        {
            minDist = Mathf.Min(minDist, DistanceToPolyline(point, lineRenderer));
        }
        return minDist;
    }

    private float DistanceToPolyline(Vector2 point, LineRenderer lineRenderer)
    {
        if (lineRenderer == null || lineRenderer.positionCount < 2)
            return float.MaxValue;

        Vector3[] positions = new Vector3[lineRenderer.positionCount];
        lineRenderer.GetPositions(positions);

        float minDist = float.MaxValue;
        for (int i = 0; i < positions.Length - 1; i++)
        {
            float d = DistanceToSegment(
                point,
                new Vector2(positions[i].x, positions[i].y),
                new Vector2(positions[i + 1].x, positions[i + 1].y));
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    private float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}