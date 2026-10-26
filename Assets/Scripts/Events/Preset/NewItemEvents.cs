using System.Collections.Generic;
using UnityEngine;

public static class NewItemEvents
{
    public static IEnumerable<RandomEvent> GetAll()
    {
        yield return new UnexpectedFindEvent();
        yield return new TravelingMerchantGiftEvent();
        yield return new CourtGiftEvent();
    }
}

public class UnexpectedFindEvent : RandomEvent
{
    public override string Name => "Unexpected Find";
    public override string EventText => "While poking around, you discover a small cache tucked away.";
    public override EventScope Scope => EventScope.Location;
    public override float BaseMTTHDays => 25f;

    public override bool CanTrigger(Character c)
        => c.CurrentLocation != null && !c.CurrentLocation.IsHolding;

    public override List<EventOption> GetOptions(Character c)
    {
        return new List<EventOption>
        {
            new EventOption("Take what you can carry",
                (c2) => { c2.Reknown += 1f; },
                new List<ItemGrant> { new ItemGrant("iron_ore", 2) }),
            new EventOption("Leave it alone",
                (c2) => { c2.Piety += 2f; })
        };
    }
}

public class TravelingMerchantGiftEvent : RandomEvent
{
    public override string Name => "Traveling Merchant";
    public override string EventText => "A friendly merchant shares a small token of goodwill as you pass.";
    public override EventScope Scope => EventScope.Travel;
    public override float BaseMTTHDays => 20f;

    public override bool CanTrigger(Character c) => c.IsTraveling;

    public override List<EventOption> GetOptions(Character c)
    {
        return new List<EventOption>
        {
            new EventOption("Accept graciously",
                (c2) => { c2.Wealth += 5f; },
                new List<ItemGrant> { new ItemGrant("healing_potion", 1) })
        };
    }
}

public class CourtGiftEvent : RandomEvent
{
    public override string Name => "Court Gift";
    public override string EventText => "A minor noble presents you with a sealed letter as a gesture of goodwill.";
    public override EventScope Scope => EventScope.Holding;
    public override float BaseMTTHDays => 25f;

    public override bool CanTrigger(Character c)
        => c.CurrentLocation != null && c.CurrentLocation.IsHolding;

    public override List<EventOption> GetOptions(Character c)
    {
        return new List<EventOption>
        {
            new EventOption("Accept the gift",
                (c2) => { c2.Reknown += 3f; },
                new List<ItemGrant> { new ItemGrant("quest_letter", 1) })
        };
    }
}
