using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CouncilorFinder : Window<List<Character>>
{
    [SerializeField] private GameObject resultPrefab;
    [SerializeField] private Transform contentPanel;
    [SerializeField] private ScrollRect scrollRect;

    public CouncilPosition targetPosition;

    // The appointable pool for targetPosition. Kept so a search field can filter
    // this set instead of the unfiltered realm roster.
    private List<Character> candidates = new();
    private List<GameObject> spawnedResults = new();

    // Awake fires during Instantiate, i.e. before the caller can assign targetPosition,
    // so the list is built from Initialize once the target role is known.
    public void Initialize(CouncilPosition position)
    {
        targetPosition = position;
        ShowCharacterList();
    }

    void ShowCharacterList()
    {
        if (contentPanel != null)
            contentPanel.transform.parent.gameObject.SetActive(true);

        candidates = CouncilService.GetEligibleCandidates(targetPosition?.Owner, targetPosition);
        UpdateResultsList(candidates);
    }

    public void UpdateResultsList(List<Character> results)
    {
        ClearResultsList();

        foreach (Character c in results)
        {
            var newResult = Instantiate(resultPrefab, contentPanel);
            CharacterEntry entryUI = newResult.GetComponent<CharacterEntry>();
            entryUI.Setup(c);
            entryUI.Button.onClick.RemoveAllListeners();
            entryUI.Button.onClick.AddListener(() => ChangeAppointedCouncilor(entryUI.target));
            spawnedResults.Add(newResult);
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
    }

    void ChangeAppointedCouncilor(Character c)
    {
        if (targetPosition == null)
            return;

        CouncilService.AppointResult result = CouncilService.Appoint(targetPosition.Owner, targetPosition, c);
        if (result != CouncilService.AppointResult.Success)
        {
            Debug.Log($"{c?.Name} is not eligible for the role of {targetPosition.PositionName} ({result}).");
            return;
        }

        CouncilUI.instance.SetupEntries();
        CloseWindow();
    }

    private void ClearResultsList()
    {
        foreach (var result in spawnedResults)
        {
            Destroy(result);
        }
        spawnedResults.Clear();
    }
}
