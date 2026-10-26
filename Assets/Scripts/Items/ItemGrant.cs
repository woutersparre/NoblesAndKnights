[System.Serializable]
public class ItemGrant
{
    public string itemId;
    public int count = 1;

    public ItemGrant() { }

    public ItemGrant(string itemId, int count = 1)
    {
        this.itemId = itemId;
        this.count = count;
    }
}
