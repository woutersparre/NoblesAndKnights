using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;
using static Stats;

[System.Serializable]
public class CharacterManager
{
    [SerializeReference]
    public List<Character> allCharacters = new List<Character>();
    // Reverse lookup: court member (spouse/courtier) -> the character whose court they belong to.
    // Rebuilt after character generation; used to make court membership checks O(1).
    [System.NonSerialized]
    public Dictionary<Character, Character> CourtOwner = new Dictionary<Character, Character>();

    public void RebuildCourtIndex()
    {
        CourtOwner.Clear();
        if (allCharacters == null) return;
        foreach (Character owner in allCharacters)
        {
            if (owner == null) continue;
            // Court includes spouse + courtiers (allocates once here, not in hot loops).
            foreach (Character member in owner.Court)
            {
                if (member == null || member == owner) continue;
                if (!CourtOwner.ContainsKey(member))
                    CourtOwner[member] = owner;
            }
        }
    }
    public Character Player;
    public static void CreateCouncil(Character character)
    {
        character.Council.Clear();

        int maxSlots = character.MainTitle != null ? (int)character.MainTitle.Rank : 0;
        for (int i = 0; i < maxSlots; i++)
        {
            character.Council.Add(new CouncilPosition(CouncilRole.None, character));
        }
    }
    public void PlayerSetup()
    {
        // Find a character who has at least one title
        Character selectedCharacter = null;

        int guard = 0;
        while ((selectedCharacter == null || selectedCharacter.Titles.Count == 0) && guard++ < 10000)
        {
            int count = allCharacters.Count > 0 ? allCharacters.Count : 1;
            Character candidate = allCharacters[UnityEngine.Random.Range(0, count)];
            if (candidate != null && candidate.Titles.Count > 0)
                selectedCharacter = candidate;
        }

        if (selectedCharacter == null && allCharacters.Count > 0)
            selectedCharacter = allCharacters[0];

        SetPlayer(selectedCharacter);
    }

    // Assigns a chosen character as the player: grants starting items and focuses
    // the camera. Used by both PlayerSetup (random) and the character selection
    // overlay (explicit choice).
    public void SetPlayer(Character character)
    {
        if (character == null)
            return;

        Player = character;
        GrantStartingItems(Player);
        if (CameraController.instance != null)
            CameraController.instance.FocusCameraOnFast(Player.WorldPosition);
    }

    // TEMPORARY: hands the player a few hardcoded items so the Personal Belongings
    // panel can be tested. Remove once items are obtained through normal gameplay.
    private static void GrantStartingItems(Character player)
    {
        if (player == null)
            return;

        player.GiveItem("healing_potion", 3);
        player.GiveItem("iron_ore", 10);
        player.GiveItem("quest_letter", 1);
        player.GiveItem("iron_sword", 1);
        player.GiveItem("leather_armor", 1);
        player.GiveItem("scholars_ring", 1);
        player.GiveItem("kings_crown", 1);
    }
    public static void GenerateSpouse(Character c)
    {
        Gender spouseGender = c.Gender == Gender.Male ? Gender.Female : Gender.Male;
        string spouseName = Names.GenerateRandomName(spouseGender, Names.GetLastName(c.Name));
        // Check player's gender and assign opposite gender to spouse
        Character spouse = new(name: spouseName, gender: spouseGender);
        spouse.Culture = c.Culture ?? spouse.Culture;
        spouse.Religion = c.Religion ?? spouse.Religion;
        spouse.Society = c.Society ?? spouse.Society;
        c.SetSpouseMutual(spouse);
    }

    public void AddCourtiers(List<Character> rulers)
    {

        // give some random courtiers and counsil to all Rulers
        foreach (Character c in rulers)
        {
            //give every ruler a counsil
            CreateCouncil(c);

            for (int i = 0; i < 5; i++)
            {
                Character courtier = new();
                courtier.Culture = c.Culture ?? courtier.Culture;
                courtier.Religion = c.Religion ?? courtier.Religion;
                courtier.Society = c.Society ?? courtier.Society;
                c.AddCourtier(courtier);
            }
        }
    }

