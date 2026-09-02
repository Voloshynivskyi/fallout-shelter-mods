# Write down how a build becomes an install

## Why

There is no .NET SDK in this project. `build.ps1` calls `csc.exe` out of the framework directory,
stages an archive, installs into the game folder, and `tools/verify-install.py` answers whether the
plugin the game will load is the one that was just built. All of that exists and none of it is
written down, so its rules live only in the heads of whoever wrote it — and three of those rules
were learned by getting them wrong.

The version disagreed with itself twice: once when a build script carried its own copy, and once
when the README said 0.6.0 over a source that said 1.5.0. The build hid compiler warnings behind a
green success line while the compiler was reporting an unused field and an unassigned one. The
install checked that the landed file was the right *size*. And the verifier's absent-control was a
thirty-character token no file could ever contain — a control that reported rigour and tested
nothing.

Each of those is now fixed in the tooling. This change is the part that was missing: saying what the
tooling is for, so the next person to touch it knows which of its oddities are deliberate.

## What changes

- A `release` capability, describing the build, the install and the verification as requirements
  rather than as script comments.
- No change to the mod. The scripts already behave this way.

## Non-goals

- No CI. There is no runner, and the check that matters most needs the game.
- No signing, no manifest, no package registry.
- Nothing about what the mod does; that belongs to `admin-panel`.

## What is not yet true

The verifier's marker list covers only features that shipped before 1.0: the panel, the three
original tabs, the appearance catalogue, the pet bonus wording and the named dwellers. Nothing in it
would notice if the overrides page, the preview camera or the storage guard failed to ship. The
requirement says markers cover the features that shipped; the list does not yet. That is left as a
task rather than quietly ticked.
