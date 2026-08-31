# Open the panel from a button in the game's interface

## Why

A hotkey is not discoverable and it is not what was asked for: the panel should be reachable from
the game's own interface. This is also the first piece of real NGUI work, and it is deliberately the
smallest one — a single button — before anything larger is attempted.

## What changes

- A button in the bottom-left corner of the vault HUD, beside the screenshot button, that opens and
  closes the panel.
- The hotkey keeps working.

## Where it goes, and why not where it was first asked

The first idea was the menu on the right, beside settings, stats, boxes, missions and storage. The
survey ruled it out on two counts, and the second came from playing:

- No type in the assembly owns those buttons. The menu is assembled in the scene, so it can only be
  reached by path — workable, but blind.
- It already fills the height of the screen, so another entry crowds it.

The bottom-left corner holds one button, `BTN Camera`, and is otherwise empty.

## The button is cloned, not built

```
MainScene_Root/GUI/VaultHUDWindow/VaultHUDPanel/7 BottomLeft/BTN Camera
```

An NGUI widget renders as nothing when its depth is below what it sits on, when its parent is wrong,
when its atlas lacks the sprite it names, or when its label has no font. None of those produce an
error, and from outside the running game they are indistinguishable from each other.

A clone of a button the game already built inherits its atlas, sprite, font, depth and anchoring —
every one of the things that cannot be checked from here. Building one from scratch would mean
guessing at five things at once, and a launch to find out which was wrong.

## Non-goals

- The panel itself stays as it is. Turning it into NGUI is a much larger change and does not belong
  with the first button.
- No custom icon yet. The clone keeps the camera's sprite until there is something to replace it
  with; a sprite name the atlas does not hold renders as nothing.

## Game API this depends on, and how it was confirmed

| Member | Confirmed by |
|---|---|
| `UIButton.onClick : List<EventDelegate>` | Reflection: declared fields |
| `new EventDelegate(EventDelegate.Callback)` | Reflection: constructor list |
| The paths above | The survey, run in a loaded vault |

## What cannot be verified without a running game

That the button appears where it should, looks right, and does not overlap the screenshot button.
