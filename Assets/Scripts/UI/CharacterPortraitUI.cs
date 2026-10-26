using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text;

// Reusable portrait widget for any character shown in the UI.
// Shows the portrait, hovers for a summary, and (optionally) clicks through to
// that character's screen. Used by the character screen, council entries,
// character lists/finders, province and title panels.
public class CharacterPortraitUI : MonoBehaviour, IPointerClickHandler
{
    public Image Portrait;
    // Optional and opt-in per site. When assigned the widget owns the click and opens
    // the character screen. Leave it null to keep the GameObject's existing Button
    // behaviour untouched -- several portraits already carry inspector-wired listeners
    // (spouse/liege switch, focus camera) that Setup must not clear.
    public Button Button;
    public InfoPopup infoPopup;

    private Character _character;

    public void Setup(Character c)
    {
        Setup(c, null);
    }

    public void Setup(Character c, string relation)
    {
        _character = c;
        if (Portrait != null)
            Portrait.sprite = c != null ? c.Portrait : null;

        if (infoPopup != null)
        {
            if (c == null)
            {
                infoPopup.Header = "";
                infoPopup.Body = "";
            }
            else
            {
                infoPopup.Header = string.IsNullOrEmpty(relation) ? c.Name : relation + " " + c.Name;
                infoPopup.Body = BuildBody(c);
            }
        }

        if (Button != null)
        {
            Button.onClick.RemoveAllListeners();
            Button.interactable = c != null;
            if (c != null)
            {
                Character target = c;
                Button.onClick.AddListener(() => GameUIManager.NewCharacterUI(target));
            }
        }
    }

    // Empty seats and cleared slots.
    public void Clear()
    {
        _character = null;
        if (Portrait != null)
            Portrait.sprite = null;

        if (infoPopup != null)
        {
            infoPopup.Header = "";
            infoPopup.Body = "";
        }

        if (Button != null)
        {
            Button.onClick.RemoveAllListeners();
            Button.interactable = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            return;
        if (_character != null)
            CharacterActionMenu.Show(_character, eventData.position);
    }

    private static string BuildBody(Character c)
    {
        StringBuilder b = new StringBuilder();

        if (c.MainTitle != null)
            b.AppendLine(c.MainTitle.Name);

        b.AppendLine(c.Gender.ToString());
        b.Append("Age: ").AppendLine(c.Age.ToString());

        if (c.Culture != null)
            b.Append("Culture: ").AppendLine(c.Culture.Name);

        if (c.Religion != null)
        {
            b.Append("Religion: ").Append(c.Religion.Name);
            if (!string.IsNullOrEmpty(c.Religion.Deity))
                b.Append(" (").Append(c.Religion.Deity).Append(')');
            b.AppendLine();
        }

        if (c.Society != null)
            b.Append("Society: ").Append(c.Society.Name).Append(" (Rank ").Append(Mathf.Max(1, c.SocietyRank)).Append(')').AppendLine();

        if (c.House != null)
            b.Append("House: ").AppendLine(c.House.Name);

        b.Append("Standing: Wealth ").Append(c.Wealth.ToString("F0"))
         .Append(" | Renown ").Append(c.Reknown.ToString("F0"))
         .Append(" | Piety ").Append(c.Piety.ToString("F0"));

        if (c.IsTraveling && c.TravelDestination != null)
            b.Append("\nTravelling to ").Append(c.TravelDestination.Name);
        else if (c.CurrentLocation != null)
            b.Append("\nAt ").Append(c.CurrentLocation.Name);

        // Opinion is deliberately omitted: it is shown by the separate relation-score
        // element, which has its own breakdown tooltip.

        return b.ToString();
    }
}