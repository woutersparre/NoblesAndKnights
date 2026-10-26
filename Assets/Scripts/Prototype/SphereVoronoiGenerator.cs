using System.Collections.Generic;
using UnityEngine;

namespace SphereVoronoiPrototype
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SphereVoronoiGenerator : MonoBehaviour
    {
        [Header("Generation")]
        [Tooltip("Number of cell sites generated on the sphere.")]
        [Min(4)] public int seedCount = 120;
        public bool regenerateOnStart = true;

        [Header("Pattern")]
        public int patternSeed = 0;

        [Header("Debug")]
        [Tooltip("Include cells marked unplayable by the polar mask in the rendered mesh.")]
        public bool renderUnplayableCells = true;
        public bool drawSeeds = false;
        public bool drawHull = false;
        [Tooltip("Shows Voronoi vertices.")]
        public bool drawCircumcenters = false;
        [Range(0.05f, 1f)] public float debugGizmoScale = 0.2f;
        [Tooltip("Shows the selected cell outline in red.")]
        public bool drawCellOutlines = true;
        public int debugCellId = 0;

        [Header("Gameplay")]
        [Tooltip("Make cells near either pole unplayable. Set to 0 to disable the polar cap.")]
        public bool disablePolarCells = true;
        [Range(0f, 30f)] public float polarExclusionDegrees = 10f;

        [Header("References")]
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public Material cellMaterial;

        public List<ProtoCell> Cells { get; private set; }
        public Mesh CombinedMesh { get; private set; }
        public int[] TriangleToCellId { get; private set; }
        public int LandCellCount { get; private set; }
        public int PlayableCellCount { get; private set; }

        private Vector3[] seeds;
        private Vector3[] circumcenters;
        private List<(int a, int b, int c)> hullTriangles;
        private int[][] hullNeighbors;
        private bool hullAdjacencyValid;
        private long hullDistanceChecks;
        private bool[] landCells;

        void Reset()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        void Awake()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

            if (regenerateOnStart)
                Generate();
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            seedCount = Mathf.Max(4, seedCount);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Cells = new List<ProtoCell>();
            GenerateSeeds();

            RebuildCells();
            RebuildMesh();

            stopwatch.Stop();
            Debug.Log($"[SphereVoronoiGenerator] Generated {LandCellCount} land cells " +
                $"({PlayableCellCount} playable) in {stopwatch.ElapsedMilliseconds} ms " +
                $"(hull face checks: {hullDistanceChecks})");
        }

        void RebuildMesh()
        {
            ComputeLandCells();
            BuildMesh();
        }

        void ComputeLandCells()
        {
            int count = Cells != null ? Cells.Count : 0;
            landCells = new bool[count];

            if (count == 0)
            {
                LandCellCount = 0;
                PlayableCellCount = 0;
                return;
            }

            for (int i = 0; i < count; i++)
                landCells[i] = true;

            LandCellCount = count;
            FinalizePlayabilityAndNeighbors();
        }

        void FinalizePlayabilityAndNeighbors()
        {
            PlayableCellCount = 0;
            if (Cells == null)
                return;

            bool applyPolarMask = disablePolarCells && polarExclusionDegrees > 0f;
            int northPoleCell = applyPolarMask ? FindNearestSeedIndex(Vector3.up) : -1;
            int southPoleCell = applyPolarMask ? FindNearestSeedIndex(Vector3.down) : -1;

            for (int i = 0; i < Cells.Count; i++)
            {
                ProtoCell cell = Cells[i];
                bool playable = IsLandCell(i);
                if (playable && applyPolarMask)
                    playable = i != northPoleCell && i != southPoleCell
                        && !TouchesPolarCap(cell, polarExclusionDegrees);

                cell.IsPlayable = playable;
                if (playable)
                    PlayableCellCount++;
            }

            RebuildGameplayNeighbors();
        }

        bool TouchesPolarCap(ProtoCell cell)
        {
            return TouchesPolarCap(cell, polarExclusionDegrees);
        }

        bool TouchesPolarCap(ProtoCell cell, float capDegrees)
        {
            float minimumAbsY = Mathf.Cos(Mathf.Clamp(capDegrees, 0f, 30f) * Mathf.Deg2Rad);
            if (Mathf.Abs(cell.Seed.y) >= minimumAbsY)
                return true;

            if (cell.Vertices == null)
                return false;

            for (int i = 0; i < cell.Vertices.Length; i++)
            {
                Vector3 a = cell.Vertices[i];
                Vector3 b = cell.Vertices[(i + 1) % cell.Vertices.Length];
                if (Mathf.Abs(a.y) >= minimumAbsY)
                    return true;
                if (Mathf.Abs(Vector3.Slerp(a, b, 0.25f).y) >= minimumAbsY
                    || Mathf.Abs(Vector3.Slerp(a, b, 0.5f).y) >= minimumAbsY
                    || Mathf.Abs(Vector3.Slerp(a, b, 0.75f).y) >= minimumAbsY)
                    return true;
            }

            return false;
        }

        int FindNearestSeedIndex(Vector3 direction)
        {
            if (Cells == null || Cells.Count == 0)
                return -1;

            direction.Normalize();
            int nearestId = -1;
            float nearestDot = float.NegativeInfinity;
            for (int i = 0; i < Cells.Count; i++)
            {
                float dot = Vector3.Dot(direction, Cells[i].Seed);
                if (dot > nearestDot)
                {
                    nearestDot = dot;
                    nearestId = i;
                }
            }
            return nearestId;
        }

        void RebuildGameplayNeighbors()
        {
            int count = Cells != null ? Cells.Count : 0;
            var neighbors = new HashSet<int>[count];
            for (int i = 0; i < count; i++)
            {
                neighbors[i] = new HashSet<int>();
                if (Cells[i] != null)
                    Cells[i].Neighbors = System.Array.Empty<int>();
            }

            for (int i = 0; i < count; i++)
            {
                if (Cells[i] == null || !Cells[i].IsPlayable)
                    continue;

                int[] candidates = hullAdjacencyValid && hullNeighbors != null
                    && i < hullNeighbors.Length
                    ? hullNeighbors[i]
                    : null;
                int candidateCount = candidates != null && candidates.Length > 0
                    ? candidates.Length
                    : count;

                for (int k = 0; k < candidateCount; k++)
                {
                    int other = candidates != null && candidates.Length > 0 ? candidates[k] : k;
                    if (other <= i || other >= count || Cells[other] == null || !Cells[other].IsPlayable)
                        continue;

                    if (!HasBisectorEdge(i, other) || !HasBisectorEdge(other, i))
                        continue;

                    neighbors[i].Add(other);
                    neighbors[other].Add(i);
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (Cells[i] == null)
                    continue;
                var sorted = new List<int>(neighbors[i]);
                sorted.Sort();
                Cells[i].Neighbors = sorted.ToArray();
            }
        }

        bool HasBisectorEdge(int cellId, int otherCellId)
        {
            if (cellId < 0 || otherCellId < 0
                || Cells == null || cellId >= Cells.Count || otherCellId >= Cells.Count)
                return false;

            Vector3 normal = (seeds[cellId] - seeds[otherCellId]).normalized;
            if (normal.sqrMagnitude < 1e-12f)
                return false;

            ProtoCell cell = Cells[cellId];
            if (cell.Vertices == null)
                return false;

            for (int i = 0; i < cell.Vertices.Length; i++)
            {
                Vector3 a = cell.Vertices[i];
                Vector3 b = cell.Vertices[(i + 1) % cell.Vertices.Length];
                if ((a - b).sqrMagnitude < 1e-10f)
                    continue;

                if (Mathf.Abs(Vector3.Dot(a, normal)) <= 5e-5f
                    && Mathf.Abs(Vector3.Dot(b, normal)) <= 5e-5f)
                    return true;
            }

            return false;
        }

        void GenerateSeeds()
        {
            seeds = new Vector3[seedCount];
            var random = new System.Random(patternSeed);
            // Distribute evenly over full sphere using Fibonacci-like uniform distribution
            float goldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;
            float angleIncrement = 2f * Mathf.PI * goldenRatio;
            for (int i = 0; i < seedCount; i++)
            {
                float t = (i + 0.5f) / seedCount;
                float phi = Mathf.Acos(1f - 2f * t);
                float theta = angleIncrement * i;
                seeds[i] = new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(theta),
                    Mathf.Cos(phi),
                    Mathf.Sin(phi) * Mathf.Sin(theta)
                ).normalized;
            }
            // Add small jitter if desired? keep deterministic by patternSeed via random for now
            for (int i = 0; i < seedCount; i++)
            {
                // apply tiny jitter using patternSeed
                float jx = ((float)(random.NextDouble() - 0.5) * 0.01f);
                float jy = ((float)(random.NextDouble() - 0.5) * 0.01f);
                float jz = ((float)(random.NextDouble() - 0.5) * 0.01f);
                seeds[i] = (seeds[i] + new Vector3(jx, jy, jz)).normalized;
            }
        }

        void RebuildCells()
        {
            ComputeHull();
            ComputeCircumcenters();
            BuildCells();
            CacheHullNeighbors();
        }

        private struct HullFace
        {
            public int A;
            public int B;
            public int C;
            public Vector3 Normal;
        }

        private sealed class QuickHullFace
        {
            public int A;
            public int B;
            public int C;
            public readonly double NormalX;
            public readonly double NormalY;
            public readonly double NormalZ;
            public int Neighbor0 = -1;
            public int Neighbor1 = -1;
            public int Neighbor2 = -1;
            public List<int> OutsideSites;
            public bool IsActive = true;
            public bool IsQueued;

            public QuickHullFace(int a, int b, int c, double normalX, double normalY, double normalZ)
            {
                A = a;
                B = b;
                C = c;
                NormalX = normalX;
                NormalY = normalY;
                NormalZ = normalZ;
            }

            public int EdgeStart(int edge) => edge == 0 ? A : edge == 1 ? B : C;
            public int EdgeEnd(int edge) => edge == 0 ? B : edge == 1 ? C : A;
            public int Neighbor(int edge) => edge == 0 ? Neighbor0 : edge == 1 ? Neighbor1 : Neighbor2;

            public void SetNeighbor(int edge, int faceId)
            {
                if (edge == 0) Neighbor0 = faceId;
                else if (edge == 1) Neighbor1 = faceId;
                else Neighbor2 = faceId;
            }
        }

        private struct QuickHullEdgeRef
        {
            public int FaceId;
            public int EdgeIndex;
            public int Start;
            public int End;
        }

        private struct QuickHullHorizonEdge
        {
            public int Start;
            public int End;
            public int KeptFaceId;
            public int KeptEdgeIndex;
        }

        void ComputeHull()
        {
            hullTriangles = new List<(int a, int b, int c)>();
            hullAdjacencyValid = false;
            hullDistanceChecks = 0;

            if (seeds == null || seeds.Length < 4)
                return;

            if (TryComputeHullConflictGraph())
                return;

            Debug.LogWarning("[SphereVoronoiGenerator] Conflict-graph hull validation failed; using the slower incremental fallback.", this);
            ComputeHullByFullFaceScan();
        }

        bool TryComputeHullConflictGraph()
        {
            int count = seeds.Length;
            const double visibleEpsilon = 1e-10;

            int a = 0;
            int b = -1;
            double best = 0.0;
            for (int i = 1; i < count; i++)
            {
                double distance = DistanceSquaredPrecise(seeds[i], seeds[a]);
                if (distance > best)
                {
                    best = distance;
                    b = i;
                }
            }
            if (b < 0 || best < 1e-20)
                return false;

            int c = -1;
            best = 0.0;
            for (int i = 0; i < count; i++)
            {
                double areaSquared = TriangleNormalSquaredPrecise(seeds[a], seeds[b], seeds[i]);
                if (areaSquared > best)
                {
                    best = areaSquared;
                    c = i;
                }
            }
            if (c < 0 || best < 1e-20)
                return false;

            TriangleNormalPrecise(seeds[a], seeds[b], seeds[c], out double normalX,
                out double normalY, out double normalZ);
            double normalLength = System.Math.Sqrt(normalX * normalX + normalY * normalY + normalZ * normalZ);
            if (normalLength < 1e-14)
                return false;
            normalX /= normalLength;
            normalY /= normalLength;
            normalZ /= normalLength;

            int d = -1;
            best = 0.0;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = seeds[i];
                Vector3 origin = seeds[a];
                double distance = System.Math.Abs(normalX * ((double)point.x - origin.x)
                    + normalY * ((double)point.y - origin.y)
                    + normalZ * ((double)point.z - origin.z));
                if (distance > best)
                {
                    best = distance;
                    d = i;
                }
            }
            if (d < 0 || best < 1e-12)
                return false;

            Vector3 interior = (seeds[a] + seeds[b] + seeds[c] + seeds[d]) * 0.25f;
            var faces = new List<QuickHullFace>(count * 2);

            QuickHullFace CreateFace(int i0, int i1, int i2)
            {
                TriangleNormalPrecise(seeds[i0], seeds[i1], seeds[i2],
                    out double nx, out double ny, out double nz);
                double length = System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (length < 1e-14)
                    return null;

                nx /= length;
                ny /= length;
                nz /= length;
                Vector3 point = seeds[i0];
                double interiorSide = nx * ((double)point.x - interior.x)
                    + ny * ((double)point.y - interior.y)
                    + nz * ((double)point.z - interior.z);
                if (interiorSide < 0.0)
                {
                    int temp = i1;
                    i1 = i2;
                    i2 = temp;
                    nx = -nx;
                    ny = -ny;
                    nz = -nz;
                }

                return new QuickHullFace(i0, i1, i2, nx, ny, nz);
            }

            double SignedDistance(QuickHullFace face, int pointId)
            {
                hullDistanceChecks++;
                Vector3 point = seeds[pointId];
                Vector3 anchor = seeds[face.A];
                return face.NormalX * ((double)point.x - anchor.x)
                    + face.NormalY * ((double)point.y - anchor.y)
                    + face.NormalZ * ((double)point.z - anchor.z);
            }

            void EnqueueFace(Queue<int> queue, int faceId)
            {
                QuickHullFace face = faces[faceId];
                if (face.IsQueued)
                    return;
                face.IsQueued = true;
                queue.Enqueue(faceId);
            }

            QuickHullFace initialFace = CreateFace(a, b, c);
            if (initialFace == null) return false;
            faces.Add(initialFace);
            initialFace = CreateFace(a, b, d);
            if (initialFace == null) return false;
            faces.Add(initialFace);
            initialFace = CreateFace(a, c, d);
            if (initialFace == null) return false;
            faces.Add(initialFace);
            initialFace = CreateFace(b, c, d);
            if (initialFace == null) return false;
            faces.Add(initialFace);

            var edgeMap = new Dictionary<long, QuickHullEdgeRef>(12);
            for (int faceId = 0; faceId < faces.Count; faceId++)
            {
                QuickHullFace face = faces[faceId];
                for (int edge = 0; edge < 3; edge++)
                {
                    int start = face.EdgeStart(edge);
                    int end = face.EdgeEnd(edge);
                    long key = HullEdgeKey(start, end);
                    if (edgeMap.TryGetValue(key, out QuickHullEdgeRef other))
                    {
                        if (other.Start != end || other.End != start)
                            return false;
                        face.SetNeighbor(edge, other.FaceId);
                        faces[other.FaceId].SetNeighbor(other.EdgeIndex, faceId);
                        edgeMap.Remove(key);
                    }
                    else
                    {
                        edgeMap.Add(key, new QuickHullEdgeRef
                        {
                            FaceId = faceId,
                            EdgeIndex = edge,
                            Start = start,
                            End = end
                        });
                    }
                }
            }
            if (edgeMap.Count != 0)
                return false;

            var isInitialSite = new bool[count];
            isInitialSite[a] = isInitialSite[b] = isInitialSite[c] = isInitialSite[d] = true;
            var faceQueue = new Queue<int>();

            for (int pointId = 0; pointId < count; pointId++)
            {
                if (isInitialSite[pointId])
                    continue;

                double farthest = visibleEpsilon;
                int owner = -1;
                for (int faceId = 0; faceId < faces.Count; faceId++)
                {
                    double distance = SignedDistance(faces[faceId], pointId);
                    if (distance > farthest)
                    {
                        farthest = distance;
                        owner = faceId;
                    }
                }

                if (owner >= 0)
                {
                    QuickHullFace face = faces[owner];
                    if (face.OutsideSites == null)
                        face.OutsideSites = new List<int>();
                    face.OutsideSites.Add(pointId);
                    EnqueueFace(faceQueue, owner);
                }
            }

            var visibleSet = new HashSet<int>();
            var visitedSet = new HashSet<int>();
            var visibleIds = new List<int>();
            var visibleStack = new Stack<int>();
            var horizon = new List<QuickHullHorizonEdge>();
            var orphanSites = new List<int>();
            var orphanSet = new HashSet<int>();
            var newFaceIds = new List<int>();
            var apexEdgeMap = new Dictionary<long, QuickHullEdgeRef>();

            while (faceQueue.Count > 0)
            {
                int seedFaceId = faceQueue.Dequeue();
                QuickHullFace seedFace = faces[seedFaceId];
                seedFace.IsQueued = false;
                if (!seedFace.IsActive || seedFace.OutsideSites == null || seedFace.OutsideSites.Count == 0)
                    continue;

                int eyeSite = -1;
                double farthest = visibleEpsilon;
                for (int i = 0; i < seedFace.OutsideSites.Count; i++)
                {
                    int candidate = seedFace.OutsideSites[i];
                    double distance = SignedDistance(seedFace, candidate);
                    if (distance > farthest)
                    {
                        farthest = distance;
                        eyeSite = candidate;
                    }
                }
                if (eyeSite < 0)
                {
                    seedFace.OutsideSites = null;
                    continue;
                }

                visibleSet.Clear();
                visitedSet.Clear();
                visibleIds.Clear();
                visibleStack.Clear();
                visibleStack.Push(seedFaceId);
                while (visibleStack.Count > 0)
                {
                    int faceId = visibleStack.Pop();
                    if (faceId < 0 || faceId >= faces.Count || !visitedSet.Add(faceId))
                        continue;

                    QuickHullFace face = faces[faceId];
                    if (!face.IsActive || SignedDistance(face, eyeSite) <= visibleEpsilon)
                        continue;

                    visibleSet.Add(faceId);
                    visibleIds.Add(faceId);
                    for (int edge = 0; edge < 3; edge++)
                    {
                        int neighbor = face.Neighbor(edge);
                        if (neighbor < 0 || neighbor >= faces.Count)
                            return false;
                        if (!visitedSet.Contains(neighbor))
                            visibleStack.Push(neighbor);
                    }
                }
                if (visibleIds.Count == 0)
                    return false;

                horizon.Clear();
                orphanSites.Clear();
                orphanSet.Clear();
                for (int i = 0; i < visibleIds.Count; i++)
                {
                    int faceId = visibleIds[i];
                    QuickHullFace face = faces[faceId];
                    for (int edge = 0; edge < 3; edge++)
                    {
                        int neighborId = face.Neighbor(edge);
                        if (visibleSet.Contains(neighborId))
                            continue;

                        QuickHullFace keptFace = faces[neighborId];
                        int keptEdge = FindDirectedHullEdge(keptFace,
                            face.EdgeEnd(edge), face.EdgeStart(edge));
                        if (keptEdge < 0)
                            return false;

                        horizon.Add(new QuickHullHorizonEdge
                        {
                            Start = face.EdgeStart(edge),
                            End = face.EdgeEnd(edge),
                            KeptFaceId = neighborId,
                            KeptEdgeIndex = keptEdge
                        });
                    }

                    if (face.OutsideSites == null)
                        continue;
                    for (int site = 0; site < face.OutsideSites.Count; site++)
                    {
                        int orphan = face.OutsideSites[site];
                        if (orphan != eyeSite && orphanSet.Add(orphan))
                            orphanSites.Add(orphan);
                    }
                    face.OutsideSites = null;
                }
                if (horizon.Count < 3)
                    return false;

                for (int i = 0; i < horizon.Count; i++)
                {
                    QuickHullHorizonEdge edge = horizon[i];
                    faces[edge.KeptFaceId].SetNeighbor(edge.KeptEdgeIndex, -1);
                }
                for (int i = 0; i < visibleIds.Count; i++)
                {
                    QuickHullFace face = faces[visibleIds[i]];
                    face.IsActive = false;
                    face.IsQueued = false;
                }

                newFaceIds.Clear();
                apexEdgeMap.Clear();
                for (int i = 0; i < horizon.Count; i++)
                {
                    QuickHullHorizonEdge edge = horizon[i];
                    QuickHullFace newFace = CreateFace(edge.Start, edge.End, eyeSite);
                    if (newFace == null)
                        return false;

                    int horizonEdgeIndex = FindDirectedHullEdge(newFace, edge.Start, edge.End);
                    if (horizonEdgeIndex < 0)
                        return false;

                    int newFaceId = faces.Count;
                    faces.Add(newFace);
                    newFaceIds.Add(newFaceId);
                    newFace.SetNeighbor(horizonEdgeIndex, edge.KeptFaceId);
                    faces[edge.KeptFaceId].SetNeighbor(edge.KeptEdgeIndex, newFaceId);

                    for (int newEdge = 0; newEdge < 3; newEdge++)
                    {
                        if (newEdge == horizonEdgeIndex)
                            continue;

                        int startEdge = newFace.EdgeStart(newEdge);
                        int endEdge = newFace.EdgeEnd(newEdge);
                        long keyEdge = HullEdgeKey(startEdge, endEdge);
                        if (apexEdgeMap.TryGetValue(keyEdge, out QuickHullEdgeRef otherRef))
                        {
                            if (otherRef.Start != endEdge || otherRef.End != startEdge)
                                return false;
                            newFace.SetNeighbor(newEdge, otherRef.FaceId);
                            faces[otherRef.FaceId].SetNeighbor(otherRef.EdgeIndex, newFaceId);
                            apexEdgeMap.Remove(keyEdge);
                        }
                        else
                        {
                            apexEdgeMap.Add(keyEdge, new QuickHullEdgeRef
                            {
                                FaceId = newFaceId,
                                EdgeIndex = newEdge,
                                Start = startEdge,
                                End = endEdge
                            });
                        }
                    }
                }
                if (apexEdgeMap.Count != 0)
                    return false;

                for (int i = 0; i < orphanSites.Count; i++)
                {
                    int pointId = orphanSites[i];
                    double bestDistance = visibleEpsilon;
                    int owner = -1;
                    for (int f = 0; f < newFaceIds.Count; f++)
                    {
                        int newFaceId = newFaceIds[f];
                        double distance = SignedDistance(faces[newFaceId], pointId);
                        if (distance > bestDistance)
                        {
                            bestDistance = distance;
                            owner = newFaceId;
                        }
                    }

                    if (owner >= 0)
                    {
                        QuickHullFace face = faces[owner];
                        if (face.OutsideSites == null)
                            face.OutsideSites = new List<int>();
                        face.OutsideSites.Add(pointId);
                        EnqueueFace(faceQueue, owner);
                    }
                }
            }

            int activeFaceCount = 0;
            for (int i = 0; i < faces.Count; i++)
            {
                if (faces[i].IsActive)
                    activeFaceCount++;
            }

            hullTriangles = new List<(int a, int b, int c)>(activeFaceCount);
            for (int i = 0; i < faces.Count; i++)
            {
                QuickHullFace face = faces[i];
                if (face.IsActive)
                    hullTriangles.Add((face.A, face.B, face.C));
            }

            hullAdjacencyValid = ValidateHullTopology();
            return hullAdjacencyValid;
        }

        static double DistanceSquaredPrecise(Vector3 a, Vector3 b)
        {
            double x = (double)a.x - b.x;
            double y = (double)a.y - b.y;
            double z = (double)a.z - b.z;
            return x * x + y * y + z * z;
        }

        static double TriangleNormalSquaredPrecise(Vector3 a, Vector3 b, Vector3 c)
        {
            TriangleNormalPrecise(a, b, c, out double x, out double y, out double z);
            return x * x + y * y + z * z;
        }

        static void TriangleNormalPrecise(Vector3 a, Vector3 b, Vector3 c,
            out double x, out double y, out double z)
        {
            double abx = (double)b.x - a.x;
            double aby = (double)b.y - a.y;
            double abz = (double)b.z - a.z;
            double acx = (double)c.x - a.x;
            double acy = (double)c.y - a.y;
            double acz = (double)c.z - a.z;
            x = aby * acz - abz * acy;
            y = abz * acx - abx * acz;
            z = abx * acy - aby * acx;
        }

        static long HullEdgeKey(int a, int b)
        {
            int low = a < b ? a : b;
            int high = a < b ? b : a;
            return ((long)low << 32) | (uint)high;
        }

        static int FindDirectedHullEdge(QuickHullFace face, int start, int end)
        {
            for (int edge = 0; edge < 3; edge++)
            {
                if (face.EdgeStart(edge) == start && face.EdgeEnd(edge) == end)
                    return edge;
            }
            return -1;
        }

        void ComputeHullByFullFaceScan()
        {
            hullTriangles = new List<(int a, int b, int c)>();
            hullAdjacencyValid = false;
            int count = seeds.Length;
            if (count < 4) return;

            int a = 0;
            int b = -1;
            float best = 0f;
            for (int i = 1; i < count; i++)
            {
                float distance = (seeds[i] - seeds[a]).sqrMagnitude;
                if (distance > best)
                {
                    best = distance;
                    b = i;
                }
            }
            if (b < 0 || best < 1e-10f) return;

            Vector3 ab = seeds[b] - seeds[a];
            int c = -1;
            best = 0f;
            for (int i = 0; i < count; i++)
            {
                float distance = Vector3.Cross(ab, seeds[i] - seeds[a]).sqrMagnitude;
                if (distance > best)
                {
                    best = distance;
                    c = i;
                }
            }
            if (c < 0 || best < 1e-10f) return;

            Vector3 abcNormal = Vector3.Cross(seeds[b] - seeds[a], seeds[c] - seeds[a]).normalized;
            int dIdx = -1;
            best = 0f;
            for (int i = 0; i < count; i++)
            {
                float distance = Mathf.Abs(Vector3.Dot(abcNormal, seeds[i] - seeds[a]));
                if (distance > best)
                {
                    best = distance;
                    dIdx = i;
                }
            }
            if (dIdx < 0 || best < 1e-7f) return;

            Vector3 interior = (seeds[a] + seeds[b] + seeds[c] + seeds[dIdx]) * 0.25f;

            const float visibleEpsilon = 1e-6f;
            var faces = new List<HullFace>(count * 2);
            var visibleFaces = new List<int>();
            var edgeCounts = new Dictionary<long, int>();
            var horizon = new List<(int a, int b)>();

            void AddFace(int i0, int i1, int i2)
            {
                Vector3 p0 = seeds[i0];
                Vector3 p1 = seeds[i1];
                Vector3 p2 = seeds[i2];
                Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
                float length = normal.magnitude;
                if (length < 1e-8f) return;

                normal /= length;
                if (Vector3.Dot(normal, p0 - interior) < 0f)
                {
                    int temp = i1;
                    i1 = i2;
                    i2 = temp;
                    normal = -normal;
                }

                faces.Add(new HullFace { A = i0, B = i1, C = i2, Normal = normal });
            }

            void AddEdge(int u, int v)
            {
                int lowE = u < v ? u : v;
                int highE = u < v ? v : u;
                long keyE = ((long)lowE << 32) | (uint)highE;
                edgeCounts.TryGetValue(keyE, out int usesE);
                edgeCounts[keyE] = usesE + 1;
            }

            AddFace(a, b, c);
            AddFace(a, b, dIdx);
            AddFace(a, c, dIdx);
            AddFace(b, c, dIdx);

            for (int p = 0; p < count; p++)
            {
                if (p == a || p == b || p == c || p == dIdx) continue;

                visibleFaces.Clear();
                for (int f = 0; f < faces.Count; f++)
                {
                    HullFace face = faces[f];
                    hullDistanceChecks++;
                    if (Vector3.Dot(face.Normal, seeds[p] - seeds[face.A]) > visibleEpsilon)
                        visibleFaces.Add(f);
                }

                if (visibleFaces.Count == 0) continue;

                edgeCounts.Clear();
                for (int i = 0; i < visibleFaces.Count; i++)
                {
                    HullFace face = faces[visibleFaces[i]];
                    AddEdge(face.A, face.B);
                    AddEdge(face.B, face.C);
                    AddEdge(face.C, face.A);
                }

                horizon.Clear();
                foreach (KeyValuePair<long, int> edge in edgeCounts)
                {
                    if (edge.Value != 1) continue;

                    int lowH = (int)(edge.Key >> 32);
                    int highH = (int)(edge.Key & 0xFFFFFFFFL);
                    horizon.Add((lowH, highH));
                }

                int write = 0;
                int visibleIndex = 0;
                for (int read = 0; read < faces.Count; read++)
                {
                    if (visibleIndex < visibleFaces.Count && visibleFaces[visibleIndex] == read)
                    {
                        visibleIndex++;
                        continue;
                    }

                    faces[write++] = faces[read];
                }
                faces.RemoveRange(write, faces.Count - write);

                for (int i = 0; i < horizon.Count; i++)
                    AddFace(horizon[i].a, horizon[i].b, p);
            }

            hullTriangles.Capacity = faces.Count;
            for (int i = 0; i < faces.Count; i++)
                hullTriangles.Add((faces[i].A, faces[i].B, faces[i].C));

            hullAdjacencyValid = ValidateHullTopology();
            if (!hullAdjacencyValid)
                Debug.LogWarning("[SphereVoronoiGenerator] Hull topology failed validation.", this);
        }

        bool ValidateHullTopology()
        {
            if (hullTriangles == null || hullTriangles.Count < 4 || seeds == null || seeds.Length < 4)
                return false;

            var used = new bool[seeds.Length];
            var edgeUses = new Dictionary<long, (int count, int orientation)>();
            for (int i = 0; i < hullTriangles.Count; i++)
            {
                var face = hullTriangles[i];
                if (face.a < 0 || face.a >= seeds.Length
                    || face.b < 0 || face.b >= seeds.Length
                    || face.c < 0 || face.c >= seeds.Length
                    || face.a == face.b || face.b == face.c || face.c == face.a)
                    return false;

                used[face.a] = used[face.b] = used[face.c] = true;
                AddOrientedEdge(edgeUses, face.a, face.b);
                AddOrientedEdge(edgeUses, face.b, face.c);
                AddOrientedEdge(edgeUses, face.c, face.a);
            }

            for (int i = 0; i < used.Length; i++)
            {
                if (!used[i]) return false;
            }

            foreach (var edge in edgeUses.Values)
            {
                if (edge.count != 2 || edge.orientation != 0)
                    return false;
            }

            return seeds.Length - edgeUses.Count + hullTriangles.Count == 2;
        }

        static void AddOrientedEdge(Dictionary<long, (int count, int orientation)> edgeUses, int from, int to)
        {
            int low = from < to ? from : to;
            int high = from < to ? to : from;
            long key = ((long)low << 32) | (uint)high;
            edgeUses.TryGetValue(key, out var use);
            use.count++;
            use.orientation += from < to ? 1 : -1;
            edgeUses[key] = use;
        }

        void CacheHullNeighbors()
        {
            hullNeighbors = Cells != null ? new int[Cells.Count][] : null;
            if (Cells == null)
                return;

            for (int i = 0; i < Cells.Count; i++)
                hullNeighbors[i] = Cells[i].Neighbors != null
                    ? (int[])Cells[i].Neighbors.Clone()
                    : System.Array.Empty<int>();
        }

        void ComputeCircumcenters()
        {
            circumcenters = new Vector3[hullTriangles.Count];

            for (int t = 0; t < hullTriangles.Count; t++)
            {
                var tri = hullTriangles[t];
                Vector3 p1 = seeds[tri.a];
                Vector3 p2 = seeds[tri.b];
                Vector3 p3 = seeds[tri.c];

                Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1);
                float length = normal.magnitude;

                if (length < 1e-7f)
                {
                    circumcenters[t] = (p1 + p2 + p3).normalized;
                    continue;
                }

                normal /= length;
                circumcenters[t] = normal;
            }
        }

        void BuildCells()
        {
            Cells.Clear();

            if (hullTriangles == null || circumcenters == null)
                return;

            var incidentTriangles = new List<int>[seedCount];
            for (int i = 0; i < seedCount; i++)
                incidentTriangles[i] = new List<int>();

            for (int t = 0; t < hullTriangles.Count; t++)
            {
                var tri = hullTriangles[t];
                if (tri.a >= 0 && tri.a < seedCount) incidentTriangles[tri.a].Add(t);
                if (tri.b >= 0 && tri.b < seedCount) incidentTriangles[tri.b].Add(t);
                if (tri.c >= 0 && tri.c < seedCount) incidentTriangles[tri.c].Add(t);
            }

            for (int s = 0; s < seedCount; s++)
            {
                List<int> cellTriangles = incidentTriangles[s];
                if (cellTriangles.Count == 0)
                {
                    Cells.Add(new ProtoCell
                    {
                        Id = s,
                        Seed = seeds[s],
                        Vertices = new Vector3[0],
                        Neighbors = new int[0],
                        Color = CellColor(s)
                    });
                    continue;
                }

                OrderTrianglesAroundSeed(s, cellTriangles);

                var cellVertices = new List<Vector3>(cellTriangles.Count);
                foreach (int triIndex in cellTriangles)
                {
                    if (triIndex >= 0 && triIndex < circumcenters.Length)
                        cellVertices.Add(circumcenters[triIndex]);
                }

                var neighborSet = new HashSet<int>();
                foreach (int t in cellTriangles)
                {
                    var tri = hullTriangles[t];
                    if (tri.a != s && tri.a >= 0 && tri.a < seedCount) neighborSet.Add(tri.a);
                    if (tri.b != s && tri.b >= 0 && tri.b < seedCount) neighborSet.Add(tri.b);
                    if (tri.c != s && tri.c >= 0 && tri.c < seedCount) neighborSet.Add(tri.c);
                }

                Cells.Add(new ProtoCell
                {
                    Id = s,
                    Seed = seeds[s],
                    Vertices = cellVertices.ToArray(),
                    Neighbors = new List<int>(neighborSet).ToArray(),
                    Color = CellColor(s)
                });
            }
        }

        void OrderTrianglesAroundSeed(int seedIdx, List<int> triangles)
        {
            if (triangles.Count <= 2) return;

            Vector3 up = seeds[seedIdx];
            Vector3 reference = Mathf.Abs(Vector3.Dot(up, Vector3.up)) < 0.95f
                ? Vector3.up
                : Vector3.right;

            Vector3 tangent = Vector3.Cross(reference, up).normalized;
            Vector3 bitangent = Vector3.Cross(up, tangent);

            triangles.Sort((left, right) =>
            {
                Vector3 dl = circumcenters[left] - Vector3.Dot(circumcenters[left], up) * up;
                Vector3 dr = circumcenters[right] - Vector3.Dot(circumcenters[right], up) * up;

                float angleL = Mathf.Atan2(Vector3.Dot(dl, bitangent), Vector3.Dot(dl, tangent));
                float angleR = Mathf.Atan2(Vector3.Dot(dr, bitangent), Vector3.Dot(dr, tangent));

                return angleL.CompareTo(angleR);
            });
        }

        static Color CellColor(int id)
        {
            float hue = Mathf.Repeat(0.11f + id * 0.61803398875f, 1f);
            return Color.HSVToRGB(hue, 0.62f, 0.95f);
        }

        void BuildMesh()
        {
            int vertexEstimate = Mathf.Max(1, seedCount * 6);
            int triangleEstimate = Mathf.Max(1, seedCount * 12);
            var vertices = new List<Vector3>(vertexEstimate);
            var normals = new List<Vector3>(vertexEstimate);
            var triangles = new List<int>(triangleEstimate);
            var colors = new List<Color>(vertexEstimate);
            var triToCell = new List<int>(triangleEstimate);

            foreach (var cell in Cells)
            {
                if (!renderUnplayableCells && !cell.IsPlayable)
                    continue;

                if (cell.Vertices == null || cell.Vertices.Length < 3)
                    continue;

                Color color = cell.Color;
                int baseIndex = vertices.Count;
                for (int i = 0; i < cell.Vertices.Length; i++)
                {
                    Vector3 unit = cell.Vertices[i].normalized;
                    vertices.Add(unit);
                    normals.Add(unit);
                    colors.Add(color);
                }

                for (int i = 1; i < cell.Vertices.Length - 1; i++)
                {
                    Vector3 a = cell.Vertices[0].normalized;
                    Vector3 b = cell.Vertices[i].normalized;
                    Vector3 c = cell.Vertices[i + 1].normalized;
                    Vector3 ab = b - a;
                    Vector3 ac = c - a;
                    if (ab.sqrMagnitude < 1e-16f || ac.sqrMagnitude < 1e-16f)
                        continue;

                    Vector3 triangleNormal = Vector3.Cross(ab, ac);
                    if (triangleNormal.sqrMagnitude < 1e-20f)
                        continue;
                    bool outward = Vector3.Dot(triangleNormal, a + b + c) >= 0f;
                    triangles.Add(baseIndex);
                    triangles.Add(outward ? baseIndex + i : baseIndex + i + 1);
                    triangles.Add(outward ? baseIndex + i + 1 : baseIndex + i);
                    triToCell.Add(cell.Id);
                }
            }

            var mesh = new Mesh { name = "SphereVoronoiMesh" };
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            TriangleToCellId = triToCell.ToArray();
            SetCombinedMesh(mesh);
            ApplyMaterial();
        }

        bool IsLandCell(int id)
        {
            return landCells != null
                && id >= 0
                && id < landCells.Length
                && landCells[id];
        }

        public bool TryGetCellAtDirection(Vector3 localDirection, out int cellId)
        {
            cellId = -1;
            if (Cells == null || Cells.Count == 0 || localDirection.sqrMagnitude < 1e-12f)
                return false;

            Vector3 direction = localDirection.normalized;
            int nearestId = FindNearestSeedIndex(direction);

            if (nearestId < 0 || !IsLandCell(nearestId) || Cells[nearestId] == null || !Cells[nearestId].IsPlayable)
                return false;

            cellId = nearestId;
            return true;
        }

        void SetCombinedMesh(Mesh mesh)
        {
            Mesh previousMesh = CombinedMesh;
            CombinedMesh = mesh;

            if (meshFilter != null)
                meshFilter.sharedMesh = CombinedMesh;

            if (previousMesh != null && previousMesh != mesh)
            {
                if (Application.isPlaying)
                    Destroy(previousMesh);
                else
                    DestroyImmediate(previousMesh);
            }
        }

        void ApplyMaterial()
        {
            if (meshRenderer == null) return;

            if (cellMaterial == null)
            {
                Shader shader = Shader.Find("SphereVoronoi/VertexColorLit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                if (shader != null)
                    cellMaterial = new Material(shader);
            }

            if (cellMaterial != null)
                meshRenderer.sharedMaterial = cellMaterial;
        }

        void OnDrawGizmos()
        {
            if (seeds == null) return;
            float gizmoScale = Mathf.Clamp(debugGizmoScale, 0.05f, 1f);

            if (drawSeeds)
            {
                Gizmos.color = Color.white;
                for (int i = 0; i < seeds.Length; i++)
                    Gizmos.DrawSphere(seeds[i], 0.05f * gizmoScale);
            }

            if (drawHull && hullTriangles != null)
            {
                Gizmos.color = Color.cyan;
                for (int t = 0; t < hullTriangles.Count; t++)
                {
                    var tri = hullTriangles[t];
                    Vector3 p1 = seeds[tri.a];
                    Vector3 p2 = seeds[tri.b];
                    Vector3 p3 = seeds[tri.c];
                    Gizmos.DrawLine(p1, p2);
                    Gizmos.DrawLine(p2, p3);
                    Gizmos.DrawLine(p3, p1);
                }
            }

            if (drawCircumcenters && circumcenters != null && Cells != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var cell in Cells)
                {
                    if (!IsLandCell(cell.Id) || cell.Vertices == null) continue;
                    for (int i = 0; i < cell.Vertices.Length; i++)
                        Gizmos.DrawSphere(cell.Vertices[i], 0.03f * gizmoScale);
                }
            }

            if (drawCellOutlines && Cells != null &&
                debugCellId >= 0 && debugCellId < Cells.Count)
            {
                var cell = Cells[debugCellId];
                bool active = IsLandCell(cell.Id);
                if (active && cell.Vertices != null)
                {
                    Gizmos.color = Color.red;
                    for (int i = 0; i < cell.Vertices.Length; i++)
                        Gizmos.DrawLine(cell.Vertices[i], cell.Vertices[(i + 1) % cell.Vertices.Length]);
                }

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(cell.Seed, 0.08f * gizmoScale);
            }
        }
    }
}
