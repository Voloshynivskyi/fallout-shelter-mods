# What the game's interface actually looks like

From the survey run in a loaded vault. Written down because none of it is in the assembly — the
interface is assembled in the scene, so reading code finds nothing.

## Roots

Three UI roots. The one that matters is `MainScene_Root`.

## The anchor grid

NGUI lays each panel out on a three-by-three grid of anchors, named by position. In use:

```
1 TopLeft     2 Top       3 TopRight
              5 Center
7 BottomLeft  8 Bottom    9 BottomRight
```

## Where things are

```
MainScene_Root/GUI/VaultButtonsWindow/VaultButtonsPanel                      depth 2
    3 TopRight/Buttons/BTN Build                                             depth 3

MainScene_Root/GUI/VaultHUDWindow/VaultHUDPanel                              depth 4
    1 TopLeft/Info/Dweller Button                                            depth 1
    7 BottomLeft/BTN Camera                                                  the screenshot button
    9 BottomRight/BTN PipBoy/Notification                                    depth 5, SoftClip
```

`VaultButtonsHUD` sits on `VaultButtonsWindow` and holds `m_BuildButton` as a typed field, so the
build button is reachable from code. The camera button is not: it exists only as a scene object at
that path.

Nineteen buttons are on screen at once, out of 1180 loaded. Almost everything is a pooled object
waiting to be shown.

## Attaching a button of our own

```
UIButton.onClick : List<EventDelegate>
new EventDelegate(EventDelegate.Callback)
```

So a cloned button is re-pointed by clearing `onClick` and adding one delegate.

## Why cloning rather than building

An NGUI widget renders as nothing when its depth is below what it sits on, when its parent is wrong,
when its atlas lacks the sprite it names, or when its label has no font. None of those produce an
error, and from outside the running game they all look identical: a panel that is not there.

A clone of a button the game already built inherits its atlas, sprite, font, depth and anchoring —
every one of the things that cannot be checked from here. That is the whole argument for it.

## The menu on the right was rejected

The first idea was a button in the game's right-hand menu, beside settings, stats, boxes, missions
and storage. Two reasons it is not that:

- The menu is assembled in the scene, and no type in the assembly owns those buttons, so it can only
  be reached by path.
- It already fills the height of the screen, so another entry crowds it.

The bottom-left corner, next to `BTN Camera`, is empty and out of the way.
