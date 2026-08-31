"""Draws a 256x256 terminal icon in the game's flat style, with no image library.

The game's HUD icons are flat: one bright fill, a darker outline, and cut-outs punched through to
the dark. Three colours and no gradients, which is why they stay legible when the HUD scales them
down to fifty pixels. This follows the same rule.
"""
import io
import os
import struct
import zlib

S = 256
CLEAR = (0, 0, 0, 0)
FILL = (255, 176, 0, 255)     # the bright body, as the camera icon uses green
EDGE = (140, 92, 0, 255)      # darker outline, the game's second tone
HOLE = (26, 22, 16, 255)      # punched through, the third

px = [[CLEAR] * S for _ in range(S)]


def rect(x0, y0, x1, y1, colour):
    for y in range(max(0, y0), min(S, y1)):
        for x in range(max(0, x0), min(S, x1)):
            px[y][x] = colour


def rounded_rect(x0, y0, x1, y1, r, colour):
    for y in range(max(0, y0), min(S, y1)):
        for x in range(max(0, x0), min(S, x1)):
            dx = 0
            dy = 0
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


# ---- monitor: outline, then body, then screen punched through ----
rounded_rect(20, 24, 236, 172, 18, EDGE)
rounded_rect(28, 32, 228, 164, 14, FILL)
rounded_rect(44, 48, 212, 140, 8, HOLE)

# ---- terminal text on the screen: solid bars, uneven like real output ----
for (top, left, width) in ((62, 60, 86), (82, 60, 124), (102, 60, 62)):
    rect(left, top, left + width, top + 10, FILL)

# a prompt caret on the last line
rect(132, 102, 168, 112, FILL)

# ---- stand and base ----
rect(112, 164, 144, 186, EDGE)
rect(118, 164, 138, 184, FILL)
rounded_rect(76, 186, 180, 202, 6, EDGE)
rounded_rect(80, 188, 176, 200, 5, FILL)


# ---- caption ----
# A five-by-seven block font, only the letters this word needs. Drawn rather than measured from a
# real font because there is no font to measure here, and block capitals survive being scaled down.
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
SCALE = 6
width = (5 + 1) * SCALE * len(WORD) - SCALE          # last letter has no trailing gap
text(WORD, (S - width) // 2 + 1, 213, SCALE, EDGE)   # shadow, one pixel down and right
text(WORD, (S - width) // 2, 212, SCALE, FILL)


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


write_png(r"D:\FalloutShelter-Mods\VaultAdmin\assets\button.png")
