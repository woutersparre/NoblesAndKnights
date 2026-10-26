using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class TitleIcon : MonoBehaviour
{
    public Sprite sprite;
    public Title title;

    public TitleIcon(Sprite _sprite,Title _title)
    {
        sprite = _sprite;
        title = _title;
    }

    public void FocusCameraOnTitle()
    {
        CameraController.instance.FocusCameraOn(title.Location);
    }
    public void NewTitleUI()
    {
        GameUIManager.NewTitleUI(title);
    }
}
