using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;


public class GameUIManager : MonoBehaviour
{
    public TMP_Text dayCounter, money, reknown, piety;
    public static GameUIManager instance;
    public Image PlayerPortrait;
    public static GameObject CharacterUIPrefab, LocationUIPrefab, ProvinceUIPrefab, TitleUIPrefab, InfoBoxPrefab, CharacterFinderPrefab,CouncilPrefab, SocietiesPrefab, FamilyTreeUIPrefab, IntriguePrefab, LawsPrefab, MilitaryPrefab;
    GameObject FinderInstance, CouncilInstance, SocietiesInstance, IntrigueInstance, LawsInstance, MilitaryInstance;

    // Player whose OnStatsChanged we are subscribed to (income tooltips depend on stats).
    private Character _subscribedPlayer;

    // Populated from the scene on first refresh; the popups live on the parent of
    // each counter's text (Money / Reknown / Piety).
    InfoPopup moneyPopup, reknownPopup, pietyPopup;
    
    public Transform screensTransform;
    public Transform layer1Transform;

    public void Awake()
    {
        if (instance != null && instance != this) 
        { 
            Destroy(gameObject);
            return;
        }
        instance = this;

        CharacterUIPrefab = Resources.Load<GameObject>("Prefabs/UI/Screens/CharacterScreen");
        LocationUIPrefab = Resources.Load<GameObject>("Prefabs/UI/Screens/LocationScreen");
        ProvinceUIPrefab = Resources.Load<GameObject>("Prefabs/UI/Main/Province");
        InfoBoxPrefab = Resources.Load<GameObject>("Prefabs/UI/Main/InfoBox");
        TitleUIPrefab = Resources.Load<GameObject>("Prefabs/UI/Screens/TitleScreen");
        CharacterFinderPrefab = Resources.Load<GameObject>("Prefabs/UI/Screens/CharacterFinder");
        CouncilPrefab = Resources.Load<GameObject>("Prefabs/UI/Topleftmenus/Council");
        SocietiesPrefab = Resources.Load<GameObject>("Prefabs/UI/Topleftmenus/Societies");
        FamilyTreeUIPrefab = Resources.Load<GameObject>("Prefabs/UI/Screens/FamilyTreeScreen");
        IntriguePrefab = Resources.Load<GameObject>("Prefabs/UI/Topleftmenus/Intrigue");
        LawsPrefab = Resources.Load<GameObject>("Prefabs/UI/Topleftmenus/Laws");
        MilitaryPrefab = Resources.Load<GameObject>("Prefabs/UI/Topleftmenus/Military");
    }

    public void Start()
    {
        // Subscribe through the manager so a startup order where TimeManager is
        // created later still ends up refreshing the counters.
        TimeManager.SubscribeDayPassed(UpdatePlayerStats);
        TimeManager.SubscribePayday(UpdatePlayerStats);
        
        Instantiate(ProvinceUIPrefab, layer1Transform);
        Instantiate(InfoBoxPrefab, transform);

        Transform councilParent = screensTransform != null ? screensTransform : GameObject.Find("Screens")?.transform;
        CouncilInstance = Instantiate(CouncilPrefab, transform);
        if (councilParent != null && councilParent != transform)
            CouncilInstance.transform.SetParent(councilParent, true);

        // The prefab is loaded active so its Awake runs; the instance starts hidden.
        SocietiesInstance = Instantiate(SocietiesPrefab, transform);
        if (councilParent != null && councilParent != transform)
            SocietiesInstance.transform.SetParent(councilParent, true);
        SocietiesInstance.SetActive(false);

        IntrigueInstance = Instantiate(IntriguePrefab, transform);
        if (councilParent != null && councilParent != transform)
            IntrigueInstance.transform.SetParent(councilParent, true);
        IntrigueInstance.SetActive(false);

        LawsInstance = Instantiate(LawsPrefab, transform);
        if (councilParent != null && councilParent != transform)
            LawsInstance.transform.SetParent(councilParent, true);
        LawsInstance.SetActive(false);

        MilitaryInstance = Instantiate(MilitaryPrefab, transform);
        if (councilParent != null && councilParent != transform)
            MilitaryInstance.transform.SetParent(councilParent, true);
        MilitaryInstance.SetActive(false);

        FinderInstance = Instantiate(CharacterFinderPrefab, screensTransform);

        // Wire the placeholder Societies toolbar button (its onClick is empty in the scene).
        GameObject societiesButton = GameObject.Find("Societies");
        if (societiesButton != null)
        {
            Button btn = societiesButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OpenSocietiesScreen);
        }

