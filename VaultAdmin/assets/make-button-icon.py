"""Draws the 256x256 button icon, with no image library.

The game's HUD icons are a solid green shape with the detail punched through it in dark — the
camera is a filled green body with a dark lens, not a dark body with green lines. The first few
attempts here had that backwards, which is why they read as a different set of icons however
closely the shape matched.

So: one solid green slab, everything else cut out of it in dark, and nothing thinner than about a
tenth of the width. The HUD shows this at roughly fifty pixels, where anything finer is gone.
"""
import io
import os
import struct
import zlib

S = 256
CLEAR = (0, 0, 0, 0)
GREEN = (62, 255, 62, 255)    # the HUD green
DARK = (12, 40, 12, 255)      # punched through the green, as the camera's lens is

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


# ---- the slab: solid green, reaching well down the canvas so the caption fits inside it ----
BODY_TOP = 14
BODY_BOTTOM = 242
rounded(6, BODY_TOP, 250, BODY_BOTTOM, 20, GREEN)

# ---- output lines, cut out in dark ----
BAR_LEFT = 34
BAR_HEIGHT = 20
for (top, width) in ((44, 150), (80, 188), (116, 92)):
    rect(BAR_LEFT, top, BAR_LEFT + width, top + BAR_HEIGHT, DARK)

# a caret after the short line, so it reads as a terminal rather than a paragraph
rect(BAR_LEFT + 108, 116, BAR_LEFT + 148, 116 + BAR_HEIGHT, DARK)


# ---- caption, inside the slab and cut out of it like everything else ----
# A five-by-seven block font, only the letters this word needs: there is no font to measure here,
# and block capitals are the only kind that survive being scaled down this far.
GLYPHS = {
    "A": ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    "D": ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
    "M": ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
    "I": ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
    "N": ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
}


def text(word, left, top, scale, colour):
    x = left
    for ch in word:
        rows = GLYPHS[ch]
        for r, row in enumerate(rows):
            for c, bit in enumerate(row):
                if bit == "1":
                    rect(x + c * scale, top + r * scale,
                         x + (c + 1) * scale, top + (r + 1) * scale, colour)
        x += (len(rows[0]) + 1) * scale


WORD = "ADMIN"
SCALE = 7
width = (5 + 1) * SCALE * len(WORD) - SCALE
CAPTION_TOP = 168

assert width <= S - 24, "the caption would touch the edge of the slab"
assert CAPTION_TOP + 7 * SCALE <= BODY_BOTTOM - 12, "the caption would run out of the slab"

text(WORD, (S - width) // 2, CAPTION_TOP, SCALE, DARK)


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
