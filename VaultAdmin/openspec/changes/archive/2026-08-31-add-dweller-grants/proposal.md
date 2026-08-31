# Create dwellers

## Why

Dwellers are the last of the three things asked for, and the only one where every attribute really
is editable: unlike a weapon, a dweller is serialised in full, so a name, a rarity and seven SPECIAL
values all survive into the save.

## What changes

- Create a dweller with a chosen **rarity**, **gender** and **starting level**.
- Set its **first and last name**.
- Set all seven **SPECIAL** values.
- Create any **legendary** dweller the game defines, from the game's own list.

## Non-goals

- No editing of dwellers already in the vault. Creating is a bounded surface; rewriting existing
  save records is a much larger risk and belongs in its own change.
- No appearance controls yet — hair, skin and outfit colours are stored as packed integers and
  deserve a proper colour picker rather than a number box.
- No relations, pregnancy or equipment beyond what creation already takes.

## Game API this depends on, and how it was confirmed

| Member | Confirmed by |
|---|---|
| `DwellerManager.CreateDweller(EDwellerRarity, EGender, Vector3, Quaternion, int, string, string)` | Reflection: declared methods |
| `DwellerManager.VaultIsWithMaxPopulation : bool` | Reflection; used instead of `AddDweller`, which is private and which both creation calls already invoke |
| `DwellerManager.CreateSpecialDweller(UniqueDwellerData, Vector3, Quaternion, string, string)` | Reflection |
| `DwellerManager.LegendaryDwellers : UniqueDwellerData[]` | Reflection |
| `Dweller.Name`, `.LastName`, `.Rarity` — all writable | Reflection: `CanWrite` true |
| `Dweller.Stats : DwellerStats` then `GetStat(ESpecialStat) : SpecialStat` | Reflection |
| `SpecialStat.SetValueAndMinExp(int)` | Reflection: `Value` is read-only, this is the setter |
| `EDwellerRarity` — Common, Normal, Rare, Legendary | Reflection |
| `EGender` — Any, Male, Female | Reflection |
| `ESpecialStat` — Strength through Luck | Reflection |

## Why SetValueAndMinExp rather than SetValueOnly

`SpecialStat.Value` cannot be assigned. Three methods can change it, and the choice matters because
the save keeps a value and an experience figure side by side:

```json
{ "value": 5, "mod": 0, "exp": 72084.23 }
```

`SetValueOnly` moves the value and leaves the experience where it was, which is a record describing
two different things at once. `SetValueAndMinExp` moves both, so what lands in the save is
consistent. A save the game cannot make sense of is the failure this whole project is built to
avoid.

## What cannot be verified without a running game

That a created dweller walks into the vault and behaves normally, and that the position passed to
the creation call puts them somewhere sensible.
