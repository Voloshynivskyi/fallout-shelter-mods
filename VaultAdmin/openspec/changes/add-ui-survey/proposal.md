# Survey the game's interface before building in it

## Why

The panel has to become part of the game's interface rather than a window floating over it. That is
the last thing the brief asks for, and it is the one piece of this project that cannot be checked
without eyes: an NGUI widget with the wrong depth, the wrong parent or the wrong atlas renders as
nothing at all, with no error anywhere.

Guessing at it would cost a launch per guess. That already happened with the dwellers: three
attempts at why their equipment slots were dead, each a launch, before a diagnostic settled it in
one run by printing every condition side by side.

So the diagnostic comes first this time.

## What changes

A one-shot survey, off by default, that writes to the log:

- every UI root in the scene, with its scaling and its size
- every panel under them: name, depth, clipping, parent chain
- the windows the game itself builds, so one can be cloned rather than invented
- the atlases in use and what a sprite from each is called
- the fonts in use, since a label with no font draws nothing

Nothing is created and nothing is drawn. This change only reads.

## Non-goals

- No NGUI panel yet. That is the next change, built on what this finds.
- No removal of the current window. It keeps working until there is something to replace it with.

## Game API this depends on, and how it was confirmed

| Member | Confirmed by |
|---|---|
| `UIRoot`, `UIPanel`, `UIAtlas`, `UISprite`, `UILabel`, `UIButton`, `UIWidget`, `UITexture`, `UIEventListener` | Reflection over `Assembly-CSharp.dll`: all nine present |
| `UIAtlas.texture`, `.spriteList`, `.GetSprite(string)` | Reflection |
| `UISpriteData.x/y/width/height` | Reflection |

What a `UIPanel` and a `UIWidget` expose about depth and clipping is part of what the survey is for;
it reads them by reflection and reports what it finds rather than assuming names.

## What cannot be verified without a running game

Everything this produces. That is the point: it is a way to spend one launch learning the facts
instead of several launches guessing at them.
