# admin-panel

## ADDED Requirements

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
