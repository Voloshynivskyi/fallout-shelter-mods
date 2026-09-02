# Design

## The figure on the bench

The dweller shown beside the fields is a real `Dweller`, checked out of the game's own
`DwellerPool`, parked eight thousand units under the vault on a layer nothing else draws, and
filmed by a disabled camera into a `RenderTexture` that the panel draws as an ordinary widget.

Four things had to be true before that worked, and each cost a round trip to learn:

- A pooled dweller arrives with **no pieces**. `GenerateRandomCustomization` is what fills it in;
  without it the shader has nothing to assemble and draws white.
- It arrives with **no outfit**, and `UpdateTexture` composes the head and stops when `m_outfit` is
  null. That is the head-only picture, and it was never a framing problem.
- It carries a `DwellerVisibilityDetector` that **stops the game** when something looks at it. It is
  removed with `DestroyImmediate`, because a deferred removal is still there in the frame the camera
  renders — which is how that crash happened twice.
- It carries **no `Animator` at all**. Dwellers are driven by the legacy `Animation` component with
  the game's own controller on top, and that controller replaces any clip anybody else sets, one
  frame later, for ever. The controller is switched off on the figure, and the clip comes from
  `Animation.clip` — the game's own answer to which clip is the idle.

The figure is never returned to the pool. Returning it would hand back an object with a destroyed
component, and the pool would then give that object to the game as a real dweller. The cost is two
dwellers held for the session; the alternative is a corrupted one.

## Why "random" had to stop being an option

`ApplyCustomization` only ever puts a piece **on**. There is no call that takes one off. So a slot
left on "random" applied nothing: the figure kept whatever random look it had been given, the
spawner rolled a different one for the dweller that walked away, and the two were never the same
person.

The fix is not a better "random". It is that an appearance has no empty value at all. Every slot
holds a real piece from the moment the page opens, so the figure and the dweller are described by
the same seven values.

## Why the storage guard exists, and why it verifies

Equipping the figure calls the game's `EquipWeapon`, which returns the previously worn item to the
vault. That item was fabricated for a picture and never came from the vault, so every re-dress left
a real weapon in storage — on every gender change, every visit to the page, every creation.

Taking it back is the only correct answer, and it is the most dangerous code in the mod, because it
deletes from the player's save. So it does three things it would not otherwise need to do:

1. It counts storage before and after, rather than trusting the equip call.
2. It compares the identifier of what it is about to remove against what the bench just minted, and
   leaves anything else alone, loudly. The newest row in storage belongs to whoever put it there —
   a finished craft, a squad home from the wasteland.
3. It believes a removal only when the count actually falls. The game's own removal returns a
   result, and reporting a removal that did not happen is worse than not removing at all.

## Staffing: greedy, and read from the game

Rooms are classified by asking the room: which stat it runs on, how many places it has, and whether
it produces anything. Each question is asked three ways, weakest last, so a room added by an update
or by another mod is classified by the same questions as the rooms that shipped. A room that runs on
a stat and produces nothing is a training room, whatever it is called.

Working rooms are filled first, largest first, from the top of the pool; training rooms last, from
the bottom. Greedy rather than optimal: the optimal assignment of fifty dwellers to twenty rooms is
a larger problem than one button deserves, and the difference is about a point of production.

The call that performs the assignment is found by name among the ones it could be. If none answer,
nothing is assigned and every method on `Room`, `Dweller` and `DwellerManager` that mentions
assigning is written to the log — because guessing at somebody else's API and then failing quietly
is how this project has lost most of its afternoons.

## Drawn art, and why any of it is drawn

Icons for the panel's own ideas — a die, an open padlock, a pair of chevrons, a ranking, a plus, a
minus — are drawn in code from distance fields, in the panel's own three greens. The game's atlas
has nothing that says "roll this again" or "sorted by ability", and the nearest matches were
actively wrong: an alarm clock for rushing is very nearly the opposite idea.

Two rules came out of drawing them:

- A texture must be generated at the size it is shown at, or its rounded corners are stretched with
  it. `Plate` now says so, by name, whenever that happens.
- A glyph sits where its font's baseline puts it, which is not the middle of a button. The plus and
  the minus in one row of buttons sat at visibly different heights until they were drawn.

## Text

Six sizes — Title, Heading, Row, Body, Note, Tiny — and `MakeLabel` starts every label on Row. The
ladder existed long before anything applied it, so most of the panel was drawn at whatever size the
borrowed font happened to carry, and there was no relationship between what a thing was and how
loudly it said so. Applying it in one place is what makes an item's name, a power's name and a
resource's name the same size, and what each of them does another.
