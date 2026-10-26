using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class TraitUI : MonoBehaviour
{
    public Trait trait;
    public TMP_Text text;
    public InfoPopup infoPopup;

    public void Start()
    {
        if (trait == null) return;

        GetComponent<Image>().sprite = trait.Icon;
        text.text = trait.name.Length >= 2 ? trait.name.Substring(0, 2) : trait.name;
        BuildTooltip();
    }

    private void BuildTooltip()
    {
        if (infoPopup == null) return;

        infoPopup.Header = trait.name;

        StringBuilder b = new StringBuilder();

        if (!string.IsNullOrEmpty(trait.Description))
            b.AppendLine(trait.Description);

        foreach (Modifier modifier in trait.modifiers)
            b.Append(modifier.modifierType).Append(' ')
             .Append(modifier.modifierValue.ToString("+0;-0;0")).AppendLine();

        if (trait.OpinionModifier != 0)
            b.Append("Opinion: ").Append(trait.OpinionModifier.ToString("+0;-0;0"));

        if (trait.OppositeTrait != null)
        {
            if (b.Length > 0) b.AppendLine();
            b.Append("Opposed by ").Append(trait.OppositeTrait.name);
            if (trait.OpinionOfOppositeTrait != 0)
                b.Append(" (").Append(trait.OpinionOfOppositeTrait.ToString("+0;-0;0")).Append(')');
        }

        infoPopup.Body = b.ToString();
    }
}