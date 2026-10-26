using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using TMPro;
using System.Linq;

public class MapManager : MonoBehaviour
{
    public List<Province> provincesDuJure;
    public List<Duchy> duchiesDuJure;
    public List<Kingdom> kingdomsDuJure;
    public List<Empire> empiresDuJure;

    public static MapManager instance;

    // Global references to map generation components
    public static RiverNetwork RiverNetwork { get; private set; }
    public static RoadNetwork RoadNetwork { get; private set; }
    public static TerrainAffinityManager TerrainAffinityManager { get; private set; }

    [Header("Events")]
    public static UnityEvent OnMapGenerated = new UnityEvent();

    public MapModes MapModes;

    [Header("Map Generation")]
    public bool GenerateIsland = true;

    [Header("Region Formation Settings")]
    public RegionSettings regionSettings = new RegionSettings() { enableDebugOutput = true };

    [Header("System Integration Settings")]
    public bool enableIntegratedCulturalReligiousSystem = true;
    public float simulationSpeed = 1f; // Time scale for cultural/religious changes

    public GameObject NameTagCanvas;
    public GameObject NamePrefab;

    public CharacterManager characters = new();
    [SerializeReference]
    public NewMapGen map = new();
    private void Awake()
    {
        instance = this;
        MapModes = new(this);

        // Set global references to map generation components
        RiverNetwork = GetComponent<RiverNetwork>();
        RoadNetwork = GetComponent<RoadNetwork>();
        TerrainAffinityManager = GetComponent<TerrainAffinityManager>();

        // Ensure travel systems exist (they self-bootstrap onto this GameObject)
        if (GetComponent<TravelManager>() == null)
            gameObject.AddComponent<TravelManager>();
        if (GetComponent<TravelPlanner>() == null)
            gameObject.AddComponent<TravelPlanner>();
    }
    void Start()
    {
        if (MapModes == null)
            MapModes = new(this);

        if (GameSession.HasPendingWorld)
        {
            // Launched from the main menu: apply the requested parameters, generate
            // with a loading screen, then let the player pick who to play as.
            WorldGenConfig config = GameSession.ConsumeWorldConfig();
            config?.Apply(this);

            // TimeManager auto-plays in its own Start(); pause it so the clock does
            // not tick (and fire DayPassed -> UpdatePlayerStats) while the player
            // character does not exist yet. FinalizePlayer resumes the clock.
            if (TimeManager.instance != null)
                TimeManager.instance.Pause();

            StartCoroutine(MenuBootRoutine());
        }
        else
        {
            // Direct-play / legacy behaviour: generate immediately and hand the
            // player a random landed ruler. Preserves the existing workflow when
            // playing the game scene straight from the editor.
            GenerateWorld();
            if (characters != null && characters.allCharacters.Count > 0)
            {
                characters.PlayerSetup();
                FinalizePlayer();
            }
        }
    }

    // Runs the full world generation pipeline and triggers visual generation.
    // Player-dependent setup is deliberately NOT part of this so it can be
    // deferred until a character has been chosen.
    private void GenerateWorld()
    {
        InitializeCoreData();

        map.Generate(GenerateIsland);

        if (RiverNetwork != null)
            RiverNetwork.GenerateRiverNetwork();

        GenerateRegions();

        if (characters == null || characters.allCharacters.Count == 0)
        {
            Debug.LogWarning("No characters found");
            return;
        }

        InitializeCharacterLocations();
        GenerateCharacterPopulation();

        MapModes.ShowRealms();
        OnMapGenerated.Invoke();

        if (RoadNetwork != null)
            RoadNetwork.GenerateRoadNetwork();
    }

    private void GenerateRegions()
    {
        RegionManager regionManager = new RegionManager(regionSettings);
        regionManager.GenerateAllRegions(map.landCells, out provincesDuJure, out duchiesDuJure, out kingdomsDuJure, out empiresDuJure);
        regionManager.CreateHigherTierTitles(duchiesDuJure, kingdomsDuJure, empiresDuJure);
        CreateRegionNameTags();
    }

    private void CreateRegionNameTags()
    {
        foreach (var province in provincesDuJure)
        {
            province.CreateBorderLoops();
            AddNameTag(province);
        }
        foreach (var duchy in duchiesDuJure) AddNameTag(duchy);
        foreach (var kingdom in kingdomsDuJure) AddNameTag(kingdom);
        foreach (var empire in empiresDuJure) AddNameTag(empire);
    }

    // Courtiers, families and starting ages. Depends only on map data.
    private void GenerateCharacterPopulation()
    {
        List<Character> rulers = new();
        foreach (Character c in characters.allCharacters)
            if (c.Titles.Count > 0)
                rulers.Add(c);

        characters.AddCourtiers(rulers);
        characters.ResolvePremadeRelations();
        characters.GenerateFamilies(rulers);
        characters.RebuildCourtIndex();
        PlaceDependents();
        characters.AssignStartingAges();
        characters.PropagateCultureAndReligion();
    }

