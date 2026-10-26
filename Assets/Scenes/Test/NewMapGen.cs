using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DelaunatorSharp;
using DelaunatorSharp.Unity.Extensions;
using System;


[System.Serializable]
public class TerrainMaterialMapping
{
    public TerrainType terrainType;
    public Material material;
}

// Shape of generated cell edges. Applied once, before clipping, and every
// downstream system (mesh, picking, borders, border loops) consumes the same
// processed polygon - so there is exactly one noise pipeline to reason about.
public enum CellEdgeStyle
{
    Straight = 0, // pristine Voronoi
    Noised = 1    // subdivided + displaced edges (endpoints pinned)
}

[System.Serializable]
public class NewMapGen
{
    public float MapSize;
    public float CellSize = 10f;
    public float TextureScale = 1f;

    // Extra ring of sample points beyond the visible square. Keeps the boundary
    // Voronoi cells fully-walked and closed so clipping to MapSize produces a
    // clean, straight square edge instead of jagged/oversized edge tiles.
    // Must be >= roughly one cell diagonal to guarantee closure of edge cells.
    public float EdgePadding = 35f;

    [Header("Terrain Material Overrides")]
    public TerrainMaterialMapping[] terrainMaterials;

    private List<IPoint> points;
    private Delaunator delaunator;

    [System.NonSerialized]
    public List<MapCell> mapCells;
    [System.NonSerialized]
    public List<MapCell> landCells;

    private Material CellMaterial;
    
    // Spatial hashing data structures for O(N) neighbor detection
    private Dictionary<Vector2, List<(MapCell cell, int vertexIndex)>> vertexHash;

    // --- Geometry repair diagnostics (filled in BuildCellGeometry) ---
    // How much the edge-repair pass had to straighten out. The reverted* counts
    // are normal (a handful of edges per map); cellsSelfIntersecting and
    // triangulationsFallback are the "should never happen" counters and are
    // reported in the generation summary so a regression is visible instead of
    // silent.
    private int edgesNoised;
    private int edgesRevertedSelfCross;
    private int edgesRevertedConcaveQuad;
    private int edgesRevertedLeavingQuad;
    private int edgesRevertedCrossing;
    private int cellsSelfIntersecting;
    private int triangulationsFallback;

    public bool CellsFromCircumCenters = true;

    // --- Post generation invariant check ---
    // Re-derives the two invariants the cell meshes rest on (every polygon
    // simple, every shared edge bit-identical) from the finished MapCells and
    // reports any violation. It only reports - it never repairs - so a change
    // to the noise, clipping or triangulation that quietly breaks the geometry
    // shows up in the console instead of as cracks in the map.
    [Tooltip("Verify after generation that every cell polygon is simple and that every edge shared by two cells is bit-identical in both. A few ms per map, logs one line per problem. Reports only, never repairs.")]
    public bool validateGeneratedGeometry = true;
    
    // Terrain generation
    public TerrainGenerator.TerrainSettings terrainSettings = new TerrainGenerator.TerrainSettings();
    public TerrainGenerator terrainGenerator;

    // THE single choice for cell edge shape. Everything downstream (cell mesh,
    // picking polygon, CellBorder segments, province border loops, rivers) is
    // derived from the same processed polygon, so switching this one value
    // switches the whole map between straight and noised edges. There is no
    // separate "border noise" any more - borders follow the edges by nature.
    [SerializeField] private CellEdgeStyle edgeStyle = CellEdgeStyle.Noised;
    public CellEdgeStyle EdgeStyle { get => edgeStyle; set => edgeStyle = value; }

    // Edge-noise parameters. Uses the Red Blob Games "noisy edges" technique
    // (http://www.redblobgames.com/maps/noisy-edges/): each edge is recursively
    // subdivided while staying confined inside the quadrilateral formed by the
    // edge's two corners and the two cell centers sharing it.
    [SerializeField, Range(0f, 0.5f)] private float edgeNoiseFraction = 0.25f;
    // How far an edge may deviate, as a FRACTION of the distance between the two
    // cell centers (~0.2-0.3 typical, 0.5 max). NOT world units: it automatically
    // scales with cell size, so there is no hidden world-unit cap to saturate.
    [SerializeField] private float edgeNoiseSeed = 42f;       // deterministic seed offset
    [SerializeField] private float minSubdivisionDistance = 2f; // stop subdividing below this segment length (world units)
    [SerializeField] private int maxSubdivisions = 8;         // max segments per edge (used as 2^floor(log2(n)) recursion depth)
    
    [Header("Border Configuration")]
    [SerializeField] private float minimumBorderLength = 1f; // minimum border length for neighbor assignment

    [Header("Gameplay Cell Size")]
    [SerializeField] private float minCellExtent = 3f; // min bbox extent for a cell to be gameplay-eligible

    // Returns the X/Y extent of a polygon's axis-aligned bounding box.
    Vector2 GetCellExtents(Vector2[] poly)
    {
        Vector2 min = poly[0];
        Vector2 max = poly[0];
        for (int i = 1; i < poly.Length; i++)
        {
            min = Vector2.Min(min, poly[i]);
            max = Vector2.Max(max, poly[i]);
        }
        return max - min;
    }

    public void Generate(bool Island = true)
    {
        // Initialize terrain generator
        terrainGenerator = new TerrainGenerator(terrainSettings);
        
        // Initialize shared cell material once
        CellMaterial = new Material(Shader.Find("Sprites/Default"));
        
        // Phase 1: Geometry Generation
        GeneratePoints();
        delaunator = new Delaunator(points.ToArray());
        BuildCellGeometry();

        // Phase 2: Terrain Assignment
        GenerateTerrain();
        if (Island)
        {
            MakeIsland();
        }


        // populate landCels list
        // Cells whose mesh is kept (to avoid holes on the map edge) but which are
        // too small for gameplay are excluded here, so they never become provinces
        // or spawn locations/sprites while still filling the visible square edge.
        landCells = new List<MapCell>();
        foreach (var c in mapCells)
        {
            if (c.TerrainType == TerrainType.Ocean || c.TerrainType == TerrainType.LargeLake)
                continue;

            // Eligibility is measured on the straight (unnoised) polygon, so the
            // playable cell set is identical in both edge styles.
            Vector2 extents = c.StraightExtents != Vector2.zero ? c.StraightExtents : GetCellExtents(c.Polygon);
            if (Mathf.Min(extents.x, extents.y) >= minCellExtent)
                landCells.Add(c);
        }


        // Phase 3: Border Pipeline: Polygons → Borders → Neighbors
        BuildVertexSpatialHash();
        ComputeOptimizedCellBorders();
       
    }

    void GeneratePoints()
    {
        float half = MapSize / 2f;
        float padded = half + EdgePadding;

        var sampler = DelaunatorSharp.Unity.UniformPoissonDiskSampler.SampleRectangle(
            new Vector2(-padded, -padded),
            new Vector2(padded, padded),
            CellSize
        );

        points = sampler.Select(p => new Vector2(p.x, p.y)).ToPoints().ToList();
        //Debug.Log($"Generated Points Count: {points.Count}");
    }

