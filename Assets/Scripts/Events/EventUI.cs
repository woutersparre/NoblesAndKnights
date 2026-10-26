using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class EventUI : MonoBehaviour
{
    public TMP_Text Title;
    public TMP_Text MainText;
    public List<Button> Buttons;
    public GameObject PortraitPrefab;


    private void Awake()
    {
        Transform close = transform.Find("Header/Button");
        if (close != null)
        {
            Button b = close.GetComponent<Button>();
            if (b != null)
            {
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => OnOptionClicked(0));
            }
        }
    }

    public void ShowEvent(RandomEvent evt, Character character, List<EventOption> options)
    {
        if (Title != null)
            Title.text = evt.Name;

        if (MainText != null)
            MainText.text = evt.EventText;

        for (int i = 0; i < Buttons.Count; ++i)
        {
            Button button = Buttons[i];
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(false);

            if (i < options.Count)
            {
                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                    text.text = options[i].Text;
                int index = i;
                button.onClick.AddListener(() => OnOptionClicked(index));
                button.gameObject.SetActive(true);
            }
        }

        ShowActors(evt.GetActors(character));

        gameObject.SetActive(true);
    }

    private void ShowActors(List<Character> actors)
    {
        if (actors == null || actors.Count == 0 || PortraitPrefab == null)
            return;

        Transform side1 = FindDeep(transform, "ActorSide1");
        Transform side2 = FindDeep(transform, "ActorSide2");

        // Clear whatever the previous event left standing.
        ClearChildren(side1);
        ClearChildren(side2);

        for (int i = 0; i < actors.Count; ++i)
        {
            Character actor = actors[i];
            if (actor == null) continue;

            Transform side = i == 0 ? side1 : side2;
            if (side == null) continue;

            GameObject portrait = Instantiate(PortraitPrefab, side);
            CharacterPortraitUI widget = portrait.GetComponent<CharacterPortraitUI>();
            if (widget != null)
            {
                if (actor.IsTemporary)
                    widget.Button = null;

                widget.Setup(actor);
            }
        }
    }

    private Transform FindDeep(Transform root, string childName)
    {
        foreach (Transform child in root)
        {
            if (child.name == childName)
                return child;

            Transform found = FindDeep(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }

    private void ClearChildren(Transform side)
    {
        if (side == null) return;
        for (int i = side.childCount - 1; i >= 0; --i)
            Destroy(side.GetChild(i).gameObject);
    }

    private void OnOptionClicked(int optionIndex)
    {
        gameObject.SetActive(false);
        if (RandomEventManager.instance != null)
            RandomEventManager.instance.ResolvePlayerEvent(optionIndex);
    }
}