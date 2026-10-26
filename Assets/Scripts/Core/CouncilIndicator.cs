using UnityEngine;

// World-space marker shown on the map while a councilor is deployed to a location.
// The base prefab is a simple stacked SpriteRenderer; this component adds an optional
// floating label plus a gentle bob so the player can see the councilor is "working".
public class CouncilIndicator : MonoBehaviour
{
    public CouncilPosition Position;

    private SpriteRenderer spriteRenderer;
    private TextMesh label;

    private float bobOffset;
    private Vector3 originLocalPos;

    public void Initialize(CouncilPosition position, SpriteRenderer sr)
    {
        Position = position;
        spriteRenderer = sr;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = CouncilRoleInfo.GetRoleColor(position != null ? position.Role : CouncilRole.None);
        }

        bobOffset = Random.value * Mathf.PI * 2f;
        originLocalPos = transform.localPosition;

        CreateLabel();
    }

    void CreateLabel()
    {
        if (Position == null || Position.Appointed == null) return;

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0, 2.2f, 0);
        labelGo.transform.localScale = Vector3.one * 0.25f;

        label = labelGo.AddComponent<TextMesh>();
        label.text = BuildLabelText();
        label.fontSize = 32;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
    }

    void Update()
    {
        UpdateLabel();
        UpdateBob();
    }

    string BuildLabelText()
    {
        if (Position == null || Position.Appointed == null) return "";
        string name = Position.Appointed.Name;
        if (Position.IsWorking && Position.WorkingDaysLeft > 0)
            return $"{name}\n{Position.WorkingDaysLeft}d";
        return name;
    }

    void UpdateLabel()
    {
        string text = BuildLabelText();
        if (label != null && label.text != text)
            label.text = text;
    }

    void UpdateBob()
    {
        bobOffset += Time.deltaTime * 2f;
        transform.localPosition = originLocalPos + new Vector3(0, Mathf.Sin(bobOffset) * 0.08f, 0);
    }
}