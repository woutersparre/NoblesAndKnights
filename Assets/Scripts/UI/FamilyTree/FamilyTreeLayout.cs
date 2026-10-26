using System.Collections.Generic;
using UnityEngine;

// One connector segment, already expressed in content-local coordinates.
public struct TreeLine
{
    public Vector2 A;
    public Vector2 B;
    public Color Color;
}

public class PlacedNode
{
    public Character Character;
    public string Relation;
    public bool IsFocus;
    public Vector2 Position; // content-local centre
}

public class TreeLayout
{
    public Vector2 Size;
    public readonly List<PlacedNode> Nodes = new List<PlacedNode>();
    public readonly List<TreeLine> Lines = new List<TreeLine>();
}

// Turns a FamilyTreeGraph into concrete positions and connector segments.
//
// Layout is generational: one horizontal row per generation (ancestors on top,
// focus in the middle, descendants below). Row order is refined with a couple of
// barycentre passes so parents sit roughly above their children, then each row is
// laid out left to right with couples kept adjacent and centred on the origin.
public static class FamilyTreeLayout
{
    public const float NodeWidth = 100f;
    public const float NodeHeight = 168f;
    public const float ColumnSpacing = 146f; // between two different people
    public const float SpouseGap = 128f;     // between the two halves of a couple
    public const float RowSpacing = 196f;
    private const float PaddingX = 160f;
    private const float PaddingY = 90f;

