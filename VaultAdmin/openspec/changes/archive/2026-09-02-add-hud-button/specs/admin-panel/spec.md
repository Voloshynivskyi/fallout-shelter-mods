# admin-panel

## ADDED Requirements

### Requirement: The panel opens from the game's interface

The mod SHALL place a button in the vault interface that opens and closes the panel, so it can be
reached without knowing a key.

#### Scenario: Opening from the button

- **WHEN** the player presses the button in the vault HUD
- **THEN** the panel opens, and pressing it again closes it

#### Scenario: The hotkey still works

- **WHEN** the configured key is pressed
- **THEN** the panel opens and closes as before

#### Scenario: The button cannot be placed

- **WHEN** the part of the interface it attaches to cannot be found
- **THEN** the mod logs where it looked, the hotkey continues to work, and the game is unaffected

### Requirement: The button belongs to the interface

The button SHALL be made by cloning one the game already built, so that it inherits its appearance
and placement rather than being described from scratch.

#### Scenario: Appearance

- **WHEN** the button is shown
- **THEN** it matches the interface around it in size and style

#### Scenario: The clone carries nothing it should not

- **WHEN** the button is created
- **THEN** it does only what this mod asks of it, and nothing the button it was cloned from used to
  do

### Requirement: The button is created once

The mod SHALL NOT create more than one such button, however many times the interface is rebuilt.

#### Scenario: Reopening the vault

- **WHEN** the player leaves the vault and returns
- **THEN** exactly one button is present
