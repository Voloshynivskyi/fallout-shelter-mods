# Design — resource and box grants

## The write path

```
Vault.Instance.Storage                                  // VaultStorage : Storage
    .AddResource(new GameResources(resource, amount),
                 capped: true,
                 fireCallbacks: true)
```

`capped: true` lets the game clamp to the vault's own limit, so the panel never has to know what
the cap is or reason about overflow. `fireCallbacks: true` raises `OnResourceChangedEvent`, which is
what the game's interface listens to — without it the number on screen stays stale until something
else happens to refresh it.

`GetAvailableSpace()` returns the room left as a `GameResources`, which makes "fill to cap" a
one-liner: grant exactly the available space.

Boxes take a different route entirely:

```
Vault.Instance.AddLunchBox(ELunchBoxType.Regular, quantity)
```

## Why boxes cannot go through the resource path

`EResource` has `Lunchbox`, `MrHandy` and `PetCarrier` members, and the obvious move is to grant
them like anything else. It does not work — established in this repo before this mod existed:
writing those values changes nothing a player can see, because the real store is a list of
`LunchBox` objects on the vault, reached through `AddLunchBox`.

Leaving those three in the resource list would give the panel two routes to the same thing, one of
which silently does nothing. They are excluded from the resource rows, and their grants are offered
only as boxes.

## Two hypotheses tested and dropped

Recording these because the next person will have the same ideas.

**The game's debug menu could be driven directly.** `DebugInfo` is still present in the release
build, so it looks promising. Its methods are gone: the only one left is
`FunctionThatShouldNeverBeCalled_CreatedToAvoidBuildWarnings()`. The class survives because its
serialized UI fields do; the logic was stripped.

**`DebugOpenLunchboxes` grants lunchboxes.** Its name says so and it has `OpenXLunchbox(int)`. Read
the rest of it — `LogCards`, `LogCardRarity`, `TallyFormattedName`, `FormatTallyForDisplay` — and it
is a balance tool that simulates openings and tallies the odds. It gives the player nothing.

Both were killed by reflection in a couple of minutes. Neither cost a build or a launch.

## Amounts

Each resource row offers `+100`, `+1000`, `+10000` and `Fill`. Boxes offer `+1`, `+5`, `+25`.

Round numbers, and no free-text entry: a text field invites a typo of an extra zero, and the point
of capping is undermined if the panel encourages absurd values. Anyone wanting something specific
can press a button twice.

## Containment

Every grant is wrapped individually. A failure logs the resource and the amount that failed and
returns; it does not abort the frame, and it cannot escape into Unity's loop. The panel keeps
working, because a debug tool that dies on one bad button is worse than one that says what failed.

Grants are only offered when `Vault.Loaded` is true, which the panel already checks before drawing
anything at all.
