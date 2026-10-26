using System.Collections.Generic;
using UnityEngine;

// Owns the world-space indicators for deployed councilors.
// Created on demand (Ensure), subscribes to the daily tick, decrements each working
// deployment, auto-completes it when the timer hits zero, and cleanly despawns on cancel.
public class CouncilIndicatorManager : MonoBehaviour
{
    public static CouncilIndicatorManager instance;

    private Dictionary<CouncilPosition, CouncilIndicator> active = new();

    public static CouncilIndicatorManager Ensure()
    {
        if (instance != null) return instance;

        GameObject go = new GameObject("Council Indicators");
        if (TravelManager.instance != null)
            go.transform.SetParent(TravelManager.instance.transform, false);

        return go.AddComponent<CouncilIndicatorManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Queues the tick if this manager is created before TimeManager, so deployed
        // councilors still count their days down.
        TimeManager.SubscribeDayPassed(Tick);
    }

    public GameObject Prefab;

    void LoadPrefab()
    {
        if (Prefab != null) return;
        Prefab = Resources.Load<GameObject>("Prefabs/UI/MapIndicators/Councilor Indicator");
        if (Prefab == null)
            Debug.LogWarning("CouncilIndicatorManager: Councilor Indicator prefab not found under Resources/Prefabs/UI/MapIndicators.");
    }

    public void CreateIndicator(CouncilPosition position)
    {
        if (position == null) return;

        DestroyIndicator(position);

        if (!position.IsWorking)
        {
            Debug.LogWarning($"CouncilIndicatorManager: {position.PositionName} is not working, cannot spawn indicator.");
            return;
        }

        LoadPrefab();
        if (Prefab == null) return;

        GameObject go = Instantiate(Prefab, position.ActionLocation.Position, Quaternion.identity) as GameObject;
        if (go == null) return;

        go.transform.SetParent(transform, true);
        go.transform.localScale = Vector3.one * 3f;

        CouncilIndicator indicator = go.GetComponent<CouncilIndicator>();
        if (indicator == null)
            indicator = go.AddComponent<CouncilIndicator>();

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        indicator.Initialize(position, sr);

        active[position] = indicator;
    }

    public void DestroyIndicator(CouncilPosition position)
    {
        if (position != null && active.TryGetValue(position, out CouncilIndicator indicator))
        {
            if (indicator != null)
                Destroy(indicator.gameObject);
            active.Remove(position);
        }
    }

    // Manual cancel from the council screen. Pass refreshUi = false when the caller
    // rebuilds the council screen itself (the appointment flow does), so the entries
    // are not rebuilt twice in one frame.
    public void CancelDeployment(CouncilPosition position, bool refreshUi = true)
    {
        if (position == null || !position.IsWorking) return;

        string councilor = position.Appointed != null ? position.Appointed.Name : position.PositionName;
        string location = position.ActionLocation != null ? position.ActionLocation.Name : "location";
        Debug.Log($"[CouncilIndicatorManager] CancelDeployment: {councilor} stopped working at {location}.");

        position.EndDeployment();
        DestroyIndicator(position);

        if (refreshUi)
            RefreshCouncilUI();
    }

    void Tick()
    {
        if (MapManager.instance == null || MapManager.instance.characters == null || MapManager.instance.characters.Player == null)
            return;

        foreach (var position in MapManager.instance.characters.Player.Council)
        {
            if (!position.IsWorking) continue;

            // DurationDays <= 0 means "stays active until cancelled".
            if (position.WorkingDaysLeft <= 0) continue;

            position.WorkingDaysLeft--;
            if (position.WorkingDaysLeft <= 0)
                CompleteDeployment(position);
        }

        RefreshCouncilUI();
    }

    void CompleteDeployment(CouncilPosition position)
    {
        string councilor = position.Appointed != null ? position.Appointed.Name : position.PositionName;
        string action = position.ActiveAction != null ? position.ActiveAction.Name : "task";
        string location = position.ActionLocation != null ? position.ActionLocation.Name : "location";

        Debug.Log($"[CouncilIndicatorManager] CompleteDeployment: {councilor} finished {action} at {location}.");

        position.EndDeployment();
        DestroyIndicator(position);

        // Pass the Character itself (not just its name) so the message system
        // renders the councilor as a clickable link.
        object councilorArg = position.Appointed != null ? (object)position.Appointed : position.PositionName;
        MessageManager.instance?.Send("{0} finished working: {1} at {2}.", councilorArg, action, location);
    }

    void RefreshCouncilUI()
    {
        if (CouncilUI.instance != null && CouncilUI.instance.gameObject.activeSelf)
            CouncilUI.instance.SetupEntries();
    }

    public void ClearAll()
    {
        foreach (var indicator in active.Values)
        {
            if (indicator != null)
                Destroy(indicator.gameObject);
        }
        active.Clear();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (TimeManager.instance != null)
            TimeManager.instance.DayPassed.RemoveListener(Tick);
    }
}