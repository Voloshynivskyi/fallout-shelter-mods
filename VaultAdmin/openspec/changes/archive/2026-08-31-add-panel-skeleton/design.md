# Design — panel skeleton

## Reaching live state

Confirmed by reflection over `Assembly-CSharp.dll`. No Harmony patch is needed for any of it: both
entry points are singletons and every member below is public.

```
Vault : MonoSingleton<Vault>
    bool            Loaded
    VaultStorage    Storage      ->  GameResources Resources, MaxResources
    VaultInventory  Inventory    ->  List Items, int ItemCountMax

DwellerManager : MonoSingleton<DwellerManager>
    List                 Dwellers
    int                  MaximumDwellerCount
    UniqueDwellerData[]  LegendaryDwellers
```

`Vault.Loaded` is what distinguishes "at the main menu" from "in a vault", which the spec requires
the panel to handle without throwing. `LegendaryDwellers` is noted for later; this change does not
touch it.

Resource amounts come from `GameResources` indexed by `EResource`, the same access `CapsFoundry`
already uses.

## Reading singletons safely

`MonoSingleton<T>.Instance` can be null before the game has built it, and can become null again
between scenes. Every read goes through a helper that returns false rather than throwing, and the
panel renders "no vault loaded" when it does.

Nothing is cached across frames. The panel is read-only, so re-reading is cheap and staleness would
be a bug rather than an optimisation.

## Input

Legacy `UnityEngine.Input` throws in this build. The hotkey goes through
`UnityEngine.InputSystem.Keyboard.current`, confirmed present in `Unity.InputSystem.dll`.

The key is named in config as a string and resolved by parsing against `Key`. An unparseable name
logs a warning and falls back to the default, per the spec — a mistyped setting must not leave the
panel unreachable with no explanation.

`Keyboard.current` is null when no keyboard is attached, so it is null-checked every frame rather
than resolved once.

## UI, and why it is IMGUI here and only here

The finished panel must be built from the game's own NGUI widgets — all nine types are confirmed
present, and `fs-ngui` describes the approach. That is the next change.

This change deliberately uses IMGUI instead, because the two problems are separate and mixing them
makes both harder to diagnose:

1. Can the mod reach live state, toggle, and stay out of the game's way?
2. Does a widget tree built by hand render correctly inside someone else's UI?

Proving the first with the simplest possible renderer means that when the second is attempted, any
failure belongs to the UI work alone. The IMGUI scaffold is removed before this capability is
finished, and the spec's requirements are written against behaviour, not against IMGUI, so they
survive the swap unchanged.

## Failure containment

`OnGUI` and `Update` are the two entry points Unity calls. Both wrap their whole body in a
try/catch that logs and swallows, because an exception escaping either one is a crash in the
player's game rather than a bug in a debug panel.

A repeated failure logs once, not every frame: a panel that fills the log at sixty lines a second
destroys the very evidence needed to diagnose it.

## Build

`build.ps1` follows the pattern the other two mods settled on:

- `PluginVersion` is read out of the source, so the number exists in exactly one place.
- `-Install` refuses while `FalloutShelter*` is running, because the DLL is memory-mapped and the
  copy fails silently otherwise.
- After copying, the landed file's length is compared with the built one, and the script throws if
  they differ.

References: `mscorlib`, `System`, `System.Core`, `netstandard`, `BepInEx`, `Assembly-CSharp`,
`UnityEngine`, `UnityEngine.CoreModule`, `UnityEngine.IMGUIModule`, `Unity.InputSystem`. Harmony is
not referenced: this change patches nothing.
