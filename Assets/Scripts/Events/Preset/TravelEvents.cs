using System.Collections.Generic;
using UnityEngine;

// Events that fire for characters while they are in transit between locations
public static class TravelEvents
{
    public static IEnumerable<RandomEvent> GetAll()
    {
        yield return new RoadBanditEvent();
        yield return new LuckyFindEvent();
    }
}

// While travelling, bandits ambush your party on the road.
public class RoadBanditEvent : RandomEvent
{
    public override string Name => "Road Ambush";
    public override string EventText => "As you travel, armed men spring from the roadside brush, blocking your path and demanding valuables.";
    public override EventScope Scope => EventScope.Travel;
    public override float BaseMTTHDays => 25f;

    public override float GetMTTHModifier(Character c)
        => c.Stats.Martial >= 6 ? 0.5f : 1f;

    public override bool CanTrigger(Character c) => c.IsTraveling;

    public override List<EventOption> GetOptions(Character c)
    {
        return new List<EventOption>
        {
            new EventOption("Fight them off",
                (c2) => { c2.Reknown += 5f; c2.Wealth -= 5f; }),
            new EventOption("Pay the toll they demand",
                (c2) => { c2.Wealth -= 10f; }),
            new EventOption("Try to slip past unseen",
                (c2) => { if (Random.value >= 0.5f) c2.Wealth -= 10f; })
        };
    }

    // One of the men springing from the brush, standing in for the ambush.
    public override List<Character> GetActors(Character c)
    {
        Character bandit = new Character(temporary: true);
        return new List<Character> { bandit, c };
    }

    public override int ChooseAIOption(Character c)
        => c.Stats.Martial >= c.Stats.Intrigue ? 0 : 2;
}

// You come across something left behind by a previous traveller.
public class LuckyFindEvent : RandomEvent
{
    public override string Name => "Lucky Find";
    public override string EventText => "Beside the road you find a discarded satchel. Inside is a small pouch of coins, no owner in sight.";
    public override EventScope Scope => EventScope.Travel;
    public override float BaseMTTHDays => 30f;

    public override bool CanTrigger(Character c) => c.IsTraveling;

    public override List<EventOption> GetOptions(Character c)
    {
        return new List<EventOption>
        {
            new EventOption("Take the coins",
                (c2) => { c2.Wealth += 15f; }),
            new EventOption("Leave it for its rightful owner",
                (c2) => { c2.Piety += 5f; })
        };
    }
}