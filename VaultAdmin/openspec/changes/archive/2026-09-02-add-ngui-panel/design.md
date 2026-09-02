# Design

## Why not the game's atlas for the panel's own frame

The obvious way to draw in the game's style is to use its sprites. It was tried for the HUD button
and failed: every accessor reported the atlas as having no texture, and four rounds of guessing at
the style came out of it. So the panel's frame, rows and buttons are textures drawn at runtime from
the palette instead — rounded frames in three weights, cached by size. It costs a few kilobytes and
depends on nothing.

The item icons are the exception, and they are the case where the atlas route works: a `UISprite`
takes an atlas and a sprite name and resolves the rest itself, with no texture access from here.

## Why the pages are built once

Widgets are built once and switched by activation; rows are rewritten rather than rebuilt when the
list is paged or filtered. A panel meant to be opened often should not be a source of garbage, and
NGUI has no layout pass here — positions are computed once, from the measured window, rather than
every frame.

## Why the window is measured rather than fixed

`UIRoot.activeHeight` gives the height the interface is scaled to; the width follows from the
screen's aspect. A fixed size would be a third of the width on one screen and the whole of it on
another.

## Why the scaffold stays

If no UI root or font is available, a window built from NGUI draws nothing and says nothing about
it. The scaffold is the difference between a mod that is degraded and one that is unreachable, so it
stays behind a check for the window's absence.

## Why the pet controls take rows from the list

Pets are the one family that carries data per copy, so they are the one the panel can name and tune.
Their controls appear only for them, and the list gives up its last two rows rather than the page
growing past the window's bottom edge.

## Why the filter is read on the refresh tick

`UIInput` has a change event, but the panel already runs a refresh tick while it is open to keep the
resource figures current. Reading the filter there costs one string comparison every thirty frames
and avoids wiring a second event path into widgets built by hand.
