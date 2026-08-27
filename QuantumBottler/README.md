# Quantum Bottler

**Makes the Nuka-Cola Bottler produce Nuka-Cola Quantum instead of food and water.**

In the base game, Quantum is a premium currency you can only get from lunchboxes, quests and
events — no room produces it. This mod turns the Bottler into a real Quantum production room,
at a deliberately slow, balanced rate, so Quantum becomes something you build toward rather
than something you cheat in.

Caps from the Luck bonus are left untouched, exactly as in vanilla.

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
     and a `BepInEx` folder sitting next to the exe.
   - Launch the game once, then close it. This makes BepInEx generate its folders.
2. Extract this mod's archive into the same game folder, so the DLL lands in
   `BepInEx\plugins\NukaColaQuantumProduction.dll`.
3. Launch the game.

To confirm it loaded, open `BepInEx\LogOutput.log` and look for:

```
[Info   :Nuka-Cola Quantum Production] Nuka-Cola Quantum Production <version> loaded. Hours per bottle (size 1): L1=4 L2=3 L3=2.
```

### Finding your game folder

Steam → right-click **Fallout Shelter** → *Manage* → *Browse local files*.

## Uninstallation

Delete `BepInEx\plugins\NukaColaQuantumProduction.dll`.

Your Bottler goes back to producing food and water on the next game launch. Nothing the mod
does is written into your save — it only changes behaviour while the game is running, so
removing it is completely safe and reversible.

To remove BepInEx as well, delete `winhttp.dll`, `doorstop_config.ini` and the `BepInEx` folder.

---

## Production rate

```
Quantum per hour = (1 / HoursForThisLevel) × roomSize × workerEfficiency
```

- `roomSize` is how many segments wide the room is (1–3, from merging rooms)
- `workerEfficiency` is the game's own stat-based efficiency (1.0 = fully staffed with maxed SPECIAL)

Time to produce one bottle, at full worker efficiency:

| Room level | Size 1 | Size 2 | Size 3 |
|-----------:|-------:|-------:|-------:|
| Level 1    | 4h     | 2h     | 1h 20m |
| Level 2    | 3h     | 1h 30m | 1h     |
| Level 3    | 2h     | 1h     | 40m    |

So a fresh one-segment room takes four hours per bottle, a level-3 one takes two, and a fully
upgraded 3-wide Bottler manages one every forty minutes — slow enough that Quantum stays worth
something.

The room accumulates a whole bottle and then waits to be collected, like any other production
room — you never collect a fraction. Rushing works normally.

### In the upgrade window

The production row reads **Quantum / day** and shows the real improvement — 18 → 24 for a
three-wide room going from level 1 to level 2.

It is stated as a rate on purpose. Upgrading shortens the cycle rather than enlarging the batch, so
a per-cycle figure would read as 1 at every level, and a per-hour figure would round to zero. The
balance is unchanged; only the unit shown differs from vanilla rooms, which is why the row is
labelled rather than left as a bare number.

The Storage row is hidden: this mod withdraws the Bottler's food-and-water capacity bonus, so there
is nothing to report there.

## Configuration

Settings live in `BepInEx\config\ovolo.falloutshelter.nukaquantum.cfg`, created on
first launch. Edit it and restart the game.

| Setting | Default | Description |
|---|---|---|
| `HoursLevel1` | `4` | Hours a **level 1**, one-segment room takes per bottle at full efficiency. |
| `HoursLevel2` | `3` | Same for a **level 2** room. |
| `HoursLevel3` | `2` | Same for a **level 3** room. |
| `SuppressCapsBonus` | `false` | Set to `true` to also remove the vanilla Luck-based caps bonus, making the Bottler yield Quantum and nothing else. |
| `QuantumIconOverride` | *(empty)* | UI sprite name for the Bottler's icon. Empty uses the built-in `Icon_NukaQuantum`. |
| `LogProduction` | `false` | Logs the computed rate for each Bottler, plus the full resource-to-sprite table, to `BepInEx\LogOutput.log`. |

Want it faster? Lower the hour values. Want Quantum to stay rare? Raise them.

---

## How it works

For anyone curious or wanting to build on this, here's what the mod actually patches.

Resource production in Fallout Shelter is **data-driven**, not hardcoded per room type.
`ProductionRoom.GetProducedResources()` reads the amounts straight off the room level asset:

```
GetProducedResources() = ProductionLevel.m_resourcesProduced
                       × workerEfficiency
                       ÷ 60                     → a per-second rate
```

That rate is accumulated into the room's own `Storage` by `ProductionRoomWorking.WorkTaskDone()`
until the storage is full; collecting just drains that storage into the vault. Conveniently,
the engine's `GameResources` container **already has a Quantum field** — the Bottler's data
simply leaves it at zero. So no new machinery is needed, only different numbers.

The mod applies eleven Harmony patches:

1. **`ProductionRoom.GetProducedResources` (postfix)** — for `ERoomType.NukaCola`, clears the
   produced-resources builder and writes the computed Quantum rate instead.