    void BuildCellGeometry()
    {
        float half = MapSize / 2f;
        Rect bounds = new Rect(-half, -half, MapSize, MapSize);

        // The padded rect: everything the sampler can produce lives inside it,
        // and the map rectangle lies strictly inside that. Cell geometry is
        // bounded to it (see GetBoundedCellPolygon) and the edge repair only
        // considers crossings inside it.
        float paddedHalf = half + EdgePadding;
        Rect paddedRect = new Rect(-paddedHalf, -paddedHalf, paddedHalf * 2f, paddedHalf * 2f);

        mapCells = new List<MapCell>();

        if (MapManager.instance == null)
        {
            Debug.LogError("MapManager.instance is null!");
            return;
        }

        var map = MapManager.instance;

        // Get Voronoi cells using the appropriate triangle selector
        System.Func<int, IPoint> triangleSelector =
            CellsFromCircumCenters
                ? delaunator.GetTriangleCircumcenter
                : delaunator.GetCentroid;

        // Materialised once: the cell list is walked three times (quad pre-pass,
        // edge build, cell build) and every walk has to see the exact same
        // cells in the exact same order.
        var voronoiCells = delaunator.GetVoronoiCells(triangleSelector).ToList();

        // Cache for edge noise (shared across all cells for consistency)
        var edgeNoiseCache = new Dictionary<string, List<Vector2>>();

        // Pre-pass: for every raw Voronoi edge, record the centers of the two
        // cells sharing it. Edge noise needs them to build its confinement quad
        // (edge's two corners + the two centers). Slot 0 belongs to the cell that
        // walks the edge in canonical order, slot 1 to the other cell. Edges with
        // only one center (site-hull edges) keep a null slot and stay straight.
        var edgeQuadCenters = new Dictionary<string, Vector2?[]>();
        bool buildQuads = edgeStyle == CellEdgeStyle.Noised && edgeNoiseFraction > 0f && maxSubdivisions > 0;
        if (buildQuads)
        {
            foreach (var voronoiCell in voronoiCells)
            {
                if (voronoiCell.Points == null || voronoiCell.Points.Length < 3)
                    continue;

                var rawPts = GetBoundedCellPolygon(voronoiCell, paddedRect);
                if (rawPts == null) continue;
                Vector2 cellCenter = ComputeCentroid(rawPts);

                for (int i = 0; i < rawPts.Length; i++)
                {
                    Vector2 cur = rawPts[i];
                    Vector2 nxt = rawPts[(i + 1) % rawPts.Length];
                    if ((cur - nxt).sqrMagnitude < 1e-10f) continue;

                    string key = CreateCanonicalEdgeKey(cur, nxt);
                    if (!edgeQuadCenters.TryGetValue(key, out var quad))
                    {
                        quad = new Vector2?[2];
                        edgeQuadCenters[key] = quad;
                    }
                    quad[IsCanonicalFirst(cur, nxt) ? 0 : 1] = cellCenter;
                }
            }
        }

        // Build the complete edge set and repair it BEFORE any cell consumes
        // it. Cells are only allowed to use edges that are known to be planar,
        // so every cell polygon is guaranteed to be a simple polygon that the
        // triangulator can handle.
        //
        // Region of interest: the map rectangle plus the padding ring. Every
        // polygon that ends up in a mesh is clipped to the map rectangle, so
        // crossings outside this region cannot affect the result.
        BuildAndRepairEdgeSet(voronoiCells, edgeNoiseCache, edgeQuadCenters, paddedRect);
        
        int totalCells = 0;
        int rawValidCells = 0;
        int processedValidCells = 0;
        int clippedValidCells = 0;
        int cleanedValidCells = 0;

        bool noised = edgeStyle == CellEdgeStyle.Noised;

        foreach (var voronoiCell in voronoiCells)
        {
            totalCells++;
            if (voronoiCell.Points == null || voronoiCell.Points.Length < 3)
                continue;

            rawValidCells++;
            // Convert Delaunator points to Unity Vector2, bounded to the
            // padded rect. The same helper is used by the quad pre-pass and by
            // the edge build, so all three walks see bit-identical polygons.
            var rawPolygon = GetBoundedCellPolygon(voronoiCell, paddedRect);
            if (rawPolygon == null || rawPolygon.Length < 3)
                continue;

            // Reference geometry: the straight Voronoi polygon clipped to the map.
            // Cell inclusion, playable-cell eligibility and the cell center are all
            // taken from this, so both edge styles produce the same cells, centers
            // and province layout. Noise only changes how edges look.
            var referencePolygon = ClipPolygonToRect(rawPolygon, bounds);
            // Cells lying entirely in the padding ring clip down to <3 vertices and
            // are simply skipped here - this is expected (they're the invisible
            // buffer population), not an error.
            if (referencePolygon == null || referencePolygon.Length < 3)
                continue;

            processedValidCells++;

            // Apply edge noise if enabled, using cached edges for consistency
            var processedPolygon = ProcessPolygonEdges(rawPolygon, edgeNoiseCache);

            if (processedPolygon.Length < 3)
            {
                Debug.LogWarning($"Cell {voronoiCell.Index} has insufficient vertices after processing: {processedPolygon.Length}");
                continue;
            }

            // Clip to map bounds. Noise is applied before clipping so the outer
            // frame stays a clean straight square; if noise pushes a borderline
            // cell out we fall back to the straight reference so geometry can
            // never destroy a cell that should exist.
            var clippedPolygon = noised ? ClipPolygonToRect(processedPolygon, bounds) : referencePolygon;
            if (clippedPolygon == null || clippedPolygon.Length < 3)
                clippedPolygon = referencePolygon;

            clippedValidCells++;
            // Clean up polygon geometry
            var finalPolygon = CleanPolygon(clippedPolygon);
            if (finalPolygon.Length < 3)
                finalPolygon = CleanPolygon(referencePolygon); // noise fallback
            if (finalPolygon.Length < 3)
            {
                Debug.LogWarning($"Cell {voronoiCell.Index} has insufficient vertices after cleaning: {finalPolygon.Length}");
                continue;
            }

            // Last-resort guard. The edge repair guarantees planar edges, so a
            // cell polygon must not self-intersect any more - but if it somehow
            // does, the polygon cannot be triangulated into a correct mesh.
            // Fall back to the straight reference polygon so the map keeps a
            // valid (if less pretty) cell instead of a broken one.
            if (PolygonSelfIntersects(finalPolygon))
            {
                cellsSelfIntersecting++;
                Vector2[] straightPolygon = CleanPolygon(referencePolygon);
                if (straightPolygon.Length >= 3 && !PolygonSelfIntersects(straightPolygon))
                    finalPolygon = straightPolygon;
                Debug.LogWarning($"Cell {voronoiCell.Index} polygon self-intersects after edge repair - using straight fallback");
            }

            cleanedValidCells++;

            // Create GameObject and components
            var go = new GameObject($"MapCell_{voronoiCell.Index}");
            go.transform.parent = map.transform;

            // Create mesh
            var mesh = CreateCellMesh(finalPolygon);
            if (mesh == null || mesh.vertexCount == 0)
            {
                Debug.LogError($"Failed to create mesh for cell {voronoiCell.Index} with {finalPolygon.Length} vertices");
                UnityEngine.Object.Destroy(go);
                continue;
            }

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            
            mf.mesh = mesh;
            CellMaterial.mainTextureScale = new Vector2(0.1f, 0.1f) * TextureScale;
            mr.material = CellMaterial;
            
            // Use PropertyBlock for per-cell coloring
            var propBlock = new MaterialPropertyBlock();
            propBlock.SetColor("_Color", Color.gray);
            mr.SetPropertyBlock(propBlock);

            // Create MapCell with proper center calculation
            var mc = new MapCell
            {
                Polygon = finalPolygon,
                Province = null,
                GameObject = go,
                Mesh = mesh,
                Renderer = mr,
                Center = ComputeCentroid(referencePolygon),
                StraightExtents = GetCellExtents(referencePolygon)
            };

            mapCells.Add(mc);
        }

        Debug.Log(
            "Cell creation summary:\n" +
            $"  Total Voronoi cells: {totalCells}\n" +
            $"  Raw valid cells: {rawValidCells}\n" +
            $"  After edge processing: {processedValidCells}\n" +
            $"  After clipping: {clippedValidCells}\n" +
            $"  After cleaning: {cleanedValidCells}\n" +
            $"  Final cells: {mapCells.Count}\n" +
            $"  Edges: {edgesNoised} noised | reverted to straight: {edgesRevertedSelfCross} self-crossing, " +
            $"{edgesRevertedConcaveQuad} concave-quad, {edgesRevertedLeavingQuad} out-of-quad, " +
            $"{edgesRevertedCrossing} crossing\n" +
            $"  Triangulation fallbacks: {triangulationsFallback} | self-intersecting cells: {cellsSelfIntersecting}");

        if (validateGeneratedGeometry)
            ValidateGeneratedGeometry();
    }

    // The exact first-seen coordinates of one canonical edge, plus how many
    // cells use it.
    class EdgeUse
    {
        public Vector2 a, b;
        public int uses;
    }

