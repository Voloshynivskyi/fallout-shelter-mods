# Making a room look different — what is actually possible

Notes from the attempt to give Caps Foundry a look of its own rather than a recoloured Nuclear
Reactor. Written down because every one of these cost a game launch or a crash to establish, and
none of it is discoverable from the outside.

The work is paused, not abandoned. This is where to pick it up.

## The short version

A room's identity is its **mesh**. Not its texture, and not its colour.

Three approaches were considered:

| Approach | Verdict |
|---|---|
| Repaint the room's texture | Dead end — see below |
| Assemble a room from other rooms' parts | Works, but the ceiling is low |
| Ship a custom mesh in an AssetBundle | The only route to a genuinely new room. Needs 3D art |

## Rooms have no texture of their own

The Nuclear Reactor is drawn from two vault-wide atlases, both 1024×1024:

- `atlas.png` — generic surfaces: walls, floors, grating, lockers, control panels, posters
- `atlas_1.png` — decorations: Nuka-Cola signage, jukebox, world map, G.O.A.T. posters

`atlas.png` alone is shared by 39 materials across the Armory, Classroom, Nuka room, Science Lab,
Weapon Factory and `RoomImpostors`. Repainting it in place would change all of them at once.

Repainting a *private copy* is possible — materials are already instanced per room — but it only
changes surfaces. The reactor still reads as a reactor, because its silhouette is geometry.

Some rooms do have their own textures (`TEX_Barbershop_18`, `TEX_baldoza_armory`,
`TEX_outfit_factory_*`). The reactor has none.

Shaders in play: `Underground/Rooms/Unlit_VertexColor_LightmapModulated`,
`Underground/Rooms/FakeDynamicLightmap`, `Underground/Rooms/Lights`.

An earlier guess that "the shader is called FakeDynamicLightmap and the renderers are called
lightmap, so the geometry must be flat and all the detail lives in the texture" was **wrong**. The
dump disproved it.

## VisualDonor must be a Production room

Setting it to anything else **kills the game natively while the vault loads** — no exception, no
stack trace, the process is simply gone. Weapon Factory is a `Crafting` room, and choosing it
crashed every load.

The redirects point the room's scene, prefab and pool lookups at the donor, and a room object of
another class cannot be driven by `ProductionRoom`. This is now enforced: `RoomInfo.m_roomClass` is
checked at startup and a bad donor falls back with a warning.

Usable donors: `Energy2`, `Geothermal`, `NukaCola`, `Water2`, `Hydroponic`.

**Borrowing a single mesh from a Crafting room is fine.** Only the mesh and its materials are
copied onto a bare object, so no foreign logic comes with it. Body and prop are different problems.

## Placing props

Established by putting two copies of one object at opposite extremes and looking at which was
visible — one launch per question instead of one per guess.

- **Coordinates are fractions of the room**: `0` centre, `1` edge, `-1` the opposite edge. A Foundry
  can be one, two or three segments wide; fixed world offsets push props through the walls of a
  narrow room.
- **`z` negative is towards the camera.** `z = 0` is the middle of the room's depth, which is
  *behind* the machinery — props placed there attach correctly and are invisible. This wasted
  several launches.
- **Scale depends on where the mesh came from.** Room machinery is authored at room scale, so `1`.
  Hand props such as bottles need `3`–`8`.
- A room measures roughly `31 × 7 × 17` units at three segments wide. Depth varies by level:
  `28.65` at one, `16.60` at another.
- The mesh pivot sits at its **base**, so `y = 0` grows a prop upward from mid-height. `y ≈ -0.72`
  stands it on the walkway.

## What a vault actually offers

Only meshes belonging to rooms **standing in the vault** can be borrowed. A broad scan of everything
loaded in memory shows far more — Weapon Factory presses, Design Factory saws, Outfit Factory
conveyors — but those are assets for rooms the player has not built, and they are not reachable.

A real vault produced 38 meshes: lightmaps, doors, elevator parts, six Sunset Sarsaparilla bottles,
Nuka-Cola decals and lights, a few animated fittings from the Cafeteria, Water Plant and Power
Plant, and some unnamed geometry (`polySurface858`–`866`, `pasted__pCylinder22`).

**There was no industrial machinery in it.** No presses, no saws, no conveyors. Every vault will
differ, and a mod cannot rely on any particular room being built.

## Why this ceiling is low

Nothing here changes the silhouette. The body dominates, and the bodies available are the five
Production rooms above — each of which the player probably already has. Props help, but a few
bottles do not disguise a reactor.

A genuinely new room needs its own mesh, which means Unity 6000.0.58 and someone to model it.

## Tools

`tools/RoomTextureDump` dumps a room's textures to PNG with a manifest naming the renderer,
material, shader and shader property behind each file. It has two modes: the rooms standing in the
vault, and everything loaded in memory. Remember that the second shows meshes that cannot actually
be borrowed.

The mod itself logs the meshes a vault offers whenever a configured part cannot be found, which is
the quicker way to get the list.
