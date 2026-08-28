# Fallout Shelter mods

Two BepInEx mods for the PC (Unity Mono) build of Fallout Shelter, plus the tooling used to work
out how the game is put together.

| Mod | What it does |
|---|---|
| [**Caps Foundry**](CapsFoundry) | Adds a genuinely new buildable room that produces caps |
| [**Quantum Bottler**](QuantumBottler) | Makes the Nuka-Cola Bottler produce Nuka-Cola Quantum instead of food and water |

Each mod has its own README covering installation, configuration and a technical write-up, and its
own CHANGELOG.

## Building

No .NET SDK required. The plugins target the Unity Mono profile, so they are compiled with the C#
compiler bundled with the .NET Framework, against the game's own assemblies.

```powershell
cd CapsFoundry        # or QuantumBottler
.\build.ps1
.\build.ps1 -GamePath "C:\Path\To\Fallout Shelter" -Install
```

Each build produces `build\<Mod>.dll` and a ready-to-upload archive in `dist\`. Both directories are
generated and stay out of version control.

## Tools

`tools\` holds two small reflection-based scripts written for this work. Fallout Shelter has no mod
support and effectively no documentation, so almost everything here was worked out by reading IL.

| Tool | Purpose |
|---|---|
| `ildasm.ps1` | Disassembles a method of `Assembly-CSharp` to IL |
| `findcallers.ps1` | Scans every method body for references to a member, i.e. finds callers |
| `RoomTextureDump/` | A BepInEx plugin that writes a room's textures to PNG, with a manifest naming the renderer, material, shader and shader property behind each file |
| `SaveBackup/` | A BepInEx plugin that copies the vault saves aside at game start, so a bad build costs a restart rather than progress |

```powershell
.\tools\ildasm.ps1 -TypeName "ProductionRoom" -MethodFilter "^GetProducedResources$"
.\tools\findcallers.ps1 -Pattern "GetProducedResources"

cd tools\RoomTextureDump
.\build.ps1 -Install       # then run the game and look at the room
.\build.ps1 -Uninstall     # a dev tool, so take it back out afterwards
```

`RoomTextureDump` exists because repainting a room means matching the UV layout the game actually
uses; its output is the template for that.

`SaveBackup` exists because the room work — pools, sections, room types — has crashed saves before.
A released mod has no business duplicating the player's save folder on every launch, which is why
Caps Foundry dropped that behaviour in 1.3.0; a tool used while building one very much does.

### Neither is ever shipped

This is structural rather than a promise to remember. Each mod's `build.ps1` stages exactly its own
DLL plus `README.md`, `LICENSE` and `CHANGELOG.md` — it has no way to pick up anything from
`tools/`. The dev plugins have no packaging step at all, only `-Install` and `-Uninstall`. And
neither mod's source contains any save-file code.

**Before publishing a release, take the dev plugins out of the game:**

```powershell
.\tools\RoomTextureDump\build.ps1 -Uninstall
.\tools\SaveBackup\build.ps1 -Uninstall
```

They do not affect what is in the archive, but leaving them installed means testing a release build
alongside plugins the players will not have.

## What was learned about the game

Both mods rest on the same findings, documented in full in each mod's *How it works* section. The
short version:

- **Resource production is data-driven.** `ProductionRoom.GetProducedResources()` reads the amounts
  off the room level asset and returns a per-second rate, which accumulates into the room's own
  storage until full. Nothing hardcodes which room makes what.
- **Room type is a key into seven independent systems** — the prefab registry, the build menu's own
  separate list, a per-type Unity scene, object pools, the identity a pooled room carries, unlock
  objectives, and the icon table. Adding a room means satisfying every one of them.
- **Three assumptions bite hard.** Caps are never a room's output, so paths that build resource
  lists exclude them. The icon lookup is by bit flag and a miss *silently returns an unrelated
  sprite* rather than failing. `GetPositiveResources` returns `null`, not an empty list.

Each of those cost a crashed save to discover, and each is written up where it is relied on.

## Licence

MIT — see the LICENSE file in each mod directory.
