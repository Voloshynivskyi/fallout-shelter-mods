# Grant pets, with a chosen name, bonus and value

## Why

Pets are the one thing in this game that really can be customised per copy. Weapons and outfits
cannot — proven in the previous change — but a pet carries its own `PetUniqueData` holding a name, a
bonus effect and a value, and all three are written into the save.

So the request for "give it a name, a stat and a value" is answerable for pets exactly as asked.

## What changes

- A pet section listing every pet the game holds, read from the game's catalogue.
- For the pet about to be granted: a **name** field, a **bonus** chosen from all 37 effects, and a
  **value**.
- Granting follows the game's own construction, then overwrites the three chosen fields.

## Non-goals

- No pet icons yet. Pet atlases load through a coroutine, one per pet type, so drawing them is a
  different problem from the item atlases and gets its own change.
- No editing of pets already in the vault. Creating is a smaller, safer surface; editing existing
  save data comes after.

## Game API this depends on, and how it was confirmed

| Member | Confirmed by |
|---|---|
| `Catalog.Instance.m_petsCustomizationData` (public field) then `PetItems` | IL of `SeasonPassRewardItem.GenerateRandomPet`; field confirmed public by reflection |
| `DwellerPetItem.PetId`, `.BaseName`, `.Type`, `.Breed`, `.BonusEffectList` | Reflection: declared properties |
| `DwellerPetItem.GenerateRandomData(Random) : PetUniqueData` | Reflection: signature |
| `PetUniqueData.Name`, `.Bonus`, `.BonusValue` — all writable | Reflection: `CanWrite` true on all three |
| `DwellerItem.ExtraData : ItemExtraData` — writable | Reflection: `CanWrite` true |
| `EItemType.Pet == 5` | Reflection, matching the `ldc.i4.5` in the game's own call |
| `EBonusEffect` — 37 members | Reflection: `Enum.GetNames` |

## The construction is the game's, not invented

`SeasonPassRewardItem.GenerateRandomPet` disassembles to exactly this:

```
new DwellerItem(EItemType.Pet, petItem.PetId);
item.ExtraData = petItem.GenerateRandomData(null);
```

The panel does the same and then overwrites `Name`, `Bonus` and `BonusValue` on the returned data.
Letting the game generate the unique data first matters: whatever else `GenerateRandomData` fills in
stays filled in, so the result is a pet the game built with three fields changed — not a record
assembled from scratch and hoped over.

## What cannot be verified without a running game

That the chosen bonus actually applies in play, and that the name shows on the pet card.
