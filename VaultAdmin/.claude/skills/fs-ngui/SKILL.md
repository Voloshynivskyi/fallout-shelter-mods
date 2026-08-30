---
name: fs-ngui
description: Build panel UI out of the game's own NGUI widgets so it belongs to the interface rather than floating over it. Use for anything the player sees.
---

# Building UI that belongs to the game

The panel must be **part of the interface**, not an overlay drawn on top of it. Fallout Shelter's UI
is NGUI, so the panel is made of the same widgets as everything else the player already trusts.

An IMGUI `OnGUI` window is acceptable only as scaffolding in the very first change, while the
plumbing is proven, and must be replaced before that change is archived.

## The widgets

| Type | Role |
|---|---|
| `UIPanel` | Clipping and draw-order container. Every window needs one |
| `UIAtlas` | Sprite sheet. Has a public `sprites` list and `MarkAsChanged()` |
| `UISprite` | A named sprite from an atlas — backgrounds, frames, buttons |
| `UILabel` | Text. Needs a font taken from an existing label |
| `UIButton` | Interaction, driven by `UIEventListener` or `onClick` |
| `UITexture` | A raw `Texture2D`, for anything not in an atlas |

Confirm each of these with `fs-game-api` before use; do not assume the NGUI version's API.

## Do not build from nothing — clone what exists

The reliable way to get a widget that matches the game's look is to find one the game already
built, clone it, and change it. A hand-built `UISprite` with a guessed atlas and a guessed sprite
name renders as nothing, or as the wrong picture, and the reason is invisible.

```bash
powershell -NoProfile -Command "
\$m='D:\SteamLibrary\steamapps\common\Fallout Shelter\FalloutShelter_Data\Managed'
\$a=[Reflection.Assembly]::LoadFrom(\"\$m\Assembly-CSharp.dll\")
\$a.GetTypes() | Where-Object { \$_.Name -match 'Window|Popup|Dialog' } |
  ForEach-Object { \$_.Name } | Sort-Object"
```

Pick an existing window of roughly the right shape, read how it assembles itself, and follow it.

## Finding the hierarchy to attach to

At runtime, locate the game's UI root and parent into it. When parenting an NGUI widget, use
`transform.SetParent(parent, false)` — keeping world position turns a panel into a speck somewhere
off screen, because NGUI works in its own scaled space.

Depth matters more than position: a widget with a depth lower than the panel behind it is invisible
even though everything else is correct.

## Icons

A sprite the game does not have in the atlas cannot simply be named. Either reuse an existing
sprite name, or inject a new one into a `UIAtlas` by adding a `UISpriteData` to its `sprites` list
and calling `MarkAsChanged()`. The lookup is by name and a miss fails quietly — the widget renders
blank rather than erroring, so verify by log, not by absence of exceptions.

## Input

Legacy `UnityEngine.Input` **throws** — the game uses the new Input System. A hotkey goes through
`UnityEngine.InputSystem.Keyboard.current`, referencing `Unity.InputSystem.dll`. NGUI's own button
events are unaffected and are the better route for anything inside the panel.

## Gate

- No IMGUI remains in the shipped panel.
- Every widget type used was confirmed by reflection.
- The panel is parented with `SetParent(parent, false)` and its depth is set explicitly.
- Nothing is created per frame; the panel is built once and shown or hidden thereafter.
