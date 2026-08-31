# Grant weapons, outfits and junk

## Why

Resources land correctly, so the write path is trusted. Items are the next thing the panel was asked
for, and unlike resources they carry an identifier — which is exactly where a save gets corrupted.
An id the game does not know produces an item that cannot resolve its own data.

## What changes

- A picker listing every weapon, outfit and junk item **the game itself holds**, read from
  `GameParameters.Instance.Items`. Nothing is typed by hand and no id is hardcoded.
- A search box, because there are hundreds of items and a flat list is unusable.
- Granting puts one of the chosen item into the vault inventory.

## Non-goals

- No decorations, themes or pets. Decorations generate random extra data in their constructor and
  pets have their own rules; both deserve their own change.
- No equipping onto a dweller. Items go to the inventory; the player assigns them.
- No editing an item's stats. That is a different kind of write and a different kind of risk.
- Still no NGUI.

## Game API this depends on, and how it was confirmed

| Member | Confirmed by |
|---|---|
| `GameParameters.Instance.Items : ItemParameters` | Reflection: property `Items`, field `m_items` |
| `ItemParameters.WeaponsList : DwellerWeaponItem[]`, `.OutfitList`, `.JunksList` | Reflection: property list |
| `DwellerWeaponItem.WeaponId : string` | IL of `ItemParameters.GetWeapon` → the lambda compares `get_WeaponId` |
| `DwellerOutfitItem.m_outfitId : string` | IL of `ItemParameters.Initialize` → `m_outfitsById.Add(item.m_outfitId, item)` |
| `new DwellerItem(EItemType, string id)` | Reflection: constructor list; IL shows it stores type and id |
| `Inventory.AddItem(DwellerItem, bool, bool)` | Reflection: declared instance methods |
| `DwellerBaseItem.Name`, `.ItemRarity`, `.IsHiddenItem` | Reflection |
| `EItemType`, `EItemRarity` | Reflection: `Enum.GetNames` |

## The identifier question, settled rather than guessed

`DwellerItem` stores a bare string and resolves it later through `GetItemData`, which calls
`ItemParameters.GetWeapon(m_id)` or `GetOutfit(m_id)`. Which string those match on is the whole
question, and the two families answer differently:

- **Weapons** are found by a linear search comparing `WeaponId`.
- **Outfits** are found in a dictionary built in `Initialize` and keyed by the field `m_outfitId` —
  which is not exposed as an `Id`-suffixed property, so scanning properties misses it entirely.

`Name` and `CodeId` are both present on these types and both wrong. Using either would produce an
item whose data never resolves.

`Inventory.HandleItem(string, ItemExtraData)` looks like the factory its name suggests. Its IL shows
it searching `m_itemList` and returning an existing item or null: it is a lookup, and creates
nothing.

## What cannot be verified without a running game

That a granted item appears in the inventory screen with the right name, icon and stats — which is
the real proof the identifier is right.
