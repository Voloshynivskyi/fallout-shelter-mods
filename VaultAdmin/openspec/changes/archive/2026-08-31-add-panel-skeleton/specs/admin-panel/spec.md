# admin-panel

## ADDED Requirements

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
