using System;

public enum EventScope
{
    Holding,
    Location,
    Travel
}

[Serializable]
public class EventOption
{
    public string Text;
    public Action<Character> OnChosen;
    public System.Collections.Generic.List<ItemGrant> ItemGrants = new System.Collections.Generic.List<ItemGrant>();

    public EventOption(string text, Action<Character> onChosen, System.Collections.Generic.List<ItemGrant> itemGrants = null)
    {
        Text = text;
        OnChosen = onChosen;
        ItemGrants = itemGrants ?? new System.Collections.Generic.List<ItemGrant>();
    }

    public void ApplyRewards(Character c)
    {
        OnChosen?.Invoke(c);
        if (c == null || ItemGrants == null) return;
        for (int i = 0; i < ItemGrants.Count; i++)
        {
            var g = ItemGrants[i];
            if (g != null && !string.IsNullOrEmpty(g.itemId) && g.count > 0)
                c.GiveItem(g.itemId, g.count);
        }
    }
}
