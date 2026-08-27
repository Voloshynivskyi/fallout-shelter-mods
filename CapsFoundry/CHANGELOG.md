# Changelog

## 1.3.0

- **Release build: the diagnostics are gone.** Everything that existed to investigate the game
  rather than to run the room has been removed.
  - **The automatic save backup has been removed.** It was added while the room was still crashing
    saves; the room is stable now, and a mod that copies your save folder on every launch is doing
    something the game never asked it to. Existing backups in
    `%LocalAppData%\FalloutShelter\ModBackups\` are left alone — delete them yourself when you no
    longer want them.
  - Removed the shader and renderer reports that fired when tinting could not find a colour slot.
    A single warning is still logged if the room ends up untinted.
  - Startup logging is one line. Warnings and errors are untouched — they only appear when something
    is actually wrong.
- No change to the room, its balance, its appearance or its save data. Upgrading from 1.2.0 is a
  straight DLL swap.

## 1.2.0

- **The room no longer distorts the vault statistics screen.** Its output was being counted into the
  production breakdown under the resource it uses internally, so the energy figures were wrong.
- **One broken patch no longer disables the mod.** Patches are applied class by class instead of all
  at once, so if a game update breaks one of them the rest still work and the failure is named in the
  log.

## 1.1.0

- **The room now looks like the Nuclear Reactor by default** (`VisualDonor = Energy2`) instead of
  the Power Plant. It suits a foundry better, and the old default was the first thing players asked
  about.

  Existing rooms restyle themselves on the next game start — rooms are rebuilt from the object pool
  whenever a vault loads. Set `VisualDonor = Geothermal` to keep the old look.

  (Earlier documentation claimed the art was fixed at build time and that you had to sell and
  rebuild. That was wrong.)

## 1.0.5

- **Fixed: CURRENT and UPGRADED showed the same number.** The figure was always computed from the
  room's current level; the game passes the next level as a separate argument, which was ignored, so
  the upgrade appeared to change nothing.
- **Fixed: the Storage row showed the production figure.** The rewrite applied to every numeric cell
  in the table rather than just the production row.
- **The Storage row is now hidden entirely.** This room adds no vault capacity — that bonus is
  refused when it is built — so the row advertised a figure that does not exist.
- The production row is labelled **Caps / hour**, so the unit is clear in game rather than only in
  this readme.

## 1.0.2

- **Fixed: the upgrade window advertised the donor room's output.** The figure comes from the
  level's own storage data, which for this room is still the power plant's, so it showed energy.
- The figure is now caps **per hour** rather than per cycle. Upgrading shortens the cycle instead of
  enlarging the batch, so a per-cycle number is identical at every level and the upgrade looks like
  it does nothing. Balance is unchanged — only the unit shown.

## 1.0.1

- Author id shortened to `ovolo`. This renames the settings file to
  `BepInEx\config\ovolo.falloutshelter.capsfoundry.cfg` — if you are upgrading, rename your old
  file to match or the mod will start from defaults.

## 1.0.0

First public release. The Caps Foundry is a fully working production room: it appears in the build
menu, unlocks alongside the Nuka-Cola Bottler, is priced like it, borrows the nuclear reactor's art,
produces caps, and survives a save/load round trip.

Notable things fixed on the way there, each of which is a separate system keying off the room type:

- **Scene loading** — every room type loads a Unity scene named `"Logic" + type`. There is no
  `LogicProteinBar`, so placing the room threw `ArgumentException: The scene is invalid`. The id is
  now redirected to the visual donor's.
- **Object pools** — `PreloadRoom` looks up a pool named `type + mergeCount`, which is created by the
  room's own scene. The game logs the miss and then dereferences the null pool anyway, force-crashing
  on placement. Pool names are redirected, with a fallback when the configured donor has no pool.
- **Room identity** — `AddRoom` takes the room object from the donor's pool, complete with the
  donor's `RoomInfo`, so the built room reported the donor's type and every patch keyed on ours
  silently skipped it. It really was a power generator. `RoomInfo` is now swapped back after
  construction, and the cached display name refreshed with it.
- **Caps as output** — the game assumes rooms never produce caps: `GetPositiveResources` excludes
  them and `MaxResourceValue` skips their slot, which left the icon lookup with an empty list and
  crashed while a save containing the room was deserialised. The cycle now runs on a carrier
  resource and converts at collection.
- **Locked entries** — a locked room is drawn by `SetNotAvailable(Objective)`, resolved from a table
  the adopted type is absent from; the null crashed the game as the entry scrolled into view. The
  room now borrows another room's unlock objective, so it locks and unlocks properly.
- **Stalled cycles** — `IsWorkCompleted()` is `RoomStorage.IsFilled()`, which requires *every*
  resource to reach its cap, so the storage cap is replaced with one covering the carrier alone.
- **Ready-to-collect icon** — the bubble showed the donor's resource. Relabelling it as caps is
  only safe alongside a second fix: `GetPositiveResources` returns *null* rather than an empty list
  and skips caps by default, and `GetResourceData` dereferences that null, so a caps-only room
  crashed the moment it finished a cycle.
- **Colouring** — the room body uses a shader whose tint lives in `_LightmapModulation`, not
  `_Color`, and its meshes hang off `RoomSections` rather than the room object. Earlier attempts
  only ever tinted particle effects. The tint is now applied once per material and renormalised to
  the original luminance, so it recolours without darkening.
- **Upgrade cost** — cloning brought across the donor's per-level upgrade prices, and the values
  are zeroed again somewhere between registration and the vault loading, so a saved room reported a
  free upgrade. Costs are copied from the price source room and restored on demand when a room finds
  them missing.
- **Level data binding** — a room resolves its current `RoomLevel` during construction, while still
  wearing the donor's `RoomInfo`, so a restored room kept the donor's level object. It is now
  rebound to the clone's own level data.
- **Vault storage** — the clone inherited the donor's storage modifier, quietly raising the vault's
  energy capacity. A caps room should add no storage, so the modifier is cleared.
- **Inherited donor perks** — the room handed out the power plant's free build resources and
  raised the vault's energy capacity. Both are decided while the room still wears the donor's
  identity, so clearing them on the clone had no effect; both are now suppressed at the source.
- **Registration timing** — registration is hooked on `ParameterDataMgr.OnAwake` rather than a
  delayed poll, which was a race against vault loading.

Also included:

- **Automatic save backups** on every game start, ten sets retained, independent of the `Enabled`
  switch. A vault containing this room cannot be loaded without the mod, so this is a safety net
  rather than a convenience.
- Configurable art donor, tint, price source, unlock source, room name, and per-level cycle times.
