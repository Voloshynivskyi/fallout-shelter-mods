# Design — item icons

## Drawing an NGUI sprite in the current panel

```
ItemParameters.WeaponAtlas : UIAtlas
    .texture            : Texture
    .GetSprite(name)    : UISpriteData  ->  x, y, width, height   (pixels)

DwellerWeaponItem.WeaponSprite : string
```

An atlas is a texture plus a rectangle, which is what `GUI.DrawTextureWithTexCoords` takes. The
coordinates it wants are normalised, and NGUI measures y downward from the top while the drawing
call measures upward from the bottom, so y is flipped:

```
new Rect(sprite.x / w,
         1f - (sprite.y + sprite.height) / h,
         sprite.width / w,
         sprite.height / h)
```

This is deliberately not tied to IMGUI: what it needs is the sprite name and the atlas, both of
which the NGUI panel will use directly through `UISprite`. The lookup written here carries over; only
the drawing call is replaced.

## Resolving the sprite name

Sprite names sit on the family-specific type — `WeaponSprite` on a weapon — and are reached through
the same reflection helper as the id, for the same reason: the member differs per family and some
are not public. Names are resolved once, when the catalogue is built, not per frame.

An atlas miss returns null and the row simply has no icon. A picker that hides items it cannot
illustrate would be worse than one with a gap.

## Cost

Nothing is created per frame. The catalogue holds the sprite name; the atlas lookup happens once per
family and is cached; drawing is one call per visible row, and the visible rows are already capped.
