# admin-panel

## ADDED Requirements

### Requirement: The panel grants resources through the game's own code

The panel SHALL grant resources by calling the game's resource-adding method with capping and
callbacks enabled. It SHALL NOT assign resource fields directly.

#### Scenario: Granting an amount

- **WHEN** the player presses a grant button for a resource
- **THEN** that resource increases by the stated amount
- **AND** the game's own interface shows the new figure without needing a reload

#### Scenario: Granting past the cap

- **WHEN** a grant would take a resource above what the vault can hold
- **THEN** the resource is raised to its cap and no further
- **AND** nothing is lost or wrapped around

#### Scenario: Filling to cap

- **WHEN** the player presses the fill button for a resource
- **THEN** that resource is raised to exactly its cap

### Requirement: Boxes are granted through the vault, not as resources

Lunchboxes, Mr Handy boxes and pet carriers SHALL be granted through the vault's own box-adding
method. They SHALL NOT be granted through the resource path, even though the resource enum contains
members with those names, because writing those members has no effect.

#### Scenario: Granting boxes

- **WHEN** the player grants a quantity of a box type
- **THEN** that many boxes of that type appear in the vault and can be opened

#### Scenario: The resource rows exclude boxes

- **WHEN** the panel lists resources
- **THEN** the three box-shaped resource members are absent from that list, so a box cannot be
  granted by two different routes

### Requirement: Nothing is granted without a loaded vault

The panel SHALL grant nothing unless a vault is loaded, and SHALL show no grant controls when one
is not.

#### Scenario: At the main menu

- **WHEN** the panel is open with no vault loaded
- **THEN** no grant control is shown, and no write is attempted

### Requirement: A failed grant leaves the vault untouched

Any exception raised while granting SHALL be caught, logged with the resource and amount that
failed, and SHALL NOT propagate. A grant that fails part way SHALL NOT leave the vault in a state
the game cannot load.

#### Scenario: A grant throws

- **WHEN** granting raises an exception
- **THEN** the mod logs what failed, the game keeps running, and the vault still saves and loads

### Requirement: A granted vault loads without the mod

Anything this panel grants SHALL be ordinary game data. A vault that has received grants SHALL load
and play normally with the plugin removed.

#### Scenario: Removing the mod after granting

- **WHEN** resources and boxes have been granted, the game saved, and the plugin deleted
- **THEN** the vault loads normally and the granted resources and boxes are still there
