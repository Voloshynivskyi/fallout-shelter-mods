# Tasks — a button in the HUD

## 1. Probe

- [x] 1.1 `UIButton.onClick : List<EventDelegate>` confirmed
- [x] 1.2 `new EventDelegate(Callback)` confirmed
- [x] 1.3 The path to `BTN Camera` confirmed by the survey
- [x] 1.4 The anchor grid confirmed: nine slots, `7 BottomLeft` in use

## 2. Implementation

- [x] 2.1 Find the camera button by path
- [x] 2.2 Clone it into the same parent with `SetParent(parent, false)`
- [x] 2.3 Offset it so it does not overlap
- [x] 2.4 Strip the components it should not keep, and log what was stripped
- [x] 2.5 Clear `onClick` and add ours
- [x] 2.6 Create it once, however often the HUD is rebuilt
- [x] 2.7 A missing path logs what was looked for and changes nothing else

## 3. Verify without the game

- [x] 3.1 Compile: zero errors, zero warnings
- [x] 3.2 Still no Harmony patch targets
- [x] 3.3 Markers present, all three decodings, with controls; hash matches
- [x] 3.4 Grep: nothing unbounded per frame

## 4. Batch for a launch

- [ ] 4.1 **[launch]** The button appears in the bottom-left and opens the panel
- [ ] 4.2 **[launch]** It does not overlap the screenshot button, and the screenshot button still works
- [ ] 4.3 **[launch]** Leaving the vault and returning leaves exactly one button
- [x] 4.4 Append to `openspec/testplan.md`

## 5. Close out

- [x] 5.1 CHANGELOG
- [ ] 5.2 After the batch passes: archive
