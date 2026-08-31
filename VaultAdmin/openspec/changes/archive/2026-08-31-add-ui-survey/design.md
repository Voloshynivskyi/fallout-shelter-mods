# Design — interface survey

## Why a survey rather than an attempt

An NGUI widget renders as nothing when its depth is below what it sits on, when its parent is wrong,
when its atlas does not hold the sprite it names, or when its label has no font. None of those
produce an error. From this side of the screen every one of them looks identical: a panel that is
not there.

The dweller work established what that costs. Three guesses, three launches, and then a diagnostic
that answered it in one. This starts where that ended up.

## What it reads

```
UIRoot            scaling style, manual height, the transform beneath it
UIPanel           name, depth, clipping, sorting order, parent chain
UIWidget          type, depth, dimensions, anchors
UIAtlas           name, texture size, how many sprites, a sample of their names
UILabel           the font each uses — bitmap or dynamic
```

Windows are found by name: the game's own types ending in `Window`, `HUD` or `Popup` that exist in
the scene. One of them, of roughly the right shape, is what the real panel should be cloned from —
cloning a window the game already built is the only way to get its look, depth and parenting right
without seeing them.

## Reading, not assuming

Member names on `UIPanel` and `UIWidget` are read by reflection and reported as found. Writing
`panel.depth` in the code would compile against whatever this NGUI version calls it and quietly
report nothing if it is called something else. The survey exists to find out, so it asks.

## Cost and safety

One-shot, on a button, off by default. It walks the scene once, allocates strings, writes them, and
stops. Nothing is created, nothing is changed, no component is added. The worst it can do is log a
lot, which is why it is bounded: a cap on how many objects of each kind are reported, with a count
of how many were skipped.
