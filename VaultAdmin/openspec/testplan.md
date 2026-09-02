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

---

## The workshop and the overrides

### 33. The bench shows what it will make

- [ ] Open WORKSHOP. Set a hairstyle, a face, a hair colour and a skin you can recognise. Give the
      dweller a name and a weapon. Press CREATE DWELLER.

**Expect** the dweller waiting at the vault door to look like the figure that was standing on the
bench, and to be carrying what was chosen. The log line beginning `Asked for` names both halves
side by side; if they disagree, that line says where.

### 34. The bench clears itself

- [ ] After creating one, look at the bench without touching anything.

**Expect** every field back to a value the bench would produce, and a new random figure in the box
wearing the plain outfit with empty hands. It must never go on showing the person who has just left.

### 35. The die

- [ ] Press the die inside the figure's box, several times.

**Expect** the four appearance rows to take new values and the figure to change with them, every
time. The die turns and settles; the figure changes at the moment of the press, not at the end of
the turn.

### 36. Dressing does not mint weapons

This is the one that matters. It is the fault a player found, and the guard against it deletes from
the save.

- [ ] Note how many of one weapon the vault holds. Set that weapon on the bench.
- [ ] Change the gender ten times. Leave the tab and come back three times. Create one dweller.
- [ ] Look at the vault's inventory again.

**Expect** the same number of that weapon, plus the one on the dweller you created. The log may say
`Took back N item(s) the dressing table left in storage`, which is the guard working. It must never
say `Something else reached storage while the bench was dressing` — that means it declined to delete
something, which is right, but the reason wants looking at.

### 37. Staffing

- [ ] Open OVERRIDES and press BEST DWELLER IN EVERY ROOM.

**Expect** a line saying how many were posted across how many rooms. Then look at a production room
and a training room: the production room should hold your highest scorers in its stat, and the
training room your lowest.

If nothing is posted, the log lists every method on `Room`, `Dweller` and `DwellerManager` that
mentions assigning. That list is the answer; send it.

### 38. The panel button

- [ ] Look at the top-left of the vault HUD, under the game's own dwellers button.

**Expect** the panel button there, and pressing it opens the panel. If it is beside the camera
button instead, the search fell back; the log line beginning `Dwellers buttons in the vault HUD`
names every candidate it considered, with whether each was switched on and clickable.

### 39. The log after an ordinary session

- [ ] Play for a few minutes with the panel open and closed a few times. Read `LogOutput.log`.

**Expect** no atlas dumps, no lists of animation clips, and no line saying a plate is drawn at one
size and shown at another. Those are diagnostics; in a shipped build they should have nothing to
say.

---

# Release 1.0.0 — the whole panel, end to end

Every capability, and every way through it. Written to be walked start to finish in one sitting.

**Before starting.** Use a scratch vault for everything marked *destructive*; several of these
cannot be undone. Switch `TraceActions = true` for the run, so a fault has a witness. Note which
save slot the vault is — the panel keys its settings by it.

Mark each line PASS, FAIL or N/A. A FAIL wants four things: what was pressed, what happened, what
the band said, and what the log said.

## A. Starting up

- [ ] A1. `Enabled = false`: the game runs, no button, no panel, nothing in the log past the load line.
- [ ] A2. `Enabled = true`: the button appears **under the dwellers button, top left**, and does not
      move afterwards. No slide, no jump.
- [ ] A3. `F8` opens and closes the panel. With `ShowHudButton = false` the button is gone and the
      key still works.
- [ ] A4. A nonsense `ToggleKey` falls back to F8 and says so once in the log.
- [ ] A5. At the main menu with no vault: no panel, no grey window, nothing drawn.

## B. The panel itself

- [ ] B1. All four tabs open: RESOURCES, ITEMS, WORKSHOP, OVERRIDES.
- [ ] B2. Every page scrolls, and the bar is there on every page that overflows — the animal bench
      included.
