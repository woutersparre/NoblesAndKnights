using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LocationUI : Window<Location>
{
    
    public TMP_Text LocationName, tax, fort, defenders, available;
    public Image Image;
    public GameObject ImprovementsScreen;
    public GameObject ImprovementPrefab;  // Prefab for the button that will represent an improvement
    [SerializeField]
    private Button levyButton;
    
    [Header("Actions list Integration")]
    public ActionUI actionsUI;

    private void OnEnable()
    {
        TravelManager.OnTravelStateChanged += RefreshFromTravel;
    }

    private void OnDisable()
    {
        TravelManager.OnTravelStateChanged -= RefreshFromTravel;
    }

    private void RefreshFromTravel()
    {
        if (Target != null)
            UpdateUI();
    }

    public void UpdateUI()
    {
        Image.sprite = Target.Image;
        LocationName.text = Target.Name;
        
        if(Target.IsHolding)
        {
            tax.text = Target.Ruler.CalculateIncome(Target.GetTax()).ToString();
            fort.text = Target.FortificationStrength.ToString();
            available.text = $"{Target.AvailableLevy} / {Target.MilitaryStrength}";
            defenders.text = Target.DefensiveStrength.ToString();
            tax.transform.parent.GetComponent<InfoPopup>().Header = "Tax: " + tax.text;
            fort.transform.parent.GetComponent<InfoPopup>().Header = "FortLevel: " + fort.text;
            defenders.transform.parent.GetComponent<InfoPopup>().Header = "Defenders: " + defenders.text;
            available.transform.parent.GetComponent<InfoPopup>().Header = "Available Militairy: " + available.text;
        }
        else
        {
            levyButton.gameObject.SetActive(false);
            available.transform.parent.parent.gameObject.SetActive(false);
        }
        // Update the improvements list
        UpdateImprovements();
        
        // Update the actions list
        actionsUI.UpdateActions(Target);
    }
    
    public void Initialize(Location _target)
    {
        // Drop any previous subscription first: this window can be retargeted, and
        // a stale listener would keep refreshing it from a location it no longer
        // shows.
        if (Target != null)
            Target.OnUpgradeFinish.RemoveListener(UpdateUI);

        Target = _target;
        Target.OnUpgradeFinish.AddListener(UpdateUI);
        if (Target.Ruler != MapManager.instance.characters.Player) 
            levyButton.enabled = false; 
        else 
            levyButton.enabled = true;
        UpdateUI();
    }

    protected override void OnDestroy()
    {
        // The location outlives its window, and construction now invokes
        // OnUpgradeFinish every day a build runs: without this the destroyed window
        // kept receiving refreshes and threw on its dead UI references.
        if (Target != null)
            Target.OnUpgradeFinish.RemoveListener(UpdateUI);

        base.OnDestroy();
    }

    public void Empty()
    {
        LocationName.text = "Empty location";

        tax.transform.parent.gameObject.SetActive(false);
        fort.transform.parent.gameObject.SetActive(false);
        defenders.transform.parent.gameObject.SetActive(false);
        available.transform.parent.gameObject.SetActive(false);
        
    }

public void RaiseLevy()
    {
        Target.RaiseLocalLevy();
        UpdateUI();
    }

    // Update the Improvements UI
    private void UpdateImprovements()
    {
        // Clear previous improvement buttons (in case of update)
        foreach (Transform child in ImprovementsScreen.transform)
        {
            Destroy(child.gameObject);
        }

        // Populate with new improvements (this assumes Target has an 'Improvements' list)
        foreach (Improvement improvement in Target.Improvements)
        {
            GameObject improvementButton = Instantiate(ImprovementPrefab, ImprovementsScreen.transform);
            ImprovementEntry entry = improvementButton.GetComponent<ImprovementEntry>();
            entry.UpdateEntry(improvement);
            if (Target.Ruler == MapManager.instance.characters.Player)
            {
                entry.Button.gameObject.SetActive(true);
                entry.Button.onClick.AddListener(() => ImprovementUpgradeClicked(improvement));
            }
            else
                entry.Button.gameObject.SetActive(false);
        }
    }

    // Called when an improvement is clicked
    private void ImprovementUpgradeClicked(Improvement improvement)
    {
        Target.UpgradeImprovement(improvement, MapManager.instance.characters.Player);
    }
}
