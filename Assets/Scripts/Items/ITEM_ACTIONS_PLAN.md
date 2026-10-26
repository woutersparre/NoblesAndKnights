# Item Actions Plan (Consumables / Use)

Goal: give non-equipment items (consumables, quest items, etc.) an effect when
the player (or an AI character) uses them, instead of being inert data.

## 1. Data model

- Add an `ItemAction` concept to `ItemDef`:
  - `public List<ItemAction> actions = new();` (or a single `ItemAction action`).
  - `ItemAction` is a serializable class with:
    - `string actionId` (e.g. "use", "read", "consume")
    - `string displayName` (button label, e.g. "Drink", "Read")
    - `List<ItemEffect> effects`
    - optional `int cooldownDays`, `int charges` (for limited-use items)
- `ItemEffect` is a serializable class with:
  - `EffectType type` enum: `RestoreHealth`, `ModifyStat`, `AddTrait`, `RemoveTrait`,
    `GrantItem`, `RemoveItem`, `TriggerEvent`, `Custom`.
  - payload fields: `ModifierType stat`, `int amount`, `string traitId`, `string itemId`,
    `string eventId`, and a `string customKey` for game-specific hooks.
- Keep `ItemType.Consumable` as a hint; an item is "usable" when it has at least one
  action (so quest items can also be "read" without being consumable).

## 2. Execution

- Add `Character.UseItem(string itemId, int actionIndex = 0)`:
  - Validate ownership (`GetItemCount > 0`), item exists, action exists.
  - Apply each `ItemEffect` to the character (and optionally to a target).
  - If the item is consumable: `TakeItem(itemId, 1)` after success.
  - If charges/cooldown: decrement charges / set `LastUsedDay`.
  - Fire a low-priority message ("Used Healing Potion: +X health") via MessageManager.
  - Recompute stats (`ApplyStatusModifiers`) if a stat/trait changed.
- For non-consumable but usable items (quest letters), do not remove on use; instead
  set a "used/read" flag (per character or per item instance) to avoid repeat.
- AI characters can call the same path from event resolution / council actions.

## 3. Effects implementation (start small)

Implement only these first:
1. `RestoreHealth` (placeholder until health system is wired; can be a no-op/log).
2. `ModifyStat` (Diplomacy/Martial/.../CombatStrength) — permanent or timed.
   - Permanent: adjust `BaseStats` (or a new `ItemStatBonus` list).
   - Timed: reuse the existing timed-modifier infra (`AddTimedModifier`) if available.
3. `GrantItem` (e.g. a key unlocks a chest item).
4. `AddTrait` / `RemoveTrait`.

Defer `TriggerEvent` and `Custom` until needed.

## 4. UI

- In `PersonalBelongingsUI` inventory rows: show a "Use" button when the item has
  actions, in addition to the existing Equip button for equipment.
  - Button label = action.displayName.
  - Disabled when charges == 0 or on cooldown; tooltip explains why.
- Equipment rows: keep Unequip; optionally add "Use" for items with both equip and use.
- Show action results via the existing message system (low priority).
- Show remaining charges in the row (e.g. "x3 (2 uses)").

## 5. Validation / feedback

- `CanUse(itemId)` checks ownership, charges, cooldown day.
- Cooldowns tracked per character: `Dictionary<string, int> itemCooldowns`.
- Charges tracked in `InventoryEntry` (add `charges` field) or on the item instance.

## 6. Persistence

- Once a save system exists, persist `InventoryEntry.charges`, cooldowns, and used-flags.
  Until then everything is runtime-only (consistent with current Inventory).

## 7. Ordering / scope for first pass

1. Add `ItemAction`/`ItemEffect` data classes + `actions` field (no effects yet).
2. Add `Character.UseItem` with `ModifyStat` + `GrantItem` effects and consumable removal.
3. Add one usable item to `ItemDatabase` (e.g. a "Tonic" that +1 Martial for a month).
4. Add the "Use" button to inventory rows + low-priority message.
5. Add charges/cooldowns last.

## Open questions

- Should using an item be an `IAction` (council/court action) or a free action?
  Recommendation: free action from the belongings screen; council actions can wrap it.
- Permanent vs timed stat changes: timed is safer (avoids runaway stacking).
- Do AI characters use items autonomously? Defer to the big AI/economy pass.