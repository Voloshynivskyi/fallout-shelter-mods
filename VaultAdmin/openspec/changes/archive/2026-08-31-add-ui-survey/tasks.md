# Tasks — interface survey

## 1. Probe

- [x] 1.1 Nine NGUI types confirmed present
- [x] 1.2 `UIAtlas.texture`, `.spriteList`, `.GetSprite` confirmed
- [x] 1.3 `UISpriteData` fields confirmed

## 2. Implementation

- [x] 2.1 A survey button in the panel
- [x] 2.2 Report UI roots and their scaling
- [x] 2.3 Report panels with depth, clipping and parent chain
- [x] 2.4 Report the game's own windows present in the scene
- [x] 2.5 Report atlases with texture size, sprite count and sample names
- [x] 2.6 Report the fonts labels are using
- [x] 2.7 Read member names by reflection and report what was found, not what was assumed
- [x] 2.8 Bounded output, with a count of what was skipped
- [x] 2.9 A failure in one section does not stop the others

## 3. Verify without the game

- [x] 3.1 Compile: zero errors, zero warnings
- [x] 3.2 Still no Harmony patch targets
- [x] 3.3 Markers present in the installed DLL, all three decodings, with controls
- [x] 3.4 Hash matches the build output
- [x] 3.5 Grep: the survey creates no object and adds no component

## 4. Batch for a launch

- [x] 4.1 **[launch]** Run the survey in a loaded vault and keep the log
- [x] 4.2 Append to `openspec/testplan.md`

## 5. Close out

- [x] 5.1 Record what the survey found in the repo, so the next change is built on it
- [x] 5.2 After the batch passes: archive
