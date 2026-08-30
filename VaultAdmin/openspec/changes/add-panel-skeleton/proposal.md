# Add the panel skeleton

## Why

Vault Admin will eventually create dwellers, weapons and pets with full control over every attribute
the game holds. All of that writes to the player's save, and a save that stops loading is lost
progress — it has already happened once in this repo.

So nothing that writes anything gets built until the plumbing underneath it is proven: the plugin
loads, it can be switched off, it can be opened and closed, and it can *read* live game state. This
change is that floor. It grants nothing and changes nothing.

## What changes

- A new standalone BepInEx plugin, `VaultAdmin`, in its own folder. It shares no code with
  `CapsFoundry` or `QuantumBottler`.
- A master switch in config, **defaulting to off**, so installing the mod without deliberately
  enabling it changes nothing at all.
- A hotkey that opens and closes a panel.
- The panel shows live vault state, read-only: current resources, dweller count, inventory size.
- `build.ps1` following the house pattern — version read from source, refuses to install while the
  game is running, artefact verified after copying.

## Non-goals

- **No writes of any kind.** Not resources, not items, not dwellers. Read-only throughout.
- No NGUI yet. This change proves the plumbing with a scaffold; the panel becomes real game UI in
  the next change, before this capability is considered finished.
- No item, dweller or weapon pickers.
- No Harmony patches. If reading live state can be done without patching, it should be.

## Game types this depends on, and how they were confirmed

| Type | Confirmed by |
|---|---|
| `UnityEngine.InputSystem.Keyboard.current` | Reflection over `Unity.InputSystem.dll`: type found, static property `current` present |
| `UIPanel`, `UIAtlas`, `UISprite`, `UILabel`, `UIButton`, `UITexture`, `UIRoot`, `UIEventListener`, `UIWidget` | Reflection over `Assembly-CSharp.dll`: all nine present |

Legacy `UnityEngine.Input` is not used: it throws in this build.

The types that expose live vault state — resources, dweller count, inventory — are **not yet
confirmed** and are the first job of the design step. No code is written against them until they
are.

## What cannot be verified without a running game

- That the hotkey actually fires.
- That the panel draws, and is legible.
- That the values shown match what the game's own UI shows.

These go to `openspec/testplan.md` as numbered steps and are batched into a single launch. Nothing
else in this change needs the game.