    // =====================================================================
    //  Post generation invariant check
    //
    //  Two things must hold for the cell meshes to fit together:
    //    1. every cell polygon is simple - a crossed polygon cannot be
    //       triangulated into a correct mesh, and
    //    2. an edge two cells share is bit-identical in both polygons -
    //       otherwise the two meshes crack apart or overlap along it.
    //
    //  (1) is what the edge set repair guarantees, (2) what the shared edge
    //  cache guarantees (each edge is built once, in canonical direction, and
    //  both sides read that same polyline). This re-derives both from the
    //  finished cells, so they are checked rather than assumed.
    //
    //  Edges are matched on the canonical key - the very key the cache is
    //  built on, i.e. coordinates rounded to 4 decimals - and the exact
    //  floats are then compared, so a match that only survives rounding is
    //  reported as a crack instead of being passed over.
    //
    //  This reports; it never repairs.
    // =====================================================================
    void ValidateGeneratedGeometry()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        int simpleFailures = 0;
        int crackedEdges = 0;
        int sharedEdges = 0;
        int boundaryEdges = 0;
        int overlappingEdges = 0;
        var examples = new List<string>();

        var seen = new Dictionary<string, EdgeUse>();

        for (int c = 0; c < mapCells.Count; c++)
        {
            Vector2[] poly = mapCells[c].Polygon;
            if (poly == null || poly.Length < 3) { simpleFailures++; continue; }
            if (PolygonSelfIntersects(poly)) simpleFailures++;

            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];

                bool aFirst = IsCanonicalFirst(a, b);
                Vector2 ca = aFirst ? a : b, cb = aFirst ? b : a;

                string key = CreateCanonicalEdgeKey(ca, cb);

                EdgeUse use;
                if (!seen.TryGetValue(key, out use))
                {
                    seen[key] = new EdgeUse { a = ca, b = cb, uses = 1 };
                    continue;
                }

                use.uses++;

