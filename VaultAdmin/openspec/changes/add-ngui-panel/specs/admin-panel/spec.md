# admin-panel

## ADDED Requirements

### Requirement: The panel is drawn in the game's interface

The panel SHALL be built from the game's own widget types and parented under its UI root, so that it
scales with the interface and is drawn in the game's own style rather than over it.

#### Scenario: The panel is opened

- **WHEN** the player opens the panel
- **THEN** a window appears in the game's style, sized and scaled with the rest of the interface

#### Scenario: The interface cannot be reached

- **WHEN** no UI root or font is available
- **THEN** the mod falls back to the plain scaffold, so the panel is never unreachable

### Requirement: The panel is divided into three tabs

The panel SHALL present resources, dwellers, and items and pets as three tabs, with one shown at a
time and the chosen one marked.

#### Scenario: Choosing a tab

- **WHEN** the player presses a tab
- **THEN** that tab's page is shown, the others are hidden, and the chosen tab is drawn as the
  emphasised control

#### Scenario: Switching back

- **WHEN** the player returns to a tab
- **THEN** the page appears as it was left, without being rebuilt

### Requirement: The panel leaves the game's own controls reachable

The window SHALL occupy at most a third of the screen's width, sit against the left edge, and stop
short of the full height.

#### Scenario: A different screen

- **WHEN** the panel is opened on any screen size or aspect
- **THEN** its width is at most a third of the interface's width, and space remains above and below
  it for the game's own controls

### Requirement: Items are listed with the game's own art

Items SHALL be listed with the picture the game itself draws for them, taken from the family's
atlas.

#### Scenario: Browsing a family

- **WHEN** the player chooses a family
- **THEN** the list shows each item's own icon and name, a page at a time

#### Scenario: An item with no picture

- **WHEN** an item's sprite cannot be resolved
- **THEN** the row is still listed and still grants, with the picture left blank

### Requirement: The list can be filtered and paged

The items page SHALL offer a filter and paging, so a family of any length can be reached.

#### Scenario: Filtering

- **WHEN** the player types into the filter
- **THEN** the list shows only matching items, from its first page

#### Scenario: Nothing matches

- **WHEN** no item matches the filter
- **THEN** the list says so rather than showing an empty frame

### Requirement: Dwellers and pets are configured in the panel

The panel SHALL offer, in the game's own controls, the attributes the grant paths already accept:
name, rarity, gender, level and SPECIAL for a dweller; name, bonus and bonus value for a pet.

#### Scenario: Creating a dweller

- **WHEN** the player fills the fields and presses create
- **THEN** a dweller with those attributes arrives at the vault door, as before

#### Scenario: Granting a pet

- **WHEN** the player sets a name and bonus and grants a pet
- **THEN** the pet is added carrying them
