using System.Collections.Generic;

[System.Serializable]
public class InventoryEntry
{
    public string itemId;
    public int count;
    // Day (TimeManager.daysPassed) on which this stack first entered the inventory.
    public int dayAdded;

    public InventoryEntry()
    {
        dayAdded = TimeManager.instance != null ? TimeManager.instance.daysPassed : 0;
    }

    public InventoryEntry(string id, int c)
    {
        itemId = id;
        count = c;
        dayAdded = TimeManager.instance != null ? TimeManager.instance.daysPassed : 0;
    }

    public InventoryEntry(string id, int c, int day)
    {
        itemId = id;
        count = c;
        dayAdded = day >= 0 ? day : (TimeManager.instance != null ? TimeManager.instance.daysPassed : 0);
    }
}

[System.Serializable]
public class Inventory
{
    public List<InventoryEntry> Entries = new List<InventoryEntry>();

    public int GetCount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].itemId == itemId) return Entries[i].count;
        }
        return 0;
    }

    // day < 0 means "use the current day".
    public void Add(string itemId, int count, int day = -1)
    {
        if (count <= 0 || string.IsNullOrEmpty(itemId)) return;
        int resolvedDay = day < 0 ? (TimeManager.instance != null ? TimeManager.instance.daysPassed : 0) : day;
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].itemId == itemId)
            {
                Entries[i].count += count;
                // Keep the original acquisition day for the stack.
                return;
            }
        }
        Entries.Add(new InventoryEntry(itemId, count, resolvedDay));
    }

    public bool Remove(string itemId, int count)
    {
        if (count <= 0 || string.IsNullOrEmpty(itemId)) return false;
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].itemId == itemId)
            {
                if (Entries[i].count < count) return false;
                Entries[i].count -= count;
                if (Entries[i].count <= 0)
                    Entries.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public bool Has(string itemId, int count = 1)
    {
        return GetCount(itemId) >= count;
    }

    public List<InventoryEntry> GetAll()
    {
        return Entries;
    }
}