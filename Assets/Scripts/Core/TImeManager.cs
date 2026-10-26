using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

// Awake first, so anything built during scene setup finds instance already set
// instead of taking the deferred path. Awake order within a scene is otherwise
// undefined, which is what made ActionManager report a queued subscription.
[DefaultExecutionOrder(-1000)]
public class TimeManager : MonoBehaviour
{
    // Singleton instance
    public static TimeManager instance;

    // Enum for different time states
    public enum TimeState { Paused, Normal, FastForward }
    public TimeState currentState = TimeState.Paused;

    // Time settings
    public float normalTimeScale = 1f;
    public float fastForwardTimeScale = 3f;
    public float secondsPerDay = 1f; // 1 day passes every second at normal speed

    // Events
    public UnityEvent Payday = new();
    public UnityEvent DayPassed = new();
    public UnityEvent MonthPassed = new();
    public UnityEvent YearPassed = new();

    // Calendar: 12 months of 30 days = the 360-day year the game always used.
    // daysPassed remains the single source of truth; months and years are just
    // views of it so existing systems keep working unchanged.
    public const int DaysPerMonth = 30;
    public const int MonthsPerYear = 12;
    public const int DaysPerYear = DaysPerMonth * MonthsPerYear;

    [Header("Calendar")]
    public int startYear = 800;

    private static readonly string[] MonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    // Time tracking
    public int daysPassed = 0;
    private Coroutine timeLoopCoroutine; // Reference to time coroutine

    public int YearsPassed => daysPassed / DaysPerYear;
    public int MonthsPassed => daysPassed / DaysPerMonth;
    public int CurrentYear => startYear + YearsPassed;
    public int MonthIndex => (daysPassed / DaysPerMonth) % MonthsPerYear;
    public int DayOfMonth => daysPassed % DaysPerMonth + 1; // 1-30

    // e.g. "March 12, 812"
    public string GetDateString() => GetDateStringForDay(daysPassed);

    // Same calendar rendering for any absolute day value (e.g. birth dates).
    // Uses floor division so dates before day 0 (birth years) render correctly.
    public string GetDateStringForDay(int day)
    {
        int year = startYear + Mathf.FloorToInt(day / (float)DaysPerYear);
        int dayOfYear = day - (year - startYear) * DaysPerYear; // 0..359
        int month = dayOfYear / DaysPerMonth;
        int dayOfMonth = dayOfYear % DaysPerMonth + 1;

        return $"{MonthNames[month]} {dayOfMonth}, {year}";
    }

    // Listeners that registered before this manager existed. Characters, Locations
    // and the UI are all built during scene setup, which can run before or after
    // this component's Awake; they used to test instance, find it null, and drop the
    // subscription for the entire session, so they never received a single payday
    // or daily tick. Queue them here and flush them in Awake instead.
    private static readonly List<UnityAction> pendingDayPassed = new();
    private static readonly List<UnityAction> pendingPayday = new();
    private static readonly List<UnityAction> pendingMonthPassed = new();
    private static readonly List<UnityAction> pendingYearPassed = new();

    // The only supported way to hook the clock. Use these instead of touching
    // instance.DayPassed and friends directly, so the subscription survives a
    // manager that has not been created yet.
    public static void SubscribeDayPassed(UnityAction action)
    {
        if (action == null) return;
        if (instance != null) { instance.DayPassed.AddListener(action); return; }
        Defer(action, pendingDayPassed, nameof(DayPassed));
    }

    public static void SubscribePayday(UnityAction action)
    {
        if (action == null) return;
        if (instance != null) { instance.Payday.AddListener(action); return; }
        Defer(action, pendingPayday, nameof(Payday));
    }

    public static void SubscribeMonthPassed(UnityAction action)
    {
        if (action == null) return;
        if (instance != null) { instance.MonthPassed.AddListener(action); return; }
        Defer(action, pendingMonthPassed, nameof(MonthPassed));
    }

    public static void SubscribeYearPassed(UnityAction action)
    {
        if (action == null) return;
        if (instance != null) { instance.YearPassed.AddListener(action); return; }
        Defer(action, pendingYearPassed, nameof(YearPassed));
    }

    private static void Defer(UnityAction action, List<UnityAction> pending, string eventName)
    {
        pending.Add(action);

        // Self-healing: the queue is flushed the moment the manager's Awake runs, so
        // this is only ever a note about ordering, not a fault. (If no manager ever
        // starts up there is no clock either, so nothing would tick regardless.)
        Debug.Log($"TimeManager starts later; queued {Describe(action)} for {eventName} until then.");
    }

    private static string Describe(UnityAction action)
    {
        return $"{action.Method.DeclaringType?.Name}.{action.Method.Name}";
    }

    private static void Flush(List<UnityAction> pending, UnityEvent evt)
    {
        for (int i = 0; i < pending.Count; i++)
            evt.AddListener(pending[i]);
        pending.Clear();
    }

    private void Awake()
    {
        // Ensure singleton
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        Flush(pendingDayPassed, DayPassed);
        Flush(pendingPayday, Payday);
        Flush(pendingMonthPassed, MonthPassed);
        Flush(pendingYearPassed, YearPassed);
    }

    private void Start()
    {
        Play();
    }

    private void OnDestroy()
    {
        // Leaves instance genuinely null (rather than a destroyed object that still
        // compares non-null in plain C#), so later subscribers queue up for the next
        // manager instead of attaching to a dead event.
        if (instance == this)
            instance = null;
    }

    // Starts or restarts the game time loop
    private void StartTimeLoop()
    {
        if (timeLoopCoroutine != null)
            StopCoroutine(timeLoopCoroutine);
        timeLoopCoroutine = StartCoroutine(TimeLoop());
    }

    // Main coroutine handling time progression
    private IEnumerator TimeLoop()
    {
        while (true)
        {
            // Paused used to compute secondsPerDay / 0 = infinity and then hang on
            // WaitForSecondsRealtime(Infinity) forever, so resuming never worked
            // unless something restarted the coroutine. Idle a frame instead and
            // re-check the state.
            if (currentState == TimeState.Paused)
            {
                yield return null;
                continue;
            }

            // Belt and braces against a timescale that is zero from somewhere else.
            float speed = Mathf.Max(Time.timeScale, 0.0001f);
            yield return new WaitForSecondsRealtime(secondsPerDay / speed);

            if (currentState != TimeState.Paused)
                IncrementDay();
        }
    }

    private void IncrementDay()
    {
        daysPassed++;
        DayPassed.Invoke();

        if (daysPassed % 15 == 0)
            Payday.Invoke();

        if (daysPassed % DaysPerMonth == 0)
            MonthPassed.Invoke();

        if (daysPassed % DaysPerYear == 0)
            YearPassed.Invoke();
    }

    public void Pause()
    {
        currentState = TimeState.Paused;
        SetTimeScale(0f);
    }

    public void Play()
    {
        currentState = TimeState.Normal;
        SetTimeScale(normalTimeScale);
        StartTimeLoop(); // Ensure time progression resumes
    }

    public void FastForward()
    {
        currentState = TimeState.FastForward;
        SetTimeScale(fastForwardTimeScale);
        StartTimeLoop(); // Restart loop at new speed
    }

    private void SetTimeScale(float scale)
    {
        Time.timeScale = scale;

        // A zero fixedDeltaTime is invalid (physics divides by it); pausing already
        // freezes physics through timeScale, so leave the step alone when stopped.
        if (scale > 0f)
            Time.fixedDeltaTime = 0.02f * scale;
    }
}