    public static TreeLayout Compute(FamilyTreeGraph graph, Color spouseColor, Color descentColor)
    {
        TreeLayout layout = new TreeLayout();
        if (graph == null || graph.Members.Count == 0)
        {
            layout.Size = new Vector2(NodeWidth + PaddingX, NodeHeight + PaddingY);
            return layout;
        }

        // ---- group into rows, keyed by generation -------------------------------
        List<int> generations = new List<int>();
        foreach (FamilyTreeMember member in graph.Members)
        {
            if (!generations.Contains(member.Generation))
                generations.Add(member.Generation);
        }
        generations.Sort();

        Dictionary<int, List<FamilyTreeMember>> rows = new Dictionary<int, List<FamilyTreeMember>>();
        foreach (int generation in generations)
            rows[generation] = new List<FamilyTreeMember>();
        foreach (FamilyTreeMember member in graph.Members)
            rows[member.Generation].Add(member);

        // ---- neighbour lookup tables -------------------------------------------
        Dictionary<FamilyTreeMember, List<FamilyTreeMember>> parents = new Dictionary<FamilyTreeMember, List<FamilyTreeMember>>();
        Dictionary<FamilyTreeMember, List<FamilyTreeMember>> children = new Dictionary<FamilyTreeMember, List<FamilyTreeMember>>();
        Dictionary<FamilyTreeMember, FamilyTreeMember> spouse = new Dictionary<FamilyTreeMember, FamilyTreeMember>();

        foreach (FamilyTreeMember member in graph.Members)
        {
            List<FamilyTreeMember> memberParents = new List<FamilyTreeMember>();
            FamilyTreeMember father = graph.Get(member.Character.Father);
            FamilyTreeMember mother = graph.Get(member.Character.Mother);
            if (father != null) memberParents.Add(father);
            if (mother != null) memberParents.Add(mother);
            parents[member] = memberParents;
            children[member] = new List<FamilyTreeMember>();

            FamilyTreeMember partner = graph.Get(member.Character.Spouse);
            if (partner != null && partner != member)
                spouse[member] = partner;
        }

        foreach (FamilyTreeMember member in graph.Members)
        {
            foreach (FamilyTreeMember parent in parents[member])
                children[parent].Add(member);
        }

        // ---- barycentre ordering ----------------------------------------------
        Dictionary<FamilyTreeMember, int> index = new Dictionary<FamilyTreeMember, int>();
        Reindex(rows, index);

        for (int pass = 0; pass < 4; pass++)
        {
            // Downwards: order each row (except the top one) by its parents.
            for (int r = 1; r < generations.Count; r++)
                OrderRow(rows[generations[r]], parents, index);
            // Upwards: order each row (except the bottom one) by its children.
            for (int r = generations.Count - 2; r >= 0; r--)
                OrderRow(rows[generations[r]], children, index);
        }

        // ---- assign x/y positions ---------------------------------------------
        Dictionary<FamilyTreeMember, float> x = new Dictionary<FamilyTreeMember, float>();
        Dictionary<FamilyTreeMember, float> y = new Dictionary<FamilyTreeMember, float>();

        foreach (int generation in generations)
        {
            List<FamilyTreeMember> row = rows[generation];
            HashSet<FamilyTreeMember> placed = new HashSet<FamilyTreeMember>();
            float cursor = 0f;
            float first = 0f;
            float last = 0f;
            bool any = false;

            for (int i = 0; i < row.Count; i++)
            {
                FamilyTreeMember member = row[i];
                if (placed.Contains(member))
                    continue;

                // Couples are laid out as one unit: the partner sits immediately to
                // the right. The ordering pass can leave the partner in front of the
                // member it belongs to, so the unit starts at whichever comes first
                // in the row -- otherwise the row's centre would drift sideways.
                FamilyTreeMember partner = SpouseInRow(member, row, spouse);
                bool memberIsRightHalf = partner != null && !placed.Contains(partner) && row.IndexOf(partner) < i;

                placed.Add(member);
                x[member] = cursor;
                if (!any) { first = cursor; any = true; }
                last = cursor;

                if (partner != null && !placed.Contains(partner))
                {
                    placed.Add(partner);
                    cursor += SpouseGap;
                    x[partner] = cursor;
                    if (memberIsRightHalf && Mathf.Approximately(first, x[member]))
                        first = cursor; // the unit started on the partner, not the member
                    last = cursor;
                }

                cursor += ColumnSpacing;
            }

            float shift = any ? -(first + last) * 0.5f : 0f;
            foreach (FamilyTreeMember member in row)
            {
                x[member] += shift;
                y[member] = -generation * RowSpacing;
            }
        }

        // ---- bounds ------------------------------------------------------------
        float left = float.MaxValue, right = float.MinValue, top = float.MinValue, bottom = float.MaxValue;
        foreach (FamilyTreeMember member in graph.Members)
        {
            left = Mathf.Min(left, x[member] - NodeWidth * 0.5f);
            right = Mathf.Max(right, x[member] + NodeWidth * 0.5f);
            top = Mathf.Max(top, y[member] + NodeHeight * 0.5f);
            bottom = Mathf.Min(bottom, y[member] - NodeHeight * 0.5f);
        }
        left -= PaddingX; right += PaddingX; top += PaddingY; bottom -= PaddingY;

        Vector2 centre = new Vector2((left + right) * 0.5f, (top + bottom) * 0.5f);
        layout.Size = new Vector2(right - left, top - bottom);

        Vector2 Local(FamilyTreeMember member) => new Vector2(x[member] - centre.x, y[member] - centre.y);

        foreach (FamilyTreeMember member in graph.Members)
        {
            layout.Nodes.Add(new PlacedNode
            {
                Character = member.Character,
                Relation = member.Relation,
                IsFocus = member.IsFocus,
                Position = Local(member)
            });
        }

        // ---- spouse connectors -------------------------------------------------
        // Drawn only in the gap between the two portraits, at the row's centre line.
        foreach (FamilyTreeMember member in graph.Members)
        {
            if (!spouse.TryGetValue(member, out FamilyTreeMember partner)) continue;
            if (member.Character.UniqueId > partner.Character.UniqueId) continue; // draw once

            Vector2 a = Local(member);
            Vector2 b = Local(partner);
            float midY = (a.y + b.y) * 0.5f;
            float x1 = Mathf.Min(a.x, b.x) + NodeWidth * 0.5f;
            float x2 = Mathf.Max(a.x, b.x) - NodeWidth * 0.5f;
            if (x2 - x1 < 2f) continue;

            layout.Lines.Add(new TreeLine { A = new Vector2(x1, midY), B = new Vector2(x2, midY), Color = spouseColor });
        }

        // ---- descent connectors ------------------------------------------------
        // One drop per child, taken from the father (or mother) that is present,
        // so a couple only produces a single set of lines down to its children.
        foreach (FamilyTreeMember child in graph.Members)
        {
            FamilyTreeMember primaryParent = null;
            FamilyTreeMember otherParent = null;

            FamilyTreeMember father = graph.Get(child.Character.Father);
            FamilyTreeMember mother = graph.Get(child.Character.Mother);

            if (father != null)
            {
                primaryParent = father;
                otherParent = mother;
            }
            else if (mother != null)
            {
                primaryParent = mother;
                otherParent = father;
            }

            if (primaryParent == null) continue;

            Vector2 parentPos = Local(primaryParent);
            float anchorX = parentPos.x;

            // If the other parent is present and actually partnered with this one,
            // drop from the midpoint of the couple instead of one of them.
            if (otherParent != null && spouse.TryGetValue(primaryParent, out FamilyTreeMember partner) && partner == otherParent)
                anchorX = (parentPos.x + Local(otherParent).x) * 0.5f;

            Vector2 anchor = new Vector2(anchorX, parentPos.y - NodeHeight * 0.5f + 6f);
            Vector2 childPos = Local(child);
            Vector2 attach = new Vector2(childPos.x, childPos.y + NodeHeight * 0.5f - 6f);
            float busY = (anchor.y + attach.y) * 0.5f;

            layout.Lines.Add(new TreeLine { A = anchor, B = new Vector2(anchorX, busY), Color = descentColor });
            layout.Lines.Add(new TreeLine { A = new Vector2(anchorX, busY), B = new Vector2(childPos.x, busY), Color = descentColor });
            layout.Lines.Add(new TreeLine { A = new Vector2(childPos.x, busY), B = attach, Color = descentColor });
        }

        return layout;
    }

