# Tasks — pet grants

## 1. Probe

- [x] 1.1 Pet catalogue located: `Catalog.Instance.m_petsCustomizationData` then `PetItems`
- [x] 1.2 `DwellerPetItem` members confirmed
- [x] 1.3 `GenerateRandomData(Random) : PetUniqueData` confirmed
- [x] 1.4 `Name`, `Bonus`, `BonusValue` confirmed writable
- [x] 1.5 `DwellerItem.ExtraData` confirmed writable
- [x] 1.6 `EItemType.Pet == 5`, matching the game's own `ldc.i4.5`
- [x] 1.7 Construction sequence read from the IL of `GenerateRandomPet`

## 2. Implementation

- [x] 2.1 Read the pet catalogue once, through reflection, and cache it
- [x] 2.2 Name, bonus and value controls
- [x] 2.3 Grant following the game's sequence, then overwrite the three fields
- [x] 2.4 An empty name leaves the generated one alone
- [x] 2.5 Wrapped; a failure names the pet and returns

## 3. Verify without the game

- [x] 3.1 Compile: zero errors, zero warnings
- [x] 3.2 Still no Harmony patch targets
- [x] 3.3 Markers with controls; hash matches
- [x] 3.4 Grep: no pet identifier hardcoded
- [x] 3.5 Check the save's own pet records to confirm the fields written are the fields stored

## 4. Batch for a launch

- [x] 4.1 **[launch]** A granted pet carries its chosen name, bonus and value
- [x] 4.2 **[launch]** The bonus applies in play
- [x] 4.3 **[launch]** A save with a customised pet loads with the plugin removed
- [x] 4.4 Append to `openspec/testplan.md`

## 5. Close out

- [x] 5.1 CHANGELOG
- [x] 5.2 After the batch passes: archive
