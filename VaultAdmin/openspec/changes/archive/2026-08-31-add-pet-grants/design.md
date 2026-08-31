# Design — pet grants

## The catalogue

```
Catalog.Instance.m_petsCustomizationData   (public field)
    .PetItems : List<DwellerPetItem>

DwellerPetItem
    PetId : string        the identifier a pet is created with
    BaseName : string     display name
    Type : EPetType       which atlas the pet's art lives in
    Breed : EPetBreed
    BonusEffectList       the effects this pet normally rolls
```

The field's own type does not resolve by name from the assembly, so `PetItems` is read through the
same reflection helper the item catalogue uses. The field itself is public and was confirmed so.

## Creating one

Taken verbatim from the IL of `SeasonPassRewardItem.GenerateRandomPet`:

```
DwellerItem item = new DwellerItem(EItemType.Pet, petItem.PetId);
item.ExtraData = petItem.GenerateRandomData(null);
```

Then, and only then, the three chosen fields are written onto the returned `PetUniqueData`.

The order matters. Generating first means everything `GenerateRandomData` fills in stays filled in,
so the result is a pet the game built with three fields changed — rather than a record assembled
field by field and hoped over, which is how saves stop loading.

`GenerateRandomData` takes a `Random`, and the game passes null, so the panel does too.

## Bonuses

`EBonusEffect` has 37 members and each pet template carries a `BonusEffectList` of the ones it would
normally roll. The panel offers **all** of them rather than just that list: this is a debug panel,
the field is per-copy data the game reads back without checking it against the template, and
restricting the choice would remove the point of having it.

The value is a plain number field, unclamped. The game stores a float and the request was for
control over it.

## Why not pet icons yet

Item atlases are properties that are simply there. `PetAtlasManager.LoadAtlases(EPetType)` returns a
`Coroutine`: pet art loads asynchronously, per type, on demand. Drawing a list of pets means either
requesting every type's atlas up front or drawing rows whose icons appear a moment later. Both are
real designs, and neither belongs in the change that first makes pets grantable.
