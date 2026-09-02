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

### Requirement: The button sits beside the thing it belongs with

The panel button SHALL be placed under the game's own dwellers button, found by searching the vault
HUD rather than by a fixed path, so that a change to the interface moves it rather than breaking it.

#### Scenario: The dwellers button is found

- **WHEN** the vault HUD carries a dwellers button that is switched on and takes a press
- **THEN** the panel button is cloned from it and placed directly beneath it

#### Scenario: Several candidates

- **WHEN** the HUD carries more than one button for the dwellers list, as it does when it keeps one
  per layout
- **THEN** only one that is switched on and accepts a press is used, preferring the one in the
  corner the player sees

#### Scenario: None is found

- **WHEN** no such button can be found
- **THEN** the panel button falls back to the position it had before, the hotkey still works, and
  every candidate that was considered is written to the log with the reason it was passed over

### Requirement: The panel is drawn in one palette and one set of sizes

The panel SHALL draw itself in three greens and nothing else, and SHALL set every label to one of
six named sizes chosen for what the label is rather than for where it sits.

#### Scenario: A colour

- **WHEN** any part of the panel is drawn
- **THEN** its colour is one of the three, or one of the three at reduced strength

#### Scenario: A label

- **WHEN** a label is created
- **THEN** it is given a size from the ladder, and labels that say the same kind of thing in
  different places are given the same one

### Requirement: Things are named as the player knows them

The panel SHALL show the name a player would use, not the name the game's code uses.

#### Scenario: A resource

- **WHEN** a resource is listed
- **THEN** it reads CAPS, POWER, STIMPAK and so on, rather than the identifier its enum carries

#### Scenario: A thing the game has no name for

- **WHEN** the game's own name for something is a bare number, as it is for most hairstyles
- **THEN** the number is given something to be the number of, rather than being shown alone or
  replaced by an invented name

### Requirement: Diagnostics are off by default and say enough to act on

Every diagnostic SHALL be off in a fresh configuration. When one does run, it SHALL write down what
was actually found rather than that something was not.

#### Scenario: An ordinary session

- **WHEN** the mod is enabled with a fresh configuration and played with
- **THEN** the log carries no listings of atlases, animation clips or interface hierarchies

#### Scenario: Something the panel depends on is missing

- **WHEN** a member, method or list the panel needs cannot be found
- **THEN** the panel writes down what the object it was looking at actually holds, so the next
  attempt is informed rather than another guess

### Requirement: The panel takes the wheel and the drag while it is open

The game reads the mouse wheel and the drag for its own camera whatever the interface does, so
scrolling a list in the panel would zoom the vault at the same time. While the panel is open the
panel SHALL have them.

#### Scenario: Scrolling a list

- **WHEN** the player scrolls or drags inside the panel
- **THEN** the list moves and the vault behind it does not

#### Scenario: Closing the panel

- **WHEN** the panel is closed, or the mod is disabled
- **THEN** the game's camera answers to the wheel and the drag again, exactly as before

### Requirement: A panel that cannot be built still leaves a way in

If the panel cannot be built from the game's own widgets, the mod SHALL fall back to a plain
scaffold rather than leaving the player with a hotkey that does nothing.

#### Scenario: The interface cannot be reached

- **WHEN** the game's UI root, font or atlases cannot be found
- **THEN** a plain window is drawn instead, the log says what could not be found, and the game is
  otherwise untouched

#### Scenario: The fallback is not a second interface

- **WHEN** the fallback is shown
- **THEN** it offers the same actions and applies the same limits to what can be typed, so nothing
  reachable there can write a value the panel would have refused
