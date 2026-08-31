# Tasks — item icons

## 1. Probe
- [x] 1.1 `WeaponSprite` confirmed on `DwellerWeaponItem`
- [x] 1.2 `UIAtlas.texture` and `GetSprite(string)` confirmed
- [x] 1.3 `UISpriteData.x/y/width/height` confirmed
- [x] 1.4 Proved weapons and outfits cannot carry per-instance data, two independent ways

## 2. Implementation
- [x] 2.1 Resolve and cache the sprite name per catalogue entry
- [x] 2.2 Resolve the atlas per family, once
- [x] 2.3 Draw with normalised, y-flipped coordinates
- [x] 2.4 A missing sprite or atlas leaves a gap, never an exception

## 3. Verify without the game
- [x] 3.1 Compile: zero errors, zero warnings
- [x] 3.2 Still no Harmony patch targets
- [x] 3.3 Markers present, with controls; hash matches
- [x] 3.4 Grep: nothing per-frame in the drawing path that allocates

## 4. Batch for a launch
- [ ] 4.1 **[launch]** Icons appear and match the items
- [x] 4.2 Append to `openspec/testplan.md`

## 5. Close out
- [x] 5.1 CHANGELOG, recording why weapon stats are not editable
- [ ] 5.2 After the batch passes: archive
