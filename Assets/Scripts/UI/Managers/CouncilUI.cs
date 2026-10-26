using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class CouncilUI : MonoBehaviour
{
    public static CouncilUI instance;
    public List<CouncilEntry> entries = new();

    public GameObject AppointSearchPrefab, EntryPrefab, EmptySlotPrefab;
    [SerializeField] private TMP_Text councilCounter;
    [SerializeField] private Transform entriesList;

    private void Awake()
    {
        if (instance != null && instance != this) 
        { 
            Destroy(gameObject);
            return;
        }
        instance = this;
        HideWindow();
    }

    public void HideWindow()
    {
        gameObject.SetActive(false);
    }

    public void SetupEntries()
    {
        // Destroy all old entries
        foreach (Transform child in entriesList)
        {
            Destroy(child.gameObject);
        }

        if (MapManager.instance == null || MapManager.instance.characters == null || MapManager.instance.characters.Player == null)
        {
            Debug.LogError("Cannot setup entries: MapManager or Player is null");
            return;
        }

        foreach (var position in MapManager.instance.characters.Player.Council)
        {
            if (position.Role == CouncilRole.None)
            {
                EmptyCouncilSlot emptySlot = Instantiate(EmptySlotPrefab, entriesList).GetComponent<EmptyCouncilSlot>();
                emptySlot.Initialize(position);
                continue;
            }

            CouncilEntry entry = Instantiate(EntryPrefab, entriesList).GetComponent<CouncilEntry>();
            entry.Initialize(position);
            entry.AppointButton.onClick.RemoveAllListeners();
            entry.AppointButton.onClick.AddListener(() => AppointButton(entry));
        }

        UpdateCounter();

    }

    void UpdateCounter()
    {
        if (councilCounter == null || MapManager.instance == null || MapManager.instance.characters == null || MapManager.instance.characters.Player == null)
            return;

        int filled = 0;
        foreach (var position in MapManager.instance.characters.Player.Council)
        {
            if (position.Role != CouncilRole.None)
                filled++;
        }

        councilCounter.text = $"{filled}/{MapManager.instance.characters.Player.Council.Count}";
    }

    public void AppointButton(CouncilEntry entry)
    {
        // Use cached transform from GameUIManager or find it
        Transform screensTransform = GameUIManager.instance != null && GameUIManager.instance.screensTransform != null 
            ? GameUIManager.instance.screensTransform 
            : GameObject.Find("Screens")?.transform;
            
        if (screensTransform != null)
        {
            CouncilorFinder finder = Instantiate(AppointSearchPrefab, screensTransform).GetComponent<CouncilorFinder>();
            finder.Initialize(entry.position);
        }
        else
        {
            Debug.LogError("Cannot create CouncilorFinder: 'Screens' GameObject not found!");
        }
    }   
    

    public void CloseButton()
    {
        HideWindow();
    }
}
