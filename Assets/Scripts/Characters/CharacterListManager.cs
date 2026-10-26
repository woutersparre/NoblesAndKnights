using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class CharacterListManager : MonoBehaviour
{
    List<Character> toShow = new();
    List<GameObject> spawnedResults = new();
    [SerializeField] GameObject resultPrefab; // Assign in Inspector
    [SerializeField] Transform contentPanel;  // Parent container for results
    [SerializeField] ScrollRect scrollRect;  // Parent container for results

    public Character Target;
    public void ShowCourt()
    {
        toShow = Target.Court;
        UpdateResultsList(toShow);
    }
    public void ShowVassals()
    {
        toShow = Target.Vassals;
        UpdateResultsList(toShow);
    }
    public void ShowFamily()
    {
        toShow = Target.Family;
        UpdateResultsList(toShow);
    }

    private void ClearResultsList()
    {
        foreach (var result in spawnedResults)
        {
            Destroy(result);
        }
        spawnedResults.Clear();
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

        Canvas.ForceUpdateCanvases(); // Ensure layout is calculated before resetting
        scrollRect.verticalNormalizedPosition = 1f; // Scroll to top
    }

}
