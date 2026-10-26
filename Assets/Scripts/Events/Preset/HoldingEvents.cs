using System.Collections.Generic;
using UnityEngine;

// Events that fire for characters currently located at a holding (Castle, City, Church)
public static class HoldingEvents
{
    public static IEnumerable<RandomEvent> GetAll()
    {
        yield return new PettyDisputeEvent();
        yield return new MerchantTollEvent();
        yield return new PeasantUnrestEvent();
    }
}

// A petty dispute between two villagers threatens your court's harmony.
public class PettyDisputeEvent : RandomEvent
{
    public override string Name => "A Petty Dispute";
    public override string EventText => "Two prominent villagers have come before you, shouting over a land dispute. They demand you settle the matter.";
    public override EventScope Scope => EventScope.Holding;
    public override float BaseMTTHDays => 30f;

    public override bool CanTrigger(Character c)
        => c.CurrentLocation != null && c.CurrentLocation.IsHolding;

    public override List<EventOption> GetOptions(Character c)
    {
        return new List<EventOption>
        {
            new EventOption("Hear both sides and impose a fair ruling",
                (c2) => { c2.Reknown += 5f; }),
            new EventOption("Side with the wealthier villager for a bribe",
                (c2) => { c2.Wealth += 10f; }),
            new EventOption("Dismiss them; let them sort it out themselves",
                (c2) => { c2.Reknown -= 2f; })
        };
    }

    // You are the arbiter, so you stand alone on the first side while the two
    // disputing villagers face you together on the second.
    public override List<Character> GetActors(Character c)
    {
        return new List<Character>
        {
            c,
            new Character(gender: (Gender)Random.Range(0, 2), temporary: true),
            new Character(gender: (Gender)Random.Range(0, 2), temporary: true)
        };
    }

    public override int ChooseAIOption(Character c)
        => c.Stats.Diplomacy >= c.Stats.Stewardship ? 0 : 1;
}

// A traveling merchant offers a fee for permission to trade in your holding.
public class MerchantTollEvent : RandomEvent
{
    public override string Name => "Merchant's Fee";
    public override string EventText => "A merchant caravan arrives and offers to pay for the right to trade within your lands. The offer is generous but the caravan seems suspicious.";
    public override EventScope Scope => EventScope.Holding;
    public override float BaseMTTHDays => 20f;

    public override bool CanTrigger(Character c)
        => c.CurrentLocation != null && c.CurrentLocation.IsHolding;

    public override List<EventOption> GetOptions(Character c)
    {
        return new List<EventOption>
        {
            new EventOption("Accept the fee and welcome the traders",
                (c2) => { c2.Wealth += 20f; }),
            new EventOption("Decline; you trust them little",
                (c2) => { })
        };
    }

    // The merchant is a throwaway face for this event rather than somebody in the
    // world: temporary characters are never registered with the CharacterManager, so
    // this holds nothing beyond the popup and is collected once it closes.
    public override List<Character> GetActors(Character c)
    {
        Character merchant = new Character(name: "Merchant", temporary: true);
        return new List<Character> { merchant, c };
    }
}

// Unrest brews among the peasants over taxes.
public class PeasantUnrestEvent : RandomEvent
{
    public override string Name => "Peasant Unrest";
    public override string EventText => "Word reaches you that the peasants grumble over their taxes, and some speak of leaving your lands for good.";
    public override EventScope Scope => EventScope.Holding;
    public override float BaseMTTHDays => 40f;

    public override bool CanTrigger(Character c)
        => c.CurrentLocation != null && c.CurrentLocation.IsHolding;

    public override List<EventOption> GetOptions(Character c)
    {
        return new List<EventOption>
        {
            new EventOption("Lower the taxes to calm them",
                (c2) => { c2.Wealth -= 15f; c2.Piety += 5f; }),
            new EventOption("Send guards to keep the peace",
                (c2) => { c2.Reknown += 5f; }),
            new EventOption("Ignore their complaints",
                (c2) => { c2.Wealth -= 10f; })
        };
    }

    // One of the grumbling peasants puts a face to the crowd.
    public override List<Character> GetActors(Character c)
    {
        Character peasant = new Character(gender: (Gender)Random.Range(0, 2), temporary: true);
        return new List<Character> { peasant, c };
    }

    public override int ChooseAIOption(Character c)
        => c.Piety > c.Reknown ? 0 : 2;
}