# Grant resources and boxes

## Why

The panel reads the vault correctly — every figure was confirmed against the game's own interface.
That makes this the moment to write, and resources are the right first write: they are simple,
bounded, and the game exposes its own method for them, so nothing has to be invented.

## What changes

- A row per resource in the panel with buttons granting fixed amounts, and a button to fill to cap.
- Boxes — lunchboxes, Mr Handy boxes, pet carriers — granted through the game's own method.
- Everything goes through the game's code, never through a field. `Storage.AddResource` respects
  caps and raises the callbacks the interface listens to; assigning the field directly would leave
  the UI showing a stale number and skip whatever else the game does on a change.

## Non-goals

- No removing resources. Adding is what the panel is for, and a "set to zero" button is a way to
  ruin a vault by misclick.
- No items, weapons, outfits, dwellers or pets. Each is its own change.
- Still no NGUI. The panel becomes real game UI in its own change; mixing that with the first write
  would leave two things to blame.

## Game API this depends on, and how it was confirmed

| Member | Confirmed by |
|---|---|
| `Storage.AddResource(GameResources, bool capped, bool fireCallbacks)` | Reflection over `Storage`, declared instance methods |
| `Storage.GetAvailableSpace() : GameResources` | Same |
| `Vault.AddLunchBox(ELunchBoxType, int quantity)` | Reflection over `Vault`, declared instance methods |
| `GameResources(EResource, float)` | Reflection: constructor list |
| `ELunchBoxType` — Regular, MrHandy, PetCarrier, StarterPack, NukaColaQuantum, PredefinedPack, Victor, Curie | Reflection: `Enum.GetNames` |

Two hypotheses were tested and **disproved** before landing on this, which is why the design is not
what it might have been:

- *The game ships a usable debug menu.* `DebugInfo` survives in the release build but its methods
  are stripped: the only one left is
  `FunctionThatShouldNeverBeCalled_CreatedToAvoidBuildWarnings()`. There is nothing to drive.
- *`DebugOpenLunchboxes` grants lunchboxes.* It simulates openings and tallies the results for
  balance work. It gives nothing to the player.

## Boxes are not resources, whatever the enum says

`EResource` contains `Lunchbox`, `MrHandy` and `PetCarrier`, and they are tempting. Writing them
does nothing: this repo established earlier that the real store is a list on the vault, reached
through `AddLunchBox`. The panel therefore routes boxes away from the resource path entirely, and
the three resource-shaped enum members are excluded from the resource rows so they cannot be
granted twice by two different routes.

## What cannot be verified without a running game

That the granted amounts appear in the game's own interface immediately, which is what
`fireCallbacks` is for. Everything else — that the write lands, that it respects caps, that the save
still loads without the mod — is provable from the save file.
