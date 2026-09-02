# Design

## Why there is a verifier at all, separate from the build

A build reports what the compiler did. It cannot report what the game will load, and those are
different questions whenever a copy is skipped, locked, or lands somewhere else. The house rule this
project keeps returning to is that a success line is not evidence — so the check that answers "is
the plugin the game will load the one I just built" is deliberately not part of the thing that would
be reporting its own success.

It answers two questions, and only the first is certain:

- **Identity**, by hash. This is sound and it is the reason the tool exists.
- **Features**, by markers. This is weak by construction: a marker proves a string literal survived
  into the assembly, not that any code around it works. `"WORKSHOP"` would still be found if the
  whole page failed to build, because the tab strip supplies the word. It is a smoke test for "did
  this feature ship at all", and it should not be read as more than that.

## The control, and why the obvious one is useless

A marker check that only looks for things it expects to find cannot tell a working search from one
that matches everything. So it also looks for a marker it expects to be absent.

The absent marker has to be capable of being found. The first one was
`ZZ_THIS_STRING_IS_NOT_IN_THE_BUILD_ZZ` — thirty-eight characters that no file could contain by
chance, which means its absence proved nothing at all. The control is now a marker for a feature
that was genuinely removed, in the same character class and the same encodings as the real markers.
If the search is matching noise, that is what it will match.

## Decoding

A .NET assembly stores string literals as UTF-16, and the byte alignment of a given literal is not
knowable from outside. The verifier searches three views of the file: UTF-16 at both alignments, and
a latin-1 view for anything stored as plain bytes. The latin-1 view does not subsume the UTF-16 ones
even for ASCII needles, which is why all three are there.

## The version

One declaration in the source, read by everything else. `build.ps1` parses it out rather than
carrying a copy, because it once carried a copy and a release archive was named for a version the
assembly did not have.

The rule extends past the tooling: the README and the changelog are two more places a version can be
written, and both have disagreed with the source at least once. There is no mechanism enforcing
that, only the requirement — which is at least something to point at when they drift again.

## Refusing a running game

The install checks for the process before copying. It is a check-then-act with a window between,
so a game started in that window still produces a locked file — but the failure is then a thrown
error rather than a silent skip, which is the outcome that matters. Closing the window entirely
would mean holding a lock this script has no business holding.
