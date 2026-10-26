using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class InfoPopup : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string Header, Body;

    public float showDelay = 0.025f;

    private LocationVisual locationVisual;
    private Coroutine showRoutine;
    private bool hovered;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        RequestShow(Header, Body, readLive: true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        CancelShow();
        Hide();
    }

    private void Awake()
    {
        locationVisual = GetComponent<LocationVisual>();
        if (locationVisual != null)
        {
            UnifiedSelectionManager.OnLocationHovered += HandleLocationHovered;
            UnifiedSelectionManager.OnLocationExited += HandleLocationExited;
        }
    }

    private void HandleLocationHovered(Location location)
    {
        if (locationVisual.location != location) return;

        hovered = true;
        RequestShow(location.Name, BuildBody(location));
    }

    private void HandleLocationExited(Location location)
    {
        if (locationVisual.location != location) return;

        hovered = false;
        CancelShow();
        Hide();
    }

    private static string BuildBody(Location location)
    {
        return $"{location.Type}\nPopulation: {location.Population:F0}\nMilitary: {location.MilitaryStrength:F0} | Defense: {location.DefensiveStrength:F0}";
    }

    private void RequestShow(string header, string body, bool readLive = false)
    {
        CancelShow();

        if (showDelay <= 0f)
        {
            Show(readLive ? Header : header, readLive ? Body : body);
            return;
        }

        showRoutine = StartCoroutine(ShowAfterDelay(header, body, readLive));
    }

    private IEnumerator ShowAfterDelay(string header, string body, bool readLive)
    {
        // Realtime so tooltips still work while the game is paused.
        yield return new WaitForSecondsRealtime(showDelay);
        showRoutine = null;

        // The pointer may have left, or this object may have been disabled or
        // destroyed, while we were waiting.
        if (!hovered || !isActiveAndEnabled || InfoUI.instance == null) yield break;

        Show(readLive ? Header : header, readLive ? Body : body);
    }

    private void CancelShow()
    {
        if (showRoutine == null) return;

        StopCoroutine(showRoutine);
        showRoutine = null;
    }

    private void Show(string header, string body)
    {
        if (InfoUI.instance == null) return;

        InfoUI.instance.HeaderText.text = header;
        InfoUI.instance.BodyText.text = body;
        InfoUI.instance.gameObject.SetActive(true);
    }

    private void Hide()
    {
        if (InfoUI.instance == null) return;

        InfoUI.instance.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        CancelShow();
        hovered = false;
    }

    private void OnDestroy()
    {
        if (locationVisual != null)
        {
            UnifiedSelectionManager.OnLocationHovered -= HandleLocationHovered;
            UnifiedSelectionManager.OnLocationExited -= HandleLocationExited;
        }
    }
}
