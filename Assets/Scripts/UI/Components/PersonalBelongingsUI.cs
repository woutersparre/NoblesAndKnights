using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PersonalBelongingsUI : Window<Character>
{
    [Header("References")]
    public Transform contentRoot;
    public GameObject itemRowPrefab;
    public Transform equipmentRoot;
    public GameObject equipmentSlotPrefab;
    public Button closeButton;
    public TMP_Text headerText;

    private readonly List<GameObject> itemRows = new List<GameObject>();
    private readonly List<GameObject> slotRows = new List<GameObject>();

    protected override void Awake()
    {
        base.Awake();
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseWindow);
    }

    public void Setup(Character character)
    {
        Target = character;
        if (headerText != null)
            headerText.text = character != null ? character.Name + "'s Belongings" : "Personal Belongings";
        Refresh();
    }

    public void Refresh()
    {
        ClearRows();

        if (Target == null)
            return;

        BuildEquipmentSlots();

        if (Target.Inventory == null)
            return;

        foreach (InventoryEntry entry in Target.Inventory.GetAll())
            CreateItemRow(entry);
    }

    // ---- Equipment slots ----

    private void BuildEquipmentSlots()
    {
        if (equipmentRoot == null || equipmentSlotPrefab == null)
            return;

        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            if (slot == EquipmentSlot.None)
                continue;

            CreateSlotRow(slot);
        }
    }

    private void CreateSlotRow(EquipmentSlot slot)
    {
        GameObject row = Instantiate(equipmentSlotPrefab, equipmentRoot);
        slotRows.Add(row);

        string equippedId = Target.GetEquippedItemId(slot);
        ItemDef def = ItemDatabase.Get(equippedId);

        TMP_Text slotLabel = FindText(row, "SlotName");
        if (slotLabel != null)
            slotLabel.text = slot.ToString();

        TMP_Text itemLabel = FindText(row, "ItemName");
        if (itemLabel != null)
            itemLabel.text = def != null ? def.displayName : "(empty)";

        Button button = row.GetComponent<Button>();
        if (button != null)
        {
            // Only a filled slot can be clicked (to take the item off).
            button.interactable = def != null;
            EquipmentSlot captured = slot;
            button.onClick.AddListener(() =>
            {
                if (Target != null && Target.Unequip(captured))
                    Refresh();
            });
        }

        // Tooltip describing the equipped item (or the empty slot).
        InfoPopup popup = row.GetComponent<InfoPopup>();
        if (popup == null)
            popup = row.AddComponent<InfoPopup>();

        if (def != null)
        {
            int day = (Target != null && Target.EquippedItemDay != null && Target.EquippedItemDay.TryGetValue(slot, out int d)) ? d : 0;
            popup.Header = def.displayName;
            popup.Body = BuildItemBody(def, day, true);
        }
        else
        {
            popup.Header = slot.ToString();
            popup.Body = "No item equipped.";
        }
    }

    // ---- Inventory ----

    private void CreateItemRow(InventoryEntry entry)
    {
        if (contentRoot == null || itemRowPrefab == null || entry == null)
            return;

        string itemId = entry.itemId;
        int count = entry.count;

        GameObject row = Instantiate(itemRowPrefab, contentRoot);
        itemRows.Add(row);

        ItemDef def = ItemDatabase.Get(itemId);

        TMP_Text nameText = FindText(row, "Name");
        if (nameText != null)
            nameText.text = def != null ? def.displayName : itemId;

        Image iconImage = FindImage(row, "Icon");
        if (iconImage != null && def != null && def.icon != null)
            iconImage.sprite = def.icon;

        TMP_Text cnt = FindText(row, "Count");
        if (cnt != null)
            cnt.text = count.ToString();

        Button button = row.GetComponent<Button>();
        if (button != null)
        {
            bool equippable = def != null && def.IsEquippable;
            button.interactable = equippable;

            if (equippable)
            {
                string captured = itemId;
                button.onClick.AddListener(() => { if (Target.Equip(captured)) Refresh(); });
            }
        }

        // Tooltip describing the item, its bonuses and when it was acquired.
        InfoPopup popup = row.GetComponent<InfoPopup>();
        if (popup == null)
            popup = row.AddComponent<InfoPopup>();
        popup.Header = def != null ? def.displayName : itemId;
        popup.Body = BuildItemBody(def, entry.dayAdded, false);
    }

    private static TMP_Text FindText(GameObject row, string childName)
    {
        TMP_Text text = row.transform.Find(childName)?.GetComponent<TMP_Text>();
        if (text == null)
            text = row.GetComponentInChildren<TMP_Text>(true);
        return text;
    }

    private static Image FindImage(GameObject row, string childName)
    {
        Image image = row.transform.Find(childName)?.GetComponent<Image>();
        if (image == null)
            image = row.GetComponentInChildren<Image>(true);
        return image;
    }

    private static string BuildItemBody(ItemDef def, int dayAdded, bool equipped)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (def != null && !string.IsNullOrEmpty(def.description))
            sb.AppendLine(def.description);

        if (def != null && def.bonuses != null && def.bonuses.Count > 0)
        {
            sb.AppendLine("Bonuses:");
            foreach (Modifier m in def.bonuses)
                sb.AppendLine("  +" + (int)m.modifierValue + " " + ModifierDisplayName(m.modifierType));
        }

        if (equipped)
            sb.AppendLine("(Equipped)");

        string date = FormatDate(dayAdded);
        if (!string.IsNullOrEmpty(date))
            sb.AppendLine("Acquired: " + date);

        return sb.ToString().TrimEnd();
    }

    private static string ModifierDisplayName(ModifierType type)
    {
        switch (type)
        {
            case ModifierType.CombatStrengh: return "Combat Strength";
            default: return type.ToString();
        }
    }

    private static string FormatDate(int day)
    {
        if (TimeManager.instance != null)
            return TimeManager.instance.GetDateStringForDay(day);
        return "Day " + day;
    }

    private void ClearRows()
    {
        for (int i = 0; i < itemRows.Count; i++)
        {
            if (itemRows[i] != null) Destroy(itemRows[i]);
        }
        itemRows.Clear();

        for (int i = 0; i < slotRows.Count; i++)
        {
            if (slotRows[i] != null) Destroy(slotRows[i]);
        }
        slotRows.Clear();
    }

    public void Close() => CloseWindow();
}