- [ ] B3. CLOSE closes it; the button comes back.
- [ ] B4. Leaving to the main menu closes the panel. No grey window, no "no vault loaded".
- [ ] B5. Re-entering a vault: the button is there, the panel opens fresh.
- [ ] B6. The answer band sits above CLOSE with an **i** in a ring at its left, on every page.

## C. RESOURCES

- [ ] C1. Caps, food, water, power, stimpaks, RadAway, Quantum — for each, the +N button raises the
      count, the flight plays **once**, and the band says `Gave N <NAME>` in player words.
- [ ] C2. Fill-to-cap fills it and says how much went in.
- [ ] C3. Fill-to-cap on something already full says so — it does not sit silent.
- [ ] C4. Lunchbox, Mr Handy, pet carrier: +1 gives **exactly one**. Count before and after.
- [ ] C5. The counters follow the vault as it changes without the panel.

## D. ITEMS

- [ ] D1. Every family lists: WEAPON, OUTFIT, JUNK, PET, DWELLER. Icons draw for all of them,
      **pets included, on the very first open**, without paging.
- [ ] D2. Search narrows the list; clearing it restores it.
- [ ] D3. Sort by RARITY and by each SPECIAL stat reorders the list.
- [ ] D4. The row button says **GIVE** on weapons, outfits, junk and pets, and **INVITE** on dwellers.
- [ ] D5. Give a weapon, an outfit and a piece of junk: each arrives, and the band names it the way
      a player would — not `032Pistol_Rusty`.
- [ ] D6. Give a pet: it arrives, with its picture.
- [ ] D7. Invite a rolled dweller of each rarity: each arrives at the door.
- [ ] D8. Invite a named dweller: arrives with their own look and stats.
- [ ] D9. **Storage full**: giving a weapon refuses **in red** and adds nothing. The row does not
      claim success.
- [ ] D10. Storage full: a pet refuses in red the same way.

## E. WORKSHOP — dwellers

- [ ] E1. The figure appears, stands, and **keeps idling** — it does not freeze after one movement.
- [ ] E2. The gender switch changes the figure and the appearance lists.
- [ ] E3. Every picker steps: hair, face, hair colour, skin, headgear, outfit, weapon. No slot is
      ever empty.
- [ ] E4. The die re-rolls the whole look and the figure follows.
- [ ] E5. Name, rarity, level and all seven SPECIAL fields accept values.
- [ ] E6. CREATE DWELLER: the band says `CREATED <NAME>` with rarity and level, the log carries the
      full description, and the dweller waits at the door **looking as the figure did**.
- [ ] E7. Create several in a row, changing weapons between them. **Storage does not grow** with
      weapons nobody granted.
- [ ] E8. Leaving the bench puts the stand-in away; nobody is left standing in the vault.

## F. WORKSHOP — animals

- [ ] F1. The animal's picture shows **on the first open**, before touching an arrow.
- [ ] F2. Breed arrows step through, picture and name following.
- [ ] F3. The grade row steps.
- [ ] F4. The bonus row: arrows change the bonus, the sentence reads as the game words it — not
      `ADD MAX …` — and the tally `n/N` sits by the arrows.
- [ ] F5. Typing a value updates the sentence; MAX fills in the strongest the game gives.
- [ ] F6. The plate behind the die reads as a **button**, distinct from the well behind the figure.
- [ ] F7. CREATE PET: arrives with its picture, its name and the bonus chosen.
- [ ] F8. Storage full: refuses in red, and says so.

## G. OVERRIDES — the actions

*All destructive. Scratch vault.*

