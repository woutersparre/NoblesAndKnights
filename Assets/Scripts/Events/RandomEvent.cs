using System.Collections.Generic;

public abstract class RandomEvent
{
    public abstract string Name { get; }
    public abstract string EventText { get; }
    public abstract EventScope Scope { get; }

    // CK2-style Mean Time To Happen in days
    public abstract float BaseMTTHDays { get; }

    // Earliest day this event can fire (prevents events on day one)
    public virtual int MinTriggerDay => 30;

    // 0-1 chance per day after MTTH modifiers are applied
    public float GetDailyChance(Character c)
    {
        float effective = BaseMTTHDays * GetMTTHModifier(c);
        if (effective <= 0f) effective = 1f;
        return 1f / effective;
    }

    // Override to make the event more or less likely based on character traits/stats/state
    // 0.5 = fires twice as fast, 2.0 = fires half as fast, 1.0 = no modifier
    public virtual float GetMTTHModifier(Character c) => 1f;

    // Can this character trigger this event right now?
    public abstract bool CanTrigger(Character c);

    // The choices presented to the player (or resolved by AI)
    public abstract List<EventOption> GetOptions(Character c);

    // Which option does the AI pick? Returns index into GetOptions()
    public virtual int ChooseAIOption(Character c) => 0;

    // Characters taking part in this event, in stage order: the first is placed in
    // the left-hand actor slot, the next in the right. List the player last so they
    // appear on the right, answering whoever is speaking.
    //
    // Defaults to just the character the event fired for. Events that involve
    // somebody else -- a disputing villager, a merchant, a rival -- override this
    // and put that character in front of the player.
    public virtual List<Character> GetActors(Character c) => new List<Character> { c };
}
