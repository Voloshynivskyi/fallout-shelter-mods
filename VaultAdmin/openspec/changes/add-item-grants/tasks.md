# Tasks — item grants

## 1. Probe
- [x] 1.1 `GameParameters.Instance.Items` confirmed
- [x] 1.2 `WeaponsList`, `OutfitList`, `JunksList` confirmed
- [x] 1.3 Weapon id confirmed as `WeaponId` from the IL of `GetWeapon`
- [x] 1.4 Outfit id confirmed as the field `m_outfitId` from the IL of `Initialize`
- [x] 1.5 `new DwellerItem(EItemType, string)` confirmed; IL read
- [x] 1.6 `Inventory.AddItem` and `EmptySpace` confirmed
- [x] 1.7 Hypothesis "HandleItem creates items" tested and disproved by IL

## 2. Catalogue
- [x] 2.1 Read the three tables once and cache them
- [x] 2.2 Take each id by its own family's member, via reflection
- [x] 2.3 Skip `IsHiddenItem`
- [x] 2.4 Log how many of each family were found, once

## 3. Granting
- [x] 3.1 `GrantItem(EItemType, string id)` constructing and adding
- [x] 3.2 Refuse and report when `EmptySpace()` is zero
- [x] 3.3 Wrapped; a failure names the item and returns

## 4. Panel
- [x] 4.1 Family selector: weapons, outfits, junk
- [x] 4.2 Filter box matching display name, case-insensitively
- [x] 4.3 Scrolling list, each row showing name, rarity and a grant button
- [x] 4.4 Row count capped so a long list cannot stall the frame

## 5. Verify without the game
- [x] 5.1 Compile: zero errors, zero warnings
- [x] 5.2 Still no Harmony patch targets
- [x] 5.3 Grep the source: no hardcoded item id anywhere
- [x] 5.4 Markers present in the installed DLL, with both controls
- [x] 5.5 Hash matches the build output
- [x] 5.6 Cross-check the ids the mod would use against the save's own item records

## 6. Batch for a launch
- [ ] 6.1 **[launch]** A granted weapon shows the right name, icon and stats
- [ ] 6.2 **[launch]** A granted outfit is equippable
- [ ] 6.3 **[launch]** Filtering finds a known item
- [ ] 6.4 **[launch]** A full inventory refuses rather than losing the item
- [ ] 6.5 **[launch]** A save with granted items loads with the plugin removed
- [x] 6.6 Append to `openspec/testplan.md`

## 7. Close out
- [x] 7.1 README and CHANGELOG
- [ ] 7.2 After the batch passes: `openspec archive add-item-grants`
