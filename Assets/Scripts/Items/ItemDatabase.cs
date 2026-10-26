using System.Collections.Generic;

public static class ItemDatabase
{
    public static readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>();
    public static readonly List<ItemDef> All = new List<ItemDef>();

    static ItemDatabase()
    {
        // ---- Consumables / materials / quest items ----

        Register(new ItemDef
        {
            id = "healing_potion",
            displayName = "Healing Potion",
            description = "Restores minor vitality.",
            itemType = ItemType.Consumable,
            stackable = true,
            maxStack = 99
        });

        Register(new ItemDef
        {
            id = "iron_ore",
            displayName = "Iron Ore",
            description = "Raw iron for smithing.",
            itemType = ItemType.Material,
            stackable = true,
            maxStack = 99
        });

        Register(new ItemDef
        {
            id = "quest_letter",
            displayName = "Sealed Letter",
            description = "A mysterious sealed letter.",
            itemType = ItemType.Quest,
            stackable = false,
            maxStack = 1
        });

        // ---- Equipment ----

        Register(new ItemDef
        {
            id = "iron_sword",
            displayName = "Iron Sword",
            description = "A plain but serviceable blade.",
            itemType = ItemType.Equipment,
            slot = EquipmentSlot.Weapon,
            stackable = false,
            maxStack = 1,
            bonuses = new List<Modifier>
            {
                new Modifier { modifierType = ModifierType.Martial, modifierValue = 2 },
                new Modifier { modifierType = ModifierType.CombatStrengh, modifierValue = 3 }
            }
        });

        Register(new ItemDef
        {
            id = "leather_armor",
            displayName = "Leather Armor",
            description = "Light protection for the road.",
            itemType = ItemType.Equipment,
            slot = EquipmentSlot.Body,
            stackable = false,
            maxStack = 1,
            bonuses = new List<Modifier>
            {
                new Modifier { modifierType = ModifierType.CombatStrengh, modifierValue = 2 }
            }
        });

        Register(new ItemDef
        {
            id = "steel_shield",
            displayName = "Steel Shield",
            description = "A sturdy shield bearing a faded crest.",
            itemType = ItemType.Equipment,
            slot = EquipmentSlot.Offhand,
            stackable = false,
            maxStack = 1,
            bonuses = new List<Modifier>
            {
                new Modifier { modifierType = ModifierType.CombatStrengh, modifierValue = 2 }
            }
        });

        Register(new ItemDef
        {
            id = "kings_crown",
            displayName = "Gilded Crown",
            description = "Heavy is the head that wears it.",
            itemType = ItemType.Equipment,
            slot = EquipmentSlot.Head,
            stackable = false,
            maxStack = 1,
            bonuses = new List<Modifier>
            {
                new Modifier { modifierType = ModifierType.Diplomacy, modifierValue = 2 },
                new Modifier { modifierType = ModifierType.Stewardship, modifierValue = 1 }
            }
        });

        Register(new ItemDef
        {
            id = "scholars_ring",
            displayName = "Scholar's Ring",
            description = "Its engraving is worn smooth by study.",
            itemType = ItemType.Equipment,
            slot = EquipmentSlot.Ring,
            stackable = false,
            maxStack = 1,
            bonuses = new List<Modifier>
            {
                new Modifier { modifierType = ModifierType.Learning, modifierValue = 2 },
                new Modifier { modifierType = ModifierType.Diplomacy, modifierValue = 1 }
            }
        });

        Register(new ItemDef
        {
            id = "silver_amulet",
            displayName = "Silver Amulet",
            description = "A whispered secret hangs from its chain.",
            itemType = ItemType.Equipment,
            slot = EquipmentSlot.Amulet,
            stackable = false,
            maxStack = 1,
            bonuses = new List<Modifier>
            {
                new Modifier { modifierType = ModifierType.Intrigue, modifierValue = 2 },
                new Modifier { modifierType = ModifierType.Learning, modifierValue = 1 }
            }
        });
    }

    private static void Register(ItemDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return;
        Items[def.id] = def;
        All.Add(def);
    }

    public static ItemDef Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Items.TryGetValue(id, out var v);
        return v;
    }

    public static bool Has(string id)
    {
        return !string.IsNullOrEmpty(id) && Items.ContainsKey(id);
    }
}