    // Player-specific hookup: portrait, stats, clock. Safe to call once a character
    // has been chosen (or a random one assigned by PlayerSetup).
    private void FinalizePlayer()
    {
        if (characters == null || characters.Player == null)
        {
            Debug.LogWarning("FinalizePlayer called with no player character");
            return;
        }

        if (GameUIManager.instance != null && GameUIManager.instance.PlayerPortrait != null)
        {
            CharacterPortraitUI playerPortraitUI = GameUIManager.instance.PlayerPortrait.GetComponent<CharacterPortraitUI>();
            if (playerPortraitUI != null)
                playerPortraitUI.Setup(characters.Player);
            else
                GameUIManager.instance.PlayerPortrait.sprite = characters.Player.Portrait;
        }

        if (GameUIManager.instance != null)
        {
            GameUIManager.instance.gameObject.SetActive(true);
            GameUIManager.instance.UpdatePlayerStats();
        }

        if (TimeManager.instance != null)
            TimeManager.instance.Play();
    }

    // Menu flow: loading screen -> generation -> character selection.
    private IEnumerator MenuBootRoutine()
    {
        yield return null; // let other scene objects finish Awake/Start

        // Keep the main HUD hidden until a player character has been chosen.
        if (GameUIManager.instance != null)
            GameUIManager.instance.gameObject.SetActive(false);

        GenerationProgressUI.Show("Preparing world...");
        yield return null;

        GenerationProgressUI.Set(0.05f, "Loading cultures, religions and societies...");
        InitializeCoreData();
        yield return null;

        GenerationProgressUI.Set(0.15f, "Carving the land...");
        map.Generate(GenerateIsland);
        yield return null;

        GenerationProgressUI.Set(0.35f, "Carving rivers...");
        if (RiverNetwork != null)
            RiverNetwork.GenerateRiverNetwork();
        yield return null;

        GenerationProgressUI.Set(0.5f, "Forming provinces and realms...");
        GenerateRegions();
        yield return null;

        if (characters == null || characters.allCharacters.Count == 0)
        {
            Debug.LogWarning("No characters found");
            GenerationProgressUI.Close();
            yield break;
        }

        GenerationProgressUI.Set(0.65f, "Settling the land...");
        InitializeCharacterLocations();
        yield return null;

        GenerationProgressUI.Set(0.78f, "Populating the realms...");
        GenerateCharacterPopulation();
        yield return null;

        GenerationProgressUI.Set(0.9f, "Drawing borders and roads...");
        MapModes.ShowRealms();
        OnMapGenerated.Invoke();
        if (RoadNetwork != null)
            RoadNetwork.GenerateRoadNetwork();
        yield return null;

        GenerationProgressUI.Set(1f, "Done");
        yield return null;
        GenerationProgressUI.Close();

        // Let the player pick their ruler.
        Character chosen = null;
        yield return CharacterSelectUI.Run(characters.allCharacters, c => chosen = c);

        if (chosen == null)
        {
            Debug.LogWarning("No character chosen; falling back to a random ruler.");
            characters.PlayerSetup();
        }
        else
        {
            characters.SetPlayer(chosen);
        }

        FinalizePlayer();
    }

    // Post-map-gen: assign CurrentLocation to every character. Rulers get it from HoldingTitle;
    // spouses/courtiers without titles mirror their Spouse. Called once before gameplay begins.
    private void InitializeCharacterLocations()
    {
        foreach (var c in characters.allCharacters)
        {
            // Rulers with a HoldingTitle already in their list
            foreach (var t in c.Titles)
            {
                if (t is HoldingTitle ht)
                {
                    c.CurrentLocation = ht.AssociatedLocation;
                    ht.AssociatedLocation.Visit(c); // living there = it is visited
                    break;
                }
            }

            // Titleless characters (spouses, courtiers): mirror their spouse if that spouse is already set
            if (c.CurrentLocation == null && c.Spouse != null && c.Spouse.CurrentLocation != null)
            {
                c.CurrentLocation = c.Spouse.CurrentLocation;
                c.CurrentLocation.Visit(c);
            }
        }
    }

    private void PlaceDependents()
    {
        foreach (var c in characters.allCharacters)
        {
            if (c.CurrentLocation != null)
                continue;

            Location home = ResolveHomeLocation(c);
            if (home == null)
                continue;
            c.CurrentLocation = home;
            home.Visit(c);
        }
    }

