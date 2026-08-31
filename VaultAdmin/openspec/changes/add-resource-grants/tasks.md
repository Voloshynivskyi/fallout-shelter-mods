# Tasks — resource and box grants

## 1. Probe

- [x] 1.1 `Storage.AddResource(GameResources, bool, bool)` confirmed
- [x] 1.2 `Storage.GetAvailableSpace()` confirmed
- [x] 1.3 `Vault.AddLunchBox(ELunchBoxType, int)` confirmed
- [x] 1.4 `GameResources(EResource, float)` constructor confirmed
- [x] 1.5 `ELunchBoxType` members enumerated
- [x] 1.6 Hypothesis "the game's debug menu is usable" tested and disproved
- [x] 1.7 Hypothesis "DebugOpenLunchboxes grants boxes" tested and disproved

## 2. Grant plumbing

- [x] 2.1 `Grant(EResource, float)` calling `AddResource` with capped and fireCallbacks true
- [x] 2.2 `FillToCap(EResource)` using `GetAvailableSpace()`
- [x] 2.3 `GrantBoxes(ELunchBoxType, int)` calling `AddLunchBox`
- [x] 2.4 Each wrapped individually; a failure logs the resource and amount and returns

## 3. Panel

- [x] 3.1 A row per resource with +100, +1000, +10000, Fill
- [x] 3.2 The three box-shaped resource members excluded from the resource rows
- [x] 3.3 A box section with +1, +5, +25 per box type
- [x] 3.4 No grant control drawn when no vault is loaded

## 4. Verify without the game

- [x] 4.1 Compile: zero errors, zero warnings
- [x] 4.2 `inspect_mod.ps1`: still no Harmony patch targets
- [x] 4.3 Confirm the installed DLL carries markers unique to this build, searching both UTF-16
      alignments and ASCII, and prove the search works before trusting an absence
- [x] 4.4 Hash-compare the installed DLL against the build output
- [x] 4.5 Grep the source: no direct assignment to a resource field anywhere

## 5. Prove the write — fs-save-roundtrip

- [x] 5.1 Take a working copy of a real save
- [x] 5.2 Record what the game's own format holds for resources and lunchboxes, from the copy
- [ ] 5.3 Full-document diff of a save before and after granting: only intended keys change
- [ ] 5.4 **[launch]** A granted vault loads with the plugin removed

## 6. Batch for a launch

- [ ] 6.1 **[launch]** Grants appear in the game's own interface immediately
- [ ] 6.2 **[launch]** Granting past a cap stops at the cap
- [ ] 6.3 **[launch]** Granted boxes can actually be opened
- [x] 6.4 Append all of the above to `openspec/testplan.md`

## 7. Close out

- [x] 7.1 README and CHANGELOG updated
- [x] 7.2 Record the two disproved hypotheses where the next person will find them
- [ ] 7.3 After the batch passes: `openspec archive add-resource-grants`
