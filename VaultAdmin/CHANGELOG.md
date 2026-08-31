# Changelog

## 0.6.2

Two bugs found by playing, both mine, both traced back to code.

### Created dwellers had dead equipment slots

Their outfit, weapon and pet slots were drawn but did nothing when clicked. Existing dwellers were
fine.

`CreateDweller` adds the dweller to `DwellerManager`'s own list, which is enough for it to exist and
walk around. Nothing in that path calls `DwellerPool.AddToActiveDweller` — only `SetupDweller` does,
and `CreateDweller` does not call it. So the dweller was alive and visible but never registered as
active, and the interface had nothing to act on.

Fixed by registering it directly. `SetupDweller` would have registered it too, but it also re-rolls
stats from rarity and picks a random level, which would have thrown away the SPECIAL and level the
panel was asked to set.

Also stopped assigning `Rarity` after creation: `DwellerPool.GetInstance` already takes it as an
argument and sets it, so that line only wrote the same value back.

### Created pets had no icon

The game asks for a pet type's atlas before it builds the pet —
`PetAtlasManager.LoadAtlases(petItem.Type)` — because pet art loads asynchronously per type rather
than being simply present the way item atlases are.

That line was in the IL that this feature was built from, and it was read and skipped, on the
reasoning that icons were a separate change. The reasoning was wrong: the atlas load is part of
creating a pet, not part of drawing a list.

### Modified stats are recalculated

`CalculateModStats` is called after rewriting SPECIAL. The game does this after every stat change —
`CreateDweller` does it twice in its own body — and equipment bonuses are applied on top of modified
stats, so leaving them stale after setting all seven values left the dweller describing itself
wrongly.

## 0.6.0

Creates dwellers, with a name, a rarity, a level and all seven SPECIAL values — plus any legendary
dweller the game defines.

- Rarity, gender and starting level; first and last name, where an empty field keeps whatever the
  game generated; and the seven SPECIAL values.
- Legendary dwellers are listed from the game's own `LegendaryDwellers` and created through the call
  the game uses for them. They are deliberately left unedited: a legendary brings its own name, look
  and stats, and overwriting those produces something that looks legendary and is not.

### SPECIAL is set the way that keeps the record consistent

`SpecialStat.Value` cannot be assigned, and of the methods that can change it the choice matters.
The save stores a value and an experience figure side by side — `{"value": 5, "mod": 0, "exp":
72084.23}` — so `SetValueOnly` would move one and leave the other, producing a record describing two
different things. `SetValueAndMinExp` moves both.

### Creation admits the dweller by itself

The first draft called `AddDweller` after creating. It is private, which is how the mistake
surfaced — but reading the IL showed it was also redundant: both `CreateDweller` and
`CreateSpecialDweller` end in a call to it. The dweller is in the vault by the time the call
returns.

That is also why a full vault is now caught with `VaultIsWithMaxPopulation` **before** creating.
Waiting for a refusal afterwards is not possible when there is nothing left to refuse.

## 0.5.0

Grants pets, with a name, a bonus and a value of your choosing.

- Every pet the game holds, read from its own catalogue at runtime.
- Before granting: a **name**, a **bonus** picked from all 37 effects the game defines, and a
  **value**. An empty name keeps whatever the game generated.
- Creation follows the game's own sequence, disassembled from `GenerateRandomPet`: construct the
  item, let the game generate the pet's unique data, and only then overwrite the three chosen
  fields. Anything the panel does not offer keeps whatever the game put there.

### Why pets can be customised when weapons cannot

Because the save has somewhere to put it. A real vault stores a pet like this:

```json
{ "id": "husky_c", "type": "Pet",
  "extraData": { "uniqueName": "Biba", "bonus": "FasterWastelandReturnSpeed", "bonusValue": 1.25 } }
```

Three fields, exactly the three the panel writes. A weapon in the same save has no `extraData` at
all — which is the whole reason weapon stats are not offered.

Bonus values are left unclamped deliberately: pets already in that vault carry values from 1.25 to
95.0, so there is no sensible range to enforce.

## 0.4.0

Every item in the picker now shows its own picture.

- Icons are drawn straight from the game's atlases: an atlas is a texture plus a table of pixel
  rectangles, and `WeaponSprite`, `OutfitSprite` and `JunkSprite` name the rectangle for each item.
  Nothing is created per frame; the atlas is resolved once per family and the sprite name is stored
  with the catalogue entry.
- An item whose sprite is missing keeps its row and stays grantable, with a gap where the icon
  would be. Hiding items that cannot be illustrated would be worse than a few blanks.

