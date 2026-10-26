using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System;

public class ProvinceUI : MonoBehaviour
{
    public List<Button> locationButtons;
    public Province province;
    public List<GameObject> Locations;
    public Image rulerPortrait;
    public TMP_Text provinceName, provincePopulation, culture, religion;
    public static ProvinceUI instance;
    
    private void Awake()
    {
        if (instance != null && instance != this) 
        { 
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        gameObject.SetActive(false);

        UnifiedSelectionManager.OnProvinceSelected += ShowProvince;
    }

    public void ShowProvince(Region _province)
    {
        if(_province == null)
        {
            gameObject.SetActive(false);
            return;
        }
        
        
        if(_province is Province p)
        {
            // Validate province has required data
            if (p.Capital == null)
            {
                Debug.LogWarning($"Province {p.Name} has no capital");
            }
            else
            {
                provinceName.text = p.Capital?.Name ?? p.Name ?? "Unknown Capital";

                Character ruler = p.Capital?.Ruler;
                CharacterPortraitUI rulerUI = rulerPortrait != null ? rulerPortrait.GetComponent<CharacterPortraitUI>() : null;
                if (rulerUI != null)
                {
                    if (ruler != null) rulerUI.Setup(ruler, "Ruler");
                    else rulerUI.Clear();
                }
                else if (rulerPortrait != null)
                {
                    rulerPortrait.sprite = ruler != null ? ruler.Portrait : GetDefaultPortrait();
                }
            }
            
            province = p;
            provincePopulation.text = p.GetPopulation().ToString("N0");
            culture.text = p.Culture?.Name ?? "Unknown Culture";
            religion.text = p.Religion?.Name ?? "Unknown Religion";
            
            UpdateHoldings(p);
            gameObject.SetActive(true);
        }
    }

    private Sprite GetDefaultPortrait()
    {
        // Return a default portrait or null if not available
        return Resources.Load<Sprite>("Portraits/Male/DefaultMale") ?? 
               Resources.Load<Sprite>("Portraits/Female/DefaultFemale");
    }
    
    public void UpdateHoldings(Province province)
    {
        // Clear existing listeners to avoid duplication
        foreach (Button b in locationButtons)
        {
            b.onClick.RemoveAllListeners();
        }

        // Assign listeners dynamically for each location
        for (int i = 0; i < province.Locations.Count; i++)
        {
            int index = i;
            if (i < locationButtons.Count)
            {
                locationButtons[i].onClick.AddListener(() => GameUIManager.NewLocationUI(province.Locations[index]));
            }
        }

        for (int i = 0; i < Locations.Count; i++)
        {
            if (i >= province.Locations.Count)
            {
                Locations[i].SetActive(false);
                continue;
            }

            // Get to location and corresponding UI element
            Location location = province.Locations[i];
            GameObject locationUIElement = Locations[i];

            locationUIElement.SetActive(true);
            // Update to InfoPopup text
            locationUIElement.GetComponent<InfoPopup>().Header = location.Name;

            locationUIElement.transform.Find("Image").GetComponent<Image>().sprite = location.Image;
        }
    }

    public void ShowDuJureDuchy()
    {
        MapManager.instance.MapModes.ShowDuJureDuchies();
    }
    
    public void NewProvinceLordUI()
    {
        GameUIManager.NewCharacterUI(province.Capital.Ruler);
    }
}