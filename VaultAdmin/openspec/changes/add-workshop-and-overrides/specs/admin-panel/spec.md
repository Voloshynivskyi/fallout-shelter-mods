# admin-panel

## MODIFIED Requirements

### Requirement: The panel is divided into four pages

The panel SHALL group its work onto four pages, each named for what it holds rather than for what
is done to it: RESOURCES, ITEMS, WORKSHOP and OVERRIDES.

#### Scenario: Choosing a page

- **WHEN** the player presses one of the four page buttons
- **THEN** that page is shown, the chosen button is drawn as chosen, and the others are not

#### Scenario: A page keeps its place

- **WHEN** the player leaves a page and returns to it
- **THEN** the page is rebuilt in a state that matches what it would produce, rather than in the
  state it was left in

## ADDED Requirements

### Requirement: A dweller is built to order and shown before it is made

The panel SHALL offer a bench that builds one dweller from a name, a gender, a rarity, a level, the
seven SPECIAL values, an appearance and a set of gear. Beside the fields it SHALL show a live
figure of the dweller those fields describe.

#### Scenario: What is shown is what is made

- **WHEN** the player sets any field on the bench
- **THEN** the figure beside the fields changes to match, and creating the dweller produces one that
  matches the figure

#### Scenario: The bench clears itself

- **WHEN** a dweller has been created
- **THEN** every field returns to a value the bench would produce, and the figure is rebuilt to
  match, so the page never describes somebody who has already left

#### Scenario: The figure is not a real dweller

- **WHEN** the bench is closed, the tab is changed, or the panel is hidden
- **THEN** the figure is put back where it was borrowed from, and nothing about it reaches the vault

### Requirement: An appearance has no empty option

Every appearance slot SHALL hold a real value at all times. The panel SHALL NOT offer "random" or
"leave it alone" as a choice in any appearance list, because such a choice writes nothing and leaves
the created dweller wearing whatever the game rolled — which is never what the figure showed.

#### Scenario: Opening the bench

- **WHEN** the bench is opened, the gender is changed, or a dweller has just been created
- **THEN** hair, face, hair colour and skin each hold a real value chosen at random

#### Scenario: Rolling again

- **WHEN** the player presses the die inside the figure's box
- **THEN** all four appearance slots take new random values and the figure is redrawn to match

#### Scenario: Gear is not an appearance

- **WHEN** the outfit or the weapon is set to none
- **THEN** the created dweller has none, and the figure wears the vault's plain outfit rather than
  whatever it wore before

### Requirement: An animal is built to order

The panel SHALL offer a bench that builds one pet from a breed, a grade, a name, a bonus and the
value of that bonus.

#### Scenario: Only bonuses that exist

- **WHEN** the bonus list is offered
- **THEN** it holds only the bonuses that pets in this build of the game are actually made with, and
  not every value the type defines

#### Scenario: Grade is grade

- **WHEN** a grade is chosen
- **THEN** the row says only how the game grades it, and what the animal does is said by the bonus
  row instead

### Requirement: The bench leaves the vault's storage as it found it

Dressing the bench's figure calls the game's own equip methods, and the game returns whatever was
being worn to the vault's storage. Anything that reaches storage this way SHALL be taken back.

#### Scenario: Dressing the figure

- **WHEN** the figure is dressed, redressed, or has its weapon removed
- **THEN** the vault holds exactly what it held before

#### Scenario: Something else arrives at the same moment

- **WHEN** an item that the bench did not mint appears in storage while the figure is being dressed
- **THEN** it is left alone, and the panel says in the log what it found and what it expected

#### Scenario: The removal does not take

- **WHEN** the game's own removal is called and the vault's contents do not shrink
- **THEN** the panel does not report a removal it did not make

### Requirement: A grant says so on the row that was pressed

Granting anything SHALL be confirmed on the row the player pressed, since a message at the edge of
the screen answers whether anything happened and not whether it happened to that row.

#### Scenario: Granting

- **WHEN** the player presses GIVE on any row
- **THEN** that row's own line says so for a moment, its button moves under the press, and both
  return to what they were

#### Scenario: Nothing happened

- **WHEN** the grant is refused
- **THEN** the row says nothing, because it did nothing

### Requirement: The overrides page holds the vault-wide switches

The panel SHALL offer a page of actions and switches that act on the whole vault. Switches SHALL
persist between sessions, SHALL be re-asserted while the game runs, and SHALL be restored to what
the game had when the mod is disabled.

#### Scenario: A switch is thrown

- **WHEN** a switch is turned on
- **THEN** the panel writes the game's own value down before changing it, and holds the new value
  against the game changing it back

#### Scenario: The mod is disabled

- **WHEN** the mod is disabled or the game closes
- **THEN** every value the panel took over is put back as it was found

#### Scenario: Another vault is loaded

- **WHEN** a different vault is loaded
- **THEN** values belonging to a vault are forgotten and read again, and values belonging to the
  game as a whole are not

### Requirement: The vault can be staffed by ability

The panel SHALL offer to fill every room in the vault from the dwellers available, judged by the one
SPECIAL stat that room runs on.

#### Scenario: A working room

- **WHEN** a room produces something and runs on a stat
- **THEN** it is given the highest scorers in that stat, and rooms with more places are filled first

#### Scenario: A training room

- **WHEN** a room teaches a stat rather than producing anything
- **THEN** it is given the LOWEST scorers in that stat, since training raises what it teaches and a
  dweller at the maximum learns nothing

#### Scenario: A room the panel has never heard of

- **WHEN** a room is added by a game update or by another mod
- **THEN** it is classified by asking the room itself which stat it uses, how many places it has and
  whether it produces anything, rather than by matching its name against a list

#### Scenario: The game offers no way to assign

- **WHEN** no method for assigning a dweller to a room can be found
- **THEN** nothing is assigned, the player is told, and every method that mentions assigning is
  written to the log

### Requirement: Everything drawn is drawn at the size it is shown

Textures the panel draws for itself SHALL be generated at the size the widget shows them at. A
texture shown at a different size has its rounded corners pulled out of shape.

#### Scenario: A plate is stretched

- **WHEN** a drawn plate is shown at a size other than the one it was drawn at
- **THEN** the panel names that plate in the log once, with both sizes

#### Scenario: A fill

- **WHEN** the texture has no corners to pull
- **THEN** it may be stretched over anything, and is not reported
