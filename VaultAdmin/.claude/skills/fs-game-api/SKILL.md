---
name: fs-game-api
description: Confirm a Fallout Shelter type, member, enum or method signature exists before writing code against it. Use at the start of every change, and whenever tempted to guess an API.
---

# Station 1 — Probe

Fallout Shelter has no mod support and no documentation. Everything is worked out by reading its
assemblies. **A guessed signature is a wasted build at best and a corrupted save at worst.**

Never write code against a game type until it has been confirmed here.

## Where things live

```
D:\SteamLibrary\steamapps\common\Fallout Shelter\FalloutShelter_Data\Managed\Assembly-CSharp.dll
D:\SteamLibrary\steamapps\common\Fallout Shelter\BepInEx\core\0Harmony.dll
```

## List a type's members

```bash
powershell -NoProfile -Command "
\$m='D:\SteamLibrary\steamapps\common\Fallout Shelter\FalloutShelter_Data\Managed'
\$a=[Reflection.Assembly]::LoadFrom(\"\$m\Assembly-CSharp.dll\")
\$t=\$a.GetType('TYPENAME')
\$t.GetMembers('Public,NonPublic,Instance,Static') |
  Where-Object { \$_.Name -notmatch '^(get_|set_|add_|remove_)' } |
  ForEach-Object { '{0,-10} {1}' -f \$_.MemberType, \$_.Name } | Sort-Object -Unique"
```

## Dump an enum

```bash
powershell -NoProfile -Command "
\$m='D:\SteamLibrary\steamapps\common\Fallout Shelter\FalloutShelter_Data\Managed'
\$a=[Reflection.Assembly]::LoadFrom(\"\$m\Assembly-CSharp.dll\")
[Enum]::GetNames(\$a.GetType('ENUMNAME'))"
```

Enums are the source of truth for anything the panel offers. **Build every picker from an enum or a
game table, never from a list typed by hand.**

## Read the method that already does it

The game already creates dwellers, grants items and adds resources somewhere. Find that code and
drive the same path rather than inventing one.

```bash
powershell -NoProfile -File ..\tools\ildasm.ps1 -TypeName "TYPE" -MethodFilter "^METHOD$"
powershell -NoProfile -File ..\tools\findcallers.ps1 -Pattern "MEMBERNAME"
```

`GetParameters()` can throw `TypeLoadException` on methods using `Span`/`ReadOnlySpan`. That is a
reflection limitation, not a missing method — fall back to `ildasm.ps1`.

## Field names from the save

The save is the fastest way to learn what the game actually persists, under its real field names
and with real value ranges.

```bash
cd ../scratchpad && python -c "
import json, fslib
d = json.loads(fslib.load(r'C:\Users\ASUS ZenBook\AppData\Local\FalloutShelter\Vault2.sav'))
print(json.dumps(d['dwellers']['dwellers'][0], indent=2)[:3000])"
```

Read a real record before designing anything that writes one.

## Gate

The station is passed when, for every game type the change touches, you can name:

- the exact type name,
- the exact member or method signature,
- how it was confirmed — reflection output or IL, quoted in the proposal.

If any of those is "probably", go back.
