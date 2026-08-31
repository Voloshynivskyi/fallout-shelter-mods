# Tasks — dweller creation

## 1. Probe

- [x] 1.1 `CreateDweller` and `AddDweller` confirmed
- [x] 1.2 `CreateSpecialDweller` and `LegendaryDwellers` confirmed
- [x] 1.3 `Dweller.Name`, `.LastName`, `.Rarity` confirmed writable
- [x] 1.4 `DwellerStats.GetStat(ESpecialStat)` confirmed
- [x] 1.5 `SpecialStat.Value` read-only; `SetValueAndMinExp` chosen and justified
- [x] 1.6 `EDwellerRarity`, `EGender`, `ESpecialStat` enumerated
- [x] 1.7 Save record read: every field the game stores per dweller

## 2. Implementation

- [x] 2.1 Rarity, gender and level controls
- [x] 2.2 First and last name fields; empty keeps the generated one
- [x] 2.3 Seven SPECIAL fields
- [x] 2.4 Create, edit, then admit
- [x] 2.5 Position taken from an existing dweller, origin as fallback
- [x] 2.6 Report and create nobody when admission is refused
- [x] 2.7 Legendary list read from the game, created through its own call
- [x] 2.8 Wrapped; a failure names what was being created and returns

## 3. Verify without the game

- [x] 3.1 Compile: zero errors, zero warnings
- [x] 3.2 Still no Harmony patch targets
- [x] 3.3 Markers with controls; hash matches
- [x] 3.4 Grep: no dweller name or legendary id hardcoded
- [x] 3.5 Confirm the fields written are the fields the save stores

## 4. Batch for a launch

- [ ] 4.1 **[launch]** A created dweller joins the vault with the chosen name and SPECIAL
- [ ] 4.2 **[launch]** A legendary dweller arrives intact
- [ ] 4.3 **[launch]** A full vault refuses cleanly
- [ ] 4.4 **[launch]** Created dwellers survive the mod being removed
- [x] 4.5 Append to `openspec/testplan.md`

## 5. Close out

- [x] 5.1 CHANGELOG
- [ ] 5.2 After the batch passes: archive
