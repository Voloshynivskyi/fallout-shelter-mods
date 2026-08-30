---
name: fs-build-verify
description: Build the Vault Admin plugin, install it, and prove the file that landed is the file that was built. Use after every code change, before claiming anything is installed.
---

# Station 3 — Build and verify

**A build script's success line is not evidence.** In this repo `Installed` was printed while the
copy had failed against a memory-mapped DLL and the old file stayed in place. The wrong build ran
for hours and the diagnosis that followed was wrong because of it.

Read the artefact back. Every time.

## Build

```bash
powershell -NoProfile -Command "& 'D:\FalloutShelter-Mods\VaultAdmin\build.ps1'"
```

Zero errors **and zero warnings**. A warning here usually means an unused local left behind by a
half-removed idea.

## Install

The game memory-maps its plugins, so the DLL cannot be replaced while it runs. `build.ps1 -Install`
refuses outright rather than failing quietly.

```bash
powershell -NoProfile -Command "& 'D:\FalloutShelter-Mods\VaultAdmin\build.ps1' -Install"
```

If it reports the game is running, stop and tell the user to close it. Do not work around it.

## Verify — the part that is not optional

**Prefer a hash.** When a known-good reference exists, compare SHA256 and stop there. It cannot
produce a false answer:

```bash
powershell -NoProfile -Command "
(Get-FileHash 'D:\FalloutShelter-Mods\VaultAdmin\build\VaultAdmin.dll' -Algorithm SHA256).Hash -eq
(Get-FileHash 'D:\SteamLibrary\steamapps\common\Fallout Shelter\BepInEx\plugins\VaultAdmin.dll' -Algorithm SHA256).Hash"
```

**Searching a DLL for a string is subtler than it looks, and getting it wrong is worse than not
checking at all** — a false negative reads as proof of absence. .NET keeps user strings as UTF-16 in
a heap that is *not* guaranteed to start at an even byte offset. Decoding the file from offset 0
then shifts every character by one byte and turns real text into garbage. This has already produced
a confident, wrong "clean of" in this repo.

Decode **both alignments and ASCII**, and search all three:

```bash
powershell -NoProfile -Command "
function Test-Dll(\$path, \$needles) {
  \$b = [IO.File]::ReadAllBytes(\$path)
  \$hay = [Text.Encoding]::Unicode.GetString(\$b) +
           [Text.Encoding]::Unicode.GetString(\$b, 1, \$b.Length - 1) +
           [Text.Encoding]::ASCII.GetString(\$b)
  '{0}  ({1} bytes)' -f (Split-Path \$path -Leaf), (Get-Item \$path).Length
  foreach (\$n in \$needles) {
    if (\$hay -match [regex]::Escape(\$n)) { '    CONTAINS ' + \$n } else { '    clean of  ' + \$n } }
}
Test-Dll 'PATH\TO\VaultAdmin.dll' @('MARKER_ONE','MARKER_TWO')"
```

Pick markers that exist **only in the new build** — a new config key, a new method name, the new
version string. Matching sizes prove nothing; two different builds can be the same length.

Before trusting a "clean of" result, confirm the harness works by searching for a string you *know*
is in the file. A search that finds nothing at all is a broken search, not a clean build.

## Inspect what it declares

```bash
powershell -NoProfile -File ..\scratchpad\inspect_mod.ps1 -Dll "PATH\TO\VaultAdmin.dll"
```

Shows the BepInPlugin attribute, the Harmony patch targets, and any API that reaches outside the
process. Check the patch list matches what the change intended — nothing extra.

## Gate

- Compiler: zero errors, zero warnings.
- The installed file carries a marker unique to this build.
- The declared patch targets are exactly the intended ones.
- The version reported by the DLL matches `PluginVersion` in the source.

Only then may the words "installed" or "ready to test" be used.
