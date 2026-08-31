# admin-panel Specification

## Purpose
TBD - created by archiving change add-panel-skeleton. Update Purpose after archive.

## Requirements

### Requirement: The mod does nothing unless deliberately enabled

Vault Admin SHALL default to disabled. With the plugin installed and its configuration untouched,
the game SHALL behave exactly as it does without the plugin.

#### Scenario: Installed but never enabled

- **WHEN** the plugin is installed and the game is started with a freshly generated configuration
- **THEN** no panel is reachable, no hotkey responds, and no game state is read or altered
- **AND** the log carries a single line naming the mod, its version, and that it is disabled

#### Scenario: Enabled

- **WHEN** `Enabled` is set to true in the configuration and the game is restarted
- **THEN** the hotkey opens and closes the panel

### Requirement: The panel opens and closes on a hotkey

The mod SHALL bind a configurable key that toggles the panel. It SHALL read that key through the
new Input System, because legacy `UnityEngine.Input` throws in this build of the game.

#### Scenario: Toggling

- **WHEN** the player presses the configured key
- **THEN** the panel appears if it was hidden, and disappears if it was shown

#### Scenario: The key is misconfigured

- **WHEN** the configured key name does not correspond to a key
- **THEN** the mod logs a warning naming the bad value, falls back to its default key, and continues
  to work

### Requirement: The panel reports live vault state and changes nothing

The panel SHALL display the vault's current resources, its dweller count, and its inventory size,
read from live game state. It SHALL NOT write any game state.

#### Scenario: Reading state

- **WHEN** the panel is open in a loaded vault
- **THEN** it shows each resource with its current amount, the number of dwellers, and the number of
  items held
- **AND** the values match those shown by the game's own interface

#### Scenario: No vault loaded

- **WHEN** the panel is opened at the main menu, before a vault is loaded
- **THEN** it reports that no vault is loaded and shows no figures, rather than throwing

#### Scenario: Removing the mod

- **WHEN** the plugin is deleted after any amount of use
- **THEN** every vault loads and plays exactly as before, because nothing was ever written

### Requirement: A failure in the panel never reaches the game

Any exception raised while building, drawing or reading for the panel SHALL be caught and logged.
It SHALL NOT propagate into Unity's update or render loop.

#### Scenario: The panel throws

- **WHEN** reading a value for display raises an exception
- **THEN** the mod logs a warning naming what failed, and the game continues to run normally

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
