using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ActionEntry : MonoBehaviour
{
    public TMP_Text actionNameText;
    public Button Button;
    public InfoPopup popup;

    public void UpdateEntry(string actionName, string description, string cost, string reward, string risk)
    {
        actionNameText.text = actionName;
        popup.Header = actionName;
        popup.Body = description + "\n\n" + cost + "\n\n" + reward + "\n\n" + risk;

    }
}