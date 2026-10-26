using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public enum ItemType
{
    Misc,
    Consumable,
    Material,
    Quest,
    Equipment
}

// Body slots a character can wear equipment in. None means the item cannot be equipped.
[System.Serializable]
public enum EquipmentSlot
{
    None,
    Head,
    Body,
    Weapon,
    Offhand,
    Ring,
    Amulet
}

[System.Serializable]
public class ItemDef
{
    public string id;
    public string displayName;
    public string description;
    public Sprite icon;
    public ItemType itemType = ItemType.Misc;
    public bool stackable = true;
    public int maxStack = 99;

    // Equipment
    // Which slot the item fits. None = the item is not equipment.
    public EquipmentSlot slot = EquipmentSlot.None;
    // Stat bonuses granted while the item is equipped. Reuses the trait Modifier type.
    public List<Modifier> bonuses = new List<Modifier>();

    public bool IsEquippable => slot != EquipmentSlot.None;
}
