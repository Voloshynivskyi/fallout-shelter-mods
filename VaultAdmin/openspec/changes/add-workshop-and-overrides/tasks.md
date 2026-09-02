# Tasks

## 1. The workshop

- [x] 1.1 A bench for a dweller: name, gender, rarity, level, SPECIAL, appearance, gear
- [x] 1.2 A live figure of the dweller the fields describe, filmed into a render texture
- [x] 1.3 The figure idles continuously, driven through the legacy `Animation` component
- [x] 1.4 The figure is put back on its own layer and position whenever it is let go
- [x] 1.5 Appearance lists hold no empty option; every slot is filled at random on open and on reset
- [x] 1.6 A die inside the figure's box rolls all four appearance slots
- [x] 1.7 A bench for a pet: breed, grade, name, bonus, value
- [x] 1.8 The bonus list holds only bonuses that pets are actually built with

## 2. Storage safety

- [x] 2.1 Count the vault before dressing the figure and take back what appeared
- [x] 2.2 Remove only what matches the identifier the bench minted; leave anything else, loudly
- [x] 2.3 Believe a removal only when the vault's contents shrink
- [x] 2.4 The same guard covers the outfit, the weapon and the plain-outfit fallback

## 3. Overrides

- [x] 3.1 A page of vault-wide actions and switches, each with a drawn or borrowed icon
- [x] 3.2 Switches persist and are re-asserted on a slow beat
- [x] 3.3 Every value taken over is written down first and restored when the mod is disabled
- [x] 3.4 Per-vault captures are forgotten on a vault change; process-wide ones are not
- [x] 3.5 Staffing: working rooms take the best, training rooms take those with the most to learn
- [x] 3.6 Rooms are classified by asking the room, three ways, rather than by matching names

## 4. The panel itself

- [x] 4.1 Four pages named for what they hold
- [x] 4.2 One ladder of six text sizes, applied by default rather than per label
- [x] 4.3 A grant is confirmed on the row that was pressed
- [x] 4.4 Drawn plates report themselves when shown at a size they were not drawn at
- [x] 4.5 The panel button attaches under the game's own dwellers list

## 5. Proof

- [x] 5.1 Builds clean, with warnings shown rather than hidden behind a success line
- [x] 5.2 Installed artifact verified by hash and by markers with a control
- [x] 5.3 Grep: nothing unbounded per frame; no colour outside the palette; no size off the ladder
- [x] 5.4 An independent audit of the whole file, and its blockers fixed

## 6. Batch for a launch

- [ ] 6.1 **[launch]** The staffing pass finds the assignment call and posts dwellers
- [ ] 6.2 **[launch]** The panel button attaches under the dwellers list and opens the panel
- [ ] 6.3 **[launch]** Leaving the vault and returning leaves exactly one panel button
- [ ] 6.4 **[launch]** Dressing the figure repeatedly leaves the vault's weapon count unchanged
- [ ] 6.5 **[launch]** A created dweller matches the figure that was shown
- [x] 6.6 Append to `openspec/testplan.md`

## 7. Close out

- [ ] 7.1 CHANGELOG
- [ ] 7.2 After the batch passes: archive

## Carried in

6.3 comes from `add-hud-button`, which shipped with it unticked. Nobody ever actually watched for a
second button, so it was not ticked there and is not assumed here.
