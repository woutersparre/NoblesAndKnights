using System.Collections.Generic;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager instance;

    public const int EventCooldownDays = 30;
    public bool debugLogging = true;

    private List<RandomEvent> registeredEvents = new List<RandomEvent>();
    private Queue<PendingEvent> pendingPlayerEvents = new Queue<PendingEvent>();
    private EventUI eventUI;
    private TimeManager.TimeState prevTimeState;
    private int nextHeartbeatDay;

    private struct PendingEvent
    {
        public RandomEvent Event;
        public Character Character;
        public List<EventOption> Options;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnEnable()
    {
        SubscribeToDayPassed();
    }

    private void OnDisable()
    {
        if (TimeManager.instance != null)
            TimeManager.instance.DayPassed.RemoveListener(OnDayPassed);
    }

    private void SubscribeToDayPassed()
    {
        if (TimeManager.instance == null) return;
        TimeManager.instance.DayPassed.RemoveListener(OnDayPassed);
        TimeManager.instance.DayPassed.AddListener(OnDayPassed);
        if (debugLogging) Debug.Log("[RandomEventManager] Subscribed to TimeManager.DayPassed.");
    }

    private void Start()
    {
        SubscribeToDayPassed();
        MapManager.OnMapGenerated.AddListener(LogEligibilityAfterMapGen);

        RegisterEvents(HoldingEvents.GetAll());
        RegisterEvents(LocationEvents.GetAll());
        RegisterEvents(TravelEvents.GetAll());
        RegisterEvents(NewItemEvents.GetAll());

        SetupEventUI();

        int eligibleNow = CountEligibleCharacters();
        if (debugLogging) Debug.Log($"[RandomEventManager] Started: {registeredEvents.Count} events registered, UI ready = {eventUI != null}, eligible characters now = {eligibleNow}");
    }

    private void LogEligibilityAfterMapGen()
    {
        if (debugLogging) Debug.Log($"[RandomEventManager] After map generation: {CountEligibleCharacters()} eligible characters.");
    }

    private int CountEligibleCharacters()
    {
        if (MapManager.instance?.characters == null) return -1;
        int count = 0;
        foreach (Character c in MapManager.instance.characters.allCharacters)
        {
            if (IsEligible(c)) count++;
        }
        return count;
    }

    private void SetupEventUI()
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/Screens/EventScreen");
        if (prefab == null)
        {
            Debug.LogWarning("[RandomEventManager] EventPopup prefab not found. Player events will auto-resolve.");
            return;
        }

        eventUI = Instantiate(prefab, GetUIParent()).GetComponent<EventUI>();
        if (eventUI == null)
        {
            Debug.LogWarning("[RandomEventManager] EventPopup prefab has no EventUI component. Player events will auto-resolve.");
            return;
        }
        eventUI.gameObject.SetActive(false);
    }

    private Transform GetUIParent()
    {
        if (GameUIManager.instance != null && GameUIManager.instance.screensTransform != null)
            return GameUIManager.instance.screensTransform;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.transform : transform;
    }

    public void RegisterEvent(RandomEvent evt)
    {
        if (!registeredEvents.Contains(evt))
            registeredEvents.Add(evt);
    }

    public void RegisterEvents(IEnumerable<RandomEvent> events)
    {
        foreach (var evt in events)
            RegisterEvent(evt);
    }

    private void OnDayPassed()
    {
        if (MapManager.instance?.characters == null)
        {
            Debug.LogWarning("[RandomEventManager] OnDayPassed: MapManager.characters is null, skipping day.");
            return;
        }

        Character player = MapManager.instance.characters.Player;

        int eligibleCount = 0;
        Character[] characters = MapManager.instance.characters.allCharacters.ToArray();
        foreach (Character c in characters)
        {
            if (!IsEligible(c)) continue;
            eligibleCount++;
            TryFireEvent(c, player);
        }

        if (TimeManager.instance.daysPassed >= nextHeartbeatDay)
        {
            string playerName = player != null ? player.Name : "null";
            if (debugLogging) Debug.Log($"[RandomEventManager] Day {TimeManager.instance.daysPassed}: processed {eligibleCount} eligible characters (player: {playerName}).");
            nextHeartbeatDay = TimeManager.instance.daysPassed + 10;
        }
    }

    private void TryFireEvent(Character c, Character player)
    {
        if (TimeManager.instance == null) return;
        if (TimeManager.instance.daysPassed - c.LastEventDay < EventCooldownDays) return;

        EventScope scope = GetScope(c);

        List<RandomEvent> eligible = new List<RandomEvent>();
        foreach (var evt in registeredEvents)
        {
            if (evt.Scope != scope) continue;
            if (TimeManager.instance.daysPassed < evt.MinTriggerDay) continue;
            if (!evt.CanTrigger(c)) continue;
            eligible.Add(evt);
        }

        if (eligible.Count == 0) return;

        // Roll each event against its MTTH; collect those that pass
        List<RandomEvent> passed = new List<RandomEvent>();
        List<float> weights = new List<float>();
        foreach (var evt in eligible)
        {
float chance = evt.GetDailyChance(c);
        if (Random.value < chance)
            {
                passed.Add(evt);
                weights.Add(chance);
            }
        }

        if (passed.Count == 0) return;

        RandomEvent chosen = WeightedPick(passed, weights);
        List<EventOption> options = chosen.GetOptions(c);

        // One event per character per day, then a quiet period
        c.LastEventDay = TimeManager.instance.daysPassed;

        if (c == player)
            QueuePlayerEvent(chosen, c, options);
        else
            ResolveAIEvent(chosen, c, options);
    }

    private void QueuePlayerEvent(RandomEvent evt, Character c, List<EventOption> options)
    {
        if (debugLogging) Debug.Log($"[RandomEventManager] Queued player event '{evt.Name}' for {c.Name}");
        pendingPlayerEvents.Enqueue(new PendingEvent
        {
            Event = evt,
            Character = c,
            Options = options
        });
        TryShowNextPlayerEvent();
    }

    private void TryShowNextPlayerEvent()
    {
        if (pendingPlayerEvents.Count == 0) return;

        TimeManager tm = TimeManager.instance;
        if (tm == null) return;

        prevTimeState = tm.currentState;
        tm.Pause();

        // No UI available -> auto-resolve so the game never soft-locks
        if (eventUI == null)
        {
            while (pendingPlayerEvents.Count > 0)
            {
                PendingEvent pending = pendingPlayerEvents.Dequeue();
                ResolveAIEvent(pending.Event, pending.Character, pending.Options);
            }
            if (prevTimeState == TimeManager.TimeState.FastForward)
                tm.FastForward();
            else
                tm.Play();
            return;
        }

        PendingEvent next = pendingPlayerEvents.Peek();
        eventUI.ShowEvent(next.Event, next.Character, next.Options);
    }

    // Called by EventUI when the player makes a choice
    public void ResolvePlayerEvent(int optionIndex)
    {
        if (pendingPlayerEvents.Count == 0) return;

        PendingEvent pending = pendingPlayerEvents.Dequeue();
        if (optionIndex >= 0 && optionIndex < pending.Options.Count)
        {
            if (debugLogging) Debug.Log($"[RandomEventManager] Player resolved '{pending.Event.Name}': chose '{pending.Options[optionIndex].Text}'");
            pending.Options[optionIndex].ApplyRewards(pending.Character);
        }
        OnEventResolved?.Invoke(pending.Event, pending.Character);

        if (pendingPlayerEvents.Count > 0)
            TryShowNextPlayerEvent();
        else
            RestoreTime();
    }

    private void RestoreTime()
    {
        TimeManager tm = TimeManager.instance;
        if (tm == null) return;

        if (prevTimeState == TimeManager.TimeState.FastForward)
            tm.FastForward();
        else
            tm.Play();
    }

    private void ResolveAIEvent(RandomEvent evt, Character c, List<EventOption> options)
    {
        int choice = evt.ChooseAIOption(c);
        if (choice < 0 || choice >= options.Count) choice = 0;
        options[choice].ApplyRewards(c);
        if (debugLogging) Debug.Log($"[RandomEvent] {c.Name} experienced '{evt.Name}' (AI choice: {options[choice].Text})");
        OnEventResolved?.Invoke(evt, c);
    }

    // Fired after any event is resolved (player or AI)
    public System.Action<RandomEvent, Character> OnEventResolved;

    private EventScope GetScope(Character c)
    {
        if (c.IsTraveling) return EventScope.Travel;
        if (c.CurrentLocation != null && c.CurrentLocation.IsHolding) return EventScope.Holding;
        return EventScope.Location;
    }

    private bool IsEligible(Character c)
    {
        if (c == null || !c.IsAlive) return false;
        if (c.Titles.Count > 0) return true;

        // Court members (spouse/courtiers of any character) are eligible too
        foreach (Character other in MapManager.instance.characters.allCharacters)
        {
            if (other.Court.Contains(c))
                return true;
        }
        return false;
    }

    private RandomEvent WeightedPick(List<RandomEvent> events, List<float> weights)
    {
        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
            total += weights[i];

        float roll = Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < events.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return events[i];
        }

        return events[events.Count - 1];
    }
}