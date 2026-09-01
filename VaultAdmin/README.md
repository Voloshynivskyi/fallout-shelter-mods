# Vault Admin

**A debug panel for Fallout Shelter, built into the game's own interface.**

Create dwellers and pets with control over every attribute the game holds — name, gender, rarity,
level, SPECIAL, hair, face, hair colour, skin, headgear, outfit and weapon — grant any item, pet,
resource or lunchbox in the game, and throw a set of vault-wide switches. The panel is built from
the game's own NGUI widgets, so it looks and behaves like the rest of the interface rather than
floating over it.

> **Status: 1.0.0** — first public release. Feature-complete for what it set out to do. No Harmony patches: everything goes
> through the game's own methods, so the vault never disagrees with itself the next time it saves.

---

## Requirements

- Fallout Shelter for PC (Steam / Bethesda / Windows Store), Unity **Mono** build
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) — **x64, Unity Mono** variant
- Tested against Fallout Shelter **2.6.0**

## Installation

1. Install BepInEx into the folder containing `FalloutShelter.exe`, run the game once, then close it.
2. Extract this archive into the same folder, so the DLL lands at `BepInEx\plugins\VaultAdmin.dll`.
3. Start the game once to generate the config, then close it.
4. Open `BepInEx\config\ovolo.falloutshelter.vaultadmin.cfg` and set `Enabled = true`.
5. Start the game and press **F8**, or use the button that appears in the top bar.

**It does nothing until step 4.** Installed and left alone, this mod is inert by design: a debug
tool has no business changing anything it was not asked to.

## What it does

**STOCK** — every resource the vault holds, with the game's own icons and its own caps. Lunchboxes,
Mr Handys and pet carriers are offered separately, because the game hands those over by a different
route than the resource counters.

**GRANT** — every weapon, outfit and piece of junk in the game, each with its real name and the
game's own description of what it does; every pet record, one row per animal per grade; rolled
dwellers of each rarity; and the named dwellers, each with the portrait the game draws for them.
Searchable, sortable by rarity or by any single SPECIAL stat. The row you press says GIVEN back to
you, so you know which of two hundred rows just fired.

**CREATE** — a bench with a live, idling figure of the dweller you are describing, and a second one
for animals. Everything is applied through the game's own customisation calls, so what you see is
what arrives at the vault door.

**POWERS** — vault-wide switches and one-off actions: full health, full happiness, level 50, ten in
every stat, finish every pregnancy, grow every child, finish every training, the three resources to
their caps, a population limit of your choosing, unlock every recipe, and switches for incidents,
the wandering pair, and rush failures. The switches persist and are re-asserted on a slow beat, and
they are put back the way they were found when the mod is disabled.

## Configuration

`BepInEx\config\ovolo.falloutshelter.vaultadmin.cfg`

### General

| Setting | Default | Description |
|---|---|---|
| `Enabled` | `false` | Master switch. While false, nothing is read, drawn or bound |
| `ToggleKey` | `F8` | Key that opens and closes the panel, named as in `UnityEngine.InputSystem.Key`. An unrecognised name falls back to `F8` with a warning |

### Powers

These persist between sessions and are re-asserted while the game runs. All are restored to what
the game had when the mod is disabled.

| Setting | Default | Description |
|---|---|---|
| `IncidentsOff` | `false` | Keeps fires, infestations and raiders from starting |
| `BottleAndCappyOff` | `false` | Keeps Bottle and Cappy from wandering the vault |
| `RushAlwaysWorks` | `false` | Rushing never fails. Sets the vault's minimum failure chance and the per-tier rise to nothing, and clears what has accumulated |
| `MaxDwellers` | `0` | How many dwellers the vault will take. Zero leaves the game's own limit alone |

### Interface

| Setting | Default | Description |
|---|---|---|
| `ShowHudButton` | `true` | A button in the game's top bar that opens the panel |
| `HudButtonImage` | `button.png` | A picture of your own for that button, read from the plugin folder. The file name only — a path is ignored |
| `HudButtonSprite` | *(empty)* | Instead of a picture: the name of a sprite from the game's own HUD atlas |
| `HudButtonTint` | *(empty)* | Hex colour for the borrowed sprite, e.g. `#14FF17` |
| `HudButtonIconScale` | `1` | Size of the picture within the button |
| `HudButtonOffsetX` | `90` | How far along the top bar the button sits |

### Diagnostics

Off by default, and worth leaving that way. They write to the BepInEx log and to a text file beside
the DLL.

| Setting | Default | Description |
|---|---|---|
| `WriteIconReport` | `false` | Writes every catalogue entry, every atlas sprite name and the appearance catalogue to `VaultAdmin-icons.txt` |
| `PreviewWholeSheet` | `false` | Draws the stand-in's whole texture sheet rather than the framed figure |

## Uninstalling

Delete `BepInEx\plugins\VaultAdmin.dll`.

**What it leaves behind.** Anything you granted or created is yours and stays in your vault, exactly
as if the game had given it to you — items, pets, dwellers, resources. The vault-wide switches are a
different matter: while the mod is running it holds incidents, the wandering pair, the rush failure
chance and the population limit at values of its choosing, and it restores each of them when it is
disabled. Removing the DLL while the game is closed is safe; the switches were never written to your
save, only held in memory while the mod was loaded.

The mod does not remove or alter anything you owned before installing it.

---

## How it is built

Development is spec-driven through [OpenSpec](https://github.com/Fission-AI/OpenSpec). Specs live in
`openspec/specs/`, work in flight in `openspec/changes/`, and the checks that need a running game in
`openspec/testplan.md`.

There is no .NET SDK in this project: `build.ps1` calls `csc.exe` out of the framework directory
directly, which means the source is C# 5. It reads the version from the source so there is only one
place to change it, refuses to install while the game is running, and proves the copy landed by
hashing both files. `tools/verify-install.py` checks the installed DLL against the build by hash and
looks for markers of features that should be in it.

### Reaching the game

No Harmony patches. Everything is reflection over the game's own singletons and methods:

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

### Three things this game does differently

**Legacy `UnityEngine.Input` throws.** The game uses the new Input System, so the hotkey goes through
`UnityEngine.InputSystem.Keyboard.current`, null-checked every frame because it is null with no
keyboard attached and again between some scene loads.

**The UI is NGUI.** The panel is built from the game's own `UIPanel`, `UISprite`, `UILabel` and
`UIButton`, parented into its hierarchy. An IMGUI scaffold remains as a fallback for when the window
cannot be built at all, so a failure there is a UI failure and nothing else.

**Dwellers are not animated by Mecanim.** They carry no `Animator` at all — a legacy `Animation`
component and the game's own controller on top of it. The bench's figure is a pooled dweller filmed
by a private camera into a render texture, with that controller switched off so the idle it was
given is the idle it keeps.

## Licence

MIT.
