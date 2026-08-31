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

### Requirement: The panel offers every pet the game holds

The pet list SHALL be read from the game's own pet catalogue at runtime, with no pet identifier
hardcoded.

#### Scenario: Listing pets

- **WHEN** the player opens the pet section
- **THEN** every pet in the game's catalogue is offered, showing its name, type, breed and rarity

### Requirement: A granted pet carries the chosen name, bonus and value

Before granting, the player SHALL be able to set the pet's name, choose its bonus from every effect
the game defines, and set the bonus value. The granted pet SHALL carry exactly those three.

#### Scenario: Granting a customised pet

- **WHEN** the player sets a name, picks a bonus, sets a value, and grants a pet
- **THEN** the pet arrives carrying that name, that bonus and that value
- **AND** the bonus applies in play as any other pet's would

#### Scenario: Leaving the name empty

- **WHEN** the name field is left empty
- **THEN** the pet keeps whatever name the game generated for it

### Requirement: A granted pet is built the way the game builds one

A pet SHALL be created through the game's own construction and its unique data generated by the
game, with only the chosen fields overwritten afterwards.

#### Scenario: Fields the panel does not offer

- **WHEN** a pet is granted
- **THEN** every part of its data the panel does not offer holds whatever the game generated, rather
  than a default chosen by the mod

#### Scenario: Removing the mod

- **WHEN** a customised pet has been granted, the game saved, and the plugin deleted
- **THEN** the vault loads and the pet is present with its name, bonus and value intact

### Requirement: The panel creates dwellers with chosen attributes

The panel SHALL create a dweller through the game's own creation method, with a chosen rarity,
gender and starting level, and SHALL admit it to the vault through the game's own admission method.

#### Scenario: Creating a dweller

- **WHEN** the player chooses a rarity, gender and level and creates a dweller
- **THEN** a dweller with those attributes joins the vault and behaves as any other

#### Scenario: The vault is at its population limit

- **WHEN** the vault cannot take another dweller
- **THEN** the mod reports it and creates nobody, rather than leaving a dweller with no home

### Requirement: A created dweller can be named

The panel SHALL let the first and last name be set before creation, and the created dweller SHALL
carry them.

#### Scenario: Naming

- **WHEN** a first and last name are given
- **THEN** the dweller carries exactly those

#### Scenario: No name given

- **WHEN** either name is left empty
- **THEN** the dweller keeps whatever the game generated for that part

### Requirement: SPECIAL can be set on creation

The panel SHALL let all seven SPECIAL values be set, and SHALL set them in a way that leaves the
dweller's stored experience consistent with the value.

#### Scenario: Setting SPECIAL

- **WHEN** the seven values are set and a dweller is created
- **THEN** the dweller shows exactly those values
- **AND** each stat's stored experience matches its value rather than contradicting it

### Requirement: Legendary dwellers can be created

The panel SHALL offer every legendary dweller the game defines, read from the game's own list, and
create the chosen one through the game's method for it.

#### Scenario: Creating a legendary dweller

- **WHEN** the player picks a legendary dweller and creates it
- **THEN** that dweller joins the vault with its own name, appearance and stats

### Requirement: A created dweller survives the mod being removed

A dweller created by the panel SHALL be ordinary game data, indistinguishable from one that arrived
through play.

#### Scenario: Removing the mod

- **WHEN** dwellers have been created, the game saved, and the plugin deleted
- **THEN** the vault loads and every created dweller is present and intact

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
