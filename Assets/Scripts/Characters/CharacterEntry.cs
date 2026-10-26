using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Text;


public class CharacterEntry : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text nameText, Diplomacy, Martial, Stewardship, Intrigue, Learning,RelationScore;
    public Character target;
    [SerializeField] GameObject TraitsObject;
    public Button Button;
    public void Setup(Character character)
    {
        nameText.text = character.Name;
        target = character;

        // The portrait row's own Button is the click target, so the widget is left
        // without a Button reference and the click bubbles up to this row instead.
        CharacterPortraitUI portraitUI = portrait != null ? portrait.GetComponent<CharacterPortraitUI>() : null;
        if (portraitUI != null)
            portraitUI.Setup(character);
        else if (portrait != null)
            portrait.sprite = character.Portrait;

        UpdateStats(target.Stats);
        UpdateTraits();
        UpdateRelationScore();
    }

    void UpdateRelationScore()
    {
        Character player = MapManager.instance != null && MapManager.instance.characters != null
            ? MapManager.instance.characters.Player
            : null;

        if (player == null || player == target)
        {
            RelationScore.text = "-";
            RelationScore.color = Color.gray;
            return;
        }

        int opinion = player.GetOpinion(target);
        RelationScore.text = opinion.ToString();
        RelationScore.color = opinion >= 40 ? Color.green : opinion <= -40 ? Color.red : Color.white;

        InfoPopup popup = RelationScore.transform.parent != null
            ? RelationScore.transform.parent.GetComponent<InfoPopup>()
            : null;
        if (popup != null)
        {
            popup.Header = "Opinion of " + target.Name;
            popup.Body = player.GetOpinionTooltip(target);
        }
    }
    public void ShowCharacterButton()
    {
        GameUIManager.NewCharacterUI(target);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Right && target != null)
        {
            CharacterActionMenu.Show(target, eventData.position);
            eventData.Use();
        }
    }
    void UpdateStats(Stats s)
    {
        Diplomacy.text = s.Diplomacy.ToString();
        Martial.text = s.Martial.ToString();
        Stewardship.text = s.Stewardship.ToString();
        Intrigue.text = s.Intrigue.ToString();
        Learning.text = s.Learning.ToString();

        UpdateStatTooltip(Diplomacy, target, Stats.StatType.Diplomacy);
        UpdateStatTooltip(Martial, target, Stats.StatType.Martial);
        UpdateStatTooltip(Stewardship, target, Stats.StatType.Stewardship);
        UpdateStatTooltip(Intrigue, target, Stats.StatType.Intrigue);
        UpdateStatTooltip(Learning, target, Stats.StatType.Learning);
    }

    // Mirrors CharacterUIManager.UpdateStatTooltip: current value, base value and the
    // traits responsible for the difference. Silently skipped when the prefab row has no
    // InfoPopup on that stat text.
    private static void UpdateStatTooltip(TMP_Text text, Character c, Stats.StatType stat)
    {
        if (c == null) return;

        InfoPopup popup = FindPopup(text);
        if (popup == null) return;

        StringBuilder b = new StringBuilder();
        b.Append(stat).Append(": ").AppendLine(GetStatValue(c.Stats, stat).ToString());
        b.Append("Base: ").AppendLine(GetStatValue(c.BaseStats, stat).ToString());

        foreach (Trait trait in c.Traits)
        {
            foreach (Modifier modifier in trait.modifiers)
            {
                if (MatchesStat(modifier.modifierType, stat))
                    b.Append(trait.name).Append(' ').Append(modifier.modifierValue.ToString("+0;-0;0")).AppendLine();
            }
        }

        popup.Body = b.ToString();
    }

    private static InfoPopup FindPopup(TMP_Text text)
    {
        if (text == null) return null;

        InfoPopup popup = text.GetComponent<InfoPopup>();
        if (popup != null) return popup;

        return text.transform.parent != null ? text.transform.parent.GetComponent<InfoPopup>() : null;
    }

    private static int GetStatValue(Stats stats, Stats.StatType stat)
    {
        switch (stat)
        {
            case Stats.StatType.Diplomacy: return stats.Diplomacy;
            case Stats.StatType.Martial: return stats.Martial;
            case Stats.StatType.Stewardship: return stats.Stewardship;
            case Stats.StatType.Intrigue: return stats.Intrigue;
            case Stats.StatType.Learning: return stats.Learning;
            case Stats.StatType.CombatStrength: return stats.CombatStrength;
            default: return 0;
        }
    }

    private static bool MatchesStat(ModifierType type, Stats.StatType stat)
    {
        switch (stat)
        {
            case Stats.StatType.Diplomacy: return type == ModifierType.Diplomacy;
            case Stats.StatType.Martial: return type == ModifierType.Martial;
            case Stats.StatType.Stewardship: return type == ModifierType.Stewardship;
            case Stats.StatType.Intrigue: return type == ModifierType.Intrigue;
            case Stats.StatType.Learning: return type == ModifierType.Learning;
            case Stats.StatType.CombatStrength: return type == ModifierType.CombatStrengh;
            default: return false;
        }
    }

    void UpdateTraits()
    {
        foreach (Trait trait in target.Traits)
        {
            TraitUI traitUI = Instantiate(trait.Prefab, TraitsObject.transform).GetComponent<TraitUI>();
            traitUI.trait = trait;
            traitUI.name = trait.name;
        }
    }

}