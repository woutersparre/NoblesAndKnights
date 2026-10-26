using UnityEngine;
using System.Collections.Generic;

public class MountainGenerator : MonoBehaviour
{
    [Header("Mountain Settings")]
    public Sprite[] sprites;
    public float spacing = 1f;           // Minimum distance between points (Poisson disk)
    //public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
    public Material material;
    public float mountainSize = 0.5f;

    // Struct to store UV data for each sprite
    struct SpriteUVData
    {
        public float uMin, uMax, vMin, vMax;
        public SpriteUVData(Rect rect, Texture2D tex)
        {
            uMin = rect.x / tex.width;
            uMax = (rect.x + rect.width) / tex.width;
            vMin = rect.y / tex.height;
            vMax = (rect.y + rect.height) / tex.height;
        }
    }
    private void Awake()
    {
        MapManager.OnMapGenerated.AddListener(Skibidi);
    }
    void Skibidi()
    {
        foreach (MapCell cell in MapManager.instance.map.mapCells)
        {
            if (cell.IsMountainCell)
            {
                try
                {
                    GenerateMountains(cell);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[MountainGenerator] GenerateMountains threw on {cell.GameObject.name}: {e}");
                }
            }
        }
    }
    public void GenerateMountains(MapCell cell)
    {
        if (sprites == null || sprites.Length == 0 || material == null || cell.Polygon == null || cell.Polygon.Length < 3)
        {
            return;
        }



        // Create new GameObject for this mountain cell
        GameObject mountainCellGO = new GameObject($"Mountain at {cell.GameObject.name}");
        mountainCellGO.transform.SetParent(cell.GameObject.transform);

        // Add mesh components to the new GameObject
        MeshFilter cellMeshFilter = mountainCellGO.AddComponent<MeshFilter>();
        MeshRenderer cellMeshRenderer = mountainCellGO.AddComponent<MeshRenderer>();
        cellMeshRenderer.sharedMaterial = material;

        // 1. Generate Poisson disk positions inside polygon
        List<Vector2> positions2D = DelaunatorSharp.Unity.UniformPoissonDiskSampler.SampleCircle(cell.Center, 8.5f, spacing);
        List<Vector2> validPositions = new List<Vector2>();
        foreach (Vector2 point in positions2D)
        {
            if (MapManager.PointInPolygon(point, cell.Polygon))
            {
                //Debug.Log(MapManager.instance.map.terrainGenerator.GetElevationAt(point));
                validPositions.Add(point);
            }
        }
        // 2. Convert to 3D positions
        List<Vector3> positions = new List<Vector3>();
        foreach (var pos in validPositions)
        {
            positions.Add(new Vector3(pos.x, pos.y, 0f));
        }



        // Pre-compute UV data for all sprites
        SpriteUVData[] spriteUVData = new SpriteUVData[sprites.Length];
        for (int s = 0; s < sprites.Length; s++)
        {
            spriteUVData[s] = new SpriteUVData(sprites[s].textureRect, sprites[s].texture);
        }

        // 3. Prepare quad mesh data
        int quadCount = positions.Count;
        Vector3[] vertices = new Vector3[quadCount * 4];
        Vector2[] uvs = new Vector2[quadCount * 4];
        int[] triangles = new int[quadCount * 6];
        Color[] colors = new Color[quadCount * 4]; // optional, white for no tint

        // Get dimensions from first sprite (all sprites same size)
        float halfWidth = sprites[0].bounds.size.x / 2f;
        float halfHeight = sprites[0].bounds.size.y / 2f;

        for (int i = 0; i < quadCount; i++)
        {
            Vector3 pos = positions[i];

            // Random sprite selection and get corresponding UV data
            int spriteIndex = Random.Range(0, sprites.Length);
            SpriteUVData uvData = spriteUVData[spriteIndex];

            // Random scale
            //float scale = Random.Range(scaleRange.x, scaleRange.y);
            float scale = MapManager.instance.map.terrainGenerator.GetElevationAt(pos) + mountainSize;

            Vector3 right = new Vector3(halfWidth * scale, 0f, 0f);
            Vector3 up = new Vector3(0f, halfHeight * scale, 0f);


            // Vertices for quad
            int vIndex = i * 4;
            vertices[vIndex + 0] = pos - right - up; // bottom-left
            vertices[vIndex + 1] = pos - right + up; // top-left
            vertices[vIndex + 2] = pos + right + up; // top-right
            vertices[vIndex + 3] = pos + right - up; // bottom-right

            // UVs - using pre-computed coordinates for the randomly selected sprite
            uvs[vIndex + 0] = new Vector2(uvData.uMin, uvData.vMin); // bottom-left
            uvs[vIndex + 1] = new Vector2(uvData.uMin, uvData.vMax); // top-left
            uvs[vIndex + 2] = new Vector2(uvData.uMax, uvData.vMax); // top-right
            uvs[vIndex + 3] = new Vector2(uvData.uMax, uvData.vMin); // bottom-right

            // Triangles
            int tIndex = i * 6;
            triangles[tIndex + 0] = vIndex + 0;
            triangles[tIndex + 1] = vIndex + 1;
            triangles[tIndex + 2] = vIndex + 2;
            triangles[tIndex + 3] = vIndex + 2;
            triangles[tIndex + 4] = vIndex + 3;
            triangles[tIndex + 5] = vIndex + 0;

            // Vertex colors (optional)
            colors[vIndex + 0] = Color.white;
            colors[vIndex + 1] = Color.white;
            colors[vIndex + 2] = Color.white;
            colors[vIndex + 3] = Color.white;
        }

        // 4. Create mesh
        Mesh mesh = new Mesh { name = "MountainCellMesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.RecalculateBounds();

        cellMeshFilter.mesh = mesh;
    }
}
