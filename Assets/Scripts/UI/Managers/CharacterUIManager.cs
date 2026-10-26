using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class CharacterUIManager : Window<Character>
{
    public TMP_Text RulerName;
    public Image Portrait, PortraitSpouse, PortraitLiege;
    public GameObject TraitsObject, TitlesObject, HoldingIconPrefab;
    public TMP_Text Diplomacy,Martial,Stewardship,Intrigue,Learning,CombatStrength,Culture,Religion,Society,RelationScore;
    public CharacterListManager CharacterListManager;

    // Character whose OnStatsChanged we are currently subscribed to.
    private Character _subscribedTarget;
    public void UpdateUI(Character _char)
    {
        if(_char == null || _char.Name == "")return;

        // Stop listening to the previous character before switching targets.
        if (_subscribedTarget != null && _subscribedTarget != _char)
        {
            _subscribedTarget.OnStatsChanged -= HandleStatsChanged;
            _subscribedTarget = null;
        }

        Target = _char;

        if (_subscribedTarget != Target)
        {
            Target.OnStatsChanged += HandleStatsChanged;
            _subscribedTarget = Target;
        }

        CharacterPortraitUI portraitUI = GetPortraitUI(Portrait);
        if (portraitUI != null) portraitUI.Setup(Target);
        else if (Portrait != null) Portrait.sprite = Target.Portrait;

        RulerName.text = Target.Name; 

        //set spouse info
        CharacterPortraitUI spouseUI = GetPortraitUI(PortraitSpouse);
        if (Target.Spouse != null)
        {
            PortraitSpouse.enabled = true;
            if (spouseUI != null) spouseUI.Setup(Target.Spouse, "Spouse");
            else if (PortraitSpouse != null)
            {
                PortraitSpouse.sprite = Target.Spouse.Portrait;
                InfoPopup fallback = PortraitSpouse.GetComponent<InfoPopup>();
                if (fallback != null) fallback.Header = Target.Spouse.Name;
            }
        }
        else
        {
            PortraitSpouse.enabled = false;
            if (spouseUI != null) spouseUI.Clear();
        }

        //set liege info
        CharacterPortraitUI liegeUI = GetPortraitUI(PortraitLiege);
        if (Target.Liege != null)
        {
            PortraitLiege.enabled = true;
            if (liegeUI != null) liegeUI.Setup(Target.Liege, "Liege");
            else if (PortraitLiege != null)
            {
                PortraitLiege.sprite = Target.Liege.Portrait;
                InfoPopup fallback = PortraitLiege.GetComponent<InfoPopup>();
                if (fallback != null) fallback.Header = Target.Liege.Name;
            }
        }
        else
        {
            PortraitLiege.enabled = false;
            if (liegeUI != null) liegeUI.Clear();
        }

        //clear all traits;
        foreach (Transform child in TraitsObject.transform)
        {
            Destroy(child.gameObject);
        }

        //clear all Titles;
        foreach (Transform child in TitlesObject.transform)
        {
            Destroy(child.gameObject);
        }

        //update traits
        foreach (Trait trait in _char.Traits)
        {
            TraitUI traitUI = Instantiate(trait.Prefab, TraitsObject.transform).GetComponent<TraitUI>();
            traitUI.trait = trait;
            traitUI.name = trait.name;
        }

        //update titles
        foreach (var title in _char.Titles)
        {
            TitleIcon icon = Instantiate(HoldingIconPrefab, TitlesObject.transform).GetComponent<TitleIcon>();
            icon.sprite = title.Image;
            icon.title = title;

            InfoPopup titlePopup = icon.GetComponent<InfoPopup>();
            if (titlePopup != null)
            {
                titlePopup.Header = title.Name;
                titlePopup.Body = title.Holder != null ? "Held by " + title.Holder.Name : "Unclaimed";
            }
        }
        //update stats
        if(Target != null)
        {
            UpdateStats();
            CharacterListManager.Target = Target;
            CharacterListManager.ShowCourt();
        }
    }

    public void UpdateStats()
    {
        Diplomacy.text = Target.Stats.Diplomacy.ToString();
        Martial.text = Target.Stats.Martial.ToString();
        Stewardship.text = Target.Stats.Stewardship.ToString();
        Intrigue.text = Target.Stats.Intrigue.ToString();
        Learning.text = Target.Stats.Learning.ToString();
        CombatStrength.text = Target.Stats.CombatStrength.ToString();
        Culture.text = Target.Culture != null ? Target.Culture.Name : "Unknown";
        Religion.text = Target.Religion != null ? Target.Religion.Name : "Unknown";
        if (Society != null)
            Society.text = Target.Society != null ? $"{Target.Society.Name} (Rank {Mathf.Max(1, Target.SocietyRank)})" : "None";

        UpdateStatTooltip(Diplomacy, Target, Stats.StatType.Diplomacy);
        UpdateStatTooltip(Martial, Target, Stats.StatType.Martial);
        UpdateStatTooltip(Stewardship, Target, Stats.StatType.Stewardship);
        UpdateStatTooltip(Intrigue, Target, Stats.StatType.Intrigue);
        UpdateStatTooltip(Learning, Target, Stats.StatType.Learning);
        UpdateStatTooltip(CombatStrength, Target, Stats.StatType.CombatStrength);

        if (RelationScore != null)
        {
            Character player = MapManager.instance != null && MapManager.instance.characters != null
                ? MapManager.instance.characters.Player
                : null;

            if (player == null || player == Target)
            {
                RelationScore.text = "-";
                RelationScore.color = Color.gray;
            }
            else
            {
                int opinion = player.GetOpinion(Target);
                RelationScore.text = opinion.ToString();
                RelationScore.color = opinion >= 40 ? Color.green : opinion <= -40 ? Color.red : Color.white;
                InfoPopup popup = RelationScore.GetComponent<InfoPopup>();
                if (popup != null)
                {
                    popup.Header = "Opinion of " + Target.Name;
                    popup.Body = player.GetOpinionTooltip(Target);
                }
            }
        }
    }

    private void HandleStatsChanged()
    {
        if (Target == null || !gameObject.activeInHierarchy)
            return;
        UpdateStats();
    }

    protected override void OnDestroy()
    {
        if (_subscribedTarget != null)
        {
            _subscribedTarget.OnStatsChanged -= HandleStatsChanged;
            _subscribedTarget = null;
        }
        base.OnDestroy();
    }

    // Stat rows carry an InfoPopup whose header is baked into the prefab; fill in the
    // body so the player can see the current value, the base value, and which traits
    // are responsible for the difference (the same maths as Character.ApplyStatusModifiers).
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

        // Equipped items contribute too; show them so the breakdown matches Stats.
        if (c.EquippedItems != null)
        {
            foreach (var equipped in c.EquippedItems)
            {
                ItemDef def = ItemDatabase.Get(equipped.Value);
                if (def == null || def.bonuses == null) continue;
                foreach (Modifier modifier in def.bonuses)
                {
                    if (MatchesStat(modifier.modifierType, stat))
                        b.Append(def.displayName).Append(' ').Append(modifier.modifierValue.ToString("+0;-0;0")).AppendLine();
                }
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

    // Portraits on this screen carry a CharacterPortraitUI for hover info. Falls back to
    // a plain sprite swap when the widget is not present.
    private static CharacterPortraitUI GetPortraitUI(Image image)
    {
        return image != null ? image.GetComponent<CharacterPortraitUI>() : null;
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

    /// ///////////buttons
    public void CharacterUISpouseSwitch()
    {
        if (Target.Spouse != null) // Ensure the spouse exists
        {
            UpdateUI(Target.Spouse);
        }
    }
    public void CharacterLiegeSwitch()
    {
        if (Target.Liege != null) // Ensure the spouse exists
        {
            if (Target.Liege.MainTitle != null)
            {
                CameraController.instance.FocusCameraOnMainTitle(Target.Liege);
            }
            UpdateUI(Target.Liege);
        }
    }

    [Header("Belongings")]
    public GameObject personalBelongingsPrefab;
    private GameObject personalBelongingsInstance;

    public void OpenPersonalBelongings()
    {
        if (Target == null) return;
        if (personalBelongingsInstance == null)
        {
            if (personalBelongingsPrefab == null)
            {
                personalBelongingsPrefab = Resources.Load<GameObject>("Prefabs/UI/Screens/PersonalBelongingsPanel");
            }
            if (personalBelongingsPrefab != null)
            {
                Transform parent = transform.parent != null ? transform.parent : transform;
                personalBelongingsInstance = Instantiate(personalBelongingsPrefab, parent);
            }
            else
            {
                Debug.LogWarning("PersonalBelongingsPanel prefab not found in Resources/Prefabs/UI/Screens/");
                return;
            }
        }
        personalBelongingsInstance.SetActive(true);
        PersonalBelongingsUI ui = personalBelongingsInstance.GetComponent<PersonalBelongingsUI>();
        if (ui != null)
        {
            ui.Setup(Target);
            return;
        }
        ui = personalBelongingsInstance.GetComponentInChildren<PersonalBelongingsUI>(true);
        if (ui != null)
        {
            ui.Setup(Target);
        }
    }

    public void CenterOnCharacter()
    {
        if (Target == null) return;
        CameraController.instance.FocusCameraOn(Target.WorldPosition);
    }

    // Wired to the Family button in the CharacterScreen header panel.
    public void OpenFamilyTree()
    {
        if (Target == null) return;
        GameUIManager.NewFamilyTreeUI(Target);
    }

}
