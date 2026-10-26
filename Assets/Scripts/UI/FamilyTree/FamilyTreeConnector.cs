using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Draws the family tree's connector lines (couples, and the vertical drop to a
// sibling bar then to each child) as a single mesh, so a large tree costs one
// extra draw call instead of one GameObject per line. Lives on the Content rect
// and is not raycastable, so it sits behind and never blocks the portraits.
[RequireComponent(typeof(CanvasRenderer))]
public class FamilyTreeConnector : MaskableGraphic
{
    public List<TreeLine> Lines = new List<TreeLine>();
    public float Thickness = 3f;

    public void SetLines(List<TreeLine> lines)
    {
        Lines = lines ?? new List<TreeLine>();
        SetVerticesDirty();
        SetMaterialDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        maskable = true;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (Lines == null) return;

        for (int i = 0; i < Lines.Count; i++)
            AddSegment(vh, Lines[i]);
    }

    private void AddSegment(VertexHelper vh, TreeLine line)
    {
        Vector2 direction = line.B - line.A;
        if (direction.sqrMagnitude < 0.01f)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (Thickness * 0.5f);
        UIVertex vertex = UIVertex.simpleVert;
        int start = vh.currentVertCount;

        vertex.color = line.Color;
        vertex.position = line.A - normal; vh.AddVert(vertex);
        vertex.position = line.A + normal; vh.AddVert(vertex);
        vertex.position = line.B + normal; vh.AddVert(vertex);
        vertex.position = line.B - normal; vh.AddVert(vertex);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
