# Changelog

## 1.4.1

- **Fixed: the game died while loading a save.** Two defects in 1.4.0's new part system combined
  into one. A part name that could not be found was not remembered as unfindable, so the search ran
  again every time; and a room with no parts attached never looked done, so it was retried on every
  frame of the fifteen-second wait for its sections. Together that meant walking every object loaded
  in the game, several times a second, during a vault load.
- **The search is now bounded.** Parts are looked for among the rooms standing in the vault rather
  than among every loaded object. `Resources.FindObjectsOfTypeAll` also returns assets that are
  mid-load and objects belonging to no scene, which is not a safe thing to walk while a vault is
  loading. Names are resolved once, at most three times a session, hits and misses alike.
- **A part that cannot be found now lists what can.** The log prints the meshes this vault actually
  offers, so choosing a part is a matter of reading a list rather than guessing a name.
- Attaching parts can no longer throw into Unity's update loop.

## 1.4.0

- **The room is built from parts of several rooms instead of wearing one room's model.** The default
  body is now the Weapon Factory — presses and anvils, the closest thing the game has to a mint —
  and meshes borrowed from other rooms are parented onto it. The result is a silhouette the base
  game does not have, assembled entirely from stock assets.
- **New `ExtraParts` setting.** A semicolon-separated list of `MeshName [@ x,y,z [@ rx,ry,rz [@
  scale]]]`. Only the mesh and its materials are copied onto a bare object — no scripts, no
  animators — so a borrowed part cannot bring another room's logic into this one. A part with no
  coordinates lands in the middle of the room.
- **Strong Nuka-Cola red by default** (`#E01B24` at 0.85 strength) rather than the previous muted
  brick.
- Added parts are removed again from any room that is not a Caps Foundry. Rooms come from a shared
  pool, so one of ours can come back as a Power Generator, and it must not keep our machinery.

Existing installs keep their old settings: BepInEx does not overwrite a config that already exists.
Delete `BepInEx\config\ovolo.falloutshelter.capsfoundry.cfg` to pick up the new look.

## 1.3.2

Nothing here changes the room, its balance, its appearance or its save data. Upgrading from 1.2.0 is
a straight DLL swap. (1.3.0 and 1.3.1 were never released; this entry covers them.)

- **The mod no longer copies your saves.** The automatic backup was added while the room was still
  under construction and could corrupt a vault. It is stable now, and a mod that duplicates your
  save folder on every launch is doing something the game never asked it to. Any folders already in
  `%LocalAppData%\FalloutShelter\ModBackups\` are left alone — delete them when you want to.
  **The uninstall rule has not changed: sell every Caps Foundry before removing the DLL**, or that
  vault will not load until you put the mod back.
- **Fixed: a room could be painted more than once and grow darker each time.** The room borrows the
  Nuclear Reactor's model and is recoloured at runtime; the guard against recolouring the same
  material twice keyed on an id that Unity replaces when the colour is written back, so it could
  stop recognising its own work. As the colour is applied by multiplication, each repeat darkened
  the room further.
- **Faster while a room is appearing.** Whether a mesh still needs colouring is now decided without
  cloning its materials, so the wait for a room's sections to load no longer allocates on every
  frame.
- **Logging is useful instead of noisy.** Four lines a session — version, registration, pricing,
  build-menu injection — which is what a bug report needs. Everything that repeats per room or per
  upgrade now needs `VerboseLogging = true` in the config, and costs nothing while it is off.
  Warnings and errors are always logged.
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