        // Wire the Intrigue toolbar button if present in the scene.
        GameObject intrigueButton = GameObject.Find("Intrigue");
        if (intrigueButton != null)
        {
            Button btn = intrigueButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OpenIntrigueScreen);
        }
        else if (societiesButton != null)
        {
            // No authored Intrigue button exists yet: clone the Societies button so
            // the screen is reachable, mirroring the Societies/Council entry point.
            GameObject cloned = Instantiate(societiesButton, societiesButton.transform.parent);
            cloned.name = "Intrigue";
            RectTransform rt = cloned.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Place it just beside the Societies button.
                rt.anchoredPosition += new Vector2(60f, 0f);
            }
            // Replace any TMP label text.
            foreach (var tmp in cloned.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp.text == "Societies" || tmp.text.ToLower().Contains("societ"))
                    tmp.text = "Intrigue";
            }
            Button btn = cloned.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OpenIntrigueScreen);
            }
        }

        // Wire the Laws toolbar button (scene name is "Law").
        GameObject lawButton = GameObject.Find("Law");
        if (lawButton == null) lawButton = GameObject.Find("Laws");
        if (lawButton != null)
        {
            Button btn = lawButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OpenLawsScreen);
        }
        else if (societiesButton != null)
        {
            GameObject cloned = Instantiate(societiesButton, societiesButton.transform.parent);
            cloned.name = "Law";
            RectTransform rt = cloned.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition += new Vector2(120f, 0f);
            }
            foreach (var tmp in cloned.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp.text == "Societies" || tmp.text.ToLower().Contains("societ"))
                    tmp.text = "Laws";
            }
            Button btn = cloned.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OpenLawsScreen);
            }
        }

        // Wire the Military toolbar button (scene name is "Militairy").
        GameObject militaryButton = GameObject.Find("Militairy");
        if (militaryButton == null) militaryButton = GameObject.Find("Military");
        if (militaryButton != null)
        {
            Button btn = militaryButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OpenMilitaryScreen);
        }
        else if (societiesButton != null)
        {
            GameObject cloned = Instantiate(societiesButton, societiesButton.transform.parent);
            cloned.name = "Militairy";
            RectTransform rt = cloned.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition += new Vector2(180f, 0f);
            }
            foreach (var tmp in cloned.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp.text == "Societies" || tmp.text.ToLower().Contains("societ"))
                    tmp.text = "Military";
            }
            Button btn = cloned.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OpenMilitaryScreen);
            }
        }

        // Populate the resource tooltips as soon as a player exists, so they are not
        // blank until the first day ticks over.
        RefreshIncomeTooltips();
    }

    void Update()
    {
        // Keyboard fallback: Esc closes the topmost window, so a screen dragged
        // off-screen (or just covered) can always be dismissed.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Window.CloseTopmostWindow();
    }
    public void FocusCameraOnMainTitle()
    {
        if (CameraController.instance != null && MapManager.instance != null && MapManager.instance.characters != null)
        {
            CameraController.instance.FocusCameraOnMainTitle(MapManager.instance.characters.Player);
        }
        else
        {
            Debug.LogError("Cannot focus camera: Missing CameraController.instance or MapManager.instance");
        }
    }
    public static void NewLocationUI(Location location)
    {
        if (Window.HasWindow<LocationUI, Location>(location))
            return;

        // Use cached transform or find it if not cached
        Transform screensTransform = instance != null ? instance.screensTransform : GameObject.Find("Screens")?.transform;
        if (screensTransform != null)
        {
            Instantiate(LocationUIPrefab, screensTransform).GetComponent<LocationUI>().Initialize(location);
        }
        else Debug.LogError("Cannot create LocationUI: 'Screens' GameObject not found!");
    }
    public void OpenCouncilScreen()
    {
        CouncilInstance.SetActive(true);
        CouncilUI.instance.SetupEntries();
    }

    public void OpenSocietiesScreen()
    {
        if (SocietiesInstance != null)
        {
            SocietiesInstance.SetActive(true);
            SocietiesUI ui = SocietiesInstance.GetComponent<SocietiesUI>();
            if (ui == null) ui = SocietiesInstance.GetComponentInChildren<SocietiesUI>(true);
            if (ui != null) ui.Setup();
        }
    }

    public void OpenIntrigueScreen()
    {
        if (IntrigueInstance != null)
        {
            IntrigueInstance.SetActive(true);
            IntrigueUI ui = IntrigueInstance.GetComponent<IntrigueUI>();
            if (ui == null) ui = IntrigueInstance.GetComponentInChildren<IntrigueUI>(true);
            if (ui != null) ui.Setup();
        }
    }

    public void OpenLawsScreen()
    {
        if (LawsInstance != null)
        {
            LawsInstance.SetActive(true);
            LawsUI ui = LawsInstance.GetComponent<LawsUI>();
            if (ui == null) ui = LawsInstance.GetComponentInChildren<LawsUI>(true);
            if (ui != null) ui.Setup();
        }
    }

    public void OpenMilitaryScreen()
    {
        if (MilitaryInstance != null)
        {
            MilitaryInstance.SetActive(true);
            MilitaryUI ui = MilitaryInstance.GetComponent<MilitaryUI>();
            if (ui == null) ui = MilitaryInstance.GetComponentInChildren<MilitaryUI>(true);
            if (ui != null) ui.Setup();
        }
    }
    public void UpdatePlayerStats()
    {
        if (MapManager.instance == null || MapManager.instance.characters == null || MapManager.instance.characters.Player == null)
        {
            Debug.LogWarning("Cannot update player stats: MapManager or Player is null");
            return;
        }
        
        if (MapManager.instance.characters.Player.Name == "")
        {
            return;
        }

        if (TimeManager.instance == null)
        {
            Debug.LogWarning("Cannot update player stats: TimeManager.instance is null");
            return;
        }

        Character player = MapManager.instance.characters.Player;

        // Keep income tooltips in sync when equipment changes the player's stats.
        if (_subscribedPlayer != player)
        {
            if (_subscribedPlayer != null)
                _subscribedPlayer.OnStatsChanged -= HandlePlayerStatsChanged;
            _subscribedPlayer = player;
            _subscribedPlayer.OnStatsChanged += HandlePlayerStatsChanged;
        }

        money.text = player.Wealth.ToString("F0");
        reknown.text = player.Reknown.ToString("F0");
        piety.text = player.Piety.ToString("F0");
        dayCounter.text = TimeManager.instance.GetDateString(); // e.g. "March 12, 812"

        RefreshIncomeTooltips();
    }

    private void HandlePlayerStatsChanged()
    {
        // Only the income tooltips depend on stats; counters are day-driven.
        if (_subscribedPlayer == null)
            return;
        RefreshIncomeTooltips();
    }

    private void OnDestroy()
    {
        if (_subscribedPlayer != null)
        {
            _subscribedPlayer.OnStatsChanged -= HandlePlayerStatsChanged;
            _subscribedPlayer = null;
        }
    }

    private void RefreshIncomeTooltips()
    {
        if (moneyPopup == null && money != null) moneyPopup = money.GetComponentInParent<InfoPopup>();
        if (reknownPopup == null && reknown != null) reknownPopup = reknown.GetComponentInParent<InfoPopup>();
        if (pietyPopup == null && piety != null) pietyPopup = piety.GetComponentInParent<InfoPopup>();

        Character player = MapManager.instance != null && MapManager.instance.characters != null
            ? MapManager.instance.characters.Player
            : null;
        if (player == null) return;

        float grossTax = player.PropertyIncome();
        float wealthPerDay = player.CalculateIncome(grossTax);
        float stewardshipFactor = player.CalculateIncome(1f);
        float renownPerDay = player.CalculateReknown();
        // Titles contribute a flat +1 each, so whatever is left of the total is the
        // stewardship slice.
        float renownFromStewardship = renownPerDay - player.Titles.Count;
        float pietyPerDay = player.CalculatePiety();

        if (moneyPopup != null)
        {
            moneyPopup.Header = "Wealth";
            moneyPopup.Body =
                $"Per day: +{wealthPerDay:F1}\n" +
                $"Tax from holdings: {grossTax:F1}\n" +
                $"Stewardship {player.Stats.Stewardship}: x{stewardshipFactor:F2}";
        }

        if (reknownPopup != null)
        {
            reknownPopup.Header = "Renown";
            reknownPopup.Body =
                $"Per day: +{renownPerDay:F1}\n" +
                $"Titles held: {player.Titles.Count} (+1 each)\n" +
                $"Stewardship {player.Stats.Stewardship}: +{renownFromStewardship:F1}";
        }

        if (pietyPopup != null)
        {
            pietyPopup.Header = "Piety";
            pietyPopup.Body =
                $"Per day: +{pietyPerDay:F1}\n" +
                $"Derived from Learning {player.Stats.Learning}";
        }
    }
    public static void NewCharacterUI(Character character)
    {
        if (character == null)
            return;

        // If a window for this character is already open, refresh it instead of
        // opening a duplicate (or leaving stale data).
        if (Window.HasWindow<CharacterUIManager, Character>(character) && instance != null)
        {
            foreach (var w in Window.instancesByType[typeof(CharacterUIManager)])
            {
                CharacterUIManager ui = w as CharacterUIManager;
                if (ui != null && ui.Target != null && ui.Target.Equals(character))
                {
                    ui.UpdateUI(character);
                    return;
                }
            }
        }

        if (instance == null || CharacterUIPrefab == null)
            return;

        Instantiate(CharacterUIPrefab, instance.screensTransform)
            .GetComponentInChildren<CharacterUIManager>()
            .UpdateUI(character);
    }

    // Opens the family tree window centred on a character. Only one tree is shown
    // at a time: if one is already open it is retargeted in place instead of
    // stacking a second window.
    public static void NewFamilyTreeUI(Character character)
    {
        if (character == null)
            return;

        if (FamilyTreeUIPrefab == null)
        {
            Debug.LogError("Cannot create FamilyTreeUI: prefab failed to load.");
            return;
        }

        FamilyTreeUI existing = FindOpenFamilyTree();
        if (existing != null)
        {
            existing.UpdateUI(character);
            existing.BringToFront();
            return;
        }

        Transform screensTransform = instance != null ? instance.screensTransform : GameObject.Find("Screens")?.transform;
        if (screensTransform == null)
        {
            Debug.LogError("Cannot create FamilyTreeUI: 'Screens' GameObject not found!");
            return;
        }

        Instantiate(FamilyTreeUIPrefab, screensTransform)
            .GetComponentInChildren<FamilyTreeUI>()
            .UpdateUI(character);
    }

    private static FamilyTreeUI FindOpenFamilyTree()
    {
        if (!Window.instancesByType.TryGetValue(typeof(FamilyTreeUI), out var set))
            return null;

        foreach (Window window in set)
            if (window is FamilyTreeUI tree)
                return tree;

        return null;
    }

    public static void NewTitleUI(Title title)
    {
        if (Window.HasWindow<TitleUI, Title>(title))
            return;

        Instantiate(TitleUIPrefab, instance.screensTransform)
            .GetComponentInChildren<TitleUI>()
            .UpdateUI(title);
    }

    public void PlayerCharacterUI()
    {
        if (MapManager.instance != null && MapManager.instance.characters != null && MapManager.instance.characters.Player != null)
        {
            NewCharacterUI(MapManager.instance.characters.Player);
        }
        else
        {
            Debug.LogError("Cannot open Player Character UI: MapManager or Player is null");
        }
    }
    public void PauseButton()
    {
        if (TimeManager.instance != null)
            TimeManager.instance.Pause();
    }
    public void PlayButton()
    {
        if (TimeManager.instance != null)
            TimeManager.instance.Play();
    }
    public void FastForwardButton()
    {
        if (TimeManager.instance != null)
            TimeManager.instance.FastForward();
    }
    public void OpenFindCharacterMenu()
    {
        FinderInstance.SetActive(true);
    }
}
