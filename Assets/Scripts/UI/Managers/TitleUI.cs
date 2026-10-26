using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class TitleUI : Window<Title>
{
    public List<GameObject> SubTitlesObjects;
    public Image holderPortrait;
    public TMP_Text TitleName;
    public GameObject TitleHolder;
    public List<Title> SubTitles = new();


    public void UpdateUI(Title title)
    {
        Target = title;

        CharacterPortraitUI holderUI = holderPortrait != null ? holderPortrait.GetComponent<CharacterPortraitUI>() : null;
        if (holderUI != null && title.Holder != null) holderUI.Setup(title.Holder, "Holder");
        else if (holderUI == null && title.Holder != null && holderPortrait != null) holderPortrait.sprite = title.Holder.Portrait;

        TitleName.text = title.Name;

        foreach (Transform child in TitleHolder.transform) //clear all Titles;
        {
            Destroy(child.gameObject);
        }
        if(title is RegionTitle t)
        {
            SubTitles.Clear();
            GameObject iconPrefab = Resources.Load<GameObject>("Prefabs/UI/Icons/Title");
            foreach (Title subTitle in t.SubTitles)
            {
                SubTitles.Add(subTitle);
                if (iconPrefab == null)
                {
                    Debug.LogWarning("[TitleUI] Missing sub-title icon prefab: Prefabs/UI/Icons/Title");
                    continue;
                }

                TitleIcon icon = Instantiate(iconPrefab, TitleHolder.transform).GetComponent<TitleIcon>();
                icon.title = subTitle;
                icon.sprite = subTitle.Image;

                InfoPopup subPopup = icon.GetComponent<InfoPopup>();
                if (subPopup != null)
                {
                    subPopup.Header = subTitle.Name;
                    subPopup.Body = subTitle.Holder != null ? "Held by " + subTitle.Holder.Name : "Unclaimed";
                }

                Image iconImage = icon.GetComponent<Image>();
                if (iconImage != null && icon.sprite != null)
                    iconImage.sprite = icon.sprite;
            }
        }

    }

    // Focus the map camera on the real-world location tied to this title.
    public void FocusCamera()
    {
        if (Target == null) return;
        if (CameraController.instance == null)
        {
            Debug.LogWarning("[TitleUI] FocusCamera: CameraController.instance is null.");
            return;
        }
        Debug.Log($"[TitleUI] FocusCamera: focusing on {Target.Name}.");
        CameraController.instance.FocusCameraOn(Target.Location);
    }
}
