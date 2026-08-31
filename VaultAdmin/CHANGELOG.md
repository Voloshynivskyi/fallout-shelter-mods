# Changelog

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
