"""Checks that what is installed is what was just built, and that it contains what it should.

Four rounds of this project were lost to a confident wrong answer from a string search: .NET
user strings are UTF-16 in a heap that may begin at an odd byte, so decoding from offset zero
garbles them. This decodes from both alignments and as bytes, and refuses to report anything at
all until a marker known to be present is found and one known to be absent is not.

    python tools/verify-install.py                 # markers for the current version
    python tools/verify-install.py Foo Bar         # plus markers of your own
"""

import hashlib
import io
import os
import re
import sys

BUILT = r"D:\FalloutShelter-Mods\VaultAdmin\build\VaultAdmin.dll"
INSTALLED = r"D:\SteamLibrary\steamapps\common\Fallout Shelter\BepInEx\plugins\VaultAdmin.dll"
SOURCE = r"D:\FalloutShelter-Mods\VaultAdmin\src\VaultAdminPlugin.cs"

# Markers that must be present, one per capability the panel claims to have. Each is a string
# only that feature puts in the assembly, so a missing one means a feature quietly did not ship.
REQUIRED = [
    ("VAULT ADMIN", "the window itself"),
    ("RESOURCES", "the resources tab"),
    ("ITEMS", "the items tab"),
    ("WORKSHOP", "the workshop tab"),
    ("OVERRIDES", "the overrides tab"),
    ("BEST DWELLER IN EVERY ROOM", "staffing the vault by ability"),
    ("TryAssignDweller", "the game's own way of assigning a dweller"),
    ("GetRoomInfoForType", "reading which stat a room runs on"),
    ("VaultAdmin_Preview", "the camera that films the figure on the bench"),
    ("the dressing table left in storage", "the guard that stops the bench minting items"),
    ("Something else reached storage", "the refusal that protects the player's own items"),
    ("Storage held ", "the measurement of where a returned item actually lands"),
    ("THIS CANNOT BE UNDONE", "the confirmation on the powers that cannot be taken back"),
    ("ApplyCustomization", "the dweller's looks"),
    ("m_dwellerCustomizationData", "the appearance catalogue"),
    ("LocalizedBonusWithValue", "the game's wording for pet bonuses"),
    ("DwellerFullName", "the named dwellers"),
    ("Window drawing check: ", "the check that the window reached the screen"),
]

# A control. If this is ever found, the search is matching noise and every other answer here is
# worthless.
# A control has to be capable of being found. A thirty-character unique token never appears by
# chance in any file, so testing for it proved nothing at all — it reported rigour and delivered
# none. This is a marker for a feature that was removed, in the same character class and the same
# encodings as the real ones: if the search is matching noise, this is what it will match.
ABSENT = "PreviewCopyGame"


def views(raw):
    return [
        raw.decode("utf-16-le", "ignore"),
        raw[1:].decode("utf-16-le", "ignore"),
        raw.decode("latin-1", "ignore"),
    ]


def main():
    if not os.path.exists(BUILT):
        print("no build to check: " + BUILT)
        return 1

    if not os.path.exists(INSTALLED):
        print("nothing installed at: " + INSTALLED)
        return 1

    built = open(BUILT, "rb").read()
    installed = open(INSTALLED, "rb").read()

    same = hashlib.sha256(built).hexdigest() == hashlib.sha256(installed).hexdigest()
    print("installed matches the build : " + ("yes" if same else "NO"))
    print("size                        : " + str(len(installed)) + " bytes")

    source = io.open(SOURCE, encoding="utf-8").read()
    found = re.search(r'PluginVersion\s*=\s*"([^"]+)"', source)
    version = found.group(1) if found else "?"
    print("version in the source        : " + version)

    text = views(installed)

    def holds(needle):
        return any(needle in v for v in text)

    if holds(ABSENT):
        print("\nthe control string was found; this search is matching noise. Nothing below counts.")
        return 1

    if not holds("VAULT ADMIN"):
        print("\nthe control string that must be present was not found; the search is not working.")
        return 1

    print("version in the assembly      : " + ("yes" if holds(version) else "NO"))

    print("")
    bad = 0
    for needle, why in REQUIRED + [(m, "asked for on the command line") for m in sys.argv[1:]]:
        ok = holds(needle)
        if not ok:
            bad += 1
        print(("  ok   " if ok else "  MISS ") + why + "  (" + needle + ")")

    print("")
    if same and bad == 0:
        print("the installed plugin is this build, and every marker is in it.")
        return 0

    print("something is wrong: " + ("the file differs. " if not same else "") +
          (str(bad) + " marker(s) missing." if bad else ""))
    return 1


if __name__ == "__main__":
    sys.exit(main())
