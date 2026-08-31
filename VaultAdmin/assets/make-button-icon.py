"""Draws the 256x256 button icon, with no image library.

Two things this gets right that earlier attempts did not.

The game's HUD icons are a solid body with the detail punched through it in dark, and an outline a
shade darker than the fill around the whole shape — the camera is a filled green body with a dark
lens and a dark-green rim, not a dark body with green lines. Getting that backwards is why earlier
versions read as a different set of icons however closely the outline matched.

The three colours are the game's own, given
directly. Matching them by eye failed three times, and reading them out of the atlas at runtime
failed as well: that atlas exposes no texture to read. So they are written down here, and the tint
is left off so nothing multiplies over them.
"""
import io
import os
import struct
import zlib

S = 256
# The game's own values, given rather than guessed at. Three attempts to match this by eye all
# missed, and sampling it out of the atlas at runtime failed too — the atlas exposes no texture.
CLEAR = (0, 0, 0, 0)
FILL = (0x14, 0xFF, 0x17, 255)   # 14FF17, the bright body
CUT = (0x08, 0x51, 0x08, 255)    # 085108, punched through, as the camera's lens is
RIM = (0x08, 0x60, 0x0A, 255)    # 08600A, the outline

px = [[CLEAR] * S for _ in range(S)]


def rect(x0, y0, x1, y1, colour):
    for y in range(max(0, y0), min(S, y1)):
        for x in range(max(0, x0), min(S, x1)):
            px[y][x] = colour


def rounded(x0, y0, x1, y1, r, colour):
    for y in range(max(0, y0), min(S, y1)):
        for x in range(max(0, x0), min(S, x1)):
            dx = dy = 0
            if x < x0 + r:
                dx = x0 + r - x
            elif x >= x1 - r:
                dx = x - (x1 - r - 1)
            if y < y0 + r:
                dy = y0 + r - y
            elif y >= y1 - r:
                dy = y - (y1 - r - 1)
            if dx * dx + dy * dy <= r * r:
                px[y][x] = colour


# ---- the slab: an outline, then the fill inset into it ----
BODY_TOP = 10
BODY_BOTTOM = 246
OUTLINE = 10

rounded(2, BODY_TOP, 254, BODY_BOTTOM, 22, RIM)
rounded(2 + OUTLINE, BODY_TOP + OUTLINE, 254 - OUTLINE, BODY_BOTTOM - OUTLINE, 16, FILL)

# ---- output lines, cut out ----
BAR_LEFT = 38
BAR_HEIGHT = 20
for (top, width) in ((44, 142), (80, 180), (116, 88)):
    rect(BAR_LEFT, top, BAR_LEFT + width, top + BAR_HEIGHT, CUT)

rect(BAR_LEFT + 104, 116, BAR_LEFT + 140, 116 + BAR_HEIGHT, CUT)   # caret after the short line


# ---- caption, cut out of the slab like everything else ----
# A five-by-seven block font, only the letters this word needs. Drawn four times at one-unit
# offsets to thicken it: at fifty pixels a single-unit stroke all but vanishes, which is what
# "too thin" meant.
GLYPHS = {
    "A": ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    "D": ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
    "M": ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
    "I": ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
    "N": ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
}

BOLD = 3   # how many units the stroke is thickened by


def text(word, left, top, scale, colour):
    x = left
    for ch in word:
        rows = GLYPHS[ch]
        for r, row in enumerate(rows):
            for c, bit in enumerate(row):
                if bit != "1":
                    continue
                rect(x + c * scale, top + r * scale,
                     x + (c + 1) * scale + BOLD, top + (r + 1) * scale + BOLD, colour)
        x += (len(rows[0]) + 1) * scale


WORD = "ADMIN"
SCALE = 7
width = (5 + 1) * SCALE * len(WORD) - SCALE + BOLD
CAPTION_TOP = 166

assert width <= S - 40, "the caption would touch the outline"
assert CAPTION_TOP + 7 * SCALE + BOLD <= BODY_BOTTOM - OUTLINE - 8, "the caption would reach the rim"

text(WORD, (S - width) // 2, CAPTION_TOP, SCALE, CUT)


def write_png(path):
    raw = bytearray()
    for row in px:
        raw.append(0)
        for (r, g, b, a) in row:
            raw += bytes((r, g, b, a))

    def chunk(tag, data):
        out = struct.pack(">I", len(data)) + tag + data
        return out + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", S, S, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")

    os.makedirs(os.path.dirname(path), exist_ok=True)
    io.open(path, "wb").write(png)
    print("wrote %s (%d bytes, %dx%d)" % (path, len(png), S, S))


write_png(os.path.join(os.path.dirname(os.path.abspath(__file__)), "button.png"))
