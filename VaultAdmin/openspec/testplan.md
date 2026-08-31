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

- [x] Open the panel in a loaded vault, look at the **Items** section.

**Expect** in the log, once: `Item catalogue read from the game: N weapons, M outfits, K junk.`

Numbers in the hundreds. Zero anywhere means the tables were not reached.

### 14. A granted weapon is a real weapon

- [x] Choose **Weapon**, type `flamer` in the filter, grant one.

**Expect** it in the inventory with its **proper name, icon and damage**, equippable to a dweller.

A blank name or a missing icon means the identifier is wrong — the item exists but cannot find its
own data. That is the single most important thing this batch checks.

### 15. Outfits, which use a different identifier

- [x] Choose **Outfit**, grant one, equip it.

Outfits are keyed differently from weapons internally, so this is a separate check, not a repeat.

### 16. Filtering

- [x] Type part of a name you know exists.

**Expect** the list to narrow. With no filter and a long family, expect a line saying how many
matched and that the list was cut.

### 17. A full inventory

- [x] Fill the inventory, then try to grant.

**Expect** `The inventory is full; <item> was not granted.` in the log, and no item lost.

### 18. Granted items survive the mod being removed

- [x] Grant several items. Save and quit. Rename the DLL to `.off`, start, load.

**Expect** the vault to load with every granted item present and named.

- [x] Rename the DLL back.

## Batch 4 — add-item-icons — PASSED

### 19. Icons

- [x] Open the item list, look at **Weapon**, then **Outfit**, then **Junk**.

**Expect** each row to show that item's picture beside its name.

Weapons, outfits and junk each use a different atlas and a differently-named sprite field, so all
three are worth a glance. A whole family with no pictures means that family's atlas was not reached;
a scattered blank here and there is expected and harmless.

## Batch 5 — add-pet-grants — PASSED

### 20. The pet catalogue

- [x] Open the panel, look at the **Pets** section.

**Expect** in the log, once: `Pet catalogue read from the game: N pets.`

### 21. A customised pet

- [x] Type a name, step the bonus to something recognisable such as `CapsBoost`, set the value to
      `50`, and grant a pet.
- [x] Open the pet in the inventory.

**Expect** exactly that name, that bonus and that value on the card.

The save stores these as `uniqueName`, `bonus` and `bonusValue`, and real pets in this vault already
carry values from 1.25 to 95, so an odd number is not a problem — a *wrong* one is.

### 22. The bonus works

- [x] Equip the pet on a dweller and check the effect applies.

### 23. Survives the mod being removed

- [x] Save and quit, rename the DLL to `.off`, start, load.

**Expect** the pet still there, still named, still carrying its bonus.

## Batch 6 — add-dweller-grants — PASSED

### 24. A made-to-order dweller

- [x] Set a first and last name, pick a rarity, set every SPECIAL to `10`, create.

**Expect** the dweller to walk into the vault carrying that name, and to show **10 in all seven
stats** on their card.

The stats are the part worth staring at: the save keeps each stat's value and its experience side by
side, and they must agree. A stat showing 10 with the experience of a 1 is the failure this looks
for.

### 25. Legendary

- [x] Pick one from the legendary list and create it.

**Expect** it to arrive with its own name, face and stats — not the ones set in the fields above.

### 26. A full vault

- [x] Fill the vault to its population limit, then try.

**Expect** `The vault is at its population limit; no dweller was created.` in the log, and nobody
created.

### 27. Survives the mod being removed

- [x] Save and quit, rename the DLL to `.off`, start, load.

**Expect** every created dweller present, named, with their stats intact.

## Batch 7 — add-ui-survey — PASSED

### 28. The interface survey

- [x] Load a vault, open the panel, press **Survey UI** (top of the window, beside the note about
      grants).
- [x] Quit and send the whole `BepInEx\LogOutput.log`.

**Expect** a block between `=== interface survey ===` and `=== end of survey ===` listing the UI
roots, the visible panels with their depths, the game's own windows, the atlases and the fonts.

Nothing changes on screen: the survey only reads. Its whole purpose is to spend one launch learning
the facts the real panel has to be built against, instead of several launches guessing at them.

Also expect a `buttons on screen` section listing every button with its parent path. That is how the
menu on the right — settings, stats, boxes, missions, storage — gets located: it is assembled in the
scene, so nothing in the assembly describes it, but its buttons are visible and their paths say
where a new one would belong.

If the menu is one that opens rather than one always shown, **open it before pressing Survey UI**,
so its buttons are on screen while the survey runs.

## Batch 8 — add-hud-button

### 29. A button in the corner

- [ ] Load a vault. Look at the **bottom-left corner**, beside the screenshot button.

**Expect** a second button there, the same size and style, and `Placed a panel button in the vault
HUD` in the log.

- [ ] Press it.

**Expect** the panel to open. Press again: it closes. **F8** still does the same.

### 30. It has not eaten the screenshot button

- [ ] Press the original screenshot button.

**Expect** a screenshot, as before. The clone was stripped of what the original did, but the
original itself must be untouched.

### 31. Only ever one

- [ ] Leave the vault to the main menu and come back. Look again.

**Expect** exactly one panel button, not two.

### 32. If it overlaps

The two buttons may sit too close. `HudButtonOffsetX` in the config moves it; raise it and restart.
Tell me the value that looks right and it becomes the default.
