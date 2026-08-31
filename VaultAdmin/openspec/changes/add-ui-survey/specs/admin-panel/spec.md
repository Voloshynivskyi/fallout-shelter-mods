# admin-panel

## ADDED Requirements

### Requirement: The panel can report the game's interface structure

The mod SHALL be able to write a description of the game's own interface to the log, on demand, and
SHALL do so without creating, changing or drawing anything.

#### Scenario: Running the survey

- **WHEN** the player asks for the survey
- **THEN** the log receives the UI roots, the panels beneath them with their depths, the game's own
  windows, the atlases and the fonts
- **AND** nothing in the game changes as a result

#### Scenario: Part of the interface is unavailable

- **WHEN** some part cannot be read
- **THEN** the survey reports what it could not read and continues with the rest, rather than
  stopping

### Requirement: The survey is off unless asked for

The survey SHALL run only when explicitly requested, and SHALL NOT run on load.

#### Scenario: Ordinary play

- **WHEN** the mod is enabled and the survey has not been asked for
- **THEN** nothing is surveyed and nothing is logged about the interface