### Why weapon stats, rarity and name are not editable

This was asked for, and it is not something the game can represent. Two independent proofs:

- `ItemExtraData` — the base class for anything an item carries per copy — is abstract with exactly
  four implementors: `DwellerDecorationItem`, `PetUniqueData`, `RecipeUniqueData` and
  `ThemeItemUniqueData`. **Weapons and outfits are absent**, so they hold no per-copy data at all.
- A real save stores a weapon as four fields: `id`, `type`, `hasBeenAssigned`,
  `hasRandonWeaponBeenAssigned`. There is nowhere to keep a damage figure or a custom name.

Damage, rarity and name live on the shared template every copy of that weapon reads from. Writing
them would change every copy in the game at once and would be gone on the next restart.

What replaces it: the picker grants **any** weapon the game holds, at every rarity, hidden ones
included.

**Pets are the opposite.** `PetUniqueData` carries `Name`, `Bonus` and `BonusValue` per copy, across
37 bonus types, so a pet really can be given a name, an effect and a value. Dwellers are serialised
per copy too. Both are next.

## 0.3.0

Grants weapons, outfits and junk, picked from the game's own tables.

- An item section with a family selector, a filter box and a scrolling list. Every entry is read
  from `GameParameters.Instance.Items` at runtime; **no item identifier is hardcoded anywhere**, so
  a game update that changes the item set is picked up without touching the mod.
- Items the game marks hidden are skipped.
- Granting refuses, and says so, when the inventory is full — rather than calling into an add that
  might quietly drop the item.

### The identifier is not the name, and differs by family

An item is stored as an id and a type, and resolves its data later by looking that id up. Which
string it must be was read out of the game's IL, not guessed:

- **Weapons** are found by a search comparing `WeaponId`.
- **Outfits** are found in a dictionary keyed on `m_outfitId` — a private field with no
  `Id`-suffixed property, so listing properties finds `CodeId` and misses it entirely.

Both types also carry `Name` and `CodeId`, and both are wrong for this. A real save settles it: the
game writes `{"id": "Flamer_Rusty", "type": "Weapon"}` — an internal id, never the display name.

`Inventory.HandleItem(string, ItemExtraData)` reads like the factory for this. Its IL is a search
over the existing inventory returning an item or null: it finds, it does not create.

## 0.2.0

Grants resources and boxes. Everything goes through the game's own methods.

- A row per resource with **+100**, **+1000**, **+10000** and **Fill**. Granting uses
  `Storage.AddResource` with capping on, so the vault clamps to its own limit, and with callbacks
  on, so the figure at the top of the screen updates immediately instead of going stale.
- **Fill** grants exactly the space the vault reports as available, landing on the cap.
- Lunchboxes, Mr Handy boxes, pet carriers and Nuka-Cola Quantum boxes in **+1**, **+5**, **+25**.
- No resource field is ever assigned directly.

### Boxes are not resources, whatever the save says

The save carries a resource counter called `Lunchbox`, and granting through it is the obvious move.
It does not work. In a real save that counter read 5 while `LunchBoxesByType` — where boxes actually
live — was an empty list: a number with nothing behind it.

Boxes therefore go through `Vault.AddLunchBox`, and the three box-shaped members of the resource
enum are excluded from the resource rows so nothing can be granted by two routes, one of which
quietly does nothing.

### Two ideas that did not survive contact with the assemblies

- **The game's debug menu.** `DebugInfo` is still in the release build, but its methods were
  stripped: the only one left is `FunctionThatShouldNeverBeCalled_CreatedToAvoidBuildWarnings()`.
- **`DebugOpenLunchboxes`.** The name promises box granting; the body is a balance tool that
  simulates openings and tallies odds. It gives the player nothing.

Both took minutes to disprove by reflection, and neither cost a build or a game launch.

## 0.1.0

First build. It reads and displays; it writes nothing.

- A panel on a configurable hotkey, defaulting to **F8**, showing the vault's resources against
  their caps, its dweller count against the maximum, and its inventory size against its limit.
- **Disabled by default.** Installed and left alone, the mod reads nothing, draws nothing and binds
  no key.
- A mistyped `ToggleKey` logs a warning naming the bad value and falls back to `F8`, rather than
  leaving the panel unreachable with no explanation.
- No Harmony patches at all. Everything read is public on two singletons.
- Failures in the panel are caught and logged once each, never per frame, and never escape into
  Unity's update or render loop.

The panel is drawn with IMGUI in this version only. The finished one is built from the game's own
NGUI widgets so it belongs to the interface rather than floating over it.