2. **`ProductionRoom.OnChangeRoomLevel` (postfix)** — replaces the room's storage capacity with
   one that caps Quantum and *nothing else*. This part is essential and non-obvious:
   `IsWorkCompleted()` is really `RoomStorage.IsFilled()`, which requires **every** resource to
   reach its cap. The vanilla food/water caps come from the level's `m_resourcesReserve`, so
   once food/water production is removed those caps can never be met and **the room stalls
   forever** — no production timer, output only via rushing. Zeroing them fixes it.

   Note also that `Storage.SetMaxResources` stores the *reference* it is handed, and the game
   passes it the level's shared `m_resourcesReserve` asset — so the mod assigns a fresh
   `GameResources` rather than writing into that shared object.

3. **`ProductionRoom.GetCurrentReserve` (postfix)** — reports a Quantum-only reserve. The
   production timer, the collect button and the room management panel all show
   `GetCurrentReserve().MaxResourceValue()`, which otherwise still reads the vanilla food/water
   figures off the level asset. All three call sites are UI-only, so this does not affect
   production. The icon beside those numbers is a separate problem — see the next patch.

4. **`RoomInfo.get_MainIconPlain` (postfix)** — swaps the Bottler's icon for the Quantum one.
   The icon on every production readout comes from this room asset field, *not* from what the
   room produces — for the Bottler it's a single combined food + water sprite.

   The sprite name (`Icon_NukaQuantum`) is hardcoded, which deserves an explanation. Icons are
   looked up by **bit flag** (`ResourceParameters.m_resourcesIcons` keyed by
   `EResourceExtensions.GetFlagValue`), and one entry can represent a whole *combination* of
   resources. That table has no entry for `NukaColaQuantum` at all, and a miss does not throw —
   it silently falls back to an unrelated sprite. Asking the game for the Quantum icon therefore
   returns `Icon_WeaponGreen`, the crafted-weapon icon. (`Lunchbox`, `MrHandy` and `PetCarrier`
   are missing outright and throw `KeyNotFoundException`.) Enable `LogProduction` to dump the
   whole table if you are adapting this to another resource.

5. **`ResourceParameters.GetResourceData(List<EResource>)` (postfix)** — fixes the icon on the
   "ready to collect" tapping message, which is a *separate* path from the room icon:
   `UIRoomTappingMessage.SetAsResourceIcon` reads its sprites out of the returned `ResourceData`.
   That lookup ORs the resources into one flag and hits the same missing-Quantum fallback, so it
   returned the crafted-weapon entry — a pistol floating over the room. A proper Quantum
   `ResourceData` is supplied instead, carrying over the original collect sound.

   Its `m_tappingIconType` is deliberately left `None`: `SetAsGenericIcon` switches on that value
   and for several types overwrites the sprite it was just handed, while `None` skips it.

6. **`ResourceParameters.GetIconName(EResource)` (postfix)** — same fallback problem on the
   single-resource overload, used by the rush window among others.

7. **`Room+RoomBuilding.GetFinishBuildingResources` (prefix)** — stops the Bottler handing out a
   free batch of food and water when it is built. Rooms grant starter resources on finishing
   construction, which made sense while this one produced food and water.

8. **`ProductionRoom.OnChangeRoomLevel` (postfix)** — withdraws the vault storage bonus. Production
   rooms register a `StorageModifier` raising the vault's capacity for what they make, and the
   Bottler's is still the vanilla food-and-water one — which a Quantum room should not be enlarging.

9. **`ProductionLevel.GetUpgradeRoomGUILabel` (postfix)** — fixes the upgrade window's production
   row. The figure there is derived from the level's own `m_resourcesReserve`, which for the Bottler
   is still its vanilla food-and-water data. Two details matter: the substitution is scoped to the
   production row only — an earlier version rewrote every numeric cell and clobbered the storage row
   — and the UPGRADED column must be computed from the `nextLevel` argument, not the room's current
   level, or both columns show the same number.

10. **`RoomUpgradeWindow.UpdateLabel` (prefix)** — hides the Storage row, since this mod withdraws
    the Bottler's capacity bonus. It is hidden exactly the way the game hides a row a level says it
    does not use: deactivate it and report false.

11. **`ProductionRoom.GetLuckNukaProduced` (postfix)** — optional, off by default. The Luck-based
   caps bonus is awarded on a path entirely separate from `GetProducedResources`: `CollectResources`
   adds it straight into resource index 0. Only `SuppressCapsBonus = true` zeroes it.


## Building from source

No .NET SDK needed — the build uses the C# compiler bundled with the .NET Framework.

```powershell
.\build.ps1
.\build.ps1 -GamePath "C:\Path\To\Fallout Shelter" -Install
```

Outputs `build\NukaColaQuantumProduction.dll` and a ready-to-upload archive in `dist\`.

`..\tools\` contains the small reflection-based IL disassembler and call-scanner used to reverse
engineer the production logic, in case you want to explore `Assembly-CSharp.dll` yourself.

## Compatibility

- Only touches rooms of type `ERoomType.NukaCola`; every other room behaves normally.
- Doesn't modify game files or save files. Saves stay vanilla-compatible.
- A Fallout Shelter update may rename or restructure the patched methods, which would stop the
  mod from loading. Check `BepInEx\LogOutput.log` if it goes quiet after an update.
- Steam's *Verify integrity of game files* removes BepInEx; just reinstall it.

## License

MIT — see [LICENSE](LICENSE).
