# Changelog

## 1.0.0 — first public release

Everything below this line was found by playing the thing. The version numbers under this
entry are development history and were never released.

**The bench was minting weapons.** Dressing the preview figure calls the game's own `EquipWeapon`,
and the game does what it always does when a dweller's weapon is swapped: it hands the old one back
to storage. The old one was fabricated for a picture and never came from storage, so every re-dress
left a real weapon behind — on every gender change, every visit to the page, every creation. Thirty
or fifty of them in an afternoon. The bench now leaves the storage as it found it, and only ever
takes back an item it can prove it minted.

**The preview no longer outlives the state that made it.** It is put back to a plain random person
before the bench's choices go on it, its picture is composed again whenever it is dressed again, and
the panel opens with someone already standing there. If the game's spawner ever hands back the very
object the bench was using, that dweller is returned to its own layer and position and let go of,
rather than kept and re-randomised.

**Pets.** The grant list holds every record — one row per animal per grade, each with its own rarity
— rather than one row per animal with a grade rolled at random behind it. The constructor still
picks from the ninety-nine animals, and its animal row no longer prints a rarity that belongs to the
row beneath it.

**Dwellers arrive in a good mood**, are created as the gender the panel says, and stand about
properly: they carry no `Animator` at all, and the idle is driven through the legacy `Animation`
component with the game's own controller switched off.

**Grants confirm themselves** on the row that was pressed, and hairstyles and hair colours are named
after what they are rather than after the group they belong to.

### Before that

The interface settled on the three greens, three border weights and four text sizes it uses now, and
an independent audit of the whole file was acted on: a guard that fails closed rather than open,
restore paths for every vault-wide switch, and a build that shows its warnings instead of hiding
them behind a green success line.

## 0.10.0

The panel now opens from a button in the game's own interface, in the bottom-left corner beside the
screenshot button. The hotkey still works.

- The button is **cloned** from the screenshot button rather than built. A widget renders as nothing
  when its depth is below what it sits on, when its parent is wrong, when its atlas lacks the sprite
  it names, or when its label has no font — and none of those produce an error, so from outside the
  running game they are indistinguishable. A clone inherits every one of those from a button that
  already works.
- Whatever the original used the button for is stripped off the clone, and what was removed is
  logged, so a button that does nothing can be told from one that still quietly takes screenshots.
- Placed once however often the HUD is rebuilt, by looking for it by name first.
- `ShowHudButton` turns it off; `HudButtonOffsetX` moves it if it sits too close to its neighbour.

### Not the menu on the right

That was the first request, and the survey ruled it out on two counts. No type in the assembly owns
those buttons — the menu is assembled in the scene, so it can only be reached by path. And it
already fills the height of the screen, so another entry crowds it. The bottom-left corner holds one
button and is otherwise empty.

### Found by looking rather than guessing

The paths came from the survey added in 0.9.x: one launch produced the UI roots, the anchor grid,
every visible panel with its depth, and every button with its parent path. Reading the assembly for
that menu had found nothing, because there was nothing in the assembly to find.

## 0.8.0

Created dwellers now genuinely queue at the vault door, waiting to be let in.

The game has a call for exactly this, and building it by hand was the mistake:

```
DwellerSpawner.CreateWaitingDweller(gender, shouldAppearInTheMiddle, middleModifier, rarity, forceCreate)
DwellerSpawner.CreateUniqueWaitingDweller(data, shouldGoOutByHappiness, ..., forceCreate)
```

It creates the dweller and registers them in the waiting line in one go. The registration was the
half that was missing: setting the waiting-approval state without it left someone waiting at a door
that did not know they were there, so they could neither be admitted nor do anything else.

Three hand-rolled pieces are gone with it — choosing a spawn position, registering with the dweller
pool, and changing state by hand. All of it was reproducing, badly, what one call already did.

Level is applied afterwards, since that call does not take one, using `SetLevelAndMinExp` — the same
call the game uses for it, which moves the level and its experience together.

### How this was found

Three guesses preceded it, each costing a launch: register with the pool, then rarity, then state.
The gate diagnostic ended it in one run by printing every condition for a created dweller beside one
the game made. Every check passed on both. The only difference was the state — which is what pointed
at the queue rather than at the gates, and from there to the call that does the queueing properly.

The diagnostic should have been the first step, not the fourth. It is staying in the panel.

## 0.7.0

Created dwellers now queue at the vault door instead of appearing inside.

Asked for so a new arrival can be seen and approved rather than simply turning up somewhere. It also
looks like the answer to the equipment slots, which 0.6.2 did not fix.

`Dweller.CanDoAction` gates every interaction with a dweller, and among its checks it reads
`m_currentState`. A dweller straight out of `CreateDweller` has no state at all — not idling, not
walking, not waiting. It exists without doing anything, which is a condition the game never produces
on its own and the interface is not written for.

`SetWaitingApproval()` calls `ChangeState` with the waiting-approval state, so the dweller becomes
something rather than merely existing — and lands in the queue at the door, which is where a
newcomer belongs anyway.

Whether this is the whole reason the slots were dead is not yet proven; the gate has several
conditions and this is the one that was clearly wrong.

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
