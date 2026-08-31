# Design — item grants

## Building the list

```
GameParameters.Instance.Items
    .WeaponsList : DwellerWeaponItem[]     id = WeaponId      (property)
    .OutfitList  : DwellerOutfitItem[]     id = m_outfitId    (field)
    .JunksList   : DwellerJunkItem[]       id = JunkId        (property)
```

Read once, on the first frame the section is drawn, and cached. The tables do not change during a
session, and rebuilding them per frame would allocate for nothing.

Items with `IsHiddenItem` set are skipped: the game hides them for its own reasons and a debug panel
that hands out things the game deliberately conceals is inviting a broken save.

## Why the identifier differs by family, and how that was settled

`DwellerItem` keeps a bare string and resolves it lazily in `GetItemData`, which switches on the
item type and calls the matching lookup. Reading those lookups is the only way to know what the
string must be:

- `ItemParameters.GetWeapon(id)` does `m_weapons.Find(w => w.WeaponId.Equals(id))`.
- `ItemParameters.GetOutfit(id)` reads `m_outfitsById`, a dictionary built in `Initialize` as
  `m_outfitsById.Add(outfit.m_outfitId, outfit)`.

Both `Name` and `CodeId` exist on these types and neither is used for the lookup. `m_outfitId` is a
private field with no `Id`-suffixed property, so a scan of properties finds `CodeId` and stops —
which is exactly the trap this design avoids by reading the IL instead.

The id is therefore taken per family, through reflection on the member that family is keyed by, and
the display name comes from `DwellerBaseItem.Name` — which is for the human, never for the game.

## Granting

```
new DwellerItem(type, id)                       // IL: stores type and id, nothing else
Vault.Instance.Inventory.AddItem(item, false, false)
```

`unlockRecipeAndIgnoreAdd` false because this is a real item, not a recipe unlock.
`addedThroughRefund` false because nothing is being refunded.

`Inventory.EmptySpace()` is checked first. The panel says the inventory is full rather than calling
into an add that may quietly drop the item.

## What was rejected

`Inventory.HandleItem(string, ItemExtraData)` reads like the factory. Its IL is a `List.Find` over
`m_itemList` returning an existing item or null. It finds; it does not create. Named plausibly
enough to have cost a build if it had not been disassembled.

## The list is long

Hundreds of items across three families, so the section is a filter box plus a scrolling list, and
the filter matches display names case-insensitively. The alternative — a scroll through everything —
is unusable, and a free-text id box is worse: it would let a player type an id the game cannot
resolve, which is the failure this whole design exists to prevent.
