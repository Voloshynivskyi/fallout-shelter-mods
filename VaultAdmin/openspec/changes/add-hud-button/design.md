# Design — a button in the HUD

## What is cloned

```
MainScene_Root/GUI/VaultHUDWindow/VaultHUDPanel/7 BottomLeft/BTN Camera
```

`7 BottomLeft` is one of the nine anchors NGUI arranges a panel on. The camera button is the only
occupant, so a sibling beside it has room.

The clone is parented to the same transform with `SetParent(parent, false)` — keeping world position
would place it somewhere off screen, because NGUI works in its own scaled space — and then offset
along x so the two do not overlap.

## Stripping the clone

A clone brings every component the original had, including whatever takes the screenshot. Those are
removed, leaving the visual pieces and the collider that makes it clickable. What was removed is
logged, so a button that does nothing can be told apart from a button that still takes screenshots.

Then `onClick` is cleared and one delegate of ours is added.

## Placing it once

The HUD is rebuilt when the vault is reloaded, and a clone made each time would stack buttons on top
of each other. The button is found by name before being created, so the second attempt finds the
first and stops.

Because the HUD does not exist at load, and because it comes and goes, the check runs on a slow
timer rather than once: the same watchdog shape the room props use, and for the same reason.

## Failure

If the path does not resolve, the mod logs exactly what it looked for and carries on. The hotkey is
unaffected. A debug panel that breaks the game's HUD to add a button to it would be a bad trade.