    // Gives a runtime-generated character a family beyond their spouse:
    // a set of parents (one generation up, never recursed) and 1-3 children,
    // all mutually linked via Father/Mother/Children.
    public static void GenerateFamily(Character c)
    {
        if (c == null)
            return;

        if (c.Spouse == null)
            GenerateSpouse(c);

        string lastName = Names.GetLastName(c.Name);

        // Parents
        if (c.Father == null && c.Mother == null)
        {
            Character father = new(name: Names.GenerateRandomName(Gender.Male, lastName));
            Character mother = new(name: Names.GenerateRandomName(Gender.Female, lastName));
            father.Culture = c.Culture ?? father.Culture;
            father.Religion = c.Religion ?? father.Religion;
            father.Society = c.Society ?? father.Society;
            mother.Culture = c.Culture ?? mother.Culture;
            mother.Religion = c.Religion ?? mother.Religion;
            mother.Society = c.Society ?? mother.Society;
            father.SetSpouseMutual(mother); // parents are married to each other
            c.SetFather(father);            // also back-fills c.Mother via father's spouse
            c.SetMother(mother);            // ensures c is in mother's Children list
        }
        else if (c.Father == null)
        {
            Character father = new(name: Names.GenerateRandomName(Gender.Male, lastName));
            father.Culture = c.Culture ?? father.Culture;
            father.Religion = c.Religion ?? father.Religion;
            father.Society = c.Society ?? father.Society;
            if (c.Mother != null) father.SetSpouseMutual(c.Mother);
            c.SetFather(father);
        }
        else if (c.Mother == null)
        {
            Character mother = new(name: Names.GenerateRandomName(Gender.Female, lastName));
            mother.Culture = c.Culture ?? mother.Culture;
            mother.Religion = c.Religion ?? mother.Religion;
            mother.Society = c.Society ?? mother.Society;
            if (c.Father != null) mother.SetSpouseMutual(c.Father);
            c.SetMother(mother);
        }

        // Children (1-3) share the family last name
        int childCount = Random.Range(1, 4);
        for (int i = 0; i < childCount; i++)
        {
            Gender childGender = Random.Range(0, 2) == 0 ? Gender.Male : Gender.Female;
            Character child = new(name: Names.GenerateRandomName(childGender, lastName));
            child.Culture = c.Culture ?? child.Culture;
            child.Religion = c.Religion ?? child.Religion;
            child.Society = c.Society ?? child.Society;
            c.AddChild(child);
        }
    }

    public void GenerateFamilies(List<Character> characters)
    {
        foreach (Character c in characters)
            GenerateFamily(c);
    }

    // A child is born while the parent is between these ages, so a parent is
    // always the older one and nobody becomes a parent while still a child.
    private const int MinParentChildGap = 18;
    private const int MaxParentChildGap = 28;
    // The age at which a character counts as an adult.
    private const int AdultAge = 18;

    // World generation has no birth dates, so ages are inferred from the family
    // graph: authored CharacterSO ages are kept, parents are always older than
    // their children, and everyone else gets a plausible adult age. Called once
    // after all characters (including generated families) exist.
    public void AssignStartingAges()
    {
        if (allCharacters == null || allCharacters.Count == 0)
            return;

        int today = TimeManager.instance != null ? TimeManager.instance.daysPassed : 0;
        List<Character> characters = allCharacters.FindAll(c => c != null && !c.IsTemporary);

        // Generation depth: 0 = no known parents, +1 per parent generation. A parent
        // always has a lower depth than its child, so sorting by it dates ancestors
        // before their descendants and a child can be dated relative to its parent.
        Dictionary<Character, int> depth = new();
        foreach (Character c in characters)
            GetGenerationDepth(c, depth, new HashSet<Character>());

        // Ancestors first, so a dated parent exists before their children are dated.
        characters.Sort((a, b) => depth[a].CompareTo(depth[b]));

        Dictionary<Character, int> descendantCache = new();
        foreach (Character c in characters)
        {
            if (c.HasBirthDate)
                continue;

            Character parent = c.Father ?? c.Mother;
            int age;

            if (parent != null && parent.HasBirthDate)
            {
                // Relative to the parent: the child is born when the parent is
                // MinParentChildGap..MaxParentChildGap years old, so the parent is
                // always older than the child.
                int parentAge = AgeInYears(today, parent.BirthDay);
                int maxGap = Mathf.Max(MinParentChildGap, Mathf.Min(MaxParentChildGap, parentAge - 1));
                age = parentAge - Random.Range(MinParentChildGap, maxGap + 1);
            }
            else
            {
                // No dated parent: this character starts a family line, so their age
                // has to leave room for every generation below them.
                int generationsBelow = DescendantGenerations(c, descendantCache);
                age = generationsBelow == 0
                    ? Random.Range(AdultAge, 61)
                    : AdultAge + generationsBelow * MinParentChildGap + Random.Range(0, 12);
            }

            age = Mathf.Max(1, age);
            c.BirthDay = today - age * TimeManager.DaysPerYear - Random.Range(0, TimeManager.DaysPerYear);
        }
    }

