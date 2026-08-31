# admin-panel

## ADDED Requirements

### Requirement: Items are shown with their own picture

Each item offered by the panel SHALL be shown with the icon the game uses for it, drawn from the
game's own atlas for that item family.

#### Scenario: Browsing items

- **WHEN** the player looks at the item list
- **THEN** each row shows that item's icon beside its name and rarity

#### Scenario: An item has no sprite

- **WHEN** an item names a sprite the atlas does not hold
- **THEN** the row still lists the item and remains grantable, with a blank space where the icon
  would be

#### Scenario: The atlas is unavailable

- **WHEN** the atlases cannot be reached
- **THEN** the list is drawn without icons rather than failing to draw
