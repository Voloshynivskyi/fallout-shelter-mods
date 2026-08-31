# Give the panel the game's own interface

## Why

The panel was an IMGUI scaffold: a grey Unity debug window over a game that has a very particular
look. It was the right thing to build first — it proved every grant path works — but it is not what
was asked for. The panel should look and behave like the game's own windows, and it should be
divided so that the three things it does are three places rather than one long column.

## What changes

- The panel is rebuilt out of the game's own widget types (`UIPanel`, `UITexture`, `UISprite`,
  `UILabel`, `UIButton`, `UIInput`), parented under the game's UI root so it inherits the scaling
  that keeps the interface the same size on every screen.
- Three tabs: resources, dwellers, items and pets.
- The window takes a third of the screen's width down the left, and stops short of the full height
  so the game's own controls along the top and bottom of the overlay stay reachable.
- Items are listed with their own art, taken from the game's atlases.
- The IMGUI scaffold stays, but only as a fallback for the case where the window cannot be built.

## What it does not change

No grant path is touched. Resources, boxes, items, pets and dwellers are created exactly as before;
this change is the surface in front of them.
