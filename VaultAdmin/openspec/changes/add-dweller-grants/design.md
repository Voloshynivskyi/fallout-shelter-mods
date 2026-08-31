# Design — dweller creation

## The creation path

```
DwellerManager.Instance
    .CreateDweller(rarity, gender, position, rotation, level, initialOutfit, initialWeapon) : Dweller
    .CreateSpecialDweller(uniqueData, position, rotation, outfitOverride, weaponOverride) : Dweller
    .LegendaryDwellers : UniqueDwellerData[]
    .VaultIsWithMaxPopulation : bool
```

Both creation calls admit the dweller themselves — the IL of each ends in a call to the private
`AddDweller`. The first draft of this design called `AddDweller` after creating, which was both
inaccessible and redundant; the dweller is already in the vault by the time the call returns.

That is also why the population limit is checked with `VaultIsWithMaxPopulation` **before** creating
rather than by reacting to a refusal afterwards: by then there is nothing to refuse.

Editing happens after the call returns. Name, rarity and SPECIAL are fields on the object, so
setting them once the dweller is in the vault works exactly as setting them before would have.

## Position

`CreateDweller` takes a world position. Rather than inventing coordinates, the panel reuses the
position of a dweller already in the vault, and falls back to the origin when the vault is empty.
A living dweller is by definition standing somewhere the game considers valid.

## Setting SPECIAL

```
dweller.Stats.GetStat(ESpecialStat.Luck).SetValueAndMinExp(10)
```

`SpecialStat.Value` is read-only and three methods can change it. The save keeps a value and an
experience figure together — `{"value": 5, "mod": 0, "exp": 72084.23}` — so a setter that moves only
one of them leaves a record describing two different things. `SetValueAndMinExp` moves both.

`ESpecialStat` has `None` and `Max` bracketing the seven real stats; both are skipped.

## Legendary dwellers

`LegendaryDwellers` is an array of `UniqueDwellerData` the game already holds, and
`CreateSpecialDweller` is the call it uses for them. A legendary dweller brings its own name,
appearance and stats, so the panel offers no editing for those: overwriting them would produce
something that looks legendary and is not.

## Population limit

`AddDweller` returns a bool. A false return means the vault would not take the dweller, and the
panel says so. Creating someone the vault refuses to admit leaves an object with no home, which is
the sort of thing that shows up later as a save that will not load.

## Why existing dwellers are not editable here

Editing a dweller already in the vault means rewriting a live record with relations, a room
assignment, pregnancy state and equipment hanging off it. Creation touches none of that. The two
have different risk profiles and do not belong in the same change.
