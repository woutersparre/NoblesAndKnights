using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

[System.Serializable]
public abstract class Region
{
    public Color MapColor = Color.white;
    public TMP_Text text;

    // Shows this region's tag, restoring the region's own name. Tags are shared
    // long-lived objects, so anything that wrote a different string into one has
    // to be undone here rather than assumed. No-ops when the tag was never
    // created (AddNameTag reports that itself).
    public void ShowOwnTag()
    {
        if (text == null) return;
        text.text = Name;
        text.enabled = true;
    }

    // Hides the tag without touching its text.
    public void SetTagVisible(bool visible)
    {
        if (text != null)
            text.enabled = visible;
    }

    public virtual Region ParentRegion { get; set; }
    public virtual List<Region> SubRegions { get; set; } = new();

    [SerializeReference] 
    public List<List<Vector3>> BorderLoops = new();
    public enum Rank
    {
        Province,
        Duchy,
        Kingdom,
        Empire
    }

    public Rank rank;

    public string Name => $"{rank} of {Capital?.Name}";

    public Location Capital
    {
        get
        {
            if (this is Province p)
                return p.Capital;

            return SubRegions.FirstOrDefault()?.Capital;
        }
    }
    public virtual List<Region> NeighboringRegions
    {
        get
        {
            HashSet<Region> neighbors = new();

            foreach (var province in GetProvinces())
            {
                foreach (var neighborProvince in province.NeighboringRegions)
                {
                    Region neighborRegion = neighborProvince.GetAncestorOfRank(rank);

                    if (neighborRegion != null && neighborRegion != this)
                        neighbors.Add(neighborRegion);
                }
            }

            return neighbors.ToList();
        }
    }
    public virtual Vector2 Center => RegionCenter();

    protected Region(Rank rank, IEnumerable<Region> subRegions)
    {
        this.rank = rank;
        if (subRegions != null)
        {
            foreach (var sub in subRegions)
            {
                sub.ParentRegion = this;
                SubRegions.Add(sub);
            }
        }
        if (this is Province)
            return;
        CreateBorderLoops();
    }


    public virtual void CreateBorderLoops()
    {
        BorderLoops.Clear();

        if (SubRegions.Count == 0) return;

        // Collect all child loops
        var allChildLoops = new List<List<Vector3>>();
        foreach (var child in SubRegions)
            allChildLoops.AddRange(child.BorderLoops);

        // Extract outer edges
        var outerEdges = ExtractOuterEdges(allChildLoops);

        // Merge edges into continuous loops
        BorderLoops = BuildLoops(outerEdges);
    }

    // Extract only edges that are external to this region
    private List<(Vector3, Vector3)> ExtractOuterEdges(List<List<Vector3>> childLoops)
    {
        var edgeCount = new Dictionary<(Vector3, Vector3), int>(new VectorPairComparer());

        foreach (var loop in childLoops)
        {
            for (int i = 0; i < loop.Count - 1; i++)
            {
                var a = loop[i];
                var b = loop[i + 1];
                var key = (a, b);
                var revKey = (b, a);

                if (edgeCount.ContainsKey(revKey)) edgeCount[revKey]++;
                else if (edgeCount.ContainsKey(key)) edgeCount[key]++;
                else edgeCount[key] = 1;
            }
        }

        // Only edges that appear once are outer edges
        return edgeCount.Where(kvp => kvp.Value == 1).Select(kvp => kvp.Key).ToList();
    }

    // BuildLoops adapted for Vector3 edges
    public static List<List<Vector3>> BuildLoops(List<(Vector3, Vector3)> edges)
    {
        var unused = new List<(Vector3, Vector3)>(edges);
        var loops = new List<List<Vector3>>();

        while (unused.Count > 0)
        {
            var start = unused[0];
            unused.RemoveAt(0);

            var loop = new List<Vector3> { start.Item1, start.Item2 };
            Vector3 current = start.Item2;
            bool closed = false;

            while (!closed)
            {
                int index = unused.FindIndex(e => VectorsAreClose(e.Item1, current) || VectorsAreClose(e.Item2, current));
                if (index == -1) break;

                var e = unused[index];
                unused.RemoveAt(index);

                Vector3 next = VectorsAreClose(e.Item1, current) ? e.Item2 : e.Item1;
                if (VectorsAreClose(next, loop[0]))
                {
                    closed = true;
                    loop.Add(loop[0]);
                }
                else
                {
                    loop.Add(next);
                    current = next;
                }
            }

            // Only add valid loops (minimum 3 points for a polygon)
            if (loop.Count >= 3)
            {
                loops.Add(loop);
            }
            else
            {
                // Optional: Log isolated edges for debugging
                //Debug.LogWarning($"Skipping isolated edge that couldn't form a valid loop: {loop.Count} points");
            }
        }

        return loops;
    }

    private static bool VectorsAreClose(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.00001f;

    // Comparer for undirected edge equality - made public for use in Province class
    public class VectorPairComparer : IEqualityComparer<(Vector3, Vector3)>
    {
        public bool Equals((Vector3, Vector3) x, (Vector3, Vector3) y)
        {
            return (VectorsAreClose(x.Item1, y.Item1) && VectorsAreClose(x.Item2, y.Item2)) ||
                   (VectorsAreClose(x.Item1, y.Item2) && VectorsAreClose(x.Item2, y.Item1));
        }

        public int GetHashCode((Vector3, Vector3) obj)
        {
            // Simple hash for undirected edge
            return VectorsAreClose(obj.Item1, obj.Item2) ? 
                   obj.Item1.GetHashCode() ^ obj.Item2.GetHashCode() :
                   obj.Item2.GetHashCode() ^ obj.Item1.GetHashCode();
        }
    }


    public virtual IEnumerable<Province> GetProvinces()
    {
        if (this is Province p)
        {
            yield return p;
            yield break;
        }

        foreach (var sub in SubRegions)
            foreach (var province in sub.GetProvinces())
                yield return province;
    }

    protected Region GetAncestorOfRank(Rank targetRank)
    {
        Region current = this;
        while (current != null && current.rank != targetRank)
            current = current.ParentRegion;

        return current;
    }


    Vector3 RegionCenter()
    {
        if (this is Province p)
            return p.Center;

        var centers = SubRegions
            .Select(r => r.RegionCenter())
            .ToList();

        return centers.Aggregate(Vector3.zero, (a, b) => a + b) / centers.Count;
    }

}

[System.Serializable]
public class Duchy : Region
{
    public Duchy(List<Province> provinces)
        : base(Rank.Duchy, provinces)
    {
    }


}


[System.Serializable]
public class Kingdom : Region
{
    public Kingdom(List<Duchy> duchies)
        : base(Rank.Kingdom, duchies)
    {
    }


}


[System.Serializable]
public class Empire : Region
{
    public Empire(List<Kingdom> kingdoms)
        : base(Rank.Empire, kingdoms)
    {
    }


}
