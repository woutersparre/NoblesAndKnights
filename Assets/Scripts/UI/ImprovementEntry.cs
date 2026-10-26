using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ImprovementEntry : MonoBehaviour
{
    public TMP_Text Level, Name;
    public Button Button;

    // The popup on this root describes the improvement as it currently stands; the
    // one on Button describes what paying for the next level would change. Both are
    // reached from objects we already stand on or reference, so nothing extra has
    // to be wired in the prefab.
    private InfoPopup ownInfo;
    private InfoPopup upgradeInfo;

    public void UpdateEntry(Improvement i)
    {
        Level.text = i.Level.ToString();
        Name.text = i.Name;

        if (ownInfo == null) ownInfo = GetComponent<InfoPopup>();
        if (upgradeInfo == null && Button != null) upgradeInfo = Button.GetComponent<InfoPopup>();

        if (ownInfo != null)
        {
            ownInfo.Header = i.Name;

            List<string> now = new List<string>();
            now.Add(i.Level == 0 ? "Not built" : $"Level {i.Level} of {i.MaxLevel}");

            if (i.IsUnderConstruction)
                now.Add($"Under construction: {i.ConstructionDaysLeft} day(s) left");

            // An unbuilt improvement contributes nothing yet, so there is no figure to
            // quote for it here; the button's popup covers what building it would give.
            if (i.Level > 0 && !string.IsNullOrEmpty(i.EffectName))
                now.Add($"{i.EffectName}: +{i.GetCurrentEffect():F1}");

            ownInfo.Body = string.Join("\n", now);
        }

        if (upgradeInfo != null)
        {
            upgradeInfo.Header = i.Name;
            upgradeInfo.Body = BuildUpgradeBody(i);
        }
    }

    // Everything here depends on the improvement's level and multipliers, so it is
    // rebuilt each time rather than authored in the prefab.
    private static string BuildUpgradeBody(Improvement i)
    {
        List<string> next = new List<string>();

        // Paid for already: the level is not there yet, so report the countdown
        // instead of a price the player has already handed over.
        if (i.IsUnderConstruction)
        {
            next.Add($"Under construction: {i.ConstructionDaysLeft} day(s) left");

            if (!string.IsNullOrEmpty(i.EffectName))
                next.Add($"{i.EffectName}: +{i.GetNextEffect():F1} when finished");

            return string.Join("\n", next);
        }

        if (!i.CanUpgrade())
        {
            next.Add("Max level reached");
        }
        else if (i.Level == 0)
        {
            if (!string.IsNullOrEmpty(i.EffectName))
                next.Add($"{i.EffectName}: +{i.GetNextEffect():F1} once built");

            next.Add($"Construction cost: {i.ConstructionCost:F0}");
            next.Add($"Build time: {i.BuildTime:F0} day(s)");
        }
        else
        {
            float current = i.GetCurrentEffect();
            float upgraded = i.GetNextEffect();

            if (!string.IsNullOrEmpty(i.EffectName))
                next.Add($"Level {i.Level + 1}: {i.EffectName} +{upgraded:F1} (+{upgraded - current:F1})");

            next.Add($"Upgrade cost: {i.GetUpgradeCost():F0}");
            next.Add($"Build time: {i.BuildTime:F0} day(s)");
        }

        return string.Join("\n", next);
    }
}
