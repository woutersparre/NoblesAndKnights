using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class CharacterFinderUI : Window
{ 
    // The finder is toggled with SetActive and reused, never destroyed,
    // so Escape must not close it.
    protected override bool CloseableByEscape => false;

    [SerializeField] private GameObject resultPrefab; // Assign in Inspector
    [SerializeField] private Transform contentPanel;  // Parent container for results
    [SerializeField] private TMP_InputField searchInput; // Reference to input field
    [SerializeField] private ScrollRect scrollRect; // Reference to scroll rect

    private List<GameObject> spawnedResults = new();
    List<Character> toSearch = new();
    protected override void Awake()
    {
        base.Awake();
        gameObject.SetActive(false);
        if (searchInput != null)
            searchInput.onValueChanged.AddListener(FilterResultsTextInput);
        RefreshSource();
    }

    // Re-read the character list when the finder is opened; generation (and the
    // addition of courtiers/family) may have happened after Awake in the menu
    // flow, so the cached list is refreshed here.
    private void OnEnable()
    {
        RefreshSource();
    }

    private void RefreshSource()
    {
        if (MapManager.instance == null || MapManager.instance.characters == null)
            return;

        toSearch = MapManager.instance.characters.allCharacters ?? new List<Character>();
        if (contentPanel != null && resultPrefab != null)
            UpdateResultsList(toSearch);
    }

    public void UpdateResultsList(List<Character> results)
    {
        // Clear old results
        ClearResultsList();

        foreach (Character c in results)
        {
            var newResult = Instantiate(resultPrefab, contentPanel); // Parent it correctly
            CharacterEntry entryUI = newResult.GetComponent<CharacterEntry>();
            entryUI.Setup(c); // Set character data properly
            spawnedResults.Add(newResult);
        }
        // Reset Scroll Position
        Canvas.ForceUpdateCanvases(); // Ensure layout is calculated before resetting
        scrollRect.verticalNormalizedPosition = 1f; // Scroll to top

    }

    private void ClearResultsList()
    {
        foreach (var result in spawnedResults)
        {
            Destroy(result);
        }
        spawnedResults.Clear();
    }

    private void FilterResultsTextInput(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            UpdateResultsList(toSearch); // Show all characters if search is empty
            return;
        }

        // Filter list by name (case insensitive)
        var filtered = toSearch
            .Where(c => c.Name.ToLower().Contains(searchTerm.ToLower()))
            .ToList();

        UpdateResultsList(filtered);
    }

    public void SearchRealm()
    {
        toSearch = MapManager.instance.characters.Player.GetRealmCharacters();
        UpdateResultsList(toSearch);
    }

    public void SearchCourt()
    {

        toSearch = MapManager.instance.characters.Player.Court;
        UpdateResultsList(toSearch);
    }
    public void SearchVassals()
    {

        toSearch = MapManager.instance.characters.Player.Vassals;
        UpdateResultsList(toSearch);
    }
    public void SearchAll()
    {

        toSearch = MapManager.instance.characters.allCharacters;
        UpdateResultsList(toSearch);
    }
}