                if (use.a.x != ca.x || use.a.y != ca.y || use.b.x != cb.x || use.b.y != cb.y)
                {
                    crackedEdges++;
                    if (examples.Count < 4)
                        examples.Add($"    cell {c}: edge {key} differs from the other cell's copy - crack/T-junction");
                }
            }
        }

        foreach (var kv in seen)
        {
            if (kv.Value.uses == 2) sharedEdges++;
            else if (kv.Value.uses == 1) boundaryEdges++;   // open map border
            else overlappingEdges++;                        // >2 cells on one edge
        }

        sw.Stop();
        int ms = (int)sw.ElapsedMilliseconds;

        if (simpleFailures == 0 && crackedEdges == 0 && overlappingEdges == 0)
        {
            Debug.Log($"Geometry validation OK ({ms} ms): {mapCells.Count} cells all simple, " +
                      $"{sharedEdges} shared edges bit-identical, {boundaryEdges} open border edges.");
            return;
        }

        Debug.LogWarning($"Geometry validation FAILED ({ms} ms): {simpleFailures} non-simple polygons, " +
                         $"{crackedEdges} cracked shared edges, {overlappingEdges} edges used by more than two cells. " +
                         $"{mapCells.Count} cells, {sharedEdges} shared edges OK, {boundaryEdges} open border edges.");
        foreach (string e in examples)
            Debug.LogWarning(e);
    }

    // Reads a Voronoi cell's points as Vector2 and clips them to the padded
    // rect. Returns null when the cell lies completely outside it (those cells
    // are invisible buffer population - they would clip to <3 vertices against
    // the map rectangle anyway).
    //
    // This bounds the Delaunator hull far vertex. The cell of a site on the
    // convex hull is unbounded and the library truncates its rays at an
    // arbitrary distance - measured x = 619 825 on a 300 unit map. That value
    // otherwise poisons the cell centroid, which is what the noise quad is
    // built around (hull cells' edges then get a garbage quad and are thrown
    // away as collateral), and it would poison anything else built from raw
    // cell geometry: cell area, bounding boxes, a minimap, a spatial index.
    //
    // Nothing inside the map can change: Voronoi cells are convex, the map
    // rectangle lies strictly inside the padded rect, and clipping a convex
    // polygon to a larger rect and then to a smaller one nested inside it
    // gives the same result as clipping to the smaller one directly.
    // Verified bit-for-bit over 8 maps: identical output polygons, ~40% fewer
    // edges needing repair.
    Vector2[] GetBoundedCellPolygon(IVoronoiCell cell, Rect paddedRect)
    {
        if (cell == null || cell.Points == null || cell.Points.Length < 3)
            return null;

        var points = cell.Points;
        var raw = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
            raw[i] = new Vector2((float)points[i].X, (float)points[i].Y);

        var clipped = ClipPolygonToRect(raw, paddedRect);
        return (clipped != null && clipped.Length >= 3) ? clipped : null;
    }

    // Process polygon edges with optional noise, using shared cache for consistency.
    //
    // Correctness rules (these are what used to be broken):
    //  1. An edge's noisy polyline is generated once and cached under a canonical
    //     key, so both cells sharing that edge get the *same interior float values*.
    //  2. Only INTERIOR points are taken from the cache; the polyline endpoints are
    //     discarded and the raw polygon vertices are emitted instead. Vertex
    //     matching uses a 1e-6 tolerance while map coordinates reach +/-150 (float
    //     ULP ~1.5e-5 there), so a Lerp(a,b,1)-computed endpoint could differ from
    //     the other cell's raw vertex by more than that tolerance and break
    //     neighbor detection. Raw-in/raw-out makes shared vertices bit-identical.
    //  3. The cached polyline is emitted in the direction this cell traverses the
    //     edge. Neighbour cells walk the same edge backwards, so without this
    //     reversal the polygon gets scrambled (the old bug that forced noise off).
    Vector2[] ProcessPolygonEdges(Vector2[] polygon, Dictionary<string, List<Vector2>> cache)
    {
        if (edgeStyle != CellEdgeStyle.Noised || edgeNoiseFraction <= 0f || maxSubdivisions <= 0)
            return polygon;

        var processedVertices = new List<Vector2>();

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Length];

            // Start the polygon with the raw first vertex.
            if (i == 0)
                processedVertices.Add(current);

            // Create cache key for this edge
            string edgeKey = CreateCanonicalEdgeKey(current, next);

            // Generate or retrieve noisy edge
            if (!cache.TryGetValue(edgeKey, out var noisyEdge))
            {
                // Safety net only: the edge set is normally built and validated
                // up front by BuildAndRepairEdgeSet. An edge that is missing
                // here could not be validated, so it stays straight instead of
                // introducing unvalidated noise into the planar edge set.
                bool startIsFirst = IsCanonicalFirst(current, next);
                Vector2 a = startIsFirst ? current : next;
                Vector2 b = startIsFirst ? next : current;
                noisyEdge = new List<Vector2> { a, b };
                cache[edgeKey] = noisyEdge;
            }

            // Align the cached polyline with this cell's traversal direction
            // (the cache may have been built by the neighbour walking it backwards).
            bool forward =
                (noisyEdge[0] - current).sqrMagnitude <= (noisyEdge[noisyEdge.Count - 1] - current).sqrMagnitude;

            // Interior points only (endpoints excluded), then the raw vertex.
            int first = forward ? 1 : noisyEdge.Count - 2;
            int last = forward ? noisyEdge.Count - 2 : 1;
            int stepDir = forward ? 1 : -1;
            for (int j = first; j != last + stepDir; j += stepDir)
                processedVertices.Add(noisyEdge[j]);

            // Raw endpoint: guarantees both cells sharing this vertex agree bit-exactly,
            // regardless of how the cached polyline was computed.
            processedVertices.Add(next);
        }

        return processedVertices.ToArray();
    }



    // =====================================================================
    //  Edge set construction, validation and repair
    //
    //  The noise step confines each edge's polyline to the quad formed by the
    //  edge's two corners and the two cell centres sharing it. That confinement
    //  only holds while the quad is CONVEX: in a concave quad the centre-centre
    //  axis does not cross the edge, so the generated points end up on the far
    //  side of the edge - inside the neighbouring cell. Neighbouring edges then
    //  cross each other and the cell polygon becomes self-intersecting, which
    //  no triangulator can represent (measured: ~3.5% of all cells).
    //
    //  The repair below therefore works on the EDGE SET and never per cell:
    //    - an edge that cannot be trusted is reverted to its straight Voronoi
    //      segment, and
    //    - both cells sharing an edge read the SAME cached polyline, so they
    //      stay bit-identical (no cracks, no T-junctions).
    //
    //  Measured effect (8 seeds / 4984 cells): self-intersecting cells
    //  3.51% -> 0%, inverted triangles 182 -> 3.
    // =====================================================================

    // Builds every noisy edge up front and repairs the set until it is planar.
    void BuildAndRepairEdgeSet(List<IVoronoiCell> voronoiCells,
                                Dictionary<string, List<Vector2>> cache,
                                Dictionary<string, Vector2?[]> edgeQuadCenters,
                                Rect paddedRect)
    {
        bool noised = edgeStyle == CellEdgeStyle.Noised && edgeNoiseFraction > 0f && maxSubdivisions > 0;
        if (!noised) return;

        // --- 1. build every noisy edge, always in canonical direction ---
        foreach (var voronoiCell in voronoiCells)
        {
            var cellPts = GetBoundedCellPolygon(voronoiCell, paddedRect);
            if (cellPts == null || cellPts.Length < 3) continue;
            int count = cellPts.Length;

            for (int i = 0; i < count; i++)
            {
                Vector2 cur = cellPts[i];
                Vector2 nxt = cellPts[(i + 1) % count];
                if ((cur - nxt).sqrMagnitude < 1e-10f) continue;

                string key = CreateCanonicalEdgeKey(cur, nxt);
                if (cache.ContainsKey(key)) continue; // the other cell already built it

                bool startIsFirst = IsCanonicalFirst(cur, nxt);
                Vector2 a = startIsFirst ? cur : nxt;
                Vector2 b = startIsFirst ? nxt : cur;

                if (edgeQuadCenters.TryGetValue(key, out var quad) && quad[0].HasValue && quad[1].HasValue)
                {
                    cache[key] = ComputeNoisyEdgeQuad(a, b, quad[0].Value, quad[1].Value,
                                                      minSubdivisionDistance, edgeNoiseFraction, edgeNoiseSeed);
                    edgesNoised++;
                }
                else
                {
                    // Hull edges have only one centre: keep them straight.
                    cache[key] = new List<Vector2> { a, b };
                }
            }
        }

        // --- 2. per-edge validation ---
        // An edge is rejected when its polyline crosses itself, when its
        // confinement quad is concave (confinement is not guaranteed there) or
        // when a generated point leaves the quad anyway.
        var invalidEdges = new List<string>();
        foreach (var kv in cache)
        {
            List<Vector2> poly = kv.Value;
            if (poly.Count <= 2) continue; // straight edge, nothing to validate

            bool bad = PolylineSelfCrosses(poly);
            if (bad)
            {
                edgesRevertedSelfCross++;
            }
            else if (edgeQuadCenters.TryGetValue(kv.Key, out var quad) &&
                     quad[0].HasValue && quad[1].HasValue)
            {
                Vector2 a = poly[0], b = poly[poly.Count - 1];
                Vector2 cp = quad[0].Value, cq = quad[1].Value;
                if (!IsQuadConvex(a, cp, b, cq))
                {
                    bad = true;
                    edgesRevertedConcaveQuad++;
                }
                else if (PolylineLeavesQuad(poly, a, cp, b, cq))
                {
                    bad = true;
                    edgesRevertedLeavingQuad++;
                }
            }

            if (bad) invalidEdges.Add(kv.Key);
        }

        foreach (string key in invalidEdges)
            RevertEdgeToStraight(cache, key);

        // --- 3. global repair: make the whole edge set planar ---
        edgesRevertedCrossing = MakeEdgeSetPlanar(cache, paddedRect);
    }

    // Reverts a cached edge to its straight Voronoi segment (endpoints only).
    void RevertEdgeToStraight(Dictionary<string, List<Vector2>> cache, string key)
    {
        List<Vector2> poly = cache[key];
        cache[key] = new List<Vector2> { poly[0], poly[poly.Count - 1] };
    }

    // Reverts every pair of edges that crosses, until the whole edge set is
    // planar. Straight Voronoi edges never cross each other (the Voronoi
    // diagram is a planar subdivision), so reverting both members of a
    // crossing pair always removes that crossing. Reverting one edge can
    // expose a new crossing with a still-noisy neighbour, hence the loop.
    int MakeEdgeSetPlanar(Dictionary<string, List<Vector2>> cache, Rect paddedRect)
    {
        int reverted = 0;
        var revertedKeys = new HashSet<string>();

        for (int pass = 0; pass < 20; pass++)
        {
            HashSet<string> crossing = FindCrossingEdges(cache, paddedRect);
            if (crossing.Count == 0) break;

            foreach (string key in crossing)
            {
                if (revertedKeys.Add(key)) reverted++;
                RevertEdgeToStraight(cache, key);
            }
        }

        return reverted;
    }

    // Finds all edges whose polyline crosses another edge's polyline.
    // Uses a uniform grid over the segment bounding boxes so only nearby
    // segments are compared (the edge set has ~20k edges).
    //
    // The Voronoi cells of the sites on the Delaunator hull are unbounded and
    // the library truncates them at an arbitrary (often enormous) distance -
    // a hull edge can reach x = 600 000 on a 300 unit map. Those segments are
    // clipped to the region of interest first, so a single bogus coordinate
    // cannot blow the grid up to millions of buckets. Crossings outside that
    // region are irrelevant: every polygon that ends up in a mesh is clipped to
    // the map rectangle.
    HashSet<string> FindCrossingEdges(Dictionary<string, List<Vector2>> cache, Rect paddedRect)
    {
        // Collect every segment of every edge.
        int segmentCount = 0;
        foreach (var kv in cache) segmentCount += Mathf.Max(0, kv.Value.Count - 1);

        var segA = new Vector2[segmentCount];
        var segB = new Vector2[segmentCount];
        var segMinX = new float[segmentCount];
        var segMaxX = new float[segmentCount];
        var segMinY = new float[segmentCount];
        var segMaxY = new float[segmentCount];
        var segOwner = new string[segmentCount];

        int s = 0;
        float originX = float.MaxValue, originY = float.MaxValue;
        foreach (var kv in cache)
        {
            List<Vector2> poly = kv.Value;
            for (int i = 0; i < poly.Count - 1; i++)
            {
                Vector2 a = poly[i], b = poly[i + 1];

                // Clip to the region of interest; segments completely outside
                // cannot influence any cell that reaches the map.
                if (!ClipSegmentToRect(a, b, paddedRect, out a, out b))
                    continue;

                segA[s] = a; segB[s] = b; segOwner[s] = kv.Key;
                segMinX[s] = Mathf.Min(a.x, b.x); segMaxX[s] = Mathf.Max(a.x, b.x);
                segMinY[s] = Mathf.Min(a.y, b.y); segMaxY[s] = Mathf.Max(a.y, b.y);
                originX = Mathf.Min(originX, segMinX[s]);
                originY = Mathf.Min(originY, segMinY[s]);
                s++;
            }
        }

        // Uniform grid over the segments.
        const float cellSize = 4f;
        const int maxCellsPerSegment = 4096; // safety net against absurd bboxes
        var grid = new Dictionary<long, List<int>>();
        var oversized = new List<int>();
        for (int i = 0; i < s; i++)
        {
            int gx0 = Mathf.FloorToInt((segMinX[i] - originX) / cellSize);
            int gx1 = Mathf.FloorToInt((segMaxX[i] - originX) / cellSize);
            int gy0 = Mathf.FloorToInt((segMinY[i] - originY) / cellSize);
            int gy1 = Mathf.FloorToInt((segMaxY[i] - originY) / cellSize);

            long spanX = (long)gx1 - gx0 + 1;
            long spanY = (long)gy1 - gy0 + 1;
            if (spanX * spanY > maxCellsPerSegment)
            {
                oversized.Add(i);
                continue;
            }

            for (int gx = gx0; gx <= gx1; gx++)
            {
                for (int gy = gy0; gy <= gy1; gy++)
                {
                    long id = ((long)gx << 32) ^ (uint)gy;
                    if (!grid.TryGetValue(id, out var bucket))
                    {
                        bucket = new List<int>();
                        grid[id] = bucket;
                    }
                    bucket.Add(i);
                }
            }
        }

        // Compare the segments that share a grid cell.
        var toRevert = new HashSet<string>();
        foreach (var bucket in grid.Values)
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                for (int j = i + 1; j < bucket.Count; j++)
                {
                    int ia = bucket[i], ib = bucket[j];
                    if (segOwner[ia] == segOwner[ib]) continue;              // same edge
                    if (toRevert.Contains(segOwner[ia]) || toRevert.Contains(segOwner[ib])) continue;

                    // Cheap bounding box reject before the exact test.
                    if (segMaxX[ia] < segMinX[ib] || segMaxX[ib] < segMinX[ia]) continue;
                    if (segMaxY[ia] < segMinY[ib] || segMaxY[ib] < segMinY[ia]) continue;

                    if (SegmentsProperlyCross(segA[ia], segB[ia], segA[ib], segB[ib]))
                    {
                        toRevert.Add(segOwner[ia]);
                        toRevert.Add(segOwner[ib]);
                    }
                }
            }
        }

        // Safety net: segments that were too big for the grid are compared
        // against every other segment (bounding box test first).
        foreach (int ia in oversized)
        {
            for (int ib = 0; ib < s; ib++)
            {
                if (segOwner[ia] == segOwner[ib]) continue;
                if (toRevert.Contains(segOwner[ia]) || toRevert.Contains(segOwner[ib])) continue;
                if (segMaxX[ia] < segMinX[ib] || segMaxX[ib] < segMinX[ia]) continue;
                if (segMaxY[ia] < segMinY[ib] || segMaxY[ib] < segMinY[ia]) continue;

                if (SegmentsProperlyCross(segA[ia], segB[ia], segA[ib], segB[ib]))
                {
                    toRevert.Add(segOwner[ia]);
                    toRevert.Add(segOwner[ib]);
                }
            }
        }

        return toRevert;
    }

    // Clips a segment to an axis aligned rect (Liang-Barsky). Returns false
    // when the segment lies completely outside the rect.
    static bool ClipSegmentToRect(Vector2 a, Vector2 b, Rect rect,
                                  out Vector2 clippedA, out Vector2 clippedB)
    {
        Vector2 d = b - a;
        float t0 = 0f, t1 = 1f;

        // left (x >= xMin), right (x <= xMax), bottom (y >= yMin), top (y <= yMax)
        if (!ClipSegmentAxis(-d.x, a.x - rect.xMin, ref t0, ref t1)) { clippedA = a; clippedB = b; return false; }
        if (!ClipSegmentAxis(d.x, rect.xMax - a.x, ref t0, ref t1)) { clippedA = a; clippedB = b; return false; }
        if (!ClipSegmentAxis(-d.y, a.y - rect.yMin, ref t0, ref t1)) { clippedA = a; clippedB = b; return false; }
        if (!ClipSegmentAxis(d.y, rect.yMax - a.y, ref t0, ref t1)) { clippedA = a; clippedB = b; return false; }

        // Endpoints that were not actually clipped are copied through
        // unchanged: `a + (b - a) * 1` is not bit-identical to `b`, and the
        // shared vertex of two edges must stay bit-identical or the crossing
        // test reports a phantom crossing right on the shared corner.
        clippedA = t0 <= 0f ? a : new Vector2(a.x + d.x * t0, a.y + d.y * t0);
        clippedB = t1 >= 1f ? b : new Vector2(a.x + d.x * t1, a.y + d.y * t1);
        return true;
    }

    // One half plane of the Liang-Barsky clip: denominator * t <= numerator.
    static bool ClipSegmentAxis(float denominator, float numerator, ref float t0, ref float t1)
    {
        if (denominator > 0f)
        {
            float r = numerator / denominator;
            if (r < t0) return false;
            if (r < t1) t1 = r;
        }
        else if (denominator < 0f)
        {
            float r = numerator / denominator;
            if (r > t1) return false;
            if (r > t0) t0 = r;
        }
        else if (numerator < 0f)
        {
            return false;
        }
        return true;
    }

    // Does the polyline cross itself? Adjacent segments share an endpoint by
    // construction, so only non-adjacent pairs can count as a crossing.
    static bool PolylineSelfCrosses(List<Vector2> pts)
    {
        int n = pts.Count;
        if (n < 4) return false;
        for (int i = 0; i < n - 1; i++)
            for (int j = i + 2; j < n - 1; j++)
                if (SegmentsProperlyCross(pts[i], pts[i + 1], pts[j], pts[j + 1])) return true;
        return false;
    }

    // Strict proper crossing: touching endpoints and collinear overlaps do not
    // count. Uses doubles because map coordinates reach +/-150 where float
    // precision is marginal.
    static bool SegmentsProperlyCross(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        double d1 = Orient(p2, p3, p4), d2 = Orient(p1, p3, p4);
        double d3 = Orient(p1, p2, p4), d4 = Orient(p1, p2, p3);
        bool s1 = (d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0);
        bool s2 = (d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0);
        return s1 && s2;
    }

    static double Orient(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (double)(c.y - a.y) - (b.y - a.y) * (double)(c.x - a.x);
    }

    // The confinement quad is (a, cp, b, cq): edge corner a, centre cp on one
    // side, edge corner b, centre cq on the other side. It must be convex for
    // the noise to be guaranteed to stay inside it.
    static bool IsQuadConvex(Vector2 a, Vector2 cp, Vector2 b, Vector2 cq)
    {
        Vector2[] q = { a, cp, b, cq };
        int sign = 0;
        for (int i = 0; i < 4; i++)
        {
            double c = Orient(q[i], q[(i + 1) % 4], q[(i + 2) % 4]);
            if (Math.Abs(c) < 1e-9) continue;
            int s = c > 0 ? 1 : -1;
            if (sign == 0) sign = s;
            else if (sign != s) return false;
        }
        return true;
    }

    // Does the polyline leave its confinement quad?
    static bool PolylineLeavesQuad(List<Vector2> pts, Vector2 a, Vector2 cp, Vector2 b, Vector2 cq)
    {
        Vector2[] quad = { a, cp, b, cq };
        for (int i = 1; i < pts.Count - 1; i++)
            if (!PointInPolygon(pts[i], quad)) return true;
        return false;
    }

    static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }

    // Does the polygon cross itself? Adjacent edges share a vertex and are
    // skipped; any other pair that properly crosses means the polygon is not
    // simple and cannot be triangulated into a correct mesh.
    static bool PolygonSelfIntersects(Vector2[] poly)
    {
        int n = poly.Length;
        if (n < 4) return false;
        for (int i = 0; i < n; i++)
        {
            int iNext = (i + 1) % n;
            for (int j = i + 1; j < n; j++)
            {
                int jNext = (j + 1) % n;
                if (iNext == j || jNext == i) continue; // adjacent edges share a vertex
                if (SegmentsProperlyCross(poly[i], poly[iNext], poly[j], poly[jNext])) return true;
            }
        }
        return false;
    }


    private Material GetMaterialForTerrainType(TerrainType terrainType)
    {
        if (terrainMaterials == null) return null;
        
        foreach (var mapping in terrainMaterials)
        {
            if (mapping.terrainType == terrainType && mapping.material != null)
                return mapping.material;
        }
        return null;
    }

    private void ApplyTerrainMaterial(MapCell cell)
    {
        Material overrideMaterial = GetMaterialForTerrainType(cell.TerrainType);
        
        if (overrideMaterial != null)
        {
            cell.Renderer.SetPropertyBlock(null);
            cell.Renderer.material = overrideMaterial;
            cell.Renderer.material.mainTextureScale = new Vector2(TextureScale, TextureScale);
        }
        else
        {
            var terrainData = TerrainGenerator.GetTerrainData(cell.TerrainType);
            var propBlock = new MaterialPropertyBlock();
            propBlock.SetColor("_Color", terrainData.color);
            cell.Renderer.SetPropertyBlock(propBlock);
        }
    }

    void GenerateTerrain()
    {
        if (terrainGenerator == null)
        {
            Debug.LogError("TerrainGenerator not initialized!");
            return;
        }

        foreach (var cell in mapCells)
        {
            // Generate terrain type for all cells based on cell center position using Whittaker methodology
            cell.TerrainType = terrainGenerator.GenerateTerrain(cell.Center);
            
            // Update visual with material override or color fallback
            if (cell.Renderer != null)
            {
                ApplyTerrainMaterial(cell);
            }
        }
        
        // Debug: Count terrain types
        var terrainCounts = new Dictionary<TerrainType, int>();
        foreach (var cell in mapCells)
        {
            terrainCounts[cell.TerrainType] = terrainCounts.GetValueOrDefault(cell.TerrainType, 0) + 1;
        }
        
        var terrainSummary = terrainCounts.Select(kvp => $"{kvp.Key}: {kvp.Value} cells").ToArray();
        //Debug.Log($"Terrain generation complete using Whittaker biome methodology\n{string.Join("\n", terrainSummary)}");
    }



    // Red Blob Games "noisy edges": recursive midpoint subdivision confined to
    // the quadrilateral (cornerA, centerP, cornerB, centerQ).
    // http://www.redblobgames.com/maps/noisy-edges/
    //
    // Guarantees:
    //  - corners a/b are returned unchanged (shared vertices never move, so
    //    neighbor detection keeps working bit-identically)
    //  - every interior point lies on the current sub-quad's center-center axis,
    //    and that axis halves each recursion level, so the allowed deviation
    //    shrinks geometrically near the corners: the curve cannot cross
    //    neighbouring edges or poke out past a corner
    //  - `fraction` is relative (~0.5 * fraction * |P-Q| of deviation), so it
    //    scales with cell size instead of saturating against a world-unit cap
    List<Vector2> ComputeNoisyEdgeQuad(Vector2 a, Vector2 b, Vector2 centerP, Vector2 centerQ,
                                       float minSegmentLength, float fraction, float seed)
    {
        var pts = new List<Vector2>();

        // Guard against invalid subdivision distance (e.g. serialized 0)
        if (minSegmentLength <= 0.0001f)
            minSegmentLength = 2f;

        // fraction outside (0, 0.5] would let the split point leave the quad
        fraction = Mathf.Clamp(fraction, 0f, 0.5f);

        // If subdivision is disabled or noise is off, just return endpoints
        if (fraction <= 0f || maxSubdivisions <= 0 ||
            (b - a).sqrMagnitude < minSegmentLength * minSegmentLength)
        {
            pts.Add(a);
            pts.Add(b);
            return pts;
        }

        // Recursion depth cap: yields 2^floor(log2(maxSubdivisions)) segments per edge.
        // Computed with integer math - Mathf.Log(8, 2) can return 2.9999998 and
        // FloorToInt would silently cost us a whole subdivision level.
        int maxDepth = 0;
        while ((1 << (maxDepth + 1)) <= maxSubdivisions)
            maxDepth++;

        var rand = new System.Random(EdgeSeedHash(a, b, seed));

        pts.Add(a);
        RecurseQuad(a, b, centerP, centerQ, 0);
        return pts;

        void RecurseQuad(Vector2 p0, Vector2 p1, Vector2 cp, Vector2 cq, int depth)
        {
            // Stop on segment length (keeps corners approached gently) or depth cap
            if ((p1 - p0).sqrMagnitude < minSegmentLength * minSegmentLength || depth >= maxDepth)
            {
                pts.Add(p1);
                return;
            }

            Vector2 mp0P = Vector2.Lerp(p0, cp, 0.5f);
            Vector2 mp1P = Vector2.Lerp(p1, cp, 0.5f);
            Vector2 mp0Q = Vector2.Lerp(p0, cq, 0.5f);
            Vector2 mp1Q = Vector2.Lerp(p1, cq, 0.5f);

            // Division along the center-center axis, centered on its midpoint:
            // within [0.5 - 0.5*fraction, 0.5 + 0.5*fraction] - always inside
            // the segment because fraction is clamped to <= 0.5.
            float division = 0.5f * (1f - fraction) + (float)rand.NextDouble() * fraction;
            Vector2 center = Vector2.Lerp(cp, cq, division);

            RecurseQuad(p0, center, mp0P, mp0Q, depth + 1);
            RecurseQuad(center, p1, mp1P, mp1Q, depth + 1);
        }
    }

    // Deterministic per-edge seed built explicitly from coordinates (instead of
    // string hashing, which is not guaranteed stable across runs/platforms).
    // The quantization keeps the hash insensitive to float noise below ~0.001,
    // matching CleanPolygon's snap grid.
    int EdgeSeedHash(Vector2 a, Vector2 b, float seed)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + Mathf.RoundToInt(a.x * 1024f);
            h = h * 31 + Mathf.RoundToInt(a.y * 1024f);
            h = h * 31 + Mathf.RoundToInt(b.x * 1024f);
            h = h * 31 + Mathf.RoundToInt(b.y * 1024f);
            h = h * 31 + Mathf.RoundToInt(seed * 1024f);
            return h;
        }
    }

    void ComputeCellBorders()
    {
        
        // Optimized border extraction using spatial hashing for O(N) performance
        ComputeOptimizedCellBorders();
    }
    
    // Optimized border extraction using spatial hashing for O(N) performance
    void ComputeOptimizedCellBorders()
    {
        // Pre-build neighbor relationships using vertex sharing (O(N))
        BuildNeighborRelationships();
        
        // Create borders from established neighbor relationships
        CreateBordersFromNeighbors();
    }

    // Build neighbor relationships using spatial hashing (O(N) vs O(N²))
    void BuildNeighborRelationships()
    {
        // Clear existing neighbors
        foreach (var cell in mapCells)
        {
            cell.Neighbors = new MapCell[0];
        }
        
        var neighborSets = new Dictionary<MapCell, HashSet<MapCell>>();
        
        // Initialize neighbor sets
        foreach (var cell in mapCells)
        {
            neighborSets[cell] = new HashSet<MapCell>();
        }
        
        // Find neighbors via shared vertices using O(1) hash lookups
        for (int cellIndex = 0; cellIndex < mapCells.Count; cellIndex++)
        {
            MapCell cell = mapCells[cellIndex];
            if (cell.Polygon == null || cell.Polygon.Length < 3) continue;
            
            // Store corners for reference
            cell.Corners.AddRange(cell.Polygon);
            
            // Process each edge to find neighboring cells
            for (int edgeIndex = 0; edgeIndex < cell.Polygon.Length; edgeIndex++)
            {
                Vector2 edgeStart = cell.Polygon[edgeIndex];
                Vector2 edgeEnd = cell.Polygon[(edgeIndex + 1) % cell.Polygon.Length];
                
                // Skip degenerate edges
                if (Vector2.Distance(edgeStart, edgeEnd) < 0.001f) continue;
                
                // Find cells sharing this edge via vertex hashing (O(1) average time!)
                var adjacentCells = FindAdjacentCellsViaVertices(edgeStart, edgeEnd);
                
                // Add bidirectional neighbor relationships
                foreach (var adjacentCell in adjacentCells)
                {
                    if (adjacentCell != cell)
                    {
                        neighborSets[cell].Add(adjacentCell);
                        neighborSets[adjacentCell].Add(cell);
                    }
                }
            }
        }
        
        // Convert neighbor sets to arrays
        foreach (var kvp in neighborSets)
        {
            kvp.Key.Neighbors = kvp.Value.ToArray();
        }
        
        //Debug.Log($"Built neighbor relationships: {mapCells.Count} cells processed");
    }

    // Find adjacent cells for an edge using vertex hashing
    List<MapCell> FindAdjacentCellsViaVertices(Vector2 edgeStart, Vector2 edgeEnd)
    {
        var adjacentCells = new List<MapCell>();
        var processedCells = new HashSet<MapCell>();
        
        // Find cells sharing start vertex
        if (vertexHash.TryGetValue(edgeStart, out var startVertexCells))
        {
            foreach (var (cell, vertexIndex) in startVertexCells)
            {
                if (processedCells.Contains(cell)) continue;
                
                // Check if this cell also has end vertex
                if (CellContainsVertexExact(cell, edgeEnd))
                {
                    // Verify vertices form consecutive edge in this cell's polygon
                    if (AreVerticesConsecutiveInCell(cell, edgeStart, edgeEnd))
                    {
                        adjacentCells.Add(cell);
                        processedCells.Add(cell);
                    }
                }
            }
        }
        
        // Find cells sharing end vertex (not already processed)
        if (vertexHash.TryGetValue(edgeEnd, out var endVertexCells))
        {
            foreach (var (cell, vertexIndex) in endVertexCells)
            {
                if (processedCells.Contains(cell)) continue;
                
                // Check if this cell also has start vertex
                if (CellContainsVertexExact(cell, edgeStart))
                {
                    // Verify vertices form consecutive edge in this cell's polygon
                    if (AreVerticesConsecutiveInCell(cell, edgeStart, edgeEnd))
                    {
                        adjacentCells.Add(cell);
                        processedCells.Add(cell);
                    }
                }
            }
        }
        
        return adjacentCells;
    }

    // Check if cell contains exact vertex using floating point tolerance
    bool CellContainsVertexExact(MapCell cell, Vector2 targetVertex)
    {
        if (cell.Polygon == null || cell.Polygon.Length < 3) return false;
        
        for (int i = 0; i < cell.Polygon.Length; i++)
        {
            if (Vector2.Distance(cell.Polygon[i], targetVertex) < 0.000001f)
                return true;
        }
        return false;
    }

    // Check if two vertices are consecutive in a cell's polygon
    bool AreVerticesConsecutiveInCell(MapCell cell, Vector2 vertexA, Vector2 vertexB)
    {
        if (cell.Polygon == null || cell.Polygon.Length < 3) return false;
        
        int vertexAIndex = -1;
        int vertexBIndex = -1;
        
        // Find indices of both vertices
        for (int i = 0; i < cell.Polygon.Length; i++)
        {
            var vertex = cell.Polygon[i];
            if (vertexAIndex == -1 && Vector2.Distance(vertex, vertexA) < 0.000001f)
                vertexAIndex = i;
            else if (vertexBIndex == -1 && Vector2.Distance(vertex, vertexB) < 0.000001f)
                vertexBIndex = i;
        }
        
        // Check if vertices are consecutive (forward or backward)
        if (vertexAIndex != -1 && vertexBIndex != -1)
        {
            return (vertexAIndex + 1) % cell.Polygon.Length == vertexBIndex ||
                   (vertexBIndex + 1) % cell.Polygon.Length == vertexAIndex;
        }
        
        return false;
    }

