"""Draws the 256x256 button icon, with no image library.

Deliberately blunt. The HUD scales this to about fifty pixels, so anything smaller than a few
pixels at full size disappears entirely: a stand, a bezel, thin outlines. What survives is a big
rectangle, a handful of thick bars, and block capitals.

Two greens and a dark, matching the game's own HUD icons.
"""
import io
import os
import struct
import zlib

S = 256
CLEAR = (0, 0, 0, 0)
GREEN = (62, 255, 62, 255)    # the HUD green, brighter than it first looked against the rock
DEEP = (26, 150, 26, 255)     # shadow, only used under the caption
DARK = (10, 26, 10, 255)      # the screen behind the lines

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


# ---- the screen: one rectangle, a thick frame, nothing else ----
SCREEN_TOP = 6
SCREEN_BOTTOM = 172
FRAME = 16

rounded(4, SCREEN_TOP, 252, SCREEN_BOTTOM, 16, GREEN)
rounded(4 + FRAME, SCREEN_TOP + FRAME, 252 - FRAME, SCREEN_BOTTOM - FRAME, 8, DARK)

# ---- lines of "output": thick enough to still be there at fifty pixels ----
BAR_LEFT = 40
BAR_HEIGHT = 18
for (top, width) in ((46, 150), (82, 176), (118, 96)):
    rect(BAR_LEFT, top, BAR_LEFT + width, top + BAR_HEIGHT, GREEN)

# a caret after the short last line, so it reads as a terminal rather than a paragraph
rect(BAR_LEFT + 112, 118, BAR_LEFT + 148, 118 + BAR_HEIGHT, GREEN)


# ---- caption ----
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
SCALE = 8            # 9 overflowed the canvas; the assertion below is what caught it
width = (5 + 1) * SCALE * len(WORD) - SCALE
CAPTION_TOP = 186

assert width <= S, "the caption is wider than the canvas"
assert CAPTION_TOP + 7 * SCALE <= S, "the caption would run off the bottom"

text(WORD, (S - width) // 2 + 3, CAPTION_TOP + 3, SCALE, DEEP)
text(WORD, (S - width) // 2, CAPTION_TOP, SCALE, GREEN)


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
