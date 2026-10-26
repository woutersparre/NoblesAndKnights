using System.Collections.Generic;
using UnityEngine;

// Events that fire for characters at any non-holding location (zones, ruins, etc.)
public static class LocationEvents
{
    public static IEnumerable<RandomEvent> GetAll()
    {
        yield return new FoundCoinsEvent();
        yield return new BanditThreatEvent();
    }
}

// While exploring, you stumble upon a small purse someone left behind.
public class FoundCoinsEvent : RandomEvent
{
    public override string Name => "Found Coins";
    public override string EventText => "While looking around, you spot a small leather purse half-hidden in the ground. It jingles with promise.";
    public override EventScope Scope => EventScope.Location;
    public override float BaseMTTHDays => 20f;

    public override bool CanTrigger(Character c)
        => c.CurrentLocation != null && !c.CurrentLocation.IsHolding;

    public override List<EventOption> GetOptions(Character c)
    {
        return new List<EventOption>
        {
            new EventOption("Take the coins for yourself",
                (c2) => { c2.Wealth += 15f; }),
            new EventOption("Leave them where you found them",
                (c2) => { c2.Piety += 5f; })
        };
    }
}

// A lone bandit blocks your path and demands coin for safe passage.
public class BanditThreatEvent : RandomEvent
{
    public override string Name => "Bandit Threat";
    public override string EventText => "A ragged bandit steps out from the shadows, knife drawn, and demands you hand over your coin.";
    public override EventScope Scope => EventScope.Location;
    public override float BaseMTTHDays => 35f;

    public override float GetMTTHModifier(Character c)
        => c.Stats.Martial >= 6 ? 0.5f : 1f;

    public override bool CanTrigger(Character c)
        => c.CurrentLocation != null && !c.CurrentLocation.IsHolding;

    public override List<EventOption> GetOptions(Character c)
    {
        return new List<EventOption>
        {
            new EventOption("Pay him off and be on your way",
                (c2) => { c2.Wealth -= 10f; }),
            new EventOption("Stand your ground and drive him off",
                (c2) => { c2.Reknown += 5f; c2.Wealth -= 5f; })
        };
    }

    // The ragged bandit who stepped out of the shadows.
    public override List<Character> GetActors(Character c)
    {
        Character bandit = new Character(temporary: true);
        return new List<Character> { bandit, c };
    }

    public override int ChooseAIOption(Character c)
        => c.Stats.Martial >= 6 ? 1 : 0;
}