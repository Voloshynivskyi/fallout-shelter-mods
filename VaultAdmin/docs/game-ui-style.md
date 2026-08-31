# The game's window style

Read off the reference screenshots — warehouse upgrade, barbershop, weapon and outfit crafting,
build menu, lunchboxes, settings, objectives, achievements, collection, inventory, dweller card and
wasteland. Written down because the panel has to look like it belongs, and "looks about right" is
not something that can be checked from outside the running game.

## Palette

Given directly rather than matched by eye, after three failed attempts at the last one:

| Role | Value |
|---|---|
| Bright fill and text | `14FF17` |
| Dark fill behind text, and cut-outs | `085108` |
| Outline | `08600A` |

The dark interior of a window is not black: it is the game showing through, dimmed. A panel is a
translucent dark plate, not an opaque one.

## Structure, consistent across every window

- **Frame.** A rounded rectangle, bright outline two or three pixels thick, dark translucent inside.
- **Title.** Centred on the top edge, sitting *on* the border rather than inside the frame, in
  bright green capitals. The border breaks around it.
- **Rows.** Content sits in rectangles of its own, each outlined, laid out in a grid. Nothing floats
  loose on the background.
- **Headers.** A row of solid bright green with dark text — inverted against the ordinary rows.
  `CURRENT` and `UPGRADED` in the warehouse window, `WEAPON / COST / TIME` in crafting.
- **Buttons.** Two kinds, and the difference is meaning, not decoration:
  - *Ordinary*: outlined, transparent inside, bright text. `RESET`, `RANDOM`, `CANCEL`.
  - *Emphasis*: solid bright fill, dark text. `CLOSE`, `SAVE`, `CONFIRM`.
  - *Danger*: solid orange-red fill, light text. `DESTROY`, `RECALL`. Reserved for the irreversible.
- **Close.** Bottom right, always, as an emphasis button.
- **Capitals** everywhere in the green windows. Mixed case belongs to the paper-styled windows —
  quests, objectives, the collection — which are a different skin entirely and not what this copies.

## What this means for the admin panel

The green terminal skin, not the paper one: this is a systems window, the same family as settings
and the build menu.

Sections become outlined rows with a solid header apiece. Grant buttons are ordinary outlined
buttons — they are frequent and reversible. Nothing here earns the danger colour, because nothing
here destroys anything.