    private static FamilyTreeMember SpouseInRow(FamilyTreeMember member, List<FamilyTreeMember> row, Dictionary<FamilyTreeMember, FamilyTreeMember> spouse)
    {
        if (!spouse.TryGetValue(member, out FamilyTreeMember partner)) return null;
        return partner.Generation == member.Generation && row.Contains(partner) ? partner : null;
    }

    private static void Reindex(Dictionary<int, List<FamilyTreeMember>> rows, Dictionary<FamilyTreeMember, int> index)
    {
        foreach (KeyValuePair<int, List<FamilyTreeMember>> row in rows)
        {
            for (int i = 0; i < row.Value.Count; i++)
                index[row.Value[i]] = i;
        }
    }

    private static void OrderRow(List<FamilyTreeMember> row, Dictionary<FamilyTreeMember, List<FamilyTreeMember>> reference, Dictionary<FamilyTreeMember, int> index)
    {
        if (row.Count <= 1) return;

        List<KeyValuePair<FamilyTreeMember, float>> keyed = new List<KeyValuePair<FamilyTreeMember, float>>(row.Count);
        foreach (FamilyTreeMember member in row)
        {
            float sum = 0f;
            int count = 0;
            if (reference.TryGetValue(member, out List<FamilyTreeMember> relatives))
            {
                foreach (FamilyTreeMember relative in relatives)
                {
                    if (index.TryGetValue(relative, out int position))
                    {
                        sum += position;
                        count++;
                    }
                }
            }
            float barycentre = count > 0 ? sum / count : (index.TryGetValue(member, out int own) ? own : 0);
            keyed.Add(new KeyValuePair<FamilyTreeMember, float>(member, barycentre));
        }

        keyed.Sort((a, b) => a.Value.CompareTo(b.Value));

        row.Clear();
        foreach (KeyValuePair<FamilyTreeMember, float> entry in keyed)
            row.Add(entry.Key);

        for (int i = 0; i < row.Count; i++)
            index[row[i]] = i;
    }
}
