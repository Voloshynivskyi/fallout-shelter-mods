# Test plan — checks that need the game running

The agent cannot launch the game or see the screen, so everything here waits for the user and is
handed over in batches, never one at a time.

Tick a step only when it has actually been run.

## Batch 1 — add-panel-skeleton

Install first. **The game must be closed**, or the DLL cannot be replaced:

```
powershell -NoProfile -Command "& 'D:\FalloutShelter-Mods\VaultAdmin\build.ps1' -Install"
```

### 1. Disabled by default

- [ ] Delete `BepInEx\config\ovolo.falloutshelter.vaultadmin.cfg` so it regenerates.
- [ ] Start the game.

**Expect** in `BepInEx\LogOutput.log`:

```
[Info   :Vault Admin] Vault Admin 0.1.0 loaded but disabled. Set Enabled = true in the config to use it.
```

**Expect** the game to behave exactly as without the plugin. Pressing F8 does nothing.

### 2. Enabled

- [ ] Set `Enabled = true` in `BepInEx\config\ovolo.falloutshelter.vaultadmin.cfg`.
- [ ] Restart the game.

**Expect**:

```
[Info   :Vault Admin] Vault Admin 0.1.0 ready; press F8 to open the panel.
```

### 3. The panel toggles

- [ ] Load a vault. Press **F8**.

**Expect** a draggable window titled `Vault Admin 0.1.0`, with the line
`Read-only. This build writes nothing.`

- [ ] Press **F8** again.

**Expect** it to disappear.

### 4. The figures are right

With the panel open in a loaded vault, compare against the game's own interface:

- [ ] Caps, food, water and energy match the numbers along the top of the screen.
- [ ] Dweller count matches the population figure.
- [ ] Inventory count matches what the inventory screen reports.

A figure that disagrees is a bug in how the panel reads state, and matters more than it looks:
every later feature writes through the same accessors.

### 5. No vault loaded

- [ ] Quit to the main menu, leaving the game running. Press **F8**.

**Expect** the panel to open and say `No vault loaded.` — and **no exception** in the log.

### 6. A bad key name falls back

- [ ] Set `ToggleKey = Banana` in the config. Restart.

**Expect**:

```
[Warning:Vault Admin] ToggleKey 'Banana' is not a key name. Using F8 instead. Names come from UnityEngine.InputSystem.Key.
```

**Expect** F8 to still open the panel.

- [ ] Set `ToggleKey` back to `F8`.

### 7. Nothing was written

- [ ] Quit the game. Rename `BepInEx\plugins\VaultAdmin.dll` to `VaultAdmin.dll.off`.
- [ ] Start the game and load the vault.

**Expect** the vault to load normally and every figure to be unchanged. This build writes nothing,
so it must leave no trace at all.

- [ ] Rename the DLL back.

## Reporting back

Copy `BepInEx\LogOutput.log` and say which numbered steps passed. A screenshot of the open panel
settles steps 3 and 4 in one go.
