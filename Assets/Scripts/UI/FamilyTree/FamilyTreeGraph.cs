using System.Collections.Generic;

// Data model for the family tree screen. The graph is rebuilt every time the
// window opens; nothing is cached on Character itself. "Generation" is measured
// relative to the focus character: 0 is the focus row, negative numbers are
// ancestors (drawn above) and positive numbers are descendants (drawn below).
public class FamilyTreeMember
{
    public Character Character;
    public int Generation;
    public string Relation;
    public bool IsFocus;
}

public class FamilyTreeGraph
{
    public Character Focus;
    public readonly List<FamilyTreeMember> Members = new();
    public readonly Dictionary<Character, FamilyTreeMember> ByCharacter = new();

    public FamilyTreeMember Get(Character c)
        => c != null && ByCharacter.TryGetValue(c, out FamilyTreeMember member) ? member : null;
}

// Walks the live Character family graph (Father/Mother/Children/Spouse) around a
// focus character. Deliberately conservative so the tree stays readable:
//   * ancestors are walked up `ancestorDepth` generations, descendants down
//     `descendantDepth`;
//   * a generation of parents contributes their children (which is how siblings
//     appear), but grandparents do NOT contribute their children, so aunts and
//     uncles are kept out;
//   * siblings do not contribute their own children, so nieces/nephews are out;
//   * spouses are shown beside their partner but are never expanded, so in-laws
//     never drag their own ancestry into the tree.
// Temporary (display-only) characters are always skipped.
public static class FamilyTreeBuilder
{
    // Hard ceiling so a pathological family graph cannot stall opening the screen.
    private const int MaxMembers = 160;

    private struct Pending
    {
        public Character Character;
        public int Generation;
        public bool ExpandAncestors;
        public bool ExpandChildren;
    }

    public static FamilyTreeGraph Build(Character focus, int ancestorDepth, int descendantDepth, bool includeSpouses)
    {
        FamilyTreeGraph graph = new FamilyTreeGraph { Focus = focus };
        if (focus == null)
            return graph;

        ancestorDepth = UnityEngine.Mathf.Max(0, ancestorDepth);
        descendantDepth = UnityEngine.Mathf.Max(0, descendantDepth);

        Queue<Pending> queue = new Queue<Pending>();
        HashSet<Character> seen = new HashSet<Character>();

        void Enqueue(Character character, int generation, string relation, bool expandAncestors, bool expandChildren)
        {
            if (character == null || character.IsTemporary) return;
            if (generation < -ancestorDepth || generation > descendantDepth) return;
            if (graph.Members.Count >= MaxMembers) return;
            if (!seen.Add(character)) return;

            FamilyTreeMember member = new FamilyTreeMember
            {
                Character = character,
                Generation = generation,
                Relation = relation,
                IsFocus = character == focus
            };
            graph.Members.Add(member);
            graph.ByCharacter[character] = member;
            queue.Enqueue(new Pending
            {
                Character = character,
                Generation = generation,
                ExpandAncestors = expandAncestors,
                ExpandChildren = expandChildren
            });
        }

        Enqueue(focus, 0, "Focus", true, true);

        while (queue.Count > 0)
        {
            Pending pending = queue.Dequeue();
            int generation = pending.Generation;

            if (pending.ExpandAncestors)
            {
                // Parents sit one generation up. They may contribute their other
                // children (to reveal siblings) only while we are at the focus row.
                bool parentsRevealSiblings = generation == 0;
                Enqueue(pending.Character.Father, generation - 1, AncestorLabel(-(generation - 1), pending.Character.Father), true, parentsRevealSiblings);
                Enqueue(pending.Character.Mother, generation - 1, AncestorLabel(-(generation - 1), pending.Character.Mother), true, parentsRevealSiblings);
            }

            if (pending.ExpandChildren && generation >= -1)
            {
                // Descendants revealed by a node on or below the focus row, but
                // siblings (generation 0, reached from a parent) never reveal theirs.
                bool childrenRevealDescendants = generation >= 0;
                foreach (Character child in pending.Character.Children)
                {
                    int childGeneration = generation + 1;
                    string relation = childGeneration == 0
                        ? "Sibling"
                        : DescendantLabel(childGeneration, child);
                    Enqueue(child, childGeneration, relation, false, childrenRevealDescendants);
                }
            }

            if (includeSpouses)
                Enqueue(pending.Character.Spouse, generation, "Spouse", false, false);
        }

        return graph;
    }

    private static string AncestorLabel(int distance, Character c)
    {
        if (c == null) return string.Empty;
        if (distance <= 1) return c.Gender == Gender.Female ? "Mother" : c.Gender == Gender.Male ? "Father" : "Parent";
        if (distance == 2) return c.Gender == Gender.Female ? "Grandmother" : c.Gender == Gender.Male ? "Grandfather" : "Grandparent";
        return "Great-" + AncestorLabel(distance - 1, c);
    }

    private static string DescendantLabel(int distance, Character c)
    {
        if (c == null) return string.Empty;
        if (distance <= 1) return c.Gender == Gender.Female ? "Daughter" : c.Gender == Gender.Male ? "Son" : "Child";
        if (distance == 2) return c.Gender == Gender.Female ? "Granddaughter" : c.Gender == Gender.Male ? "Grandson" : "Grandchild";
        return "Great-" + DescendantLabel(distance - 1, c);
    }
}