    // Where a character lives, in priority order:
    //   1. their own main title's holding location
    //   2. their spouse's residence
    //   3. the residence of whoever's court they belong to (court owner's main title holding)
    //   4. their family's residence (parents first, then children)
    private Location ResolveHomeLocation(Character c, HashSet<Character> visited = null)
    {
        if (c == null) return null;
        visited ??= new HashSet<Character>();
        if (!visited.Add(c)) return null; // guard against cycles

        // 1. Own titles
        Location own = Location.ResolveCapitalLocation(c);
        if (own != null) return own;

        // 2. Spouse
        if (c.Spouse != null)
        {
            Location sp = ResolveHomeLocation(c.Spouse, visited);
            if (sp != null) return sp;
        }

        // 3. Whose court am I in? (Court = spouse + courtiers) - O(1) via index
        if (characters.CourtOwner.TryGetValue(c, out Character owner) && owner != c && owner != null)
        {
            Location courtLocation = ResolveHomeLocation(owner, visited);
            if (courtLocation != null) return courtLocation;
        }

        // 4. Family
        if (c.Father != null)
        {
            Location l = ResolveHomeLocation(c.Father, visited);
            if (l != null) return l;
        }
        if (c.Mother != null)
        {
            Location l = ResolveHomeLocation(c.Mother, visited);
            if (l != null) return l;
        }
        if (c.Children != null)
        {
            foreach (Character child in c.Children)
            {
                Location l = ResolveHomeLocation(child, visited);
                if (l != null) return l;
            }
        }

        return null;
    }

    private void InitializeCoreData()
    {
        LoadStartingCultures();
        LoadStartingReligions();
        LoadStartingSocieties();

        // Initialize TerrainAffinityManager now that we have data
        if (TerrainAffinityManager != null)
        {
            TerrainAffinityManager.InitializeWhenReady();
        }

        // Time is started later, once a player character has been chosen (or a
        // random one assigned in the direct-play path), so the world does not
        // tick while the character selection overlay is open.
    }
    public void LoadStartingReligions()
    {
        Religion sunFaith = new("Sun Worship", "Believes the sun is a divine entity.");
        Religion moonFaith = new("Moon Cult", "Reveres the moon as a guide of fate.");
        Religion natureFaith = new("Druidic Order", "Worships nature and its cycles.");
        Religion warFaith = new("Blood Pact", "Glorifies battle and strength.");
    }
    public void LoadStartingCultures()
    {
        Culture desertTribes = new("Desert Tribes", "Saharan");
        Culture northernClans = new("Northern Clans", "Norse");
        Culture forestKin = new("Forest Kin", "Elvish");
        Culture warriorNation = new("Warrior Nation", "Guttural");
    }

    public void LoadStartingSocieties()
    {
        new Society("Monastic Order", "A devout order dedicated to charity and learning.");
        new Society("Hermetic Circle", "Scholars studying alchemy, astrology, and ancient secrets.");
        new Society("Assassins", "A secret brotherhood of sacred murderers.");
        new Society("Demon Worshippers", "A cult seeking power through dark pacts.");
    }

    void AddNameTag(Region region)
    {
        region.text = CreateNameTag(region.Name, region.Center, region.Name);
    }

    // Creates a name tag under the shared NameTags canvas. Used for regions at
    // generation time, and by map modes that label something which is not a region
    // (cultures, religions) so those get a tag of their own instead of borrowing a
    // province's. Returns null when the canvas or prefab is missing.
    public TMP_Text CreateNameTag(string label, Vector3 position, string objectName)
    {
        if (NameTagCanvas == null || NamePrefab == null)
        {
            Debug.LogError("Canvas 'NameTags' not found! or NamePrefab not assigned!");
            return null;
        }

        TMP_Text tag = Instantiate(NamePrefab, NameTagCanvas.transform).GetComponent<TMP_Text>();
        tag.transform.position = position;
        tag.fontSize = 6f;
        tag.text = label;
        tag.gameObject.name = objectName + " Nametag";
        return tag;
    }



    public IEnumerable<Character> GetTopLevelRealms()
    {
        return provincesDuJure
            .Select(p => p.Capital.Ruler)
            .Where(r => r != null && r.Liege == null)
            .Distinct();
    }
    public void DisableAllText()
    {
        // SetTagVisible null-checks, so a failed AddNameTag (missing canvas or
        // prefab) no longer takes every map mode down with an NRE.
        foreach (Region r in provincesDuJure)
        {
            r.SetTagVisible(false);
        }
        foreach (Region r in duchiesDuJure)
        {
            r.SetTagVisible(false);
        }
        foreach (Region r in kingdomsDuJure)
        {
            r.SetTagVisible(false);
        }
        foreach (Region r in empiresDuJure)
        {
            r.SetTagVisible(false);
        }
    }

    public Province GetProvinceAt(Vector2 worldPos)
    {
        MapCell cell = GetCellAt(worldPos);
        if (cell == null || cell.Province == null)
        {
            //Debug.Log("No cell detected");
            return null;
        }

        return provincesDuJure.FirstOrDefault(p => p == cell.Province);
    }

    public MapCell GetCellAt(Vector2 worldPos)
    {
        foreach (var cell in map.mapCells)
        {
            if (PointInPolygon(worldPos, cell.Polygon))
                return cell;
        }
        return null;
    }

    // Ray-casting / winding algorithm
    public static bool PointInPolygon(Vector2 point, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 pi = poly[i];
            Vector2 pj = poly[j];

            if (((pi.y > point.y) != (pj.y > point.y)) &&
                (point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x))
            {
                inside = !inside;
            }
        }
        return inside;
    }
}