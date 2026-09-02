# release Specification

## Purpose
TBD - created by archiving change add-release-pipeline. Update Purpose after archive.

## Requirements

### Requirement: The version has one home

The version SHALL be written in exactly one place — the source — and every other use of it SHALL be
read from there.

#### Scenario: Building

- **WHEN** the mod is built
- **THEN** the version stamped into the assembly, the name of the release archive and the line the
  build prints all come from the same declaration in the source

#### Scenario: A second copy

- **WHEN** a build script, a document or a manifest carries its own copy of the version
- **THEN** that is a defect: it will disagree, and the disagreement will be believed

### Requirement: A build says what the compiler said

The build SHALL show the compiler's warnings. It SHALL NOT report success in a way that hides them.

#### Scenario: The build succeeds with warnings

- **WHEN** the compiler emits warnings and the build otherwise succeeds
- **THEN** the count and the text of every warning are printed before the success line

#### Scenario: The build fails

- **WHEN** the compiler fails
- **THEN** its whole output is printed and the build stops with an error

### Requirement: Nothing is installed over a running game

The install SHALL refuse to replace the plugin while the game is running, because the file is locked
and a partial or skipped copy would be reported as success.

#### Scenario: The game is open

- **WHEN** an install is attempted while the game is running
- **THEN** the install stops and says so, and the previously installed plugin is untouched

### Requirement: An install is proved by content

The install SHALL prove the file arrived by comparing what landed against what was built, by
content rather than by size.

#### Scenario: The copy took

- **WHEN** the plugin is installed
- **THEN** the hash of the landed file is compared against the hash of the build, and any difference
  stops the install with both hashes named

#### Scenario: A change that does not alter the size

- **WHEN** a build differs from the previous one only in ways that leave its length unchanged
- **THEN** the check still detects a copy that did not happen

### Requirement: The installed plugin is verified against the build

There SHALL be a check, separate from the build, that answers whether the plugin the game will load
is the one that was just built and whether it contains the features it should.

#### Scenario: Identity

- **WHEN** the check runs
- **THEN** it compares the installed file against the build by hash and reports plainly whether they
  are the same file

#### Scenario: Features

- **WHEN** the check runs
- **THEN** it looks for a marker of each feature that should have shipped, decoding the assembly in
  every encoding those markers could be stored in

#### Scenario: The control

- **WHEN** the check reports that markers are present
- **THEN** it has also looked for a marker it expects to be absent, and that marker is one that
  could plausibly be found — a token no file could ever contain proves nothing about the search

#### Scenario: A marker is missing

- **WHEN** any expected marker is absent
- **THEN** the check fails and names which, rather than reporting a qualified success

### Requirement: The release archive carries what a stranger needs

The build SHALL stage an archive containing the plugin and the files a person installing it for the
first time needs, named for the version it contains.

#### Scenario: Packaging

- **WHEN** a release archive is produced
- **THEN** it is named for the version read from the source, and replaces any archive of that name
  rather than being added beside it
