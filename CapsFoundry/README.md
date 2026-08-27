# Caps Foundry

**Adds a new buildable room to Fallout Shelter that produces caps.**

The Foundry is a full production room: built from the build menu, staffed like any other room,
upgradeable through three levels, mergeable up to three segments wide, and unlocked alongside the
Nuka-Cola Bottler. Luck decides how fast it runs.

It also takes a **backup of every vault save each time the game starts** — see [Save safety](#save-safety).

---

## Requirements

- Fallout Shelter for PC (Steam / Bethesda / Windows Store), Unity **Mono** build
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) — **x64, Unity Mono** variant
- Tested against Fallout Shelter **2.5.1**

## Installation

1. Install BepInEx if you don't have it:
   - Download `BepInEx_win_x64_5.4.x.x.zip` from the
     [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases).
   - Extract it into your **Fallout Shelter game folder** — the one containing
     `FalloutShelter.exe`. You should end up with `winhttp.dll`, `doorstop_config.ini`
     and a `BepInEx` folder next to the exe.
   - Launch the game once, then close it, so BepInEx creates its folders.
2. Extract this mod's archive into the same game folder, so the DLL lands in
   `BepInEx\plugins\CapsFoundry.dll`.
3. Launch the game.

To confirm it loaded, open `BepInEx\LogOutput.log` and look for:

```
[Info   :Caps Foundry] Registered 'Caps Foundry' as ProteinBar (cloned from Geothermal); registry 29 -> 30 entries.
```

### Finding your game folder

Steam → right-click **Fallout Shelter** → *Manage* → *Browse local files*.

## ⚠️ Uninstallation — read this first

**A vault containing a Caps Foundry cannot be loaded without this mod.** The room is saved under a
room type the base game has no assets for, so removing the DLL makes that vault fail to load.

To uninstall safely:

1. **Sell every Caps Foundry** in every vault.
2. Save and quit.
3. Delete `BepInEx\plugins\CapsFoundry.dll`.

If you already removed the mod and a vault will not open, reinstall the mod, or restore a save from
`%LocalAppData%\FalloutShelter\ModBackups\` (see below).

## Save safety

Every time the game starts, the mod copies all `Vault*.sav` files to:

```
%LocalAppData%\FalloutShelter\ModBackups\<yyyyMMdd-HHmmss>\
```

The ten most recent sets are kept. This runs before any patching and regardless of the `Enabled`
setting, so the safety net is there even with the room switched off. To restore, close the game and
copy a save back over the original in `%LocalAppData%\FalloutShelter\`.

---

## Production rate

```
caps per cycle = CapsPerBatch × roomSize
cycle length   = HoursForThisLevel ÷ workerEfficiency
```

A wider room produces proportionally more per cycle rather than cycling faster.

| Room level | Caps per cycle (size 1 / 2 / 3) | Cycle |
|-----------:|--------------------------------:|------:|
| Level 1    | 200 / 400 / 600                 | 4h    |
| Level 2    | 200 / 400 / 600                 | 3h    |
| Level 3    | 200 / 400 / 600                 | 2h    |

At full efficiency a maxed 3-wide Foundry yields 600 caps every two hours.

### In the upgrade window

The production row reads **Caps / hour** and shows the real improvement — 150 → 200 for a
three-wide room going from level 1 to level 2.

It is stated as a rate on purpose. Upgrading shortens the cycle rather than enlarging the batch, so
a per-cycle figure would be identical at every level and the upgrade would look pointless. The
balance is unchanged; only the unit shown differs from vanilla rooms, which is why the row is
labelled rather than left as a bare number.

The Storage row is hidden: this room adds no vault capacity, so there is nothing to report.

## Configuration

Settings live in `BepInEx\config\ovolo.falloutshelter.capsfoundry.cfg`, created on first
launch. Edit it and restart the game.

| Setting | Default | Description |
|---|---|---|
| `Enabled` | `true` | Master switch. When false the room is never created and nothing is changed. |
| `RoomName` | `Caps Foundry` | Name shown in game. |
| `BuildPriceCaps` | `0` | Build price override. `0` copies the price from `PriceSourceRoom`. |
| `PriceSourceRoom` | `NukaCola` | Room whose build price this room copies. |
| `UnlockLikeRoom` | `NukaCola` | Room whose unlock condition this room copies. Empty = always available. |
| `CapsPerBatch` | `200` | Caps per completed cycle for a one-segment room. |
| `HoursLevel1` | `4` | Hours per cycle at level 1, full efficiency. |
| `HoursLevel2` | `3` | Same at level 2. |
| `HoursLevel3` | `2` | Same at level 3. |
| `VisualDonor` | `Energy2` | Room whose 3D art this room borrows — `Energy2` is the Nuclear Reactor. **Must be a Production room.** |
| `TintColor` | `#C0392B` | Colour applied to the room. |
| `TintStrength` | `0.55` | How far to push towards the tint, 0–1. |
| `TintBrightness` | `1.15` | Brightness after tinting, relative to stock. Above 1 is lighter. |

**Appearance settings apply on the next game start.** Rooms are rebuilt from the object pool each
time a vault loads, so changing the art or the colour restyles rooms you already have — restart the
game and they will look different. No selling or rebuilding needed.

### Choosing the look

`VisualDonor` must name a room whose assets are actually loaded, which in practice means a room you
can build. Crafting rooms cannot be used: the game would hand back a crafting room object and
production would never start. Working choices:

`Energy2` (Nuclear Reactor · default) · `Geothermal` (Power Plant) · `WaterPlant` · `Water2` ·
`Cafeteria` · `Hydroponic` · `NukaCola`

**Changing `VisualDonor` restyles rooms you already have** — just restart the game. Rooms are
rebuilt from the object pool every time a vault loads, and the redirect reads this setting at that
moment, so no selling or rebuilding is needed.

Seasonal rooms such as `UltraciteMining` do **not** work — their assets are absent from a normal
game. If the configured donor has no loaded assets, the mod falls back to `Geothermal` and logs a
warning rather than crashing.

---

## How it works

Fallout Shelter has no mod support, and a genuinely new room cannot be conjured from nothing: room
types live in a compiled `ERoomType` enum, and the art lives in Unity assets. What *can* be done is
adopt one of the enum values the game ships but never uses.

This mod adopts **`ProteinBar`**, a leftover from an early version. It was confirmed safe to take:
`GetRoomInfoForType` returns no prefab for it, it appears in the game files only as an enum name,
and it is present in no save. Nothing in the base game can create or reference it.

Existing rooms are safe **by construction**, not by inspection: no shipped `RoomInfo` is modified,
the room registry array is only appended to, and `Instantiate` deep-copies the prefab so the clone's
levels and materials are its own.

Getting the room to actually work meant satisfying several independent systems, each keying off the
room type in its own way:

1. **Registry** — the clone is appended to `ParameterDataMgr.RoomDataPrefabs`, hooked on that
   manager's `OnAwake`. Delayed registration is a race: a vault can start loading first, and a save
   containing the room then has no prefab to resolve.

2. **Build menu** — `FillAvailableBuildList` builds the menu from `UIRoomBuildList`'s own
   `m_roomInfo` array, *not* from the registry, so the clone is appended there too. Two parallel
   arrays are indexed by room type (`m_order[(int)type - 1]` for sorting,
   `m_roomAvailableConstruction[(int)type]` for availability) and are grown if too short.

3. **Scene** — every room type loads a Unity scene named `"Logic" + type`, built in
   `AssetManager.StartSceneLoad`. There is no `LogicProteinBar`, so the id is redirected to the
   visual donor's.

4. **Object pools** — `PreloadRoom` looks up a pool named `type + mergeCount`. Those are created by
   the room's scene, so ours never existed; the game *logs* the miss and then dereferences the null
   pool anyway, force-crashing on placement. Pool names are redirected too, with a fallback if the
   configured donor has no pool.

5. **Identity** — `AddRoom` takes the room object from `PoolMgr.GetPool(type + "Room")`, which the
   redirect points at the donor, so the game hands back a donor room carrying the *donor's*
   `RoomInfo`. The built room reported the donor's type, and every patch keyed on ours silently
   skipped it — it really was a power generator. `RoomInfo` is swapped back after construction, and
   the cached display name in `m_RoomName` is refreshed along with it.

