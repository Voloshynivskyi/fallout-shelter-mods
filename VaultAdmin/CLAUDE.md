# Vault Admin — working directives

A BepInEx mod for Fallout Shelter: a debug panel built into the game's own interface that can
create dwellers, weapons and pets with full control over every attribute the game holds.

Development is spec-driven through OpenSpec. **Read `openspec/config.yaml` before anything else** —
it carries the project context, the artifact rules and the operation guidance, and it is what the
OpenSpec commands feed on.

## The one constraint that shapes everything

**You cannot run the game or see the screen.** Every check that needs a running game costs the user
a launch, and they have already spent many. So:

- Verify against the assemblies, the save file and the compiled DLL first. Most wrong ideas die
  there for free.
- Collect everything that genuinely needs a launch into `openspec/testplan.md` as numbered steps
  with the exact expected result and the exact log line to look for.
- Hand over a batch, never a single test.

## The factory

Every change moves through the same stations, in order. A change that skips a station is not done.

| Station | Skill | Gate |
|---|---|---|
| 1. Probe | `fs-game-api` | The type and signature are confirmed by reflection or IL, not assumed |
| 2. Propose | `/opsx:propose` | Spec deltas and tasks exist; non-goals stated |
| 3. Build | `fs-build-verify` | Zero errors, zero warnings, artefact read back and confirmed |
| 4. Prove | `fs-save-roundtrip` | Writes verified against a copy of a real save, and the save loads without the mod |
| 5. Batch | — | Steps needing a launch appended to `openspec/testplan.md` |
| 6. Archive | `/opsx:archive` | Specs updated; anything learned written into the repo |

Station 4 applies to every change that touches save data, which is most of them.

## House rules

These come from failures in this repo. Each one cost a crash, a corrupted save, or a wasted launch.

**Never trust a success line.** A build script printing `Installed` has been wrong here: the copy
failed against a memory-mapped DLL and the old file stayed. Read the installed file back — size,
version, and a string only the new build contains.

**The game must be closed to install.** `build.ps1` refuses while it is running. Keep that.

**The version lives in the source only.** `build.ps1` reads `PluginVersion` out of the `.cs`. A
second copy once let an unreleased build overwrite a release archive under the wrong name.

**Guard every patch by the exact thing it targets**, and apply patch classes individually so one
failure cannot take the mod down.

**Nothing unbounded in `Update`.** `Resources.FindObjectsOfTypeAll` on every frame during a vault
load has already killed this game once.

**Never write an id the base game does not know.** Every value the panel offers comes from the
game's own tables. A picker over what exists, never a free-text id.

**Prefer the game's own code path.** To create a dweller, find what the game calls when one is born
or arrives from the wasteland, disassemble it, and drive that. A record assembled field by field is
far more likely to produce a save that will not load.

## The UI belongs to the game

The panel is not an overlay. It is built from the game's own NGUI widgets — `UIAtlas`, `UISprite`,
`UILabel`, `UIButton`, `UIPanel` — parented into the game's UI hierarchy, so it looks and behaves
like the rest of the interface. An IMGUI window drawn on top is not acceptable as the final form,
though it is acceptable as a scaffold in the very first change while the plumbing is proven.

## Layout

```
VaultAdmin/
  CLAUDE.md            this file
  build.ps1            builds and installs; version read from source
  src/                 plugin source
  openspec/
    config.yaml        project context, rules, operation guidance
    specs/             current truth
    changes/           proposals in flight
    testplan.md        the batch of checks awaiting a launch
```

The mod is standalone. It shares no code with `../CapsFoundry` or `../QuantumBottler`, though both
are worth reading for house style and for what they learned about the game.

## Safety net

`../tools/SaveBackup` copies every vault save at game start. It is installed for the whole of this
project and is never shipped. A mod whose purpose is writing to saves does not get developed
without it.
