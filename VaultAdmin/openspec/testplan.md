# Test plan — checks that need the game running

The agent cannot launch the game or see the screen, so everything here waits for the user and is
handed over in batches, never one at a time.

Tick a step only when it has actually been run.

## Batch 1 — add-panel-skeleton — PASSED 2026-08-30

Confirmed by the user: the panel opens, and every figure matches the game's own interface.

Install first. **The game must be closed**, or the DLL cannot be replaced:

```
powershell -NoProfile -Command "& 'D:\FalloutShelter-Mods\VaultAdmin\build.ps1' -Install"
```

### 1. Disabled by default — PASSED 2026-08-30

- [x] Config generated on first launch with `Enabled = false`.
- [x] Started the game.

**Expect** in `BepInEx\LogOutput.log`:

```
[Info   :Vault Admin] Vault Admin 0.1.0 loaded but disabled. Set Enabled = true in the config to use it.
```

**Observed** exactly that line in the log. The game behaved normally.

### 2. Enabled

- [x] `Enabled = true` set in `BepInEx\config\ovolo.falloutshelter.vaultadmin.cfg`.
- [x] Restart the game.

**Expect**:

```
[Info   :Vault Admin] Vault Admin 0.1.0 ready; press F8 to open the panel.
```

### 3. The panel toggles

- [x] Load a vault. Press **F8**.

**Expect** a draggable window titled `Vault Admin 0.1.0`, with the line
`Read-only. This build writes nothing.`

- [x] Press **F8** again.

**Expect** it to disappear.

### 4. The figures are right

With the panel open in a loaded vault, compare against the game's own interface:

- [x] Caps, food, water and energy match the numbers along the top of the screen.
- [x] Dweller count matches the population figure.
- [x] Inventory count matches what the inventory screen reports.

A figure that disagrees is a bug in how the panel reads state, and matters more than it looks:
every later feature writes through the same accessors.

### 5. No vault loaded

- [x] Quit to the main menu, leaving the game running. Press **F8**.

**Expect** the panel to open and say `No vault loaded.` — and **no exception** in the log.

### 6. A bad key name falls back

- [x] Set `ToggleKey = Banana` in the config. Restart.

**Expect**:

```
[Warning:Vault Admin] ToggleKey 'Banana' is not a key name. Using F8 instead. Names come from UnityEngine.InputSystem.Key.
```

**Expect** F8 to still open the panel.

- [x] Set `ToggleKey` back to `F8`.

### 7. Nothing was written

- [x] Quit the game. Rename `BepInEx\plugins\VaultAdmin.dll` to `VaultAdmin.dll.off`.
- [x] Start the game and load the vault.

**Expect** the vault to load normally and every figure to be unchanged. This build writes nothing,
so it must leave no trace at all.

- [x] Rename the DLL back.

## Reporting back

Copy `BepInEx\LogOutput.log` and say which numbered steps passed. A screenshot of the open panel
settles steps 3 and 4 in one go.

## Batch 2 — add-resource-grants — PASSED 2026-08-30

Confirmed by the user: grants land and everything adds correctly.

Install first, with the game closed:

```
powershell -NoProfile -Command "& 'D:\FalloutShelter-Mods\VaultAdmin\build.ps1' -Install"
```

### 8. Grants show up immediately

- [x] Load a vault, press **F8**, press `+1000` on **Food**.

**Expect** the food figure at the top of the screen to change **at once**, without reloading. That
is what `fireCallbacks` is for; a stale number means the callback is not reaching the interface.

**Expect** in the log: `Granted 1000 Food.`

### 9. Capping holds

- [x] Press `+10000` on a resource repeatedly, past what the vault can hold.

**Expect** it to stop at the cap and stay there. Nothing negative, nothing wrapped around.

- [x] Press **Fill** on a resource that is not full.

**Expect** it to land exactly on the cap, and the log to say by how much it rose.

### 10. Boxes are real boxes

- [x] Press `+5` on **Regular** in the Boxes section.

**Expect** five lunchboxes to appear **and be openable**. This is the step that matters most: the
save has a resource counter called `Lunchbox` that is not where boxes actually live, so a number
going up while nothing can be opened would mean the wrong path is being used.

- [x] Press `+1` on **PetCarrier** and open it.

### 11. Nothing is offered without a vault

- [x] Quit to the main menu, press **F8**.

**Expect** `No vault loaded.` and **no grant buttons at all**.

### 12. A granted vault survives the mod being removed

- [x] Grant several resources and boxes. Save and quit.
- [x] Rename `BepInEx\plugins\VaultAdmin.dll` to `.off`. Start the game and load the vault.

**Expect** the vault to load normally, with the granted resources and boxes still there. Everything
was written by the game's own code, so this should hold — but it is the check that catches it if it
does not.

- [x] Rename the DLL back.

## Batch 3 — add-item-grants

### 13. The catalogue is read

- [ ] Open the panel in a loaded vault, look at the **Items** section.

**Expect** in the log, once: `Item catalogue read from the game: N weapons, M outfits, K junk.`

Numbers in the hundreds. Zero anywhere means the tables were not reached.

### 14. A granted weapon is a real weapon

- [ ] Choose **Weapon**, type `flamer` in the filter, grant one.

**Expect** it in the inventory with its **proper name, icon and damage**, equippable to a dweller.

A blank name or a missing icon means the identifier is wrong — the item exists but cannot find its
own data. That is the single most important thing this batch checks.

### 15. Outfits, which use a different identifier

- [ ] Choose **Outfit**, grant one, equip it.

Outfits are keyed differently from weapons internally, so this is a separate check, not a repeat.

### 16. Filtering

- [ ] Type part of a name you know exists.

**Expect** the list to narrow. With no filter and a long family, expect a line saying how many
matched and that the list was cut.

### 17. A full inventory

- [ ] Fill the inventory, then try to grant.

**Expect** `The inventory is full; <item> was not granted.` in the log, and no item lost.

### 18. Granted items survive the mod being removed

- [ ] Grant several items. Save and quit. Rename the DLL to `.off`, start, load.

**Expect** the vault to load with every granted item present and named.

- [ ] Rename the DLL back.
