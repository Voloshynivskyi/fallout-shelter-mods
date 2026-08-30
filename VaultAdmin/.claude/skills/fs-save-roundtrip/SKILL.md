---
name: fs-save-roundtrip
description: Prove that something written into a Fallout Shelter save is correct, complete and survives the mod being removed. Use before any feature that writes save data is considered done.
---

# Station 4 — Prove the write

This mod exists to write into the player's save. That is its purpose and its danger. A save that
stops loading is lost progress, and it has already happened once in this repo — caps as a room
output crashed deserialisation and cost a real vault.

**Never test a write against the user's live save.** Work on a copy.

## Set up

```bash
cd ../scratchpad
python -c "
import shutil, os
src = os.path.expandvars(r'%LocalAppData%\FalloutShelter\Vault2.sav')
shutil.copy2(src, 'roundtrip.sav')
print('working copy made')"
```

## Read a record before designing one

```bash
python -c "
import json, fslib
d = json.loads(fslib.load('roundtrip.sav'))
print(json.dumps(d['dwellers']['dwellers'][0], indent=2))"
```

Real field names, real value ranges, and a reference to diff against.

## Diff the whole document, not the field you changed

The failure mode is not "the field did not change". It is "something else changed too".

```bash
python -c "
import json, fslib

def flat(o, p=''):
    if isinstance(o, dict):
        for k, v in o.items(): yield from flat(v, p + '/' + str(k))
    elif isinstance(o, list):
        for i, v in enumerate(o): yield from flat(v, p + '/' + str(i))
    else:
        yield p, o

before = dict(flat(json.loads(fslib.load('roundtrip.before.sav'))))
after   = dict(flat(json.loads(fslib.load('roundtrip.sav'))))

for k in sorted(set(before) | set(after)):
    b, a = before.get(k, '<absent>'), after.get(k, '<absent>')
    if b != a: print('%-70s %s -> %s' % (k, b, a))"
```

Every line of that output must be intended. An unexplained line is a bug, even a harmless-looking
one — it means the write touched something nobody asked it to.

## Check every id against the game

Before a value reaches a save, confirm the game knows it. Enum names, item ids, outfit ids: all of
them come from the game's own tables via `fs-game-api`. A value the base game does not recognise is
how a save stops loading.

## The test that matters most

**A save this mod wrote must still load with the mod removed.**

Anything that fails this is a corruption bug, not a feature. It belongs in `openspec/testplan.md`
as its own numbered step after every dweller, weapon or pet change, not once at the end:

```
N. Create <thing> with the panel. Save and quit.
N+1. Rename BepInEx\plugins\VaultAdmin.dll to .off. Start the game, load the vault.
     Expected: the vault loads and <thing> is present and intact.
N+2. Restore the DLL.
```

## Gate

- The write was proven on a copy, never on the live save.
- The full-document diff shows only intended changes.
- Every written id was confirmed against a game table.
- A load-without-the-mod step exists in the test plan for this feature.
