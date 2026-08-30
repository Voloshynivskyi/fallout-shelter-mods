# Changelog

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