- [ ] G1. FILL FOOD, WATER, POWER — all three to their caps.
- [ ] G2. HEAL EVERYONE — **one press** gives an irradiated dweller full health, not two.
- [ ] G3. REVIVE THE DEAD — a dead dweller stands up at full health.
- [ ] G4. MAKE EVERYONE HAPPY — everyone at 100%.
- [ ] G5. FINISH ALL TRAINING — asks first; YES finishes it, NO cancels, and NO is red.
- [ ] G6. UNLOCK EVERY RECIPE — asks first; every weapon and outfit becomes craftable.
- [ ] G7. LEVEL EVERYONE — asks first; everyone reaches 50.
- [ ] G8. MAX SPECIAL FOR EVERYONE — asks first; ten in every stat.
- [ ] G9. DELIVER EVERY BABY — asks first; every pregnancy ends.
- [ ] G10. GROW THE CHILDREN — asks first; **children become adults and the three-hour task
      clears**. *(Never yet confirmed working.)*
- [ ] G11. On every confirmation, NO leaves the row alone and does nothing.
- [ ] G12. A confirmation left alone for six seconds disarms itself.

## H. OVERRIDES — staffing and dressing

- [ ] H1. BEST DWELLER IN EVERY ROOM asks first. The band answers in two short readable lines, and
      the caption beside it does not move.
- [ ] H2. The log's rank line shows a **non-zero door**, and somebody is standing at the vault door.
- [ ] H3. Production rooms hold the highest in their stat; gyms hold the **lowest**.
- [ ] H4. Nobody in the wasteland, dead, a child, or **queued at the door** was moved. The
      population count is unchanged by the pass.
- [ ] H5. Put 2–3 outfits in storage. DRESS EVERYONE FOR THE JOB asks first, then dresses as many as
      the wardrobe allows — **not one and then stopping**.
- [ ] H6. Storage loses exactly what went onto people. Nothing duplicated, nothing vanished.
- [ ] H7. The guard on the door wears the best coat outright; the living quarters wear charisma.
- [ ] H8. Run DRESS **twice**. The total number of outfits, worn plus stored, is unchanged.
- [ ] H9. **Leave to the menu and come back.** Everyone is still dressed.

## I. OVERRIDES — the standing rules

- [ ] I1. NO INCIDENTS on: no fires, pests or raiders. The switch reads ON.
- [ ] I2. NO BOTTLE AND CAPPY on: the pair stay away. The switch reads ON.
- [ ] I3. RUSH NEVER FAILS on: rushing never fails, however often it is tried.
- [ ] I4. Each switch survives leaving to the menu and coming back.
- [ ] I5. Each switch survives a full restart of the game.

## J. Per vault, which is the whole point of them

- [ ] J1. In **vault A**: all three switches on, limit 150.
- [ ] J2. In **vault B**: all three read **OFF**, and the limit is B's own rather than 150.
- [ ] J3. In B turn one on and set 180. Back in A: still 150, still its own three on.
- [ ] J4. The config file holds `slotVaultN=…` pairs, one per vault.
- [ ] J5. The log says `a vault opened: slotVaultN` — never `unnamed`.

## K. The population limit

- [ ] K1. The field opens showing **what the vault takes now**, not what was last typed.
- [ ] K2. SET applies it; the cursor still works afterwards and a second SET works.
- [ ] K3. SET above the game's own ceiling reports the number actually given, and the field corrects
      itself to it.
- [ ] K4. RESET (red) reports what the quarters hold.
- [ ] K5. **Build another living quarters**: the limit rises on its own within a few seconds, and the
      field follows.
- [ ] K6. Leave and come back: still following the rooms, not back to a fixed number.

## L. Putting it back

- [ ] L1. `Enabled = false` while the game runs: panel and button go, incidents come back, the pair
      come back, the rush chance is what it was.
- [ ] L2. Quit and reload with the mod installed: everything granted and created is still there.
- [ ] L3. **Remove the DLL entirely** and load the same save. It loads, and everything the panel
      granted or created is still there and behaves normally.

## M. The log

- [ ] M1. With `TraceActions = true`, every action leaves a line, and every vault opening and
      closing is marked.
- [ ] M2. With it false, those lines are gone.
- [ ] M3. No exception and no `Could not …` line across the whole run.