    // Age of a character in whole years, from a birth day (mirrors Character.Age).
    private static int AgeInYears(int today, int birthDay)
    {
        if (birthDay == Character.UnassignedBirthDay)
            return 0;
        int age = (today - birthDay) / TimeManager.DaysPerYear;
        return age < 0 ? 0 : age;
    }

    // How many generations of descendants sit below a character (0 = no children).
    // The oldest member of a family line has to be old enough to leave room for
    // everyone below them.
    private static int DescendantGenerations(Character c, Dictionary<Character, int> cache, HashSet<Character> visiting = null)
    {
        if (c == null)
            return 0;
        if (cache.TryGetValue(c, out int cached))
            return cached;

        visiting ??= new HashSet<Character>();
        if (!visiting.Add(c))
            return 0; // family cycle guard

        int best = 0;
        if (c.Children != null)
        {
            foreach (Character child in c.Children)
            {
                if (child == null) continue;
                best = Mathf.Max(best, DescendantGenerations(child, cache, visiting) + 1);
            }
        }

        visiting.Remove(c);
        cache[c] = best;
        return best;
    }

    // Spreads culture and religion from rulers (who already carry their province's
    // culture/religion) to their courtiers, family and any linked premade
    // characters, so the whole world does not default to the first culture entry.
    // Anything still unset (or stuck on the default first entry with no ruler link)
    // gets a random culture/religion for variety.
    public void PropagateCultureAndReligion()
    {
        if (allCharacters == null || allCharacters.Count == 0)
            return;

        RebuildCourtIndex();

        List<Character> rulers = allCharacters.FindAll(c => c != null && !c.IsTemporary && c.Titles.Count > 0);
        System.Collections.Generic.HashSet<Character> rulerSet = new System.Collections.Generic.HashSet<Character>(rulers);

        // Rulers are anchors: make sure they have a culture/religion.
        foreach (Character r in rulers)
        {
            if (r.Culture == null && Culture.AllCultures != null && Culture.AllCultures.Count > 0)
                r.Culture = Culture.AllCultures[Random.Range(0, Culture.AllCultures.Count)];
            if (r.Religion == null && Religion.AllReligions != null && Religion.AllReligions.Count > 0)
                r.Religion = Religion.AllReligions[Random.Range(0, Religion.AllReligions.Count)];
            if (r.Society == null && Society.AllSocieties != null && Society.AllSocieties.Count > 0)
                r.Society = Society.AllSocieties[Random.Range(0, Society.AllSocieties.Count)];
        }

        // Iteratively propagate from rulers along every relationship edge.
        bool changed = true;
        int guard = 0;
        while (changed && guard++ < 20)
        {
            changed = false;
            foreach (Character c in allCharacters)
            {
                if (c == null || c.IsTemporary || rulerSet.Contains(c))
                    continue;

                Culture srcCulture = null;
                Religion srcReligion = null;
                Society srcSociety = null;

                if (CourtOwner.TryGetValue(c, out Character courtOwner) && rulerSet.Contains(courtOwner))
                {
                    srcCulture = courtOwner.Culture;
                    srcReligion = courtOwner.Religion;
                    srcSociety = courtOwner.Society;
                }

                if (srcCulture == null && c.Spouse != null)
                {
                    srcCulture = c.Spouse.Culture;
                    srcReligion = c.Spouse.Religion;
                    srcSociety = c.Spouse.Society;
                }
                if (srcCulture == null && c.Father != null)
                {
                    srcCulture = c.Father.Culture;
                    srcReligion = c.Father.Religion;
                    srcSociety = c.Father.Society;
                }
                if (srcCulture == null && c.Mother != null)
                {
                    srcCulture = c.Mother.Culture;
                    srcReligion = c.Mother.Religion;
                    srcSociety = c.Mother.Society;
                }
                if (srcCulture == null && c.Children != null)
                {
                    foreach (Character ch in c.Children)
                    {
                        if (ch != null && ch.Culture != null)
                        {
                            srcCulture = ch.Culture;
                            srcReligion = ch.Religion;
                            srcSociety = ch.Society;
                            break;
                        }
                    }
                }

                if (srcCulture != null && c.Culture != srcCulture)
                {
                    c.Culture = srcCulture;
                    if (srcReligion != null)
                        c.Religion = srcReligion;
                    if (srcSociety != null)
                        c.Society = srcSociety;
                    changed = true;
                }
            }
        }

        // Final fallback: anything still unset (or stuck on the default first entry
        // with no ruler link) gets a random culture/religion for variety.
        Culture defaultCulture = (Culture.AllCultures != null && Culture.AllCultures.Count > 0) ? Culture.AllCultures[0] : null;
        Religion defaultReligion = (Religion.AllReligions != null && Religion.AllReligions.Count > 0) ? Religion.AllReligions[0] : null;
        Society defaultSociety = (Society.AllSocieties != null && Society.AllSocieties.Count > 0) ? Society.AllSocieties[0] : null;

        foreach (Character c in allCharacters)
        {
            if (c == null || c.IsTemporary)
                continue;
            if (rulerSet.Contains(c))
                continue;

            bool unset = c.Culture == null || c.Culture == defaultCulture;
            if (unset && Culture.AllCultures != null && Culture.AllCultures.Count > 0)
                c.Culture = Culture.AllCultures[Random.Range(0, Culture.AllCultures.Count)];

            bool unsetRel = c.Religion == null || c.Religion == defaultReligion;
            if (unsetRel && Religion.AllReligions != null && Religion.AllReligions.Count > 0)
                c.Religion = Religion.AllReligions[Random.Range(0, Religion.AllReligions.Count)];

            bool unsetSoc = c.Society == null || c.Society == defaultSociety;
            if (unsetSoc && Society.AllSocieties != null && Society.AllSocieties.Count > 0)
                c.Society = Society.AllSocieties[Random.Range(0, Society.AllSocieties.Count)];
        }
    }

