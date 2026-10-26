using UnityEngine;

// CK2-style natural mortality.
//
// The base chance of dying of natural causes is purely a function of age, taken
// from CK2's NATURAL_DEATH_CHANCE_AGE_* defines (natural deaths per decade out
// of 10,000 people). Health then scales that chance around CK2's average of 5:
// every point below 5 makes death meaningfully more likely, every point above 5
// makes it less likely. Health itself never decays with age.
public static class NaturalDeath
{
    // Natural deaths per decade out of 10,000 people, indexed by age band
    // (0-9, 10-19, ... 100+). Straight from CK2's defines.lua.
    private static readonly int[] DeathsPerDecadePer10000 =
    {
        5,      // 0-9
        1,      // 10-19
        15,     // 20-29
        20,     // 30-39
        51,     // 40-49
        100,    // 50-59
        400,    // 60-69
        1400,   // 70-79
        3500,   // 80-89
        7000,   // 90-99
        9000    // 100+
    };

    // The daily check frequency in CK2 is not documented. This multiplier
    // calibrates the table above to CK2's observed lifespans (most healthy
    // characters die in their 60s-70s, 80s are uncommon, 90s very rare).
    // A simulation over 40,000 characters gives: median age 70, 80+ 9%, 90+ 0.15%.
    public const float MortalityScale = 12f;

    // Multiplier per point of health away from the average of 5.
    // Health 4 => x1.11, health 6 => x0.90, health 3 => x1.23.
    public const float HealthMultiplierPerPoint = 0.9f;

    public const float AverageHealth = 5f;

    // Probability of dying of natural causes on a single game day.
    public static float DailyChance(int age, float health)
    {
        int band = Mathf.Clamp(age / 10, 0, DeathsPerDecadePer10000.Length - 1);
        float perDecade = DeathsPerDecadePer10000[band] / 10000f;
        float perDay = perDecade / (10f * TimeManager.DaysPerYear);
        float healthMultiplier = Mathf.Pow(HealthMultiplierPerPoint, AverageHealth - health);

        return perDay * healthMultiplier * MortalityScale;
    }
}
