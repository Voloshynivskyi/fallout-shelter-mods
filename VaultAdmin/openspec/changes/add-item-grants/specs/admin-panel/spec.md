# admin-panel

## ADDED Requirements

### Requirement: The item picker offers only items the game holds

The panel SHALL build its list of grantable items by reading the game's own item tables at runtime.
It SHALL NOT contain any hardcoded item identifier.

#### Scenario: Listing items

- **WHEN** the player opens the item section
- **THEN** every weapon, outfit and junk item the game holds is offered, each shown with its display
  name and rarity

#### Scenario: A game update changes the item set

- **WHEN** the game is updated and items are added or removed
- **THEN** the panel offers the new set without the mod being changed

### Requirement: Granting an item uses the identifier the game resolves by

An item SHALL be created with the identifier its own family is looked up by — the weapon id for
weapons, the outfit id for outfits — and added through the inventory's own add method.

#### Scenario: Granting a weapon

- **WHEN** the player grants a weapon
- **THEN** one of that weapon appears in the vault inventory, showing its correct name, icon and
  stats, and can be equipped

#### Scenario: The inventory is full

- **WHEN** the inventory has no space
- **THEN** the mod reports that it is full and grants nothing, rather than silently discarding

### Requirement: Items can be found without scrolling

The panel SHALL offer a text filter over the item list, matching on display name.

#### Scenario: Filtering

- **WHEN** the player types into the filter
- **THEN** only items whose display name contains that text are listed, case-insensitively

### Requirement: A granted item survives the mod being removed

A granted item SHALL be indistinguishable from one obtained in play.

#### Scenario: Removing the mod after granting items

- **WHEN** items have been granted, the game saved, and the plugin deleted
- **THEN** the vault loads and every granted item is present, named and usable
