"""Draws the 256x256 button icon, with no image library.

The style is taken from the game's own HUD icons rather than invented: one bright green, a thick
outline, and solid fills instead of outlines-with-detail. Their terminal icon fills its square
almost edge to edge and its screen is a solid block, which is why it still reads when the HUD
scales it down to fifty pixels. Thin lines and empty space do not survive that.

Three colours, as the game uses: bright green, a darker green for depth, and the dark punched
through where a shape needs separating from another.
"""
import io
import os
import struct
import zlib

S = 256
CLEAR = (0, 0, 0, 0)
GREEN = (61, 240, 61, 255)    # the HUD green
DEEP = (22, 120, 22, 255)     # shadow side, as the game shades its icons
DARK = (12, 30, 12, 255)      # punched through, separating shapes

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


# ---- the monitor, filling nearly the whole square ----
rounded(6, 2, 250, 166, 22, DEEP)       # outer body, shadow tone
rounded(10, 2, 246, 160, 20, GREEN)     # lit face, offset up-left as the game shades

rounded(28, 20, 228, 138, 12, DARK)     # bezel cut through
rounded(36, 28, 220, 130, 8, GREEN)     # SCREEN: solid, not lines

# a single dark prompt bar keeps it reading as a terminal without going thin
rect(52, 104, 116, 118, DARK)
rect(126, 104, 152, 118, DARK)

# ---- stand and base, chunky enough to survive shrinking ----
rect(104, 166, 152, 184, DEEP)
rect(110, 166, 146, 180, GREEN)
rounded(60, 182, 196, 202, 8, DEEP)
rounded(64, 182, 192, 198, 7, GREEN)


# ---- caption ----
# A five-by-seven block font, only the letters this word needs: there is no font to measure here,
# and block capitals are the only kind that survive being scaled to a fifty-pixel button.
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
SCALE = 5
width = (5 + 1) * SCALE * len(WORD) - SCALE
# 7 rows at this scale is 35 pixels, so the top must sit at 256 - 35 - a margin.
CAPTION_TOP = 212
text(WORD, (S - width) // 2 + 2, CAPTION_TOP + 2, SCALE, DEEP)
text(WORD, (S - width) // 2, CAPTION_TOP, SCALE, GREEN)
assert CAPTION_TOP + 7 * SCALE + 2 <= S, "the caption would run off the bottom of the canvas"


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