6. **Unlocking** — a locked entry is drawn by `SetNotAvailable(Objective)`, and that objective is
   resolved from `m_unlockRoomObjectives`, a table the adopted type is absent from. Passing null
   crashed the game as soon as the entry scrolled into view. The mod registers a pairing pointing at
   another room's objective, so the entry locks, shows progress and unlocks exactly like that room.

### Not inheriting the donor's perks

A cloned room inherits more than art. Two donor behaviours had to be suppressed outright, and both
are examples of the same trap: they are decided *while the room still wears the donor's identity*,
so clearing the values on the clone came too late.

- **Free build resources** — rooms grant starter resources on finishing construction, read from
  `RoomInfo.buildingResources`. A Foundry cloned from a power plant handed out free energy, icons
  and all. `Room+RoomBuilding.GetFinishBuildingResources` is skipped for this room instead.
- **Vault storage bonus** — production rooms register a `StorageModifier` raising the vault's
  capacity for what they produce, so the Foundry was quietly enlarging the energy store. The
  registration is refused for the moment `AddRoom` spends building one of our rooms.

### Producing caps

The room does **not** accumulate caps. The game treats caps as a bonus that rooms never produce:
`GetPositiveResources` excludes them by default and `MaxResourceValue` skips their slot, so a
caps-producing room left the icon lookup with an empty resource list and crashed while a save
containing it was being deserialised.

Instead the work cycle runs on an ordinary carrier resource, and the amount is converted to caps in
`GetResourcesWithBonuses` — the point every collection path passes through, online and offline.

`IsWorkCompleted()` is really `RoomStorage.IsFilled()`, which requires *every* resource to reach its
cap, so the storage cap is replaced with one covering the carrier and nothing else. Note also that
`Storage.SetMaxResources` stores the reference it is handed, and the game passes it the level's
shared asset — so a fresh `GameResources` is assigned rather than the existing one edited.

### Cosmetic notes

The room keeps the visual donor's own particle effects — with `Energy2` that means the reactor's
electrical arcs. They are part of its art, not a sign of the room doing anything with energy.

### The ready-to-collect bubble

The bubble over a finished room advertises caps rather than the carrier resource, which needs two
patches working together — and is a good illustration of how unforgiving this codebase is about
resources it never expected a room to produce.

`GameResources.GetPositiveResources` returns **null**, not an empty list, when nothing qualifies,
and it deliberately skips caps unless explicitly asked for them. `SetAsResourceIcon` feeds that
result straight into `GetResourceData`, which dereferences it with `.Count`. So simply relabelling
the bubble as caps crashed the game the moment a room completed a cycle. Returning a real list for
that case is the fix; caps do have an icon (`Icon_nukacapsGreen`), it just never reached the lookup.

### Colouring

The room body uses the `Underground/Rooms/FakeDynamicLightmap` shader, whose tint lives in
`_LightmapModulation`; it has no `_Color` at all. Tinting only `_Color` hit nothing but the
electricity and sparkle particle effects, which is why early attempts reported success while nothing
on screen changed. The meshes are also not children of the room object — they live in its
`RoomSections`.

The tint is applied once per material and renormalised back to the original luminance: a plain
multiply by a saturated colour removes most of two channels, which reads as a room standing in deep
shadow rather than a coloured one.

## Building from source

No .NET SDK needed — the build uses the C# compiler bundled with the .NET Framework.

```powershell
.\build.ps1
.\build.ps1 -GamePath "C:\Path\To\Fallout Shelter" -Install
```

Outputs `build\CapsFoundry.dll` and a ready-to-upload archive in `dist\`.

`..\tools\` contains the reflection-based IL disassembler and call-scanner used to work all of the
above out, in case you want to explore `Assembly-CSharp.dll` yourself.

## Compatibility

- Only the adopted room type is touched; every other room behaves normally.
- Compatible with the Quantum Bottler mod.
- A Fallout Shelter update may rename or restructure the patched methods. Check
  `BepInEx\LogOutput.log` if the room stops appearing after an update.
- Steam's *Verify integrity of game files* removes BepInEx; just reinstall it.

## License

MIT — see [LICENSE](LICENSE).
