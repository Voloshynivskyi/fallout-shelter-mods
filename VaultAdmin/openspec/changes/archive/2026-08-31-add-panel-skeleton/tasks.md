# Tasks — panel skeleton

Every task is checkable by the agent alone unless marked **[launch]**, which means it needs the user
to run the game and is batched into `openspec/testplan.md`.

## 1. Probe

- [x] 1.1 Confirm `Unity.InputSystem.dll` exists and `Keyboard.current` is present
- [x] 1.2 Confirm the nine NGUI types exist in `Assembly-CSharp.dll`
- [x] 1.3 Confirm `Vault.Instance`, `Vault.Loaded`, `Vault.Storage`, `Vault.Inventory`
- [x] 1.4 Confirm `DwellerManager.Instance`, `.Dwellers`, `.MaximumDwellerCount`

## 2. Build scaffolding

- [x] 2.1 Write `build.ps1`: version read from source, install refuses while the game runs, landed
      file length compared with the built one
- [x] 2.2 Confirm it fails loudly on a deliberately wrong reference path, rather than producing a
      broken DLL

## 3. Plugin skeleton

- [x] 3.1 `BepInPlugin` attribute, GUID `ovolo.falloutshelter.vaultadmin`
- [x] 3.2 Config: `Enabled` defaulting to **false**, `ToggleKey` defaulting to a sensible key
- [x] 3.3 One startup log line naming the mod, its version, and whether it is enabled
- [x] 3.4 With `Enabled` false, `Update` returns before doing anything at all

## 4. Hotkey

- [x] 4.1 Resolve `ToggleKey` by parsing against `Key`; an unparseable value logs a warning once and
      falls back to the default
- [x] 4.2 Null-check `Keyboard.current` every frame
- [x] 4.3 Toggle a single boolean; no allocation on the hot path

## 5. Read-only panel

- [x] 5.1 Safe accessors for `Vault.Instance` and `DwellerManager.Instance` that return false rather
      than throwing
- [x] 5.2 Render each resource with its current amount, from `GameResources` indexed by `EResource`
- [x] 5.3 Render dweller count against `MaximumDwellerCount`
- [x] 5.4 Render inventory size against `ItemCountMax`
- [x] 5.5 Render "no vault loaded" when `Vault.Loaded` is false, showing no figures

## 6. Failure containment

- [x] 6.1 `Update` and `OnGUI` each wrap their whole body in try/catch that logs and swallows
- [x] 6.2 A repeated failure logs once, not every frame

## 7. Verify without the game

- [x] 7.1 Compile: zero errors, zero warnings
- [x] 7.2 `inspect_mod.ps1`: plugin attribute correct, **no Harmony patch targets at all**, no API
      reaching outside the process
- [x] 7.3 Grep the DLL for a string unique to this build and confirm the installed file carries it
- [x] 7.4 Grep the source for any write to game state and confirm there is none

## 8. Batch for a launch

- [x] 8.1 **[launch]** With `Enabled` false: game behaves normally, log shows the disabled line
- [x] 8.2 **[launch]** With `Enabled` true: the hotkey opens and closes the panel
- [x] 8.3 **[launch]** In a loaded vault: resources, dweller count and inventory size match the
      game's own interface
- [x] 8.4 **[launch]** At the main menu: the panel says no vault is loaded and does not throw
- [x] 8.5 Write all of the above into `openspec/testplan.md` before asking for the launch

## 9. Close out

- [x] 9.1 README and CHANGELOG in the style of the other two mods
- [x] 9.2 Record anything newly learned about the game in the repo
- [x] 9.3 Only after the launch batch passes: `openspec archive add-panel-skeleton`
