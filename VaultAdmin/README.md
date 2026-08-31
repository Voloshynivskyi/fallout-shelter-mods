# Vault Admin

**A debug panel for Fallout Shelter, built into the game's own interface.**

Create dwellers, weapons and pets with full control over every attribute the game holds — names,
SPECIAL, appearance — and grant resources, boxes and items besides.

> **Status: 0.3.0.** Grants resources, boxes, weapons, outfits and junk. Dwellers are still to come, and
> the panel is a plain scaffold rather than game UI — that lands in its own change, so a failure
> there is a UI failure and nothing else.

---

## Requirements

- Fallout Shelter for PC (Steam / Bethesda / Windows Store), Unity **Mono** build
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) — **x64, Unity Mono** variant
- Tested against Fallout Shelter **2.5.1**

## Installation

1. Install BepInEx into the folder containing `FalloutShelter.exe`, run the game once, then close it.
2. Extract this archive into the same folder, so the DLL lands at `BepInEx\plugins\VaultAdmin.dll`.
3. Start the game once to generate the config, then close it.
4. Open `BepInEx\config\ovolo.falloutshelter.vaultadmin.cfg` and set `Enabled = true`.
5. Start the game and press **F8**.

**It does nothing until step 4.** Installed and left alone, this mod is inert by design: a debug
tool has no business changing anything it was not asked to.

## Configuration

| Setting | Default | Description |
|---|---|---|
| `Enabled` | `false` | Master switch. While false, nothing is read, drawn or bound |
| `ToggleKey` | `F8` | Key that opens and closes the panel, named as in `UnityEngine.InputSystem.Key`. An unrecognised name falls back to `F8` with a warning |

## Uninstalling

Delete `BepInEx\plugins\VaultAdmin.dll`.

This version writes nothing to your save, so removing it is completely safe and leaves no trace.
That will remain true of reading; once the panel starts granting things, what it grants is written
into your vault like anything else you own, and stays there.

---

## How it is built

Development is spec-driven through [OpenSpec](https://github.com/Fission-AI/OpenSpec). Specs live
in `openspec/specs/`, work in flight in `openspec/changes/`, and the checks that need a running game
in `openspec/testplan.md`.

The working directives are in `CLAUDE.md`, and four skills under `.claude/skills/` carry the
stations every change passes through: probing the game's assemblies before writing code against
them, verifying a build actually landed, proving a save write round-trips, and building UI out of
the game's own widgets.

### Reaching the game

No Harmony patches. Both entry points are singletons and everything read is public:

```
Vault : MonoSingleton<Vault>
    bool            Loaded
    VaultStorage    Storage      ->  GameResources Resources, MaxResources
    VaultInventory  Inventory    ->  List Items, int ItemCountMax

DwellerManager : MonoSingleton<DwellerManager>
    List  Dwellers,  int MaximumDwellerCount,  UniqueDwellerData[] LegendaryDwellers
```

`Vault.Loaded` is what separates "at the main menu" from "in a vault", so the panel can say so
instead of throwing.

### Two things this game does differently

**Legacy `UnityEngine.Input` throws.** The game uses the new Input System, so the hotkey goes
through `UnityEngine.InputSystem.Keyboard.current`, null-checked every frame because it is null with
no keyboard attached and again between some scene loads.

**The UI is NGUI.** The finished panel will be built from the game's own `UIPanel`, `UISprite`,
`UILabel` and `UIButton`, parented into its hierarchy. The IMGUI window in 0.1.0 is scaffolding and
goes before this capability is finished — proving "can the mod reach live state" separately from
"does a hand-built widget tree render inside someone else's UI" means a failure in the second one
belongs to the UI work alone.

## Licence

MIT.