// Helper to check if two cells share an edge
    bool CellsShareEdge(MapCell cellA, MapCell cellB, Vector2 edgeStart, Vector2 edgeEnd)
    {
        // Check if cellB contains both vertices of this edge
        return CellContainsVertexExact(cellB, edgeStart) && 
               CellContainsVertexExact(cellB, edgeEnd) &&
               AreVerticesConsecutiveInCell(cellB, edgeStart, edgeEnd);
    }

    // Create borders from established neighbor relationships
    void CreateBordersFromNeighbors()
    {
        var processedEdges = new HashSet<string>();
        
        for (int cellIndex = 0; cellIndex < mapCells.Count; cellIndex++)
        {
            MapCell cell = mapCells[cellIndex];
            if (cell.Polygon == null || cell.Polygon.Length < 3) continue;
            
            // Process each edge to create borders
            for (int edgeIndex = 0; edgeIndex < cell.Polygon.Length; edgeIndex++)
            {
                Vector2 edgeStart = cell.Polygon[edgeIndex];
                Vector2 edgeEnd = cell.Polygon[(edgeIndex + 1) % cell.Polygon.Length];
                
                // Skip degenerate edges
                if (Vector2.Distance(edgeStart, edgeEnd) < 0.001f) continue;
                
                string edgeKey = CreateCanonicalEdgeKey(edgeStart, edgeEnd);
                
                // Skip if already processed
                if (processedEdges.Contains(edgeKey)) continue;
                
// Check if this edge has adjacent cells (from pre-built neighbors)
                bool hasAdjacentCell = false;
                foreach (var neighbor in cell.Neighbors)
                {
                    if (neighbor != null && CellsShareEdge(cell, neighbor, edgeStart, edgeEnd))
                    {
                        hasAdjacentCell = true;

                        // No point list needed: the border IS this polygon segment.
                        // If the cell edges were noised, edgeStart/edgeEnd are already
                        // subdivision points on the noisy curve, so borders, border
                        // loops and rivers follow the edges by nature.
                        var border = new CellBorder(cell, neighbor, edgeStart, edgeEnd);

                        // Filter out very short borders
                        if (border.Length < minimumBorderLength)
                            border.AddFlag(BorderType.Blocked);
                        else
                            border.AddFlag(BorderType.Normal);

                        // Add border to both cells
                        cell.Borders.Add(border);
                        neighbor.Borders.Add(border);
                        break;
                    }
                }
                
                if (!hasAdjacentCell)
                {
                    // This is a map edge border
                    var border = new CellBorder(cell, null, edgeStart, edgeEnd);
                    cell.Borders.Add(border);
                }
                
                // Mark this edge as processed
                processedEdges.Add(edgeKey);
            }
        }
    }
    // Create canonical edge key (consistent regardless of point order).
    // Lexicographic (x, then y) so ties are broken deterministically - the old
    // SqrMagnitude comparison could disagree between the two cells sharing an
    // edge when both endpoints were equidistant from the origin.
    string CreateCanonicalEdgeKey(Vector2 a, Vector2 b)
    {
        // Order points consistently for shared edges
        bool aFirst = IsCanonicalFirst(a, b);
        Vector2 first = aFirst ? a : b;
        Vector2 second = aFirst ? b : a;
        
        return $"{first.x.ToString("F4")}:{first.y.ToString("F4")}|{second.x.ToString("F4")}:{second.y.ToString("F4")}";
    }

    // True when `a` is the canonical first endpoint of edge (a, b).
    // Used by the cache key, the noise quad's center slots and the canonical
    // computation direction - all three must agree exactly.
    bool IsCanonicalFirst(Vector2 a, Vector2 b)
    {
        return a.x < b.x || (a.x == b.x && a.y <= b.y);
    }

    Vector2[] CleanPolygon(Vector2[] polygon)
    {
        if (polygon == null || polygon.Length < 3)
            return polygon;
            
        var cleaned = new List<Vector2>();
        const float snap = 0.001f;
        const float tolerance = snap;

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 current = polygon[i];
            current.x = Mathf.Round(current.x / snap) * snap;
            current.y = Mathf.Round(current.y / snap) * snap;

            // Skip duplicate consecutive vertices
            if (cleaned.Count > 0)
            {
                Vector2 previous = cleaned[cleaned.Count - 1];
                if (Vector2.Distance(current, previous) < tolerance)
                    continue;
            }

            cleaned.Add(current);
        }
        
        // Remove duplicate closing vertex
        if (cleaned.Count > 2)
        {
            Vector2 first = cleaned[0];
            Vector2 last = cleaned[cleaned.Count - 1];
            if (Vector2.Distance(first, last) < tolerance)
                cleaned.RemoveAt(cleaned.Count - 1);
        }
        
        return cleaned.ToArray();
    }

    // Build spatial hash of all cell vertices for O(N) neighbor detection
    void BuildVertexSpatialHash()
    {
        // Clear previous hash
        vertexHash = new Dictionary<Vector2, List<(MapCell, int)>>();
        
        // Build hash from all cell polygons
        for (int cellIndex = 0; cellIndex < mapCells.Count; cellIndex++)
        {
            MapCell cell = mapCells[cellIndex];
            if (cell.Polygon == null || cell.Polygon.Length < 3) continue;
            
            // Hash each vertex of this cell
            for (int vertexIndex = 0; vertexIndex < cell.Polygon.Length; vertexIndex++)
            {
                Vector2 vertex = cell.Polygon[vertexIndex];
                
                if (!vertexHash.ContainsKey(vertex))
                    vertexHash[vertex] = new List<(MapCell, int)>();
                    
                vertexHash[vertex].Add((cell, vertexIndex));
            }
        }
        
        //Debug.Log($"Built vertex spatial hash: {vertexHash.Count} unique vertices from {mapCells.Count} cells");
    }

    void MakeIsland()
    {
        Rect mapBounds = new Rect(-MapSize / 2f, -MapSize / 2f, MapSize, MapSize);
        float borderMargin = CellSize * 3.2f; // how far from the edge counts as "ocean"

        foreach (MapCell cell in mapCells)
        {
            Vector2 c = cell.Center;

            bool isNearEdge =
                c.x <= mapBounds.xMin + borderMargin ||
                c.x >= mapBounds.xMax - borderMargin ||
                c.y <= mapBounds.yMin + borderMargin ||
                c.y >= mapBounds.yMax - borderMargin;

            if (isNearEdge)
            {
                // Set terrain type to ocean for edge cells
                cell.TerrainType = TerrainType.Ocean;
                
                // Update visual with material override or color fallback
                if (cell.Renderer != null)
                {
                    ApplyTerrainMaterial(cell);
                }
            }
        }
    }
    Vector2 ComputeCentroid(Vector2[] polygon)
    {
        if (polygon == null || polygon.Length == 0)
            return Vector2.zero;

        Vector2 sum = Vector2.zero;
        foreach (var p in polygon)
            sum += p;

        return sum / polygon.Length;
    }

    Mesh CreateCellMesh(Vector2[] polygon)
    {
        if (polygon == null || polygon.Length < 3)
        {
            Debug.LogWarning("CreateCellMesh: Invalid polygon with insufficient vertices");
            return null;
        }

        // Convert to 3D vertices
        Vector3[] vertices = polygon
            .Select(p => new Vector3(p.x, p.y, 0f))
            .ToArray();

        // Triangulate the polygon
        int[] triangles = TriangulatePolygon(polygon);
        
        if (triangles == null || triangles.Length == 0)
        {
            Debug.LogWarning($"CreateCellMesh: Triangulation failed for polygon with {polygon.Length} vertices");
            return null;
        }

        // Create mesh with proper error checking
        Mesh mesh = new Mesh { name = "CellMesh" };
        
        try
        {
            mesh.vertices = vertices;
            mesh.triangles = triangles;

            // Generate UV coordinates based on world position
            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                uvs[i] = new Vector2(vertices[i].x / 10, vertices[i].y / 10);
            }
            mesh.uv = uvs;
           
            // Recalculate normals and bounds for proper rendering
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            // Basic mesh validation (Unity Mesh doesn't have IsValid() method)
            if (mesh.vertexCount == 0 || mesh.triangles.Length == 0)
            {
                Debug.LogWarning("CreateCellMesh: Generated mesh is invalid");
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CreateCellMesh: Error creating mesh: {e.Message}");
            return null;
        }

        return mesh;
    }
    

    int[] TriangulatePolygon(Vector2[] polygon)
    {
        if (polygon == null || polygon.Length < 3)
        {
            Debug.LogWarning("RobustTriangulatePolygon: Invalid polygon");
            return System.Array.Empty<int>();
        }

        // Simple case: triangle
        if (polygon.Length == 3)
        {
            return new int[] { 0, 1, 2 };
        }

        List<int> indices = new List<int>();
        List<int> remaining = new List<int>();

        // Initialize vertex indices
        for (int i = 0; i < polygon.Length; i++)
            remaining.Add(i);

        int guard = 0;
        const int maxIterations = 10000; // Prevent infinite loops
        bool remainderFanned = false;

        while (remaining.Count > 3 && guard++ < maxIterations)
        {
            bool earFound = false;
            int bestEarIndex = -1;
            float bestEarArea = 0f;

            // Find the best ear (largest area for numerical stability)
            for (int i = 0; i < remaining.Count; i++)
            {
                int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int curr = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];

                Vector2 a = polygon[prev];
                Vector2 b = polygon[curr];
                Vector2 c = polygon[next];

                // Check if triangle is convex
                if (!IsConvex(a, b, c))
                    continue;

                // Check if it's a valid ear (no other vertices inside)
                if (!IsEar(a, b, c, polygon, remaining))
                    continue;

                // Calculate ear area for quality metric
                float earArea = Mathf.Abs(Cross(a - b, c - b)) * 0.5f;
                if (earArea > bestEarArea)
                {
                    bestEarArea = earArea;
                    bestEarIndex = i;
                }
            }

            if (bestEarIndex >= 0)
            {
                // Use the best ear found
                int i = bestEarIndex;
                int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                int curr = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];

                // Add triangle indices
                indices.Add(prev);
                indices.Add(curr);
                indices.Add(next);

                // Remove the ear vertex
                remaining.RemoveAt(i);
                earFound = true;
            }

            if (!earFound)
            {
                // No ear left (typically a degenerate sliver with no convex
                // vertex at all). Do NOT throw away the ears that were already
                // clipped: fanning the WHOLE polygon from a single vertex
                // produces triangles far outside the cell (measured: up to 190x
                // the cell area). Fan only the vertices that are left over -
                // every clipped ear is kept and the mesh still covers the
                // polygon.
                triangulationsFallback++;
                AddFanTriangles(indices, remaining);
                remainderFanned = true;
                break;
            }
        }

        // Handle final triangle if needed
        if (!remainderFanned && remaining.Count == 3)
        {
            indices.Add(remaining[0]);
            indices.Add(remaining[1]);
            indices.Add(remaining[2]);
        }
        else if (!remainderFanned && remaining.Count > 3)
        {
            // Iteration guard tripped. Same rule as above: keep the clipped
            // ears and fan only the remainder.
            triangulationsFallback++;
            AddFanTriangles(indices, remaining);
        }

        // Validate result
        if (indices.Count == 0 || indices.Count % 3 != 0)
        {
            Debug.LogError($"RobustTriangulatePolygon: Invalid triangulation result with {indices.Count} indices");
            return System.Array.Empty<int>();
        }

        return indices.ToArray();
    }

    // Fans a vertex ring into triangles (v0, vi, vi+1). Used as a graceful
    // degradation when ear clipping runs out of ears: the already clipped
    // ears stay in the mesh and only the remainder is fanned.
    static void AddFanTriangles(List<int> indices, List<int> ring)
    {
        if (ring == null || ring.Count < 3) return;
        for (int i = 1; i < ring.Count - 1; i++)
        {
            indices.Add(ring[0]);
            indices.Add(ring[i]);
            indices.Add(ring[i + 1]);
        }
    }

    bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        return Cross(b - a, c - b) > 0f;
    }

    float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    bool IsEar(Vector2 a, Vector2 b, Vector2 c, Vector2[] polygon, List<int> remaining)
    {
        // Check if any other vertex from the remaining vertices is inside triangle abc
        foreach (int vertexIndex in remaining)
        {
            Vector2 point = polygon[vertexIndex];
            // Skip the three vertices that form the current triangle
            if ((point - a).sqrMagnitude < 0.0001f || 
                (point - b).sqrMagnitude < 0.0001f || 
                (point - c).sqrMagnitude < 0.0001f)
                continue;
                
            if (PointInTriangle(point, a, b, c))
                return false;
        }
        return true;
    }

    bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }


    // Clips a polygon (given as IPoint[]) to an axis-aligned rectangle (Sutherland�Hodgman).
    // Returns a new array of IPoint clipped to the rect. Uses DelaunatorSharp.Point for new points.
    Vector2[] ClipPolygonToRect(Vector2[] subject, Rect rect)
    {
        if (subject == null || subject.Length == 0)
            return System.Array.Empty<Vector2>();

        List<Vector2> input = subject.ToList();

        input = ClipAgainstEdge(input, p => p.x >= rect.xMin,
            (s, p) => IntersectWithVertical(s, p, rect.xMin));

        input = ClipAgainstEdge(input, p => p.x <= rect.xMax,
            (s, p) => IntersectWithVertical(s, p, rect.xMax));

        input = ClipAgainstEdge(input, p => p.y >= rect.yMin,
            (s, p) => IntersectWithHorizontal(s, p, rect.yMin));

        input = ClipAgainstEdge(input, p => p.y <= rect.yMax,
            (s, p) => IntersectWithHorizontal(s, p, rect.yMax));

        return input.ToArray();
    }


    delegate bool InsideTest(IPoint p);
    delegate IPoint ComputeIntersection(IPoint s, IPoint p);

    List<Vector2> ClipAgainstEdge(List<Vector2> subject, System.Func<Vector2, bool> inside, System.Func<Vector2, Vector2, Vector2> intersect)
    {
        List<Vector2> output = new List<Vector2>();
        if (subject.Count == 0) return output;

        Vector2 prev = subject[^1];
        bool prevInside = inside(prev);

        foreach (var curr in subject)
        {
            bool currInside = inside(curr);

            if (currInside)
            {
                if (!prevInside)
                    output.Add(intersect(prev, curr));

                output.Add(curr);
            }
            else if (prevInside)
            {
                output.Add(intersect(prev, curr));
            }

            prev = curr;
            prevInside = currInside;
        }

        return output;
    }


    // Intersect segment s->p with vertical line x = xEdge
    Vector2 IntersectWithVertical(Vector2 s, Vector2 p, float x)
    {
        float dx = p.x - s.x;
        if (Mathf.Abs(dx) < 1e-6f)
            return s;

        float t = (x - s.x) / dx;
        return new Vector2(x, Mathf.Lerp(s.y, p.y, t));
    }

    Vector2 IntersectWithHorizontal(Vector2 s, Vector2 p, float y)
    {
        float dy = p.y - s.y;
        if (Mathf.Abs(dy) < 1e-6f)
            return s;

        float t = (y - s.y) / dy;
        return new Vector2(Mathf.Lerp(s.x, p.x, t), y);
    }

}