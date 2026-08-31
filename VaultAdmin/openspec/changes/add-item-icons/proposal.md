# Show the item's own picture

## Why

A list of hundreds of names is not a picker. The game already holds a sprite for every item, and
showing it turns the list into something a person can actually choose from.

## What changes

- Each row in the item list shows that item's own icon, taken from the game's atlases.
- Rows get taller to fit the icon.

## Non-goals

- **No editing of weapon or outfit stats, rarity or name.** Not deferred — not possible. See below.
- No NGUI yet. Icons are drawn from the atlas texture directly, which works in the current panel and
  will carry straight over when the panel becomes real game UI.

## Game API this depends on, and how it was confirmed

| Member | Confirmed by |
|---|---|
| `DwellerWeaponItem.WeaponSprite : string` | Reflection: declared properties |
| `ItemParameters.WeaponAtlas`, `.OutfitAtlas`, `.JunkAtlas` : `UIAtlas` | Reflection: properties of type `UIAtlas` |
| `UIAtlas.texture : Texture` | Reflection |
| `UIAtlas.GetSprite(string) : UISpriteData` | Reflection |
| `UISpriteData.x, .y, .width, .height : int` | Reflection: fields |

An atlas gives a texture and a rectangle inside it, which is exactly what
`GUI.DrawTextureWithTexCoords` needs. The coordinates are normalised, and NGUI measures y from the
top while the drawing call measures from the bottom, so the row is flipped.

## Why weapon stats cannot be edited, established rather than assumed

The request was for per-weapon skills, stats, rarity and name. That is not something this game can
represent, and the proof is in two independent places:

- `ItemExtraData` is abstract with exactly four implementors: `DwellerDecorationItem`,
  `PetUniqueData`, `RecipeUniqueData`, `ThemeItemUniqueData`. Weapons and outfits are absent, so
  they have no per-instance data at all.
- A real save stores a weapon as four fields — `id`, `type`, `hasBeenAssigned`,
  `hasRandonWeaponBeenAssigned`. There is nowhere to put a damage figure or a custom name.

Damage, rarity and name live on the shared `DwellerWeaponItem` template. Writing them would change
every copy of that weapon in the game at once and would vanish on restart, because the save has
nowhere to keep it.

What is possible instead is granting **any** weapon the game holds, at any rarity, which the picker
already does.

Pets are different: `PetUniqueData` carries `Name`, `Bonus` and `BonusValue` per instance, so pets
really can be customised. Dwellers are serialised per instance too. Both get their own changes.
