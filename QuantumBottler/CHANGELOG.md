# Changelog

## 1.12.2

No change to production, icons or balance. Upgrading from 1.11.0 is a straight DLL swap. (1.12.0 and
1.12.1 were never released; this entry covers them.)

- **Logging is useful instead of noisy.** Two lines a session — version with the configured rate, and
  the icon sprite in use — which is what a bug report needs. The per-room rate, which the game asks
  for constantly, now needs `VerboseLogging = true` and costs nothing while it is off. Warnings and
  errors are always logged.
## 1.11.0

- **One broken patch no longer disables the mod.** Patches are applied class by class instead of all
  at once, so if a game update breaks one of them the rest still work and the failure is named in the
  log.

## 1.10.5

- **Fixed: CURRENT and UPGRADED showed the same number.** The figure was always computed from the
  room's current level; the game passes the next level as a separate argument, which was ignored, so
  the upgrade appeared to change nothing.
- **Fixed: the Storage row showed the production figure.** The rewrite applied to every numeric cell
  in the table rather than just the production row.
- **The Storage row is now hidden entirely.** The Bottler's food-and-water capacity bonus is
  withdrawn by this mod, so the row advertised a figure that does not exist.
- The production row is labelled **Quantum / day**, so the unit is clear in game rather than only in
  this readme.

## 1.10.2

- **Fixed: the upgrade window advertised food and water.** The figure comes from the level's own
  storage data, which for the Bottler is still its vanilla food-and-water figure.
- The figure is now bottles **per day** rather than per cycle. Upgrading shortens the cycle instead
  of enlarging the batch, so a per-cycle number reads as 1 at every level; per hour would round to
  zero. Balance is unchanged — only the unit shown.

## 1.10.1

- Author id shortened to `ovolo`. This renames the settings file to
  `BepInEx\config\ovolo.falloutshelter.nukaquantum.cfg` — if you are upgrading, rename your old
  file to match or the mod will start from defaults.

## 1.10.0

- **The Bottler no longer hands out free food and water when built.** Rooms grant starter resources
  on finishing construction, which made sense while this one produced food and water.
- **The Bottler no longer raises the vault's food and water capacity.** Production rooms register a
  storage modifier for what they make; the vanilla one is withdrawn now that the room makes Quantum.

## 1.9.0

- **Removed the in-game settings window.** Settings are edited in the config file.
- **`HoursPerBottle` and the level multipliers replaced by `HoursLevel1/2/3`** (`4` / `3` / `2`).
  Stating each level's time directly avoids awkward multipliers — three hours from a four-hour base
  would have meant a factor of 1.333.

  **Upgrading from 1.8.0 or earlier:** delete
  `BepInEx\config\ovolo.falloutshelter.nukaquantum.cfg` to regenerate a clean config.

## 1.8.0

- Default `HoursPerBottle` raised from `3` to `4`, so a level-1 room takes four hours per bottle
  and a level-3 one takes two.

## 1.7.1

- **Fixed: the F1 settings hotkey did nothing.** The game runs on the new Input System, under which
  the legacy `UnityEngine.Input` API throws instead of reporting key state — the guard caught it,
  but the key was dead. Now read through `Keyboard.current`; `ToggleUIKey` takes
  `UnityEngine.InputSystem.Key` names.

## 1.7.0

- **`BasePerHour` replaced by `HoursPerBottle`** (default `3`). Rates below one bottle per hour
  meant configuring the mod in fractions like `0.33`; stating the time per bottle reads far more
  naturally. The resulting production is identical — only the way you express it changed.
- The settings window and the log line now report time-per-bottle instead of bottles-per-hour.

  **Upgrading from 1.6.0 or earlier:** the old `BasePerHour` key is ignored. Delete
  `BepInEx\config\ovolo.falloutshelter.nukaquantum.cfg` to regenerate a clean config, or
  set `HoursPerBottle` by hand — it is simply `1 / BasePerHour` (`0.33/h` becomes `3` hours).

## 1.6.0

- **In-game settings window.** Press **F1** (rebindable via `ToggleUIKey`) to change the rate,
  the level multipliers and the toggles without leaving the game. Changes apply immediately and
  are written to the config file. Accepts both `0.33` and `0,33` regardless of system locale.
- **Fixed: the collect animation still showed a pistol.** The "ready to collect" tapping message
  is a separate path from the room icon — `UIRoomTappingMessage.SetAsResourceIcon` reads its
  sprites from `GetResourceData(List<EResource>)`, which hit the same missing-Quantum fallback and
  returned the crafted-weapon entry. A proper Quantum `ResourceData` is now supplied, keeping the
  original collect sound.
- `ResourceParameters.GetIconName(EResource)` is patched too, covering the rush window and any
  other single-resource icon lookup.
- Default `BasePerHour` lowered from `0.5` to `0.33`.

## 1.5.0

- **Fixed: the Bottler showed a pistol icon.** Asking the game for the Quantum icon does not
  work — `ResourceParameters` has no entry for `NukaColaQuantum`, and because lookups are by bit
  flag with a silent fallback, it returned `Icon_WeaponGreen` (the crafted-weapon sprite). The
  correct sprite, `Icon_NukaQuantum`, is now used directly. It can be changed via the new
  `QuantumIconOverride` setting.
- Diagnostics moved to the BepInEx logger; `UnityEngine.Debug.Log` never reached `LogOutput.log`.
- The resource-to-sprite table is dumped when `LogProduction` is enabled — useful to anyone
  adapting this mod to a different room or resource.

## 1.4.0

- **Fixed: the Bottler still showed the food/water icon.** Every production readout takes its
  sprite from `RoomInfo.MainIconPlain`, which for the Bottler is the combined food + water icon
  baked into the room asset — it is not derived from what the room actually produces. It is now
  swapped for the Quantum icon, scoped to `ERoomType.NukaCola` via `RoomInfo.m_eRoomType`.
- The Quantum sprite name is resolved once from the game's own resource table and logged, so a
  failed lookup is visible rather than silently falling back to an unrelated icon.

## 1.3.0

- **Fixed: the room's production readouts still showed the vanilla food/water amounts.** The
  production timer, the collect button and the room management panel all display
  `GetCurrentReserve().MaxResourceValue()`, which returns the level's `m_resourcesReserve` — data
  the mod had not touched. `GetCurrentReserve` is now reported as Quantum-only for the Bottler.
  All three call sites are UI-only, so production itself is unaffected.

## 1.2.0

- **Fixed: the Bottler never finished a production cycle.** No timer was shown and resources
  only appeared when the room was rushed. `IsWorkCompleted()` is `RoomStorage.IsFilled()`, which
  requires every resource to reach its cap — and the vanilla food/water caps could never be met
  once their production was removed. The room now gets a storage cap covering Quantum only.
- **Fixed: a shared game asset was being mutated.** `Storage.SetMaxResources` stores the
  reference it is given, and the game hands it the room level's shared `m_resourcesReserve`.
  The mod now assigns its own `GameResources` instead of writing into that object.
- Re-applies the storage cap on `OnChangeRoomLevel`, so loading a save, upgrading or merging
  the room no longer reverts it.
- Storage capacity is rounded to a whole number, since `SetMaxResources` floors it internally.
- Default `BasePerHour` lowered from `1.0` to `0.5`.

## 1.1.0

- Added `SuppressCapsBonus` (default `false`) to optionally remove the vanilla Luck-based caps
  bonus, which `CollectResources` awards on a path separate from `GetProducedResources`.

## 1.0.0

- Initial release: the Nuka-Cola Bottler produces Nuka-Cola Quantum instead of food and water,
  scaled by room level, room size and worker efficiency.