    private static int GetGenerationDepth(Character c, Dictionary<Character, int> cache, HashSet<Character> visiting)
    {
        if (c == null)
            return 0;
        if (cache.TryGetValue(c, out int cached))
            return cached;
        if (!visiting.Add(c))
            return 0; // family cycle guard

        int depth = 0;
        if (c.Father != null)
            depth = Mathf.Max(depth, GetGenerationDepth(c.Father, cache, visiting) + 1);
        if (c.Mother != null)
            depth = Mathf.Max(depth, GetGenerationDepth(c.Mother, cache, visiting) + 1);

        visiting.Remove(c);
        cache[c] = depth;
        return depth;
    }

    // Player death has no succession yet: hand control to any living character.
    public void SwitchToRandomAlivePlayer()
    {
        List<Character> alive = allCharacters.FindAll(c => c != null && c.IsAlive && !c.IsTemporary);
        if (alive.Count == 0)
            return;

        Player = alive[Random.Range(0, alive.Count)];

        if (MessageManager.instance != null)
            MessageManager.instance.Send("You now play as {0}.", Player);

        if (GameUIManager.instance != null)
        {
            if (GameUIManager.instance.PlayerPortrait != null)
            {
                CharacterPortraitUI portraitUI = GameUIManager.instance.PlayerPortrait.GetComponent<CharacterPortraitUI>();
                if (portraitUI != null)
                    portraitUI.Setup(Player);
                else
                    GameUIManager.instance.PlayerPortrait.sprite = Player.Portrait;
            }
            GameUIManager.instance.UpdatePlayerStats();
        }

        if (CameraController.instance != null)
            CameraController.instance.FocusCameraOnFast(Player.WorldPosition);
    }

    public void ResolvePremadeRelations()
    {
        foreach (Character c in allCharacters)
        {
            if (c.SourceSO == null)
                continue;

            foreach (Relationship rel in c.SourceSO.Relations)
            {
                if (rel.targetSO == null)
                    continue;

                Character target = allCharacters.Find(x => x.SourceSO == rel.targetSO);
                if (target == null || target == c)
                    continue;

                c.AddTimedModifier(target, rel.Type.ToString(), RelationshipStrength(rel), -1);

                // Wire actual family references so the family graph is real, not just opinion
                switch (rel.Type)
                {
                    case RelationshipType.Spouse:
                        c.SetSpouseMutual(target);
                        break;
                    case RelationshipType.Father:
                        c.SetFather(target);
                        break;
                    case RelationshipType.Mother:
                        c.SetMother(target);
                        break;
                    case RelationshipType.Son:
                    case RelationshipType.Daughter:
                        c.AddChild(target);
                        break;
                }
            }
        }
    }

    private static int RelationshipStrength(Relationship rel)
    {
        if (rel.Strength != 0)
            return rel.Strength;

        switch (rel.Type)
        {
            case RelationshipType.Friend:
                return 30;
            case RelationshipType.Rival:
                return -60;
            default:
                // Family relations (Spouse, Father, Mother, Son, Daughter) get
                // their opinion from CollectOpinionModifiers directly, so no
                // stored modifier is needed here (0 value = skipped).
                return 0;
        }
    }



}