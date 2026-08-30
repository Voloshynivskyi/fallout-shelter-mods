# Prompt — Vault Admin, a debug panel mod for Fallout Shelter

Paste everything below the line into a fresh session, from the repo root
`D:\FalloutShelter-Mods`.

---

Build a new, standalone BepInEx mod for Fallout Shelter called **Vault Admin**: an in-game debug
panel that can grant anything the game has. It lives in its own folder `VaultAdmin/`, alongside
`CapsFoundry/` and `QuantumBottler/`, and shares nothing with them.

Work in a loop — hypothesis, implementation, verification, next hypothesis — and do not ask me
questions you can answer yourself. Read the game's assemblies, read the save file, read this repo.
Ask only where this brief says to.

## What it must do

The centre of this mod is **creation with full control**. Everything else is secondary.

**Dwellers — the primary feature.** Create a dweller and set every attribute the game holds:
first and last name, gender, all seven SPECIAL values, level, and the complete appearance — hair
style, hair colour, skin tone, facial features, outfit, and whatever else the dweller record turns
out to carry. Legendary dwellers included, both as presets and as anything assembled by hand.

**Weapons — the same freedom.** Any weapon in the game's tables, with its rarity, damage range and
condition set as wanted, placed straight into the vault inventory.

**Pets — the same again.** Any pet, any bonus type and magnitude, any name.

Then the rest:

- Give any resource, any amount: caps, food, water, energy, stimpaks, RadAway, Quantum.
- Give lunchboxes, pet carriers, Mr Handy boxes, starter packs — every box type the game has.
- Give any outfit.
- Instantly finish, rush or reset the room being looked at.
- Anything else the game exposes that is obviously useful to a debug panel.

For all three creation features the rule is the same: every value offered in the panel must come
from the game's own tables and enums. Offer a picker over what exists; never let a hand-typed id
reach a save.

Ship it switched **off** by default, behind a config flag and a hotkey, so an accidental install
changes nothing.

## Hard constraints — read before planning

1. **You cannot run the game or see the screen.** Any check that needs the game running costs a
   round trip with me. Design for that: verify everything you possibly can without it.
2. **The game must be closed to install a DLL.** It memory-maps its plugins. The build script
   refuses to install while it runs — keep that behaviour.
3. **Never trust a success line. Read the artefact back.** A build script printing "Installed" has
   been wrong in this repo: the copy failed and the old DLL stayed. Verify the installed file's
   size and version, and grep its bytes for a string only the new build contains.
4. **The version number lives in the source only.** `build.ps1` reads `PluginVersion` out of the
   `.cs` file. A second copy once let a build with unreleased code overwrite a release archive.
5. **This mod writes to the player's save.** That is its whole point and its whole danger. Before
   any write path exists, install `tools/SaveBackup` and keep it installed for the entire project.

## Start here, not from scratch

Read these first. Every line in them cost a crash or a launch:

- `CLAUDE.md`, `README.md`
- `docs/room-visuals-findings.md` — how rooms, pools, donors and materials actually work
- `CapsFoundry/README.md` — the *How it works* section, especially the seven systems a room type
  keys into and the three engine assumptions that each cost a corrupted save
- `CapsFoundry/src/CapsFoundryPlugin.cs` — the house style for Harmony patches, config and guards

Known facts you do not need to rediscover:

- Saves are base64 → AES-256-CBC → JSON. `scratchpad/fslib.py` has `load()` and `save()` and the
  key and IV. Saves live in `%LocalAppData%\FalloutShelter\`.
- Lunchboxes are **not** a resource. They are `LunchBoxesByType` plus `LunchBoxesCount`. Writing
  them as a resource does nothing.
- `ParameterDataMgr.Instance` holds room data; `GameParameters.Instance.Resources` maps resources
  to icons. Resource icon lookup is by bit flag and a miss returns an unrelated sprite rather than
  failing.
- Legacy `UnityEngine.Input` throws — the game uses the new Input System. A hotkey must go through
  `UnityEngine.InputSystem.Keyboard.current`, referencing `Unity.InputSystem.dll`.
- `OnGUI`/IMGUI works for a panel and needs `UnityEngine.IMGUIModule.dll`.
- `tools/ildasm.ps1` disassembles a method; `tools/findcallers.ps1` finds callers. Use them before
  guessing at any API.

## The loop

Repeat until the feature list is done. One feature per pass, smallest first.

**1. Hypothesis.** State in one sentence what you believe the game does and which type or method
you intend to call. Never guess a signature — confirm it by reflection over
`FalloutShelter_Data\Managed\Assembly-CSharp.dll`, or by disassembling the method that already does
the thing you want. The game does it somewhere; find that code and copy its approach.

**2. Static verification, before writing the feature.** Prove the API exists and takes what you
think: dump the type's members, dump the enum values, disassemble the caller. If the hypothesis
does not survive this, discard it and go back to step 1. Most wrong ideas die here for free.

**3. Implement.** House style: one patch class per concern, applied individually so one failure
cannot take the mod down, every patch guarded by the exact thing it targets, no work in `Update`
that is not bounded, no unbounded scans of loaded objects — `Resources.FindObjectsOfTypeAll` on
every frame during a vault load has already killed this game once.

**4. Compile and inspect.** `.\VaultAdmin\build.ps1`. Zero errors and zero warnings. Then inspect
the DLL with `scratchpad/inspect_mod.ps1`: check the plugin attribute, the patch targets, and that
nothing reaches outside the process that should not.

**5. Verify what can be verified without the game.**
   - For a save-writing feature: write to a **copy** of `Vault2.sav` with `fslib.py`, read it back,
     and diff the JSON. Assert the intended field changed and nothing else did.
   - For an item or dweller grant: confirm every id or enum value you write exists in the game's
     own tables. **Never write an id the base game does not know** — that is how a save stops
     loading.
   - For UI: confirm the panel compiles, the hotkey binds through the new Input System, and the
     draw path cannot throw when the objects it reads are null.

**6. Batch what needs me.** Keep a running `docs/vault-admin-testplan.md`. Add each thing that
genuinely needs a running game as a numbered step with the exact expected result and the exact log
line to look for. Do not send me one test at a time. When the batch is worth a launch — several
features, or one that blocks everything after it — install, tell me the game must be closed, and
give me the numbered list.

**7. Record.** Commit after every pass with a message explaining *why*, not what. Append anything
newly learned about the game to `docs/room-visuals-findings.md` or a sibling document. Nothing
learned may live only in the chat.

## Order of work

Earlier stages must be verified before later ones start.

1. **Skeleton** — plugin loads, config with a master switch defaulting to off, hotkey, empty panel.
2. **Read-only panel** — shows current resources, dweller count, inventory size. Proves the panel
   can reach live game state before it changes any.
3. **Resources** — the simplest write, and the safest to undo. Use it to prove the whole
   write-verify-reload cycle works before anything harder.
4. **Boxes** — lunchboxes, pet carriers, Mr Handy. Uses the array-plus-count structure, not a
   resource.
5. **Weapons and pets** — driven from the game's own item tables, with every field the panel
   exposes taken from those tables rather than typed.
6. **Dwellers, in full** — the main event, and the most likely to corrupt a save, so it gets the
   most verification. Build it in three passes: spawn a default dweller and confirm the save
   survives a reload without the mod; then names, gender and SPECIAL; then the full appearance.
   Do not move to the next pass until the previous one round-trips cleanly.
7. **Outfits and room tools** — rush, complete, reset.

### Working out the dweller record

Do this before writing any dweller code, and it costs nothing:

Decrypt a real save with `scratchpad/fslib.py` and read one dweller entry in full. Every field the
game persists is there under its real name — names, SPECIAL, level, and the appearance fields.
That gives you the exact vocabulary, the value ranges actually in use, and a reference dweller to
diff against. Then find the runtime type that serialises into it and confirm the field names match.

Prefer the game's own creation path over assembling a record by hand. Find what the game calls when
a dweller is born or arrives from the wasteland, disassemble it, and drive the same code. A dweller
built by the game's own factory and then adjusted is far safer than one invented field by field.

## When to ask me

Only these:

- A batch of in-game tests is ready and needs a launch.
- A decision is genuinely mine: balance, naming, whether a capability should exist at all.
- Two readings of this brief would lead to materially different work.

Not these: which method to call, whether an enum value exists, what a field is named, whether an
approach compiles. Those are all answerable from the assemblies in front of you.

## Definition of done

- The panel does everything in the feature list.
- It is off by default and cannot affect a player who installs it without switching it on.
- No unbounded per-frame work anywhere.
- Every patch class is applied individually and guarded.
- `docs/vault-admin-testplan.md` has been walked end to end and passes.
- A save written by the panel loads correctly with the mod **removed** — anything that fails this
  is a corruption bug, not a feature. Test this specifically after each of the three dweller
  passes, not once at the end.
- A created dweller is indistinguishable from a natural one: it survives a reload, can be assigned
  to a room, levels up, can be equipped, and can be sent to the wasteland and come back.
- README and CHANGELOG written for someone who has never seen it, in the style of the other two
  mods.
