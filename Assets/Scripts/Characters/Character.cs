using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class Character
{
    private static int nextCharacterId = 1;
    private static Trait[] _cachedTraits;
    private static readonly System.Collections.Generic.Dictionary<Gender, Sprite[]> _cachedPortraits =
        new System.Collections.Generic.Dictionary<Gender, Sprite[]>();

    private static Trait[] CachedTraits
    {
        get
        {
            if (_cachedTraits == null)
                _cachedTraits = Resources.LoadAll<Trait>("Traits");
            return _cachedTraits;
        }
    }

    private static Sprite[] CachedPortraits(Gender gender)
    {
        if (!_cachedPortraits.TryGetValue(gender, out Sprite[] portraits) || portraits == null)
        {
            string folderPath = gender == Gender.Male ? "Portraits/Male" : "Portraits/Female";
            portraits = Resources.LoadAll<Sprite>(folderPath);
            _cachedPortraits[gender] = portraits;
        }
        return portraits;
    }
    public int UniqueId { get; private set; }
    
    public string Name;
    public Culture Culture;
    public Religion Religion;
    public Society Society;
    public int SocietyRank;
    public float SocietyScore;
    public House House;
    public float Wealth = 100f, Reknown = 50f, Piety = 10f;
    public Character Spouse{ get; private set; }
    public Sprite Portrait;

    public Stats BaseStats = new Stats(4, 4, 4, 4, 4, 4);
    public Stats Stats = new Stats();

    public List<Trait> Traits = new List<Trait>();

    [System.NonSerialized]
    public Dictionary<int, List<OpinionModifier>> OpinionModifiers = new();
    [System.NonSerialized]
    public Inventory Inventory = new Inventory();
    // Equipped equipment, keyed by the slot the item occupies.
    [System.NonSerialized]
    public Dictionary<EquipmentSlot, string> EquippedItems = new();
    // Day on which the item currently in each equipment slot was first acquired.
    [System.NonSerialized]
    public Dictionary<EquipmentSlot, int> EquippedItemDay = new Dictionary<EquipmentSlot, int>();

    // Raised whenever Stats are recomputed (traits/titles/items changed). UI can
    // subscribe to refresh displays without reopening the window.
    // Backing field is explicitly non-serialized (events can't carry the attribute).
    [System.NonSerialized]
    private System.Action _onStatsChanged;
    public event System.Action OnStatsChanged
    {
        add { _onStatsChanged += value; }
        remove { _onStatsChanged -= value; }
    }
    [System.NonSerialized]
    public CharacterSO SourceSO;

    public bool IsAlive = true;
    public int DeathDay = -1;
    public DeathCause CauseOfDeath;

    // Date of birth as a TimeManager.daysPassed value. The calendar starts at day 0
    // (year 800), so a negative day is a perfectly valid date: everyone born before
    // the start of the game has one. An unassigned birth date therefore uses
    // UnassignedBirthDay instead of a negative number, and
    // CharacterManager.AssignStartingAges resolves those once at the end of world
    // generation.
    public const int UnassignedBirthDay = int.MinValue;
    public int BirthDay = UnassignedBirthDay;

    // True once a real birth date is known.
    public bool HasBirthDate => BirthDay != UnassignedBirthDay;

    public int Age
    {
        get
        {
            if (!HasBirthDate)
                return 0;
            int today = TimeManager.instance != null ? TimeManager.instance.daysPassed : 0;
            int age = (today - BirthDay) / TimeManager.DaysPerYear;
            return age < 0 ? 0 : age;
        }
    }

    // Hidden health (CK2-style, never shown in the UI): around 5 is average.
    // Higher values lower the daily chance of dying of natural causes, lower
    // values raise it. Health does not decay with age; age itself is the cause
    // of the rising mortality in NaturalDeath.
    public float BaseHealth = 5f;
    public float Health => BaseHealth;

    public int LastEventDay = -999;

    public bool HasTrait(Trait trait) => Traits.Contains(trait);

    public Gender Gender;
    
    [SerializeReference]
    public Character Liege;
    [SerializeReference]
    public List<CouncilPosition> Council = new List<CouncilPosition>();
    [System.NonSerialized]
    public CouncilPosition ServesOnCouncil;
    [SerializeReference]
    public readonly List<Title> Titles = new List<Title>();

    // Physical presence model: CurrentLocation is the logical state (always non-null, origin while travelling)
    [System.NonSerialized]
    public Location CurrentLocation;
    [System.NonSerialized]
    public MapPawn ActivePawn;       // Non-null only while travelling
    [System.NonSerialized]
    public Location TravelDestination;

    public bool IsTraveling => ActivePawn != null;

    // Projected world position: pawn transform while travelling, else the location's position
    public Vector3 WorldPosition
    {
        get
        {
            if (IsTraveling)
                return ActivePawn.transform.position;
            if (CurrentLocation != null)
                return CurrentLocation.Position;
            return Vector3.zero;
        }
    }

    CharacterManager manager;
    public List<Character> Court
    {
        get
        {
            List<Character> courtMembers = new List<Character>();
            if (Spouse != null)
                courtMembers.Add(Spouse);
            courtMembers.AddRange(Courtiers);
            return courtMembers;
        }
    }

    // Blood relatives + spouse: the members shown by the family tab
    public List<Character> Family
    {
        get
        {
            List<Character> family = new List<Character>();
            if (Spouse != null) family.Add(Spouse);
            if (Father != null) family.Add(Father);
            if (Mother != null) family.Add(Mother);
            foreach (Character child in Children)
            {
                if (child != null && child != this && !family.Contains(child))
                    family.Add(child);
            }
            return family;
        }
    }

    [SerializeReference]
    List<Character> Courtiers = new List<Character>();
    [SerializeReference]
    public List<Character> Vassals = new List<Character>();

    // Family relations
    [SerializeReference]
    public Character Father;
    [SerializeReference]
    public Character Mother;
    [SerializeReference]
    public List<Character> Children = new List<Character>();

    public Title MainTitle => Titles != null && Titles.Count > 0 ? Titles[0] : null;

    // Display-only characters (event faces, prop merchants) that are never
    // registered with the manager and never subscribe to the clock, so the UI can
    // drop them and let them be collected instead of leaving them in the world.
    public bool IsTemporary { get; private set; }

    // Constructor
    public Character(string name = null, CharacterSO SO = null, Gender gender = Gender.Male, Sprite portrait = null, List<Trait> traits = null, Culture culture = null, Religion religion = null, House house = null, bool Married = false, bool temporary = false, int age = -1)
    {
        UniqueId = nextCharacterId++;
        IsTemporary = temporary;

        int today = TimeManager.instance != null ? TimeManager.instance.daysPassed : 0;
        BirthDay = age >= 0 ? today - age * TimeManager.DaysPerYear : UnassignedBirthDay;
        BaseHealth = Random.Range(4.5f, 5.5f);
        
        if (MapManager.instance == null || MapManager.instance.characters == null)
        {
            Debug.LogError("Cannot create Character: MapManager.instance or characters is null!");
            return;
        }
        manager = MapManager.instance.characters;
        Gender = gender;
        Culture = culture;
        Religion = religion; 
        if (Religion == null && Religion.AllReligions != null && Religion.AllReligions.Count > 0)
            Religion = Religion.AllReligions[Random.Range(0, Religion.AllReligions.Count)];
        if (Culture == null && Culture.AllCultures != null && Culture.AllCultures.Count > 0)
            Culture = Culture.AllCultures[Random.Range(0, Culture.AllCultures.Count)];
        if (Society == null && Society.AllSocieties != null && Society.AllSocieties.Count > 0)
            Society = Society.AllSocieties[Random.Range(0, Society.AllSocieties.Count)];
        House = house;
        // Assign Name based on the input or generate a random one
        Name = name ?? Names.GenerateRandomName(gender);
        // Assign Portrait based on gender, either passed or randomly assigned
        Portrait = portrait != null ? portrait : GetRandomPortrait(gender);
        Traits = traits;
        if (SO != null)
        {
            BaseStats = SO.Stats;
            Name = SO.CharacterName;
            Gender = SO.Gender;
            Portrait = SO.Portrait;
            Wealth = SO.Wealth;
            Traits = SO.Traits;
            SourceSO = SO;
            if (SO.age > 0)
                BirthDay = today - SO.age * TimeManager.DaysPerYear - Random.Range(0, TimeManager.DaysPerYear);
            SetSpouseMutual(manager.allCharacters.Find(c => c.Name == SO.Spouse.CharacterName));
        }
        if (Married)
        {
            CharacterManager.GenerateSpouse(this);
        }
        if (!temporary)
            manager.allCharacters.Add(this);

        Traits ??= new List<Trait>();
        if (Traits.Count < 1)
        {
            AddRandomTraits();
        }
        ApplyStatusModifiers();
        // Display-only characters never pass through world generation's age pass,
        // so give them a plausible adult age here instead.
        if (temporary && !HasBirthDate)
            BirthDay = today - Random.Range(16, 61) * TimeManager.DaysPerYear - Random.Range(0, TimeManager.DaysPerYear);
        // A temporary character takes no part in the clock either: a leaked listener
        // would keep it alive and paying out income long after the UI dropped it.
        if (!temporary)
        {
            // Subscribe through the manager rather than touching instance directly:
            // characters are created during map setup and used to silently miss the
            // payday listener whenever TimeManager had not been created yet, leaving
            // their wealth frozen for the whole session.
            TimeManager.SubscribePayday(GenerateIncome);
            TimeManager.SubscribeYearPassed(DecayOpinionModifiers);
            TimeManager.SubscribeDayPassed(CheckNaturalDeath);
        }
    }

    public void SetSpouseMutual(Character _spouse)
    {
        if (_spouse != null)
        {
            Spouse = _spouse;
            _spouse.Spouse ??= this;
        }
    }
    public void SetFather(Character father)
    {
        if (father == null || father == this) return;

        Father = father;
        if (!father.Children.Contains(this))
            father.Children.Add(this);

        // Link the other parent through father's spouse, if known
        if (Mother == null && father.Spouse != null && father.Spouse.Gender == Gender.Female)
        {
            Mother = father.Spouse;
            if (!Mother.Children.Contains(this))
                Mother.Children.Add(this);
        }
    }
    public void SetMother(Character mother)
    {
        if (mother == null || mother == this) return;

        Mother = mother;
        if (!mother.Children.Contains(this))
            mother.Children.Add(this);

        // Link the other parent through mother's spouse, if known
        if (Father == null && mother.Spouse != null && mother.Spouse.Gender == Gender.Male)
        {
            Father = mother.Spouse;
            if (!Father.Children.Contains(this))
                Father.Children.Add(this);
        }
    }
    public void AddChild(Character child)
    {
        if (child == null || child == this) return;
        if (Children.Contains(child)) return;

        Children.Add(child);

        if (Gender == Gender.Male && child.Father == null)
            child.Father = this;
        if (Gender == Gender.Female && child.Mother == null)
            child.Mother = this;

        // Link the other parent (spouse) so the family graph stays complete
        if (Spouse != null)
        {
            if (Spouse.Gender == Gender.Male && child.Father == null)
                child.Father = Spouse;
            if (Spouse.Gender == Gender.Female && child.Mother == null)
                child.Mother = Spouse;
            if (!Spouse.Children.Contains(child))
                Spouse.Children.Add(child);
        }
    }
    public void AddCourtier(Character _courtier)
    {
        if(manager.Player == this)
        {
            MessageManager.instance.Send("{0} has arrived at your court!", _courtier);
        }
        Courtiers.Add(_courtier);
    }
    public void AssignToCouncil(CouncilPosition position)
    {
        if (position == null) return;

        // Vacating the old post also stops its deployment, so no stale map indicator
        // or day-tick countdown survives for a slot that is now empty.
        if (ServesOnCouncil != null && ServesOnCouncil != position)
            CouncilService.Vacate(ServesOnCouncil, refreshUi: false);

        position.Appointed = this;
        ServesOnCouncil = position;
    }
    public void RemoveFromCouncil()
    {
        if (ServesOnCouncil == null) return;
        CouncilService.Vacate(ServesOnCouncil);
    }
    public void AddRandomTraits()
    {
        Trait[] allTraits = CachedTraits;
        if (allTraits.Length > 0)
        {
            // Add two random and unique traits
            HashSet<int> usedIndexes = new HashSet<int>();
            while (usedIndexes.Count < 2 && usedIndexes.Count < allTraits.Length)
            {
                int randomIndex = Random.Range(0, allTraits.Length);
                if (!usedIndexes.Contains(randomIndex))
                {
                    Traits.Add(allTraits[randomIndex]);
                    usedIndexes.Add(randomIndex);
                }
            }
        }
    }
    public void AddTitle(Title t)
    {
        Titles.Add(t);
        Titles.Sort((b, a) => a.Rank.CompareTo(b.Rank));
    }
    public void ApplyStatusModifiers()
    {
        Stats = BaseStats;

        // Traits are always active.
        foreach (Trait trait in Traits)
        {
            foreach (Modifier modifier in trait.modifiers)
                ApplyModifier(modifier);
        }

        // Equipment bonuses only apply while the item is actually equipped.
        foreach (var equipped in EquippedItems)
        {
            ItemDef def = ItemDatabase.Get(equipped.Value);
            if (def == null)
                continue;

            foreach (Modifier modifier in def.bonuses)
                ApplyModifier(modifier);
        }

        _onStatsChanged?.Invoke();
    }

    private void ApplyModifier(Modifier modifier)
    {
        if (modifier == null)
            return;

        switch (modifier.modifierType)
        {
            case ModifierType.Diplomacy:
                Stats.Diplomacy += (int)modifier.modifierValue;
                break;

            case ModifierType.Martial:
                Stats.Martial += (int)modifier.modifierValue;
                break;

            case ModifierType.Stewardship:
                Stats.Stewardship += (int)modifier.modifierValue;
                break;

            case ModifierType.Intrigue:
                Stats.Intrigue += (int)modifier.modifierValue;
                break;

            case ModifierType.Learning:
                Stats.Learning += (int)modifier.modifierValue;
                break;

            case ModifierType.CombatStrengh:
                Stats.CombatStrength += (int)modifier.modifierValue;
                break;
        }
    }

    public void GenerateIncome()
    {
        if (!IsAlive)
            return;

        Wealth += CalculateIncome(PropertyIncome());
        Reknown += CalculateReknown();
        Piety += CalculatePiety();
    }
    public float CalculateIncome(float amount)
    {
        return (Stats.Stewardship + 1) / 20f * amount; // apply stewardship to propertyincome
    }

    public float CalculateReknown()
    {
        float r = 0f;
        r += (Stats.Stewardship + 1) / 10f;
        for (int i = 0; i < Titles.Count; i++)
        {
            r++;
        }
        return r;
    }
    public float CalculatePiety()
    {
        return (Stats.Learning + 1) / 10f;
    }
    public float PropertyIncome() //gives income withouth Stewardship or other modifiers;
    {
        float income = 0;

        foreach (Title t in Titles)
        {
            if (t is HoldingTitle h)
            {
                income += h.AssociatedLocation.GetTax();
            }
        }
        return income;
    }
    public IEnumerable<Province> GetAllRuledProvinces()
    {
        HashSet<Province> provinces = new();
        // A ruler may hold titles from several ranks (empire/kingdom/duchy/province)
        // that overlap; use a set so each province is yielded exactly once.
        foreach (RegionTitle title in Titles.OfType<RegionTitle>())
        {
            foreach (Province province in title.AssociatedRegion.GetProvinces())
            {
                if (provinces.Add(province))
                    yield return province;
            }
        }
    }
    // Male first names
    public List<Character> GetRealmCharacters()
    {
        List<Character> realmCharacters = new List<Character>();
        Queue<Character> toProcess = new Queue<Character>();
        HashSet<Character> processed = new HashSet<Character>(); // To track already-added characters

        // Start with the ruler
        toProcess.Enqueue(this);
        processed.Add(this); // Avoid reprocessing the top ruler

        while (toProcess.Count > 0)
        {
            Character current = toProcess.Dequeue();

            realmCharacters.Add(current);

            // Process all vassals
            foreach (Character vassal in current.Vassals)
            {
                if (!processed.Contains(vassal)) // Ensure no duplicates
                {
                    processed.Add(vassal);
                    toProcess.Enqueue(vassal);
                }
            }

            // Process all court members
            foreach (Character courtMember in current.Court)
            {
                if (!processed.Contains(courtMember)) // Ensure no duplicates
                {
                    processed.Add(courtMember);
                    toProcess.Enqueue(courtMember);
                }
            }
        }

        return realmCharacters;
    }

    public int GetOpinion(Character target)
    {
        if (target == null)
            return 0;

        int score = 0;
        foreach (OpinionModifier modifier in CollectOpinionModifiers(target))
            score += modifier.Value;

        return Mathf.Clamp(score, -100, 100);
    }

    public List<OpinionModifier> GetOpinionBreakdown(Character target)
    {
        return CollectOpinionModifiers(target);
    }

    public string GetOpinionTooltip(Character target)
    {
        if (target == null)
            return "";

        StringBuilder builder = new StringBuilder();
        foreach (OpinionModifier modifier in CollectOpinionModifiers(target))
            builder.AppendLine($"{modifier.Reason}: {modifier.Value:+0;-0;0}");
        builder.AppendLine($"Total: {GetOpinion(target):+0;-0;0}");

        return builder.ToString();
    }

    private List<OpinionModifier> CollectOpinionModifiers(Character target)
    {
        List<OpinionModifier> modifiers = new List<OpinionModifier>();
        if (target == null)
            return modifiers;

        // Family ties
        if (target == Spouse)
            modifiers.Add(new OpinionModifier("Spouse", 50));
        if (target == Father)
            modifiers.Add(new OpinionModifier("Father", 40));
        if (target == Mother)
            modifiers.Add(new OpinionModifier("Mother", 40));
        if (Children.Contains(target))
            modifiers.Add(new OpinionModifier(target.Gender == Gender.Male ? "Son" : "Daughter", 45));

        // Sibling: shares at least one parent
        if (target != this)
        {
            bool isSibling =
                (Father != null && target.Father == Father) ||
                (Mother != null && target.Mother == Mother);
            if (isSibling)
                modifiers.Add(new OpinionModifier(target.Gender == Gender.Male ? "Brother" : "Sister", 30));
        }

        if (target.Liege == this || Liege == target)
            modifiers.Add(new OpinionModifier("Liege/Vassal", 15));
        if (Courtiers.Contains(target) || target.Courtiers.Contains(this))
            modifiers.Add(new OpinionModifier("Court", 5));
        if (House != null && target.House != null && House == target.House)
            modifiers.Add(new OpinionModifier("Same House", 10));

        foreach (Trait trait in Traits)
        {
            if (trait == null)
                continue;
            if (target.HasTrait(trait) && trait.OpinionModifier != 0)
                modifiers.Add(new OpinionModifier($"Shared trait: {trait.name}", trait.OpinionModifier));
            if (trait.OppositeTrait != null && target.HasTrait(trait.OppositeTrait) && trait.OpinionOfOppositeTrait != 0)
                modifiers.Add(new OpinionModifier($"Clashing trait: {trait.OppositeTrait.name}", trait.OpinionOfOppositeTrait));
        }

        int diplomacyModifier = Mathf.RoundToInt(1.5f * (target.Stats.Diplomacy - 4));
        if (diplomacyModifier != 0)
            modifiers.Add(new OpinionModifier("Diplomacy", diplomacyModifier));

        int reknownModifier = target.Reknown >= 0f
            ? Mathf.Min(Mathf.RoundToInt(target.Reknown / 200f), 10)
            : Mathf.RoundToInt(target.Reknown / 20f);
        if (reknownModifier != 0)
            modifiers.Add(new OpinionModifier("Reknown", reknownModifier));

        if (Culture != null && target.Culture != null)
        {
            int cultureModifier = Culture == target.Culture
                ? 10
                : Mathf.RoundToInt(Culture.GetCulturalCompatibility(target.Culture) * 10f);
            if (cultureModifier != 0)
                modifiers.Add(new OpinionModifier("Culture", cultureModifier));
        }

        if (Religion != null && target.Religion != null)
        {
            int religionModifier = Religion == target.Religion
                ? 12
                : Mathf.RoundToInt(Religion.GetReligiousRelation(target.Religion) * 20f);
            if (religionModifier != 0)
                modifiers.Add(new OpinionModifier("Religion", religionModifier));
        }

        if (OpinionModifiers.TryGetValue(target.UniqueId, out var stored))
        {
            foreach (OpinionModifier modifier in stored)
            {
                if (modifier.IsActive)
                    modifiers.Add(new OpinionModifier(modifier.Reason, modifier.Value, modifier.YearsLeft));
            }
        }

        return modifiers;
    }

    public void AddTimedModifier(Character target, string reason, int value, int years = -1)
    {
        if (target == null || value == 0)
            return;

        if (!OpinionModifiers.TryGetValue(target.UniqueId, out var list))
        {
            list = new List<OpinionModifier>();
            OpinionModifiers[target.UniqueId] = list;
        }

        OpinionModifier existing = list.Find(m => m.Reason == reason);
        if (existing != null)
        {
            existing.Value = value;
            existing.YearsLeft = years;
            return;
        }

        list.Add(new OpinionModifier(reason, value, years));
    }

    public void DecayOpinionModifiers()
    {
        if (!IsAlive)
            return;

        foreach (var list in OpinionModifiers.Values)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].YearsLeft > 0)
                    list[i].YearsLeft--;
                if (list[i].YearsLeft == 0)
                    list.RemoveAt(i);
            }
        }
    }

    // One death roll per game day. Age sets the base chance from CK2's mortality
    // table; health scales it around the average of 5.
    private void CheckNaturalDeath()
    {
        if (!IsAlive)
            return;

        if (Random.value < NaturalDeath.DailyChance(Age, Health))
            Die(DeathCause.Natural);
    }

    public void Die(DeathCause cause)
    {
        if (!IsAlive)
            return;

        IsAlive = false;
        CauseOfDeath = cause;
        DeathDay = TimeManager.instance != null ? TimeManager.instance.daysPassed : -1;

        // Free the council seat so it does not stay assigned to a corpse.
        RemoveFromCouncil();

        // Let go of any travel pawn; RemoveCharacter despawns it when it is empty.
        if (ActivePawn != null)
        {
            ActivePawn.RemoveCharacter(this);
            ActivePawn = null;
        }
        TravelDestination = null;

        // TODO: succession. Titles, vassals, court and Location.Ruler are
        // intentionally left untouched for now.

        // Deaths of nobodies would flood the message log every day at high speed,
        // so only titled characters and the player get a notice.
        bool isNotable = Titles.Count > 0 || (manager != null && manager.Player == this);
        if (isNotable && MessageManager.instance != null)
            MessageManager.instance.Send("{0} has died of natural causes at the age of {1}.", this, Age);

        if (manager != null && manager.Player == this)
            manager.SwitchToRandomAlivePlayer();
    }

    public void GiveItem(string itemId, int count = 1)
    {
        if (count <= 0) return;
        Inventory.Add(itemId, count);

        // Let the player know when something new lands in their inventory.
        if (manager != null && manager.Player == this && MessageManager.instance != null)
        {
            ItemDef def = ItemDatabase.Get(itemId);
            string name = def != null ? def.displayName : itemId;
            MessageManager.instance.Send(MessagePriority.Low, MessageCategory.Other, "Received {0} (x{1}).", name, count);
        }
    }

    public bool TakeItem(string itemId, int count = 1)
    {
        if (count <= 0) return false;
        return Inventory.Remove(itemId, count);
    }

    public int GetItemCount(string itemId) => Inventory.GetCount(itemId);

    // ---- Equipment ----

    public string GetEquippedItemId(EquipmentSlot slot)
        => EquippedItems.TryGetValue(slot, out string id) ? id : null;

    public bool IsEquipped(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return false;

        foreach (var entry in EquippedItems)
        {
            if (entry.Value == itemId)
                return true;
        }
        return false;
    }

    // True when the item exists, is equipment, and the character actually carries one.
    public bool CanEquip(string itemId)
    {
        ItemDef def = ItemDatabase.Get(itemId);
        return def != null && def.IsEquippable && GetItemCount(itemId) > 0;
    }

    // Equips into the slot the item itself defines.
    public bool Equip(string itemId)
    {
        ItemDef def = ItemDatabase.Get(itemId);
        if (def == null)
            return false;

        return Equip(def.slot, itemId);
    }

    // Equips only if the item fits the given slot. An item already in the slot is
    // returned to the inventory. The equipped item leaves the inventory so it cannot
    // be counted twice.
    public bool Equip(EquipmentSlot slot, string itemId)
    {
        if (slot == EquipmentSlot.None)
            return false;

        ItemDef def = ItemDatabase.Get(itemId);
        if (def == null || def.slot != slot)
            return false;
        if (GetItemCount(itemId) <= 0)
            return false;

        // Remember when this item was first acquired so the UI can show it.
        int acquiredDay = TimeManager.instance != null ? TimeManager.instance.daysPassed : 0;
        for (int i = 0; i < Inventory.Entries.Count; i++)
        {
            if (Inventory.Entries[i].itemId == itemId)
            {
                acquiredDay = Inventory.Entries[i].dayAdded;
                break;
            }
        }

        string current = GetEquippedItemId(slot);
        if (!string.IsNullOrEmpty(current))
        {
            int currentDay = EquippedItemDay.TryGetValue(slot, out int cd) ? cd : acquiredDay;
            Inventory.Add(current, 1, currentDay);
        }

        Inventory.Remove(itemId, 1);
        EquippedItems[slot] = itemId;
        EquippedItemDay[slot] = acquiredDay;
        ApplyStatusModifiers();
        return true;
    }

    public bool Unequip(EquipmentSlot slot)
    {
        string id = GetEquippedItemId(slot);
        if (string.IsNullOrEmpty(id))
            return false;

        int day = EquippedItemDay.TryGetValue(slot, out int d) ? d : (TimeManager.instance != null ? TimeManager.instance.daysPassed : 0);
        EquippedItems.Remove(slot);
        EquippedItemDay.Remove(slot);
        Inventory.Add(id, 1, day);
        ApplyStatusModifiers();
        return true;
    }

    private Sprite GetRandomPortrait(Gender gender)
    {
        // Load portraits based on gender (cached)
        Sprite[] portraits = CachedPortraits(gender);

        string folderPath = gender == Gender.Male ? "Portraits/Male" : "Portraits/Female";

        // Ensure there are portraits in the folder, then pick one at random
        if (portraits.Length > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, portraits.Length);
            return portraits[randomIndex];
        }
        else
        {
            Debug.LogWarning($"No portraits found in the {folderPath} folder.");
            return null;
        }
    }
}
public enum Gender
{
    Male,
    Female,
    Genderless
}

public enum DeathCause
{
    None,
    Natural
}

[System.Serializable]
public struct Stats
{
    public int Diplomacy;
    public int Martial;
    public int Stewardship;
    public int Intrigue;
    public int Learning;
    public int CombatStrength;

    public Stats(int diplomacy = 4, int martial = 4, int stewardship = 4, int intrigue = 4, int learning = 4, int combatStrength = 4)
    {
        Diplomacy = diplomacy;
        Martial = martial;
        Stewardship = stewardship;
        Intrigue = intrigue;
        Learning = learning;
        CombatStrength = combatStrength;
    }
    public enum StatType
    {
        Diplomacy,
        Martial,
        Stewardship,
        Intrigue,
        Learning,
        CombatStrength
    }

}