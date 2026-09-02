# Tasks

## 1. The build

- [x] 1.1 The version is read from the source and nowhere else
- [x] 1.2 Compiler warnings are counted and printed before the success line
- [x] 1.3 A failed compile prints its whole output and stops
- [x] 1.4 Every reference is checked to exist before the compiler is called

## 2. The install

- [x] 2.1 Refuses to replace the plugin while the game is running
- [x] 2.2 Proves the copy by hash rather than by file size
- [x] 2.3 Stages a release archive named for the version in the source

## 3. The verifier

- [x] 3.1 Compares the installed plugin against the build by hash
- [x] 3.2 Searches UTF-16 at both alignments and latin-1 for each marker
- [x] 3.3 The absent-control is a marker that could plausibly be found
- [x] 3.4 A missing marker fails the check and names which
- [ ] 3.5 The marker list covers the features that shipped
      *(it stops at pre-1.0: nothing in it would notice if the overrides page, the preview camera or
      the storage guard failed to ship)*

## 4. Writing it down

- [x] 4.1 A `release` capability describing the build, the install and the verification
- [x] 4.2 The design notes: why the verifier is separate, why markers are weak, why the control has
      to be findable

## 5. Close out

- [ ] 5.1 After 3.5: archive
