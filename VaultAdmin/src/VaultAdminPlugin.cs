using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Reflection;

namespace VaultAdmin
{
    /// <summary>
    /// Draws the panel's shapes at runtime, at the exact size each one needs.
    ///
    /// The alternative was nine-slicing a sprite out of the game's atlas, which this build makes
    /// impossible in two ways at once: the atlas lists no sprites and exposes no texture. Drawing
    /// instead means no atlas dependency at all, and a rectangle drawn at its final size has no
    /// stretched corners to get wrong.
    ///
    /// Every result is cached: the panel asks for the same handful of sizes every frame it is open,
    /// and a texture per frame would be a leak with extra steps.
    /// </summary>
    internal static class Skin
    {
        // Measured from the game, not matched by eye.
        // The interface has three greens and no others. A fourth invented here read as a
        // different program's idea of the same style.
        public static readonly Color Bright = new Color32(0x14, 0xFF, 0x17, 0xFF);   // 14FF17
        public static readonly Color Ink = new Color32(0x08, 0x51, 0x08, 0xFF);      // 085108
        public static readonly Color Rim = new Color32(0x08, 0x60, 0x0A, 0xFF);      // 08600A

        // The same three greens at different weights, and nothing else. The game's own windows
        // are a dark green wash over the vault with a bright edge; a card inside one is the same
        // wash again, lighter, so the vault still shows through it; a recess is the dark green
        // solid, which is what makes it read as a hole rather than a panel.
        // Measured off the game's own windows rather than chosen. Its build screen shows the
        // vault clearly through the panel, and every card on that panel is edged in the bright
        // green — not in the dark one, which is what made this panel look muddy beside it.
        public static readonly Color Plate = new Color32(0x08, 0x51, 0x08, 0xB4);   // the window
        public static readonly Color Card = new Color32(0x08, 0x51, 0x08, 0x5A);    // a row on it
        public static readonly Color Hole = new Color32(0x08, 0x51, 0x08, 0xE6);    // a recess

        // Border weights, so every edge in the panel is one of three thicknesses rather than
        // whatever each piece of code felt like.
        public const int EdgeWindow = 4;
        public const int EdgeButton = 3;
        public const int EdgeCard = 2;
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Screen pixels per interface unit.
        ///
        /// The interface is laid out at a fixed height and stretched to the screen — 720 units on a
        /// 1080-pixel display. A texture drawn one pixel per unit is then blown up by half again,
        /// which is exactly what made the first panel look coarse beside the game's own art. Drawing
        /// at the screen's own resolution costs a little memory and nothing else.
        /// </summary>
        public static float Scale = 1f;

        public static Texture2D Frame(int width, int height, int radius, int thickness,
                                      Color edge, Color inside)
        {
            return Frame(width, height, radius, thickness, edge, inside, Clear, 0);
        }

        /// <summary>
        /// The same, with a second edge outside the first.
        ///
        /// The game's windows are outlined twice — a bright line with a darker one around it — and
        /// a single line beside them reads as a rectangle drawn on top of the game rather than a
        /// panel belonging to it.
        /// </summary>
        public static Texture2D Frame(int width, int height, int radius, int thickness,
                                      Color edge, Color inside, Color outer, int outerThickness)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            int w = Mathf.Max(1, Mathf.RoundToInt(width * Scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(height * Scale));
            float r = Mathf.Clamp(radius * Scale, 0f, Mathf.Min(w, h) / 2f);
            float t = Mathf.Max(1f, thickness * Scale);

            float ot = Mathf.Max(0f, outerThickness * Scale);

            string key = w + "x" + h + "r" + radius + "t" + thickness + "o" + outerThickness +
                         "e" + ColorUtility.ToHtmlStringRGBA(edge) +
                         "i" + ColorUtility.ToHtmlStringRGBA(inside) +
                         "u" + ColorUtility.ToHtmlStringRGBA(outer);

            Texture2D cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Distance past the rounded core, measured from the corner's centre.
                    float dx = 0f;
                    float dy = 0f;
                    if (x < r) dx = r - x;
                    else if (x >= w - r) dx = x - (w - r - 1f);
                    if (y < r) dy = r - y;
                    else if (y >= h - r) dy = y - (h - r - 1f);

                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // Feathered by one pixel rather than cut at one: a hard threshold is what makes
                    // a drawn corner look like a staircase.
                    float coverage = Mathf.Clamp01(r - d + 0.5f);
                    if (r <= 0f) coverage = 1f;

                    float near = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));

                    Color colour;
                    if (ot > 0f && (near < ot || d > r - ot)) colour = outer;
                    else if (near < ot + t || d > r - ot - t) colour = edge;
                    else colour = inside;

                    colour.a *= coverage;

                    pixels[y * w + x] = colour;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            _cache[key] = texture;
            return texture;
        }

        /// <summary>The window plate: bright outline, dimmed interior.</summary>
        public static Texture2D Window(int width, int height)
        {
            return Frame(width, height, 18, EdgeWindow, Bright, Plate, Ink, EdgeCard);
        }

        /// <summary>An ordinary button: outlined, nothing behind it. Frequent, reversible actions.</summary>
        public static Texture2D Button(int width, int height)
        {
            return Frame(width, height, 8, EdgeButton, Bright, Card);
        }

        /// <summary>An emphasis button: solid, with dark text on it. Close, save, confirm.</summary>
        public static Texture2D SolidButton(int width, int height)
        {
            return Frame(width, height, 8, EdgeButton, Bright, Bright);
        }

        /// <summary>
        /// Filled, with a single dark edge — the colour of the line that runs outside the window's
        /// own. That is the point of it: the button reads as belonging to the frame it sits on.
        /// </summary>
        public static Texture2D SolidOutlined(int width, int height)
        {
            return Frame(width, height, 10, EdgeButton, Ink, Bright);
        }

        /// <summary>A flat square, meant to be tinted by the widget that draws it.</summary>
        public static Texture2D Solid()
        {
            return Frame(8, 8, 0, 0, Color.white, Color.white);
        }

        /// <summary>
        /// The recess an icon sits in.
        ///
        /// A picture laid straight onto a row runs into the words beside it. A dark square behind it
        /// says where the picture ends and the row begins.
        /// </summary>
        public static Texture2D Well(int size)
        {
            return Well(size, size);
        }

        /// <summary>
        /// A recess that is not obliged to be square.
        ///
        /// There was only the square one, and it was being handed to plates that are not square --
        /// so a texture drawn for one shape was stretched to another, and its rounded corners were
        /// stretched with it. That is where the pulled, lopsided corners come from: not from the
        /// drawing, which is even, but from drawing it at the wrong size and letting the widget
        /// squash it.
        /// </summary>
        public static Texture2D Well(int width, int height)
        {
            return Frame(width, height, 6, EdgeCard, Bright, Hole);
        }

        /// <summary>A place to type: outlined bright, sunk dark, so it reads as a field.</summary>
        public static Texture2D Field(int width, int height)
        {
            return Frame(width, height, 6, EdgeCard, Bright, Hole);
        }

        /// <summary>How far a point lies from a line segment. The workhorse of every drawn shape here.</summary>
        private static float ToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.sqrMagnitude;

            float t = len <= 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Whether a point is inside a convex quad given in order around it.</summary>
        private static bool InQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            // Either way round. The test used to demand one winding, and the cube's faces are
            // given in the other -- so not one pixel of any face passed it and all that survived
            // was the outline. A wireframe die, drawn entirely by accident.
            float ab = Side(p, a, b), bc = Side(p, b, c);
            float cd = Side(p, c, d), da = Side(p, d, a);

            return (ab >= 0f && bc >= 0f && cd >= 0f && da >= 0f) ||
                   (ab <= 0f && bc <= 0f && cd <= 0f && da <= 0f);
        }

        private static float Side(Vector2 p, Vector2 a, Vector2 b)
        {
            return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        }

        /// <summary>
        /// A die, drawn as a cube seen from a corner.
        ///
        /// Flat on, it was a square with spots and read as a button rather than as a die. Seen from
        /// above and to one side, three faces show at once, and the three greens of this panel are
        /// exactly what a lit face, a shaded face and a shadowed one want to be -- so the cube is
        /// solid without a fourth colour entering the interface to make it so.
        ///
        /// The game has no dice in any of its atlases. Six orientations, cached like every other
        /// texture here, so turning through them costs nothing after the first turn.
        /// </summary>
        public static Texture2D Die(int size, int face)
        {
            face = Mathf.Clamp(face, 1, 6);

            string key = "cube" + size + "f" + face + "s" + Scale.ToString("0.00");

            Texture2D cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            int w = Mathf.Max(12, Mathf.RoundToInt(size * Scale));
            Color[] px = new Color[w * w];

            float cx = w * 0.5f;
            float cy = w * 0.5f;

            // A cube alone on a dark recess is a shape; a cube on a ground of its own is a button.
            // The disc is what tells the eye there is something here to press.
            float ground = w * 0.48f;
            float rim = Mathf.Max(1.2f, 1.6f * Scale);

            float half = w * 0.31f;    // half the width of the top rhombus
            float rise = w * 0.33f;    // the length of a vertical edge

            // The six corners of the silhouette, and the one in the middle where all three faces
            // meet -- the near vertical edge, pointing at the viewer.
            Vector2 back  = new Vector2(cx, cy + half * 0.5f + rise * 0.5f);
            Vector2 right = new Vector2(cx + half, cy + rise * 0.5f);
            Vector2 left  = new Vector2(cx - half, cy + rise * 0.5f);
            Vector2 near  = new Vector2(cx, cy - half * 0.5f + rise * 0.5f);

            Vector2 rightLow = new Vector2(cx + half, cy - rise * 0.5f);
            Vector2 leftLow  = new Vector2(cx - half, cy - rise * 0.5f);
            Vector2 nearLow  = new Vector2(cx, cy - half * 0.5f - rise * 0.5f);

            // Three colours and no shading. Lighting the faces differently was a small painting
            // where an icon was wanted: at this size the tones read as smudges rather than as
            // form, and the shape is already told by its edges. Bright for the solid, dark for the
            // numbers, darker still for the edges between them.
            Color solid = Bright;
            Color spots = Ink;
            Color edge = Color.Lerp(Ink, Color.black, 0.45f);

            // Finer. At the old weight the edges were as loud as the numbers, and on a shape
            // this small that is a drawing of a cage rather than of a cube.
            float stroke = Mathf.Max(1f, 0.9f * Scale);

            // Every edge that shows: the six around the outside, and the three that radiate from
            // the near corner and tell the three faces apart.
            Vector2[][] edges =
            {
                new[] { back, right }, new[] { right, rightLow }, new[] { rightLow, nearLow },
                new[] { nearLow, leftLow }, new[] { leftLow, left }, new[] { left, back },
                new[] { near, right }, new[] { near, left }, new[] { near, nearLow }
            };

            int[] pips = CubeFaces(face);

            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    Color paint;

                    // The ground first, so everything else is drawn on top of it.
                    float outward = Vector2.Distance(p, new Vector2(cx, cy));

                    paint = Clear;

                    if (outward <= ground + 0.5f)
                    {
                        float onRim = Mathf.Clamp01(outward - (ground - rim) + 0.5f);
                        float inside = Mathf.Clamp01(ground - outward + 0.5f);

                        paint = Color.Lerp(Hole, Bright, onRim);
                        paint.a *= inside;
                    }

                    int which = 0;

                    if (InQuad(p, back, right, near, left)) which = 1;
                    else if (InQuad(p, right, rightLow, nearLow, near)) which = 2;
                    else if (InQuad(p, near, nearLow, leftLow, left)) which = 3;

                    if (which != 0) paint = solid;

                    if (which != 0)
                    {
                        // The numbers, sunk into whichever face the pixel belongs to. One colour
                        // for all three faces, because all three faces are one colour.
                        float ink;

                        if (which == 1) ink = PipCover(p, back, right, near, left, pips[0], w);
                        else if (which == 2) ink = PipCover(p, right, rightLow, nearLow, near, pips[1], w);
                        else ink = PipCover(p, near, nearLow, leftLow, left, pips[2], w);

                        if (ink > 0f) paint = Color.Lerp(paint, spots, ink);
                    }

                    // Drawn last and over everything, so the cube keeps its shape against whatever
                    // it is standing on.
                    float nearest = float.MaxValue;
                    for (int e = 0; e < edges.Length; e++)
                    {
                        float d = ToSegment(p, edges[e][0], edges[e][1]);
                        if (d < nearest) nearest = d;
                    }

                    float onEdge = Mathf.Clamp01(stroke - nearest + 0.5f);
                    if (onEdge > 0f) paint = Color.Lerp(paint, edge, onEdge);

                    px[y * w + x] = paint;
                }
            }

            Texture2D die = new Texture2D(w, w, TextureFormat.RGBA32, false);
            die.filterMode = FilterMode.Bilinear;
            die.wrapMode = TextureWrapMode.Clamp;
            die.SetPixels(px);
            die.Apply();

            _cache[key] = die;
            return die;
        }

        /// <summary>How much of a pip covers this pixel, in the face's own square.</summary>
        private static float PipCover(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
                                      int pips, int w)
        {
            // Where the pixel falls within the face, as a fraction along each of its two edges.
            Vector2 u = b - a;
            Vector2 v = d - a;

            float denom = u.x * v.y - u.y * v.x;
            if (Mathf.Abs(denom) < 0.0001f) return 0f;

            Vector2 q = p - a;
            float s = (q.x * v.y - q.y * v.x) / denom;
            float t = (u.x * q.y - u.y * q.x) / denom;

            int[] mask = PipMask(pips);
            float radius = 0.115f;
            float best = 0f;

            for (int cell = 0; cell < 9; cell++)
            {
                if (mask[cell] == 0) continue;

                float ds = s - (0.25f + 0.25f * (cell % 3));
                float dt = t - (0.25f + 0.25f * (cell / 3));

                // Softened in face space, then scaled back to pixels so the feather is even.
                float cover = Mathf.Clamp01((radius - Mathf.Sqrt(ds * ds + dt * dt)) * w * 0.55f);
                if (cover > best) best = cover;
            }

            return best;
        }

        /// <summary>Which numbers show on the top, right and left faces of each orientation.</summary>
        private static int[] CubeFaces(int face)
        {
            switch (face)
            {
                case 1:  return new[] { 1, 3, 2 };
                case 2:  return new[] { 2, 1, 3 };
                case 3:  return new[] { 3, 2, 1 };
                case 4:  return new[] { 4, 6, 5 };
                case 5:  return new[] { 5, 4, 6 };
                default: return new[] { 6, 5, 4 };
            }
        }

        /// <summary>Which of the nine places carry a pip, for each face of a die.</summary>
        private static int[] PipMask(int pips)
        {
            switch (pips)
            {
                case 1:  return new[] { 0, 0, 0,  0, 1, 0,  0, 0, 0 };
                case 2:  return new[] { 1, 0, 0,  0, 0, 0,  0, 0, 1 };
                case 3:  return new[] { 1, 0, 0,  0, 1, 0,  0, 0, 1 };
                case 4:  return new[] { 1, 0, 1,  0, 0, 0,  1, 0, 1 };
                case 5:  return new[] { 1, 0, 1,  0, 1, 0,  1, 0, 1 };
                default: return new[] { 1, 0, 1,  1, 0, 1,  1, 0, 1 };
            }
        }

        /// <summary>How far a point lies from an arc, or from its ends if it is past them.</summary>
        private static float ToArc(Vector2 p, Vector2 centre, float radius, float from, float to)
        {
            Vector2 away = p - centre;
            float angle = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            if (angle >= from && angle <= to) return Mathf.Abs(away.magnitude - radius);

            Vector2 a = centre + new Vector2(Mathf.Cos(from * Mathf.Deg2Rad),
                                             Mathf.Sin(from * Mathf.Deg2Rad)) * radius;
            Vector2 b = centre + new Vector2(Mathf.Cos(to * Mathf.Deg2Rad),
                                             Mathf.Sin(to * Mathf.Deg2Rad)) * radius;

            return Mathf.Min(Vector2.Distance(p, a), Vector2.Distance(p, b));
        }

        /// <summary>
        /// A padlock standing open, for the power that opens every recipe.
        ///
        /// The atlas offered a scroll and a lump of junk, neither of which is the idea. A lock with
        /// its shackle swung clear is the idea, and it is four shapes: a body, an arc, a keyhole
        /// and the hole's stem.
        /// </summary>
        public static Texture2D Padlock(int size)
        {
            string key = "padlock" + size + "s" + Scale.ToString("0.00");

            Texture2D cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            int w = Mathf.Max(12, Mathf.RoundToInt(size * Scale));
            Color[] px = new Color[w * w];

            // Lifted off the floor of its box. The whole lock had been sitting low enough to
            // look like it was resting on the frame rather than standing in it.
            float bodyLeft = w * 0.22f, bodyRight = w * 0.78f;
            float bodyLow = w * 0.15f, bodyHigh = w * 0.60f;
            float round = w * 0.09f;

            Vector2 shackleAt = new Vector2(w * 0.50f, w * 0.62f);
            float shackleR = w * 0.20f;
            float shackleT = w * 0.075f;

            Vector2 keyAt = new Vector2(w * 0.50f, w * 0.42f);

            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float ink = 0f;

                    // The body: a rectangle with its corners taken off.
                    float dx = Mathf.Max(0f, Mathf.Max(bodyLeft + round - p.x, p.x - (bodyRight - round)));
                    float dy = Mathf.Max(0f, Mathf.Max(bodyLow + round - p.y, p.y - (bodyHigh - round)));

                    if (p.x >= bodyLeft - 1f && p.x <= bodyRight + 1f &&
                        p.y >= bodyLow - 1f && p.y <= bodyHigh + 1f)
                        ink = Mathf.Max(ink, Mathf.Clamp01(round - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f));

                    // The shackle, swung open: the arc stops short of coming back down.
                    float arc = ToArc(p, shackleAt, shackleR, 25f, 180f);
                    ink = Mathf.Max(ink, Mathf.Clamp01(shackleT * 0.5f - arc + 0.5f));

                    Color paint = ink > 0f ? Color.Lerp(Clear, Bright, ink) : Clear;

                    // The keyhole, cut back out of the body.
                    float hole = Vector2.Distance(p, keyAt) - w * 0.075f;
                    float stem = ToSegment(p, keyAt, new Vector2(keyAt.x, w * 0.26f)) - w * 0.032f;
                    float cut = Mathf.Clamp01(-Mathf.Min(hole, stem) + 0.5f);

                    if (cut > 0f) paint = Color.Lerp(paint, Clear, cut);

                    px[y * w + x] = paint;
                }
            }

            return Keep(key, w, px);
        }

        /// <summary>
        /// Two chevrons, for the power that makes rushing safe.
        ///
        /// Rushing is the game's word for going faster, and going faster has looked like this on
        /// every machine anyone has ever pressed a button on. The atlas alternative was an alarm
        /// clock, which is very nearly the opposite idea.
        /// </summary>
        public static Texture2D Chevrons(int size)
        {
            string key = "chevrons" + size + "s" + Scale.ToString("0.00");

            Texture2D cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            int w = Mathf.Max(12, Mathf.RoundToInt(size * Scale));
            Color[] px = new Color[w * w];

            float thick = w * 0.09f;

            Vector2[] one =
            {
                new Vector2(w * 0.26f, w * 0.26f), new Vector2(w * 0.47f, w * 0.50f),
                new Vector2(w * 0.26f, w * 0.74f)
            };
            Vector2[] two =
            {
                new Vector2(w * 0.51f, w * 0.26f), new Vector2(w * 0.72f, w * 0.50f),
                new Vector2(w * 0.51f, w * 0.74f)
            };

            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                    float d = Mathf.Min(
                        Mathf.Min(ToSegment(p, one[0], one[1]), ToSegment(p, one[1], one[2])),
                        Mathf.Min(ToSegment(p, two[0], two[1]), ToSegment(p, two[1], two[2])));

                    float ink = Mathf.Clamp01(thick * 0.5f - d + 0.5f);
                    px[y * w + x] = ink > 0f ? Color.Lerp(Clear, Bright, ink) : Clear;
                }
            }

            return Keep(key, w, px);
        }

        private static Texture2D Keep(string key, int w, Color[] px)
        {
            Texture2D made = new Texture2D(w, w, TextureFormat.RGBA32, false);
            made.filterMode = FilterMode.Bilinear;
            made.wrapMode = TextureWrapMode.Clamp;
            made.SetPixels(px);
            made.Apply();

            _cache[key] = made;
            return made;
        }

        /// <summary>
        /// A plus or a minus, drawn rather than typed.
        ///
        /// A glyph sits where its font's baseline puts it, which is not the middle of a button --
        /// the plus and the minus in the same row of buttons were at visibly different heights,
        /// because in the typeface they are. Two bars centred on the texture are two bars centred
        /// on the button.
        /// </summary>
        public static Texture2D Sign(int size, bool plus)
        {
            string key = (plus ? "plus" : "minus") + size + "s" + Scale.ToString("0.00");

            Texture2D cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            int w = Mathf.Max(8, Mathf.RoundToInt(size * Scale));
            Color[] px = new Color[w * w];

            float thick = Mathf.Max(1.5f, w * 0.15f);
            float reach = w * 0.30f;
            float mid = w * 0.5f;

            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                    float d = ToSegment(p, new Vector2(mid - reach, mid), new Vector2(mid + reach, mid));

                    if (plus)
                        d = Mathf.Min(d, ToSegment(p, new Vector2(mid, mid - reach),
                                                      new Vector2(mid, mid + reach)));

                    float ink = Mathf.Clamp01(thick * 0.5f - d + 0.5f);
                    px[y * w + x] = ink > 0f ? Color.Lerp(Clear, Bright, ink) : Clear;
                }
            }

            return Keep(key, w, px);
        }

        /// <summary>
        /// Three bars, longest first: a ranking, for the power that puts the best dweller in each
        /// room. Nothing in the game's atlas says "sorted by ability" and this does.
        /// </summary>
        public static Texture2D Ranked(int size)
        {
            string key = "ranked" + size + "s" + Scale.ToString("0.00");

            Texture2D cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            int w = Mathf.Max(12, Mathf.RoundToInt(size * Scale));
            Color[] px = new Color[w * w];

            float thick = w * 0.13f;
            float left = w * 0.20f;
            float[] ends = { w * 0.82f, w * 0.64f, w * 0.46f };
            float[] rows = { w * 0.72f, w * 0.50f, w * 0.28f };

            for (int y = 0; y < w; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float best = float.MaxValue;

                    for (int i = 0; i < 3; i++)
                    {
                        float d = ToSegment(p, new Vector2(left, rows[i]), new Vector2(ends[i], rows[i]));
                        if (d < best) best = d;
                    }

                    float ink = Mathf.Clamp01(thick * 0.5f - best + 0.5f);
                    px[y * w + x] = ink > 0f ? Color.Lerp(Clear, Bright, ink) : Clear;
                }
            }

            return Keep(key, w, px);
        }

        /// <summary>A content row: a quieter outline, dimmed inside.</summary>
        public static Texture2D Row(int width, int height)
        {
            return Frame(width, height, 8, EdgeCard, Bright, Card);
        }

        /// <summary>A section header: solid, inverted against the rows beneath it.</summary>
        public static Texture2D Header(int width, int height)
        {
            return Frame(width, height, 6, EdgeCard, Bright, Bright);
        }
    }

    /// <summary>
    /// Vault Admin — a debug panel for Fallout Shelter.
    ///
    /// Reads live vault state; grants resources, boxes, weapons, outfits, junk and pets;
    /// builds dwellers and animals to order; and holds a set of vault-wide switches.
    ///
    /// Everything is written through the game's own methods rather than by assigning fields.
    /// Storage.AddResource clamps to the vault's cap and raises the callbacks the interface
    /// listens to; a field assignment would leave the number on screen stale and skip whatever
    /// else the game does when a resource changes.
    ///
    /// The panel is built from the game's own NGUI widgets, so it belongs to the interface
    /// rather than floating over it. An IMGUI scaffold remains as a fallback for when the window
    /// cannot be built at all; separating the two means a failure there is a UI failure and
    /// nothing else.
    ///
    /// Disabled by default. Installing this without deliberately switching it on changes nothing.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ovolo.falloutshelter.vaultadmin";
        public const string PluginName = "Vault Admin";
        public const string PluginVersion = "1.0.5";

        internal static ManualLogSource Log;

        private static ConfigEntry<bool> Enabled;
        private static ConfigEntry<string> ToggleKey;
        private static ConfigEntry<bool> WriteIconReport;
        private static ConfigEntry<bool> PreviewWholeSheet;

        private static ConfigEntry<bool> IncidentsOff;
        private static ConfigEntry<bool> BottleAndCappyOff;
        private static ConfigEntry<bool> RushAlwaysWorks;
        private static ConfigEntry<int> MaxDwellersWanted;
        private static ConfigEntry<bool> ShowHudButton;
        private static ConfigEntry<float> HudButtonOffsetX;
        private static ConfigEntry<float> HudButtonOffsetY;
        private static ConfigEntry<string> HudButtonSprite;
        private static ConfigEntry<string> HudButtonTint;
        private static ConfigEntry<string> HudButtonImage;
        private static ConfigEntry<float> HudButtonIconScale;

        private Key _toggleKey = Key.F8;
        private bool _panelOpen;

        // Failures are logged once each. A panel that writes sixty lines a second destroys the very
        // evidence needed to work out what it is failing at.
        private static readonly HashSet<string> _reported = new HashSet<string>();

        private Rect _window = new Rect(40f, 40f, 470f, 700f);
        private Vector2 _scroll;

        // EResource carries Lunchbox, MrHandy and PetCarrier, and granting them looks like the
        // obvious move. It does nothing: the real store is a list of LunchBox objects on the vault,
        // reached through Vault.AddLunchBox. Leaving them in the resource rows would give the panel
        // two routes to the same thing, one of which silently fails, so they are excluded here and
        // offered in the box section instead.
        // The enum counts several things that are not resources a vault holds: the box types are
        // counted separately below, and the crafted-item tallies, the poker chips and the dummy are
        // bookkeeping the player never sees. Listing them was noise in the one place that has least
        // room for it.
        private static readonly EResource[] NotRealResources =
        {
            EResource.Lunchbox, EResource.MrHandy, EResource.PetCarrier,
            EResource.CraftedOutfit, EResource.CraftedWeapon, EResource.CraftedTheme,
            EResource.DummyUltracite, EResource.PokerChip
        };

        private static readonly float[] GrantAmounts = { 100f, 1000f, 10000f };

        // A vault holds caps and water by the thousand and stimpaks by the handful. One set of
        // amounts for both makes the buttons useless for half of them.
        private static readonly float[] SmallAmounts = { 1f, 5f, 25f };
        private static readonly float[] MediumAmounts = { 10f, 100f, 1000f };

        private static float[] AmountsFor(EResource resource)
        {
            switch (resource)
            {
                case EResource.StimPack:
                case EResource.RadAway:
                    return SmallAmounts;
                case EResource.NukaColaQuantum:
                    return MediumAmounts;
                default:
                    return GrantAmounts;
            }
        }
        private static readonly int[] BoxAmounts = { 1, 5, 25 };

        // The item catalogue, read from the game once and kept. The tables do not change during a
        // session, and rebuilding them per frame would allocate for nothing.
        private sealed class CatalogueEntry
        {
            public EItemType Type;
            public string Id;        // what the game looks the item up by — NOT its display name
            public string Name;      // for the human only
            public EItemRarity Rarity;
            public string Sprite;    // name of this item's sprite inside its family's atlas
            public string Stats;     // rarity and what it does, for a list row
            public string Effect;    // only what it does, for a card that names the rarity itself
            public int Power;        // the same in one number, for ordering
            public int[] Stats7;     // an outfit's seven bonuses, kept so one can be sorted on
        }

        // One atlas per family, resolved once. An atlas is a texture plus a table of rectangles.
        private readonly Dictionary<EItemType, UIAtlas> _atlases = new Dictionary<EItemType, UIAtlas>();

        // Pets are the one item family that carries data of its own per copy, so they are the one
        // the panel can genuinely customise. Weapons and outfits hold nothing per copy at all.
        private sealed class PetEntry
        {
            public object Template;      // DwellerPetItem, held loosely: the catalogue is read by reflection
            public string PetId;
            public string Name;
            public string Detail;
            public object PetType;   // EPetType — which atlas this pet's art lives in
            public EItemRarity Rarity;
            public int Power;        // the best its bonus can do, for ordering
        }

        // Every record in the catalogue, one per animal per grade.
        private List<PetEntry> _pets;

        /// <summary>
        /// One animal, with every version of it the game keeps.
        ///
        /// The catalogue holds a hundred and thirty records but nothing like that many animals: the
        /// same creature is filed once per rarity, with the same name, the same breed and the same
        /// picture, differing only in how strong its bonus is. Listing all of them was a list that
        /// lied about how much was in it.
        /// </summary>
        private sealed class PetGroup
        {
            public string Name;
            public object Breed;
            public readonly List<PetEntry> Variants = new List<PetEntry>();

            public PetEntry Best
            {
                get
                {
                    PetEntry best = null;
                    for (int i = 0; i < Variants.Count; i++)
                        if (best == null || Variants[i].Power > best.Power) best = Variants[i];
                    return best;
                }
            }
        }

        private List<PetGroup> _petGroups;

        /// <summary>Collects the catalogue into one entry per animal.</summary>
        private void GroupPets()
        {
            _petGroups = new List<PetGroup>();
            if (_pets == null) return;

            Dictionary<string, PetGroup> byKind = new Dictionary<string, PetGroup>();

            for (int i = 0; i < _pets.Count; i++)
            {
                PetEntry pet = _pets[i];

                // Name and breed together: the same breed carries several different animals, and
                // the same animal is filed once per rarity.
                string key = pet.Name + "|" + (pet.Detail == null ? "" : pet.Detail);

                PetGroup group;
                if (!byKind.TryGetValue(key, out group))
                {
                    group = new PetGroup();
                    group.Name = pet.Name;
                    group.Breed = pet.PetType;
                    byKind[key] = group;
                    _petGroups.Add(group);
                }

                group.Variants.Add(pet);
            }

            Log.LogInfo("The pet catalogue holds " + _pets.Count + " records, which are " +
                        _petGroups.Count + " animals.");
        }
        private Vector2 _petScroll;
        private string _petName = "";
        private string _petBonusValue = "10";
        private int _petBonusIndex;
        private static readonly EBonusEffect[] AllBonusEffects =
            (EBonusEffect[])Enum.GetValues(typeof(EBonusEffect));

        private EBonusEffect[] _bonusChoices;

        /// <summary>
        /// The bonuses a pet can actually be given.
        ///
        /// The list used to be every value of EBonusEffect, which is not the same question. The
        /// enum carries names no pet in the game is built with, and choosing one produced an
        /// animal with an empty description and a bonus that does nothing -- while a name that
        /// does appear on a real pet, Stranger Magnet among them, worked perfectly. So the list is
        /// read off the pet templates rather than off the type: if no animal in the game has it,
        /// it is not on offer.
        /// </summary>
        private EBonusEffect[] Bonuses()
        {
            if (_bonusChoices != null) return _bonusChoices;

            List<EBonusEffect> found = new List<EBonusEffect>();

            try
            {
                if (_pets == null) BuildPetCatalogue();

                if (_pets != null)
                {
                    for (int i = 0; i < _pets.Count; i++)
                    {
                        Array bonuses = ReadObject(_pets[i].Template, "BonusEffectList") as Array;
                        if (bonuses == null) continue;

                        for (int j = 0; j < bonuses.Length; j++)
                        {
                            object bonus = bonuses.GetValue(j);
                            object effect = bonus == null ? null : ReadObject(bonus, "Effect");
                            if (!(effect is EBonusEffect)) continue;

                            EBonusEffect kind = (EBonusEffect)effect;
                            if (!found.Contains(kind)) found.Add(kind);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ReportOnce("bonuslist", "Could not read which bonuses pets have: " + e);
            }

            // Never leave the row with nothing to offer. If the templates could not be read, the
            // whole enum is a worse list than this one but better than an empty one.
            if (found.Count == 0) found.AddRange(AllBonusEffects);
            else Log.LogInfo("Pets are built with " + found.Count + " of the " +
                             AllBonusEffects.Length + " bonus kinds the game defines.");

            _bonusChoices = found.ToArray();
            _petBonusIndex = Mathf.Clamp(_petBonusIndex, 0, _bonusChoices.Length - 1);

            return _bonusChoices;
        }

        // Dwellers are serialised in full, so unlike a weapon every attribute here reaches the save.
        private static readonly EDwellerRarity[] Rarities =
        {
            EDwellerRarity.Common, EDwellerRarity.Normal,
            EDwellerRarity.Rare, EDwellerRarity.Legendary
        };

        private static readonly EGender[] Genders = { EGender.Male, EGender.Female };

        // None and Max bracket the seven real stats in the enum; neither is a stat.
        private static readonly ESpecialStat[] Specials =
        {
            ESpecialStat.Strength, ESpecialStat.Perception, ESpecialStat.Endurance,
            ESpecialStat.Charisma, ESpecialStat.Intelligence, ESpecialStat.Agility,
            ESpecialStat.Luck
        };

        // Instance ids of the dwellers this session created, so the diagnostic can tell one of
        // ours from one of the game's.
        private readonly HashSet<int> _created = new HashSet<int>();

        private int _rarityIndex;
        private int _genderIndex;
        private string _dwellerFirst = "";
        private string _dwellerLast = "";
        private string _dwellerLevel = "1";
        private readonly int[] _special = { 1, 1, 1, 1, 1, 1, 1 };
        private Vector2 _legendScroll;

        private List<CatalogueEntry> _catalogue;
        private EItemType _family = EItemType.Weapon;
        private string _filter = "";
        private Vector2 _itemScroll;

        // A long list drawn in full costs a frame. The filter is how you reach the rest.
        private const int MaxRowsShown = 40;

        // Quantum is a resource in its own right and is granted as one; counting it here as well
        // was the same thing offered twice.
        private static readonly ELunchBoxType[] BoxTypes =
        {
            ELunchBoxType.Regular, ELunchBoxType.MrHandy, ELunchBoxType.PetCarrier
        };

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", false,
                "Master switch, off by default. While this is false the mod reads nothing, draws " +
                "nothing and binds no key: the game behaves exactly as it does without the plugin. " +
                "This is a debug tool, so it stays out of the way until it is asked for.");

            // The switches are kept in the config, so a vault left in peace is still at peace
            // tomorrow. Each is enforced on a slow beat rather than set once, because the game
            // turns them back on for itself.
            IncidentsOff = Config.Bind("Powers", "IncidentsOff", false,
                "Keeps fires, infestations and raiders from starting.");

            BottleAndCappyOff = Config.Bind("Powers", "BottleAndCappyOff", false,
                "Keeps Bottle and Cappy from wandering the vault.");

            RushAlwaysWorks = Config.Bind("Powers", "RushAlwaysWorks", false,
                "Keeps the rush failure chance cleared, so rushing never goes wrong. The game " +
                "raises that chance as rooms are rushed; this releases it again as it climbs.");

            MaxDwellersWanted = Config.Bind("Powers", "MaxDwellers", 0,
                "How many dwellers the vault will take. Zero leaves the game's own limit alone.");


            PreviewWholeSheet = Config.Bind("Diagnostics", "PreviewWholeSheet", false,
                "Draws the whole of the dweller's composed picture in the constructor rather than " +
                "the corner of it the game's own call selects. On, the figure is whole; off, it is " +
                "cropped the way the game crops it for its own panels.");

            WriteIconReport = Config.Bind("Diagnostics", "WriteIconReport", false,
                "Writes VaultAdmin-icons.txt beside the plugin, listing every picture the item " +
                "lists ask for and every picture the game's atlases hold. Only useful when an icon " +
                "is missing and the right name has to be looked up rather than guessed at.");

            ToggleKey = Config.Bind("General", "ToggleKey", "F8",
                "Key that opens and closes the panel, named as in UnityEngine.InputSystem.Key — " +
                "F8, Backquote, Insert and so on. An unrecognised name falls back to F8 with a " +
                "warning rather than leaving the panel unreachable.");

            ShowHudButton = Config.Bind("Interface", "ShowHudButton", true,
                "Put a button in the bottom-left of the vault interface, beside the screenshot " +
                "button, that opens this panel. The hotkey works either way.");

            HudButtonSprite = Config.Bind("Interface", "HudButtonSprite", "",
                "Sprite for the button, by name, from the same atlas the screenshot button uses. " +
                "Empty keeps the borrowed one. The names available are written to the log the " +
                "first time the button is placed, so there is a list to choose from rather than a " +
                "guess — a name the atlas does not hold renders as nothing at all.");

            HudButtonTint = Config.Bind("Interface", "HudButtonTint", "",
                "Colour of the button icon. The icon file is greyscale and NGUI multiplies it " +
                "by this, so white in the file becomes exactly this colour. 'auto' reads the " +
                "colour out of the game's own screenshot icon, which is the only way to match it " +
                "exactly rather than by eye. A hex value such as #3CE03C overrides that; empty " +
                "leaves the icon grey.");

            HudButtonImage = Config.Bind("Interface", "HudButtonImage", "button.png",
                "A PNG to use as the button's icon, looked for beside this plugin's DLL. Any size; " +
                "square reads best. This does not go through the game's atlas at all, so the " +
                "picture can be anything — replace the file and restart. Empty uses the borrowed " +
                "sprite instead.");

            HudButtonIconScale = Config.Bind("Interface", "HudButtonIconScale", 1f,
                "How large the icon is drawn relative to the button it sits on. The game's own HUD " +
                "icons fill their square almost edge to edge, so a value under one only makes this " +
                "look undersized beside them.");

            HudButtonOffsetY = Config.Bind("Interface", "HudButtonOffsetY", 10f,
                "The gap between the dwellers-list button and the panel button beneath it. The " +
                "distance between their centres is worked out from how tall the two of them are; " +
                "this is only the air in between.");

            HudButtonOffsetX = Config.Bind("Interface", "HudButtonOffsetX", 90f,
                "How far to the right of the screenshot button the panel button sits, in the " +
                "interface's own units. Raise it if the two overlap.");

            ResolveToggleKey();

            Log.LogInfo(PluginName + " " + PluginVersion +
                        (Enabled.Value ? " ready; press " + _toggleKey + " to open the panel."
                                       : " loaded but disabled. Set Enabled = true in the config to use it."));
        }

        /// <summary>
        /// Turns the configured key name into a <see cref="Key"/>, falling back rather than failing.
        /// A mistyped setting must not leave the panel silently unreachable.
        /// </summary>
        private void ResolveToggleKey()
        {
            string configured = ToggleKey.Value;
            if (string.IsNullOrEmpty(configured)) return;

            try
            {
                _toggleKey = (Key)Enum.Parse(typeof(Key), configured.Trim(), true);
            }
            catch
            {
                Log.LogWarning("ToggleKey '" + configured + "' is not a key name. Using " +
                               _toggleKey + " instead. Names come from UnityEngine.InputSystem.Key.");
            }
        }

        // The name our button carries, which is also how it is found again. The HUD is rebuilt
        // when a vault is reloaded, and a clone made each time would stack buttons on each other.
        private const string HudButtonName = "VaultAdmin_PanelButton";

        /// <summary>
        /// The game's own button for the list of dwellers, wherever it keeps it.
        ///
        /// Not by a path, because the path is the thing I do not know and guessing it has been an
        /// expensive habit. The HUD's own hierarchy is searched for a button whose name says
        /// dwellers, and the whole of that hierarchy is written down once, so a miss is one line to
        /// correct rather than another round of guesses.
        /// </summary>
        /// <summary>
        /// Moves the button under the dwellers list once that button exists.
        ///
        /// The HUD is not finished when the mod first looks at it: both of the game's dwellers
        /// buttons report themselves switched off, and the search honestly falls back to the corner
        /// beside the camera. It becomes switched on later, and by then nothing was looking any
        /// more -- the button is made once and the search never ran again. So it keeps looking, on
        /// a slow beat, until it either finds the anchor or has waited long enough to stop.
        /// </summary>
        private void MoveToTheListWhenItAppears()
        {
            if (_buttonSettled) return;
            if (++_lookedForList < 120) return;

            _lookedForList = 0;

            // A minute of looking is long enough. If the dwellers button has not appeared by then
            // it is not going to, and checking for ever is a cost with no answer at the end of it.
            if (++_lookedTimes > 30) { _buttonSettled = true; return; }

            Transform list = FindDwellerListButton();
            if (list == null) return;

            try
            {
                _hudButton.transform.SetParent(list.parent, false);
                _hudButton.transform.localPosition =
                    list.localPosition + new Vector3(0f, -HudButtonOffsetY.Value, 0f);

                _buttonSettled = true;
                Log.LogInfo("Moved the panel button under '" + PathOf(list) + "'.");
            }
            catch (Exception e)
            {
                ReportOnce("buttonmove", "Could not move the panel button: " + e.Message);
                _buttonSettled = true;
            }
        }

        private bool _buttonSettled;
        private int _lookedForList;
        private int _lookedTimes;

        private Transform FindDwellerListButton()
        {
            try
            {
                GameObject hud = GameObject.Find(HudPanelPath);
                if (hud == null) return null;

                Transform[] all = hud.GetComponentsInChildren<Transform>(true);

                if (!_reportedHud)
                {
                    _reportedHud = true;

                    // Every candidate with its whole path, not the first hundred and twenty names
                    // in the tree. The cut fell exactly where the top-right group begins, so the
                    // one button this is looking for was the first thing the listing did not say.
                    System.Text.StringBuilder said = new System.Text.StringBuilder();
                    said.Append("Dwellers buttons in the vault HUD:");

                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i].name.IndexOf("dweller", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        said.Append("  |  ").Append(PathOf(all[i]))
                            .Append("  active=").Append(all[i].gameObject.activeInHierarchy)
                            .Append(" clickable=")
                            .Append(all[i].GetComponentInChildren<Collider>(true) != null);
                    }

                    Log.LogInfo(said.ToString());
                }

                // The one in the corner the player pointed at, if there is one there. A HUD keeps
                // several buttons for the same thing -- one per layout -- and only the corner
                // decides which of them anybody means. Top left is where this game keeps it.
                Transform corner = PickButton(all, true);
                if (corner != null) return corner;

                return PickButton(all, false);
            }
            catch (Exception e)
            {
                ReportOnce("hudsearch", "Could not look through the vault HUD: " + e.Message);
            }

            return null;
        }

        /// <summary>
        /// The dwellers button, preferring the one in the top-right corner.
        ///
        /// Three things have to hold. It has to be named for dwellers and be a button rather than
        /// one of the labels inside one. It has to be switched on: this HUD carries a handset
        /// version and a tablet version of the same button and keeps the unused one disabled, and
        /// copying that put our button on a branch nothing draws and nothing clicks. And it should
        /// be in the corner the player is looking at, which is what the layout group it sits under
        /// says.
        /// </summary>
        private static Transform PickButton(Transform[] all, bool cornerOnly)
        {
            for (int i = 0; i < all.Length; i++)
            {
                string name = all[i].name;

                if (name.IndexOf("dweller", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (name.IndexOf("btn", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("button", StringComparison.OrdinalIgnoreCase) < 0) continue;

                // Not required to be switched on. All this is asked for is where it sits, and a
                // transform has a position whether or not anything is drawing it. Requiring it to
                // be on is what sent the button back beside the camera every time, because the HUD
                // is still assembling when the mod first looks.
                //
                // Its parent has to be on, though: an object under a hidden branch is a position
                // nobody will ever see.
                if (all[i].parent == null || !all[i].parent.gameObject.activeInHierarchy) continue;
                if (all[i].GetComponentInChildren<Collider>(true) == null) continue;

                if (cornerOnly && !InTheCorner(all[i])) continue;

                Log.LogInfo("Putting the panel button under '" + PathOf(all[i]) + "'.");
                return all[i];
            }

            return null;
        }

        /// <summary>Whether anything above this in the tree names the top-left group.</summary>
        private static bool InTheCorner(Transform what)
        {
            for (Transform up = what; up != null; up = up.parent)
            {
                string name = up.name.Replace(" ", "");
                if (name.IndexOf("topleft", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        private static string PathOf(Transform what)
        {
            string path = what.name;

            for (Transform up = what.parent; up != null; up = up.parent)
                path = up.name + "/" + path;

            return path;
        }

        /// <summary>
        /// How tall a piece of the interface is, in the units its parent lays out in.
        ///
        /// The widget's own height where it has one, the largest of its children's where it does
        /// not, and a sensible guess where neither answers. A button in this HUD is a container
        /// with the sprite inside it, so asking the container alone gets nothing.
        /// </summary>
        private static float HeightOf(Transform what)
        {
            float tall = 0f;

            try
            {
                UIWidget[] widgets = what.GetComponentsInChildren<UIWidget>(true);

                for (int i = 0; i < widgets.Length; i++)
                    if (widgets[i] != null && widgets[i].height > tall) tall = widgets[i].height;
            }
            catch { }

            return tall > 0f ? tall : 50f;
        }

        private static bool _reportedHud;

        private const string HudPanelPath =
            "MainScene_Root/GUI/VaultHUDWindow/VaultHUDPanel";

        // The anchor is the thing to look for, not the button inside it.
        //
        // GameObject.Find ignores inactive objects, and the screenshot button is inactive much of
        // the time — the survey caught it at active=False. Searching for the button itself meant
        // ours could only appear at the moments the game happened to be showing theirs, which is
        // exactly the delay that was noticed. The anchor stays put, and Transform.Find below does
        // see inactive children.
        private const string AnchorPath =
            "MainScene_Root/GUI/VaultHUDWindow/VaultHUDPanel/7 BottomLeft";

        private const string CameraButtonName = "BTN Camera";

        // The button once it exists. Held so the common case is a null check rather than a
        // search: looking every frame would be wasteful, and looking twice a second made the
        // button visibly late.
        private GameObject _hudButton;

        // The picture loaded for the HUD button, kept so the next one can replace it rather than
        // leave it behind.
        private Texture2D _hudImage;
        private bool _hudPathReported;
        private bool _spriteNamesReported;

        /// <summary>
        /// Puts a button in the vault HUD that opens the panel, by cloning one the game built.
        ///
        /// An NGUI widget renders as nothing when its depth is below what it sits on, when its
        /// parent is wrong, when its atlas lacks the sprite it names, or when its label has no
        /// font — and none of those produce an error. A clone inherits all of it from a button that
        /// already works, which is the only way to get those right without seeing the screen.
        ///
        /// Checked on a slow timer rather than once: the HUD does not exist at load and is rebuilt
        /// whenever a vault is, so a single attempt would either be too early or would not survive.
        /// </summary>
        private void EnsureHudButton()
        {
            if (!ShowHudButton.Value) return;

            // Cheap enough to run every frame: once the button exists this is one null check, and
            // while it does not the button should appear the moment the interface can hold it.
            if (_hudButton != null)
            {
                MoveToTheListWhenItAppears();
                return;
            }

            try
            {
                GameObject anchorObject = GameObject.Find(AnchorPath);
                if (anchorObject == null)
                {
                    // Not an error: outside a vault this part of the interface simply is not there.
                    if (!_hudPathReported)
                    {
                        _hudPathReported = true;
                        Log.LogInfo("The HUD anchor is not present yet. Looking for: " + AnchorPath);
                    }
                    return;
                }

                Transform parent = anchorObject.transform;

                Transform existing = parent.Find(HudButtonName);
                if (existing != null) { _hudButton = existing.gameObject; return; }

                // What to copy and where to put it are two questions, and answering them with
                // one object was the mistake. The camera button is the one worth copying: it is
                // switched on, it takes a press, and everything about it is known to work. The
                // dwellers button is worth nothing but its position -- and a position is readable
                // whether or not the object is switched on, which is what made this so hard to see.
                Transform found = parent.Find(CameraButtonName);

                if (found == null)
                {
                    Log.LogWarning("No '" + CameraButtonName + "' under " + AnchorPath +
                                   "; nothing to copy the button from.");
                    return;
                }

                GameObject source = found.gameObject;
                parent = source.transform.parent;

                Vector3 place = source.transform.localPosition +
                                new Vector3(HudButtonOffsetX.Value, 0f, 0f);

                Transform anchor = FindDwellerListButton();

                if (anchor != null && anchor.parent != null)
                {
                    parent = anchor.parent;

                    // Measured off the two buttons rather than added as a constant. A fixed sixty
                    // two units put ours on top of the one it was meant to sit under, because how
                    // far below "below" is depends on how tall they both are -- and neither of
                    // them told anybody that until they were asked.
                    float drop = HeightOf(anchor) * 0.5f + HeightOf(source.transform) * 0.5f +
                                 HudButtonOffsetY.Value;

                    place = anchor.localPosition + new Vector3(0f, -drop, 0f);

                    Log.LogInfo("Anchor '" + anchor.name + "' at " + anchor.localPosition +
                                " is " + HeightOf(anchor) + " tall; the panel button is " +
                                HeightOf(source.transform) + " tall and goes to " + place + ".");
                }

                _buttonSettled = anchor != null;

                GameObject clone = UnityEngine.Object.Instantiate(source);
                clone.name = HudButtonName;

                // false, because keeping world position puts the clone somewhere off screen: NGUI
                // lays out in its own scaled space, not the world's.
                clone.transform.SetParent(parent, false);
                clone.transform.localPosition = place;
                clone.transform.localRotation = source.transform.localRotation;
                clone.transform.localScale = source.transform.localScale;

                StripClonedBehaviour(clone);
                ReleaseAnchors(clone);
                WireButton(clone);
                MakeVisible(clone, source);

                // After the anchors are gone, so the position actually holds.
                clone.transform.localPosition = place;

                StyleButton(clone);
                _hudButton = clone;

                // The clone was reported placed and could not be seen, so both are described here
                // rather than guessed at again.
                Log.LogInfo("Placed a panel button in the vault HUD.");
                Log.LogInfo("    original : " + DescribeWidget(source));
                Log.LogInfo("    clone    : " + DescribeWidget(clone));
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not place the HUD button: " + e.Message);
            }
        }

        /// <summary>
        /// Makes the button look like ours rather than like the one it was copied from.
        ///
        /// A clone carries the camera's sprite, which is why it was indistinguishable. A sprite can
        /// only be changed to a name its atlas actually holds — anything else renders as nothing —
        /// so the names on offer are written to the log once, and the setting picks from them.
        /// Until one is chosen, a tint is enough to tell the two apart and cannot fail.
        /// </summary>
        private void StyleButton(GameObject clone)
        {
            try
            {
                UISprite sprite = clone.GetComponentInChildren<UISprite>(true);
                if (sprite == null) return;

                // Behind the switch that the rest of the diagnostics are behind. This walks
                // every UISprite in memory and prints the atlas -- useful once, while working out
                // what the HUD button could borrow, and noise in a stranger's log for ever after.
                if (WriteIconReport != null && WriteIconReport.Value) ReportSpriteNames(sprite);

                // A picture of our own, if there is one. Tried first: when it works the borrowed
                // sprite underneath is hidden and the tint stops mattering.
                if (ApplyCustomImage(clone, sprite)) return;

                string wanted = HudButtonSprite.Value;
                if (!string.IsNullOrEmpty(wanted))
                {
                    // Checked first: a name the atlas cannot resolve draws nothing at all, and a
                    // blank button is worse than a borrowed icon.
                    if (sprite.atlas != null && sprite.atlas.GetSprite(wanted) == null)
                    {
                        Log.LogWarning("The atlas has no sprite called '" + wanted +
                                       "'; keeping '" + sprite.spriteName + "'. See the list above.");
                    }
                    else
                    {
                        sprite.spriteName = wanted;
                        sprite.MakePixelPerfect();
                        Log.LogInfo("Button sprite set to '" + wanted + "'.");
                    }
                }

                string hex = HudButtonTint.Value;
                if (!string.IsNullOrEmpty(hex))
                {
                    Color tint;
                    if (ColorUtility.TryParseHtmlString(hex, out tint)) sprite.color = tint;
                    else Log.LogWarning("HudButtonTint '" + hex + "' is not a colour; left as it was.");
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not style the button: " + e.Message);
            }
        }

        /// <summary>
        /// Puts a picture of our own on the button, loaded from a PNG beside the DLL.
        ///
        /// This deliberately does not touch the atlas. An atlas draws from one packed texture, so a
        /// new sprite would mean new pixels inside a texture the game owns — and this build will not
        /// even list what that atlas already holds. A UITexture draws a plain Texture2D with no
        /// atlas involved, which sidesteps all of it.
        ///
        /// The existing UISprite is kept and merely made transparent rather than removed: UIButton
        /// holds references to it for its pressed and hover states, and tearing it out would break
        /// the button to change its picture.
        /// </summary>
        private bool ApplyCustomImage(GameObject clone, UISprite sprite)
        {
            try
            {
                string file = HudButtonImage.Value;
                if (string.IsNullOrEmpty(file)) return false;

                // The file name only. Path.Combine quietly drops the base directory when the
                // second argument is rooted, so a config line naming an absolute path reached
                // straight out of the plugin folder. Bounded rather than dangerous, but a mod has
                // no business reading a file from wherever a config happens to point.
                string path = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Info.Location),
                    System.IO.Path.GetFileName(file));

                if (!System.IO.File.Exists(path))
                {
                    ReportOnce("buttonimage", "No button image at " + path + "; keeping the borrowed sprite.");
                    return false;
                }

                // The last one goes before a new one is made. A destroyed GameObject compares
                // equal to null, so the HUD button is cloned again after every vault load, and
                // each clone used to leave its predecessor's texture behind.
                if (_hudImage != null) UnityEngine.Object.Destroy(_hudImage);

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _hudImage = texture;
                if (!ImageConversion.LoadImage(texture, System.IO.File.ReadAllBytes(path)))
                {
                    Log.LogWarning("Could not read " + path + " as an image; keeping the borrowed sprite.");
                    return false;
                }
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;

                GameObject holder = new GameObject("VaultAdmin_Icon");
                holder.layer = clone.layer;                    // NGUI draws by layer; a wrong one is invisible
                holder.transform.SetParent(clone.transform, false);
                holder.transform.localPosition = Vector3.zero;
                holder.transform.localScale = Vector3.one;

                float scale = HudButtonIconScale.Value;
                if (scale <= 0f) scale = 1f;

                UITexture drawn = holder.AddComponent<UITexture>();
                drawn.mainTexture = texture;

                // Sized from the button, not from the image: the picture is 256 across and the
                // button is fifty, so its own pixels are not a size at all.
                drawn.width = Mathf.RoundToInt(sprite.width * scale);
                drawn.height = Mathf.RoundToInt(sprite.height * scale);
                drawn.depth = sprite.depth + 1;                // in front of the sprite it replaces

                Shader shader = Shader.Find("Unlit/Transparent Colored");
                if (shader != null) drawn.shader = shader;

                // The icon is drawn in greyscale and coloured here. NGUI multiplies a UITexture by
                // its colour, so white in the file becomes exactly this. Matching the game's green
                // is then a line of configuration rather than a redraw — which is worth having
                // after three wrong guesses at the shade.
                string hex = HudButtonTint.Value;
                if (string.Equals(hex, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    Color sampled;
                    if (TrySampleSpriteColour(sprite, out sampled))
                    {
                        drawn.color = sampled;
                        Log.LogInfo("Icon colour taken from the game's own button: " +
                                    ColorUtility.ToHtmlStringRGB(sampled) + ".");
                    }
                    else
                    {
                        Log.LogWarning("Could not read the game's button colour; the icon is left grey. " +
                                       "Set HudButtonTint to a hex value to choose one.");
                    }
                }
                else if (!string.IsNullOrEmpty(hex))
                {
                    Color tint;
                    if (ColorUtility.TryParseHtmlString(hex, out tint)) drawn.color = tint;
                    else Log.LogWarning("HudButtonTint '" + hex + "' is not a colour; the icon is left grey.");
                }

                sprite.alpha = 0f;                             // hidden, not removed

                Log.LogInfo("Button icon loaded from " + path + " (" +
                            texture.width + "x" + texture.height + ").");
                return true;
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not apply the custom button image: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Reads the colour straight out of the game's own icon.
        ///
        /// Guessing the shade by eye failed three times running, and there is no need to guess: the
        /// pixels are right there. Atlas textures are not readable, so they are blitted through a
        /// RenderTexture first — the same trick the room-texture work in this repo settled on.
        ///
        /// The colour taken is the most common opaque one inside the sprite's own rectangle, which
        /// for a flat HUD icon is its fill. Averaging would have produced a muddy blend of the fill
        /// and the dark cut-outs, and the brightest pixel alone would catch an antialiased edge.
        /// </summary>
        private bool TrySampleSpriteColour(UISprite sprite, out Color colour)
        {
            colour = Color.white;

            RenderTexture rt = null;
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = null;

            try
            {
                // Each step says why it gave up. The first version returned a bare false from
                // four different places, so a failure told me nothing at all — which is the same
                // mistake as guessing, just quieter.
                if (sprite.atlas == null) { Log.LogWarning("  sampling: the sprite has no atlas."); return false; }

                // The atlas exposes neither a texture nor a material here — the previous attempt
                // reported exactly that. The widget does, though: UIWidget.mainTexture is what NGUI
                // actually draws with, and the sprite plainly renders. This build keeps almost
                // nothing where the obvious accessor looks, which its empty sprite list already
                // showed.
                Texture texture = sprite.mainTexture;

                if (texture == null && sprite.material != null) texture = sprite.material.mainTexture;
                if (texture == null) texture = sprite.atlas.texture;
                if (texture == null && sprite.atlas.spriteMaterial != null)
                {
                    texture = sprite.atlas.spriteMaterial.mainTexture;
                }

                if (texture == null)
                {
                    Log.LogWarning("  sampling: no texture from the widget, its material, or the atlas.");
                    return false;
                }
                if (texture.width == 0 || texture.height == 0)
                {
                    Log.LogWarning("  sampling: the atlas texture is " + texture.width + "x" + texture.height + ".");
                    return false;
                }

                UISpriteData data = sprite.atlas.GetSprite(sprite.spriteName);
                if (data == null)
                {
                    Log.LogWarning("  sampling: the atlas has no sprite named '" + sprite.spriteName + "'.");
                    return false;
                }
                if (data.width <= 0 || data.height <= 0)
                {
                    Log.LogWarning("  sampling: sprite '" + sprite.spriteName + "' is " +
                                   data.width + "x" + data.height + ".");
                    return false;
                }

                Log.LogInfo("  sampling: atlas " + texture.width + "x" + texture.height +
                            ", sprite '" + sprite.spriteName + "' at " + data.x + "," + data.y +
                            " size " + data.width + "x" + data.height + ".");

                rt = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                                                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;

                // NGUI measures y downwards from the top of the atlas; ReadPixels measures upwards.
                int bottom = texture.height - (data.y + data.height);
                Rect area = new Rect(data.x, bottom, data.width, data.height);

                readable = new Texture2D(data.width, data.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(area, 0, 0);
                readable.Apply();

                Dictionary<int, int> tally = new Dictionary<int, int>();
                Color32[] pixels = readable.GetPixels32();

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 px = pixels[i];
                    if (px.a < 200) continue;                       // edges and empty space

                    // Quantised, so near-identical shades from antialiasing count as one.
                    int key = ((px.r >> 3) << 10) | ((px.g >> 3) << 5) | (px.b >> 3);
                    int seen;
                    tally[key] = tally.TryGetValue(key, out seen) ? seen + 1 : 1;
                }

                int bestKey = -1;
                int bestCount = 0;
                foreach (KeyValuePair<int, int> entry in tally)
                {
                    if (entry.Value <= bestCount) continue;
                    bestCount = entry.Value;
                    bestKey = entry.Key;
                }

                if (bestKey < 0)
                {
                    Log.LogWarning("  sampling: every pixel in that rectangle was transparent (" +
                                   pixels.Length + " read). The rectangle or the blit is wrong.");
                    return false;
                }

                // Back to the middle of the quantised bucket.
                float r = (((bestKey >> 10) & 31) * 8 + 4) / 255f;
                float g = (((bestKey >> 5) & 31) * 8 + 4) / 255f;
                float b = ((bestKey & 31) * 8 + 4) / 255f;
                colour = new Color(r, g, b, 1f);
                return true;
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not sample the button colour: " + e.Message);
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }
        }

        /// <summary>Writes what this atlas offers, so a sprite can be chosen rather than guessed.</summary>
        private void ReportSpriteNames(UISprite sprite)
        {
            if (_spriteNamesReported) return;
            _spriteNamesReported = true;

            try
            {
                UIAtlas atlas = sprite.atlas;
                if (atlas == null) return;

                // The atlas will not list its own sprites: GetListOfSprites reads mSprites,
                // and that is empty here even after following the replacement link, while
                // GetSprite("Icon_Camera") plainly works. The data is not laid out where the
                // list-reading path looks.
                //
                // So the names are harvested from the widgets already drawing with this atlas.
                // Every one of those is a name the atlas resolves by definition, which is a
                // stronger guarantee than a list would have been.
                SortedSet<string> names = new SortedSet<string>(StringComparer.Ordinal);
                UISprite[] all = Resources.FindObjectsOfTypeAll<UISprite>();

                for (int i = 0; i < all.Length; i++)
                {
                    UISprite other = all[i];
                    if (other == null || other.atlas == null) continue;
                    // Same atlas, or one that resolves to the same texture — References is not
                    // public, and sharing a texture means sharing a sprite table anyway.
                    if (other.atlas != atlas && other.atlas.texture != atlas.texture) continue;
                    if (string.IsNullOrEmpty(other.spriteName)) continue;
                    names.Add(other.spriteName);
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("Sprites in use on atlas '").Append(atlas.name)
                  .Append("' (").Append(names.Count).Append(" distinct), current is '")
                  .Append(sprite.spriteName).Append("':");

                foreach (string n in names) sb.Append("\n    ").Append(n);

                Log.LogInfo(sb.ToString());
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not list the atlas sprites: " + e.Message);
            }
        }

        /// <summary>
        /// Cuts the clone loose from NGUI's anchoring.
        ///
        /// A UIWidget carries four anchors, and when any of them names a target NGUI recomputes the
        /// widget's position every update and overwrites whatever localPosition was set. A clone
        /// inherits those targets, so it snaps back to exactly where the button it was copied from
        /// sits — which puts it directly behind that button, where it cannot be seen and cannot be
        /// clicked. That matches the symptom exactly: reported placed, nowhere to be found.
        ///
        /// Clearing the targets hands position back to the transform.
        /// </summary>
        private void ReleaseAnchors(GameObject clone)
        {
            try
            {
                UIWidget[] widgets = clone.GetComponentsInChildren<UIWidget>(true);
                int released = 0;

                for (int i = 0; i < widgets.Length; i++)
                {
                    UIWidget w = widgets[i];
                    if (w == null) continue;

                    bool was = w.isAnchored;

                    if (w.leftAnchor != null) w.leftAnchor.target = null;
                    if (w.rightAnchor != null) w.rightAnchor.target = null;
                    if (w.bottomAnchor != null) w.bottomAnchor.target = null;
                    if (w.topAnchor != null) w.topAnchor.target = null;

                    // Only OnEnable and OnUpdate exist; with the targets cleared anchoring
                    // is inactive either way, and OnEnable does not recompute every frame.
                    w.updateAnchors = UIRect.AnchorUpdate.OnEnable;
                    if (was) released++;
                }

                Log.LogInfo("Released anchors on " + released + " of " + widgets.Length +
                            " widget(s) in the cloned button.");
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not release the cloned button's anchors: " + e.Message);
            }
        }

        /// <summary>
        /// Forces the clone into a state where it can actually be seen.
        ///
        /// It reported itself placed and was not visible. A widget is invisible when its alpha is
        /// zero, when its game object is inactive, or when its depth puts it behind what it sits
        /// on — and none of those complain. Rather than work out which one it was from here, all
        /// three are set, and the result is logged.
        /// </summary>
        private void MakeVisible(GameObject clone, GameObject source)
        {
            try
            {
                clone.SetActive(true);

                UIWidget[] mine = clone.GetComponentsInChildren<UIWidget>(true);
                UIWidget[] theirs = source.GetComponentsInChildren<UIWidget>(true);

                int highest = 0;
                for (int i = 0; i < theirs.Length; i++)
                {
                    if (theirs[i] != null && theirs[i].depth > highest) highest = theirs[i].depth;
                }

                for (int i = 0; i < mine.Length; i++)
                {
                    if (mine[i] == null) continue;
                    mine[i].alpha = 1f;
                    mine[i].depth = highest + 1;   // in front of the button it was copied from
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not force the cloned button visible: " + e.Message);
            }
        }

        /// <summary>Everything about a widget that decides whether it can be seen.</summary>
        private static string DescribeWidget(GameObject go)
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("active=").Append(go.activeInHierarchy);
                sb.Append(" localPos=").Append(go.transform.localPosition);
                sb.Append(" localScale=").Append(go.transform.localScale);

                UIWidget[] widgets = go.GetComponentsInChildren<UIWidget>(true);
                sb.Append(" widgets=").Append(widgets.Length);
                sb.Append(" anchored=").Append(go.GetComponent<UIWidget>() != null &&
                                               go.GetComponent<UIWidget>().isAnchored);

                for (int i = 0; i < widgets.Length && i < 4; i++)
                {
                    UIWidget w = widgets[i];
                    if (w == null) continue;
                    sb.Append(" [").Append(w.GetType().Name)
                      .Append(" ").Append(w.width).Append("x").Append(w.height)
                      .Append(" alpha=").Append(w.alpha.ToString("0.00"))
                      .Append(" depth=").Append(w.depth)
                      .Append(" visible=").Append(w.isVisible)
                      .Append("]");
                }
                return sb.ToString();
            }
            catch (Exception e) { return "<could not describe: " + e.Message + ">"; }
        }

        /// <summary>
        /// Removes what the clone brought with it that is not ours.
        ///
        /// A clone carries every component the original had, including whatever takes the
        /// screenshot. What is removed gets logged, so a button that does nothing can be told from
        /// one that still quietly does the old thing.
        /// </summary>
        private void StripClonedBehaviour(GameObject clone)
        {
            MonoBehaviour[] parts = clone.GetComponentsInChildren<MonoBehaviour>(true);
            System.Text.StringBuilder removed = new System.Text.StringBuilder();

            for (int i = 0; i < parts.Length; i++)
            {
                MonoBehaviour part = parts[i];
                if (part == null) continue;

                // Everything that makes it look and behave like a button stays; anything else the
                // original used it for goes.
                if (part is UIButton || part is UIWidget || part is UIPanel ||
                    part is UISprite || part is UILabel || part is UITexture ||
                    part is UIButtonColor || part is UIButtonScale ||
                    part is UIPlayAnimation) continue;   // click feedback, harmless to keep

                if (removed.Length > 0) removed.Append(", ");
                removed.Append(part.GetType().Name);

                // Immediately. The clone is switched on three lines after this returns, and a
                // deferred removal means every stripped behaviour still wakes up on an orphan.
                UnityEngine.Object.DestroyImmediate(part);
            }

            if (removed.Length > 0) Log.LogInfo("Stripped from the cloned button: " + removed);
        }

        private void WireButton(GameObject clone)
        {
            UIButton button = clone.GetComponent<UIButton>();
            if (button == null)
            {
                Log.LogWarning("The cloned button has no UIButton; it will not respond.");
                return;
            }

            if (button.onClick != null) button.onClick.Clear();
            else button.onClick = new List<EventDelegate>();

            button.onClick.Add(new EventDelegate(TogglePanel));
        }

        private bool _cameraHeld;

        /// <summary>
        /// Stops the vault camera reading the mouse while the panel is open.
        ///
        /// NGUI only sees what its own colliders catch; the camera reads the wheel and the drag for
        /// itself, so scrolling a list zoomed the vault at the same time. The camera is a
        /// MonoSingleton and its behaviour can simply be switched off and back on, which needs no
        /// patching of the game at all.
        /// </summary>
        private void HoldCamera(bool held)
        {
            try
            {
                CameraController camera = CameraController.Instance;
                if (camera == null) return;

                if (held == _cameraHeld) return;

                camera.enabled = !held;
                _cameraHeld = held;
            }
            catch (Exception e)
            {
                ReportOnce("camera", "Could not hold the vault camera: " + e.Message);
            }
        }

        /// <summary>Takes down the camera and its film. Left behind, they outlive the mod.</summary>
        private void DropPreviewCamera()
        {
            try
            {
                if (_previewCamera != null)
                {
                    _previewCamera.targetTexture = null;
                    UnityEngine.Object.Destroy(_previewCamera.gameObject);
                    _previewCamera = null;
                }

                if (_previewFilm != null)
                {
                    _previewFilm.Release();
                    UnityEngine.Object.Destroy(_previewFilm);
                    _previewFilm = null;
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not take the camera down: " + e.Message);
            }
        }

        private void OnDisable()
        {
            // Leaving the camera switched off because the mod went away would be unforgivable, and
            // so would leaving a person standing in the vault who was only ever a picture.
            HoldCamera(false);
            DisposePreview();
            DropPreviewCamera();
            PutTheVaultBack();
        }

        private Camera _uiCamera;

        /// <summary>
        /// Holds the vault camera only while the cursor is actually over the panel.
        ///
        /// Switching it off for as long as the panel was open meant the vault could not be moved
        /// at all without closing the panel first, which is worse than the problem it fixed. The
        /// window's own rectangle is the boundary: inside it the wheel belongs to the list, outside
        /// it the game behaves exactly as it always did.
        /// </summary>
        private void UpdateCameraHold()
        {
            if (!_panelOpen || _nguiWindow == null || _frame == null)
            {
                HoldCamera(false);
                return;
            }

            try
            {
                if (_uiCamera == null) _uiCamera = NGUITools.FindCameraForLayer(_nguiWindow.layer);
                if (_uiCamera == null) { HoldCamera(true); return; }

                Mouse mouse = Mouse.current;
                if (mouse == null) { HoldCamera(true); return; }

                Transform frame = _frame.transform;
                float halfWidth = _windowWidth / 2f + 10f;
                float halfHeight = _windowHeight / 2f + 8f;

                Vector3 lowLeft = _uiCamera.WorldToScreenPoint(
                    frame.TransformPoint(new Vector3(-halfWidth, -halfHeight, 0f)));
                Vector3 topRight = _uiCamera.WorldToScreenPoint(
                    frame.TransformPoint(new Vector3(halfWidth, halfHeight, 0f)));

                Vector2 point = mouse.position.ReadValue();

                bool over = point.x >= Mathf.Min(lowLeft.x, topRight.x) &&
                            point.x <= Mathf.Max(lowLeft.x, topRight.x) &&
                            point.y >= Mathf.Min(lowLeft.y, topRight.y) &&
                            point.y <= Mathf.Max(lowLeft.y, topRight.y);

                HoldCamera(over);
            }
            catch (Exception e)
            {
                ReportOnce("hover", "Could not tell where the cursor is: " + e.Message);
                HoldCamera(true);
            }
        }

        /// <summary>Opens or closes the panel. Shared by the hotkey and the HUD button.</summary>
        public void TogglePanel()
        {
            // Reached straight from a button. NGUI's dispatch does not catch, so an
            // exception here abandons the rest of that frame's UI events -- and the spec
            // says a failure in the panel never reaches the game.
            try
            {
                _panelOpen = !_panelOpen;
                if (!_panelOpen) HoldCamera(false);
                if (!_panelOpen) DisposePreview();

                if (_nguiWindow == null) BuildWindow();

                if (_panelOpen)
                {
                    _drawChecked = false;
                    _drawCheckFrames = 0;

                    // Closing the panel puts the figure away and nothing was bringing it back: the tab
                    // has not changed, so ShowTab never runs, and the bench sat empty until the gender
                    // was stepped. Reopening is a good enough reason to stand someone there.
                    if (_tab == Tab.Create && _making == Making.Dweller) RemakePreview();
                }
                if (_nguiWindow != null) _nguiWindow.SetActive(_panelOpen);
        }
            catch (Exception e)
            {
                ReportOnce("toggle", "toggle failed: " + e);
            }
        }

        // ---- the window, built from the game's own widget types ----

        private GameObject _nguiWindow;
        private UITexture _frame;      // the window's own backing, and the proof it is being drawn
        private int _drawCheckFrames;
        private bool _drawChecked;
        private bool _nguiDrawing;     // false until the frame is seen with a draw call
        private object _font;            // UIFont or Font, whichever the game's labels use
        private int _fontSize = 28;

        // One ladder of sizes for the whole panel. Every label had been given a multiplier of its
        // own — nought point seven here, nought point seven-four there, a bare sixteen somewhere
        // else — and a dozen sizes that are nearly the same read as carelessness rather than as
        // hierarchy.
        // Six sizes, and every label in the panel is one of them. The ladder existed before
        // this; what did not was anything applying it, so most labels were drawn at the borrowed
        // font's own size and the panel read as though everything in it were equally important.
        // MakeLabel now starts every label at TextRow, and the tiers below are asked for by the
        // few places that want something quieter.
        //
        //   Title    the window's name, once
        //   Heading  a section, and the tabs
        //   Row      what a row is: an item, a power, a resource, a chosen value
        //   Body     a figure or a caption beside one
        //   Note     what a thing does, under its name
        //   Tiny     an index, a count, a position in a list
        private int TextTitle { get { return Mathf.RoundToInt(_fontSize * 1.15f); } }
        private int TextHeading { get { return Mathf.Max(13, Mathf.RoundToInt(_fontSize * 0.92f)); } }
        private int TextRow { get { return Mathf.Max(12, Mathf.RoundToInt(_fontSize * 0.74f)); } }
        private int TextBody { get { return Mathf.Max(11, Mathf.RoundToInt(_fontSize * 0.62f)); } }
        private int TextTiny { get { return Mathf.Max(9, Mathf.RoundToInt(_fontSize * 0.46f)); } }

        // Under a power's name, where the line explains rather than names. It had been the same
        // size as the small text everywhere else and, being twice the length of most of it, read
        // as the louder half of its own row.
        private int TextNote { get { return Mathf.Max(10, Mathf.RoundToInt(_fontSize * 0.52f)); } }

        private const int WindowDepth = 5000;   // above everything the game draws

        // Measured from the interface rather than fixed, so the panel keeps its proportions on any
        // screen: a third of the width, down the left, and short of the full height so the game's
        // own controls along the top and bottom of the overlay stay reachable.
        private int _windowWidth = 520;
        private int _windowHeight = 620;
        private int _windowX;

        private const int EdgeMargin = 20;
        private const int VerticalInset = 90;   // room for the game's top and bottom controls
        private const int RowHeight = 44;
        private const int RowGap = 5;
        private const int Margin = 20;

        private int _cursorY;
        private int _scrollTop;
        private int _refreshFrames;
        private int _upkeepFrames;
        private Room[] _rushingRooms;
        private bool _texturedOnce;
        private float _framedSize = -1f;
        private Vector3 _framedAt;
        private UIAtlas _menuAtlas;
        private readonly List<UITexture> _thumbs = new List<UITexture>();

        // What the panel does falls into three jobs, not three kinds of thing: top the vault up,
        // hand something over, or build something that does not exist yet. A dweller belongs in two
        // of those, which is why splitting by kind put its two halves in one place and nothing in
        // the other.
        private enum Tab { Resources, Grant, Create, Powers }

        // Dwellers are handed out from the same list as items even though the game does not count
        // them as one, so the picker has a family of its own for them.
        private enum Family { Weapon, Outfit, Junk, Pet, Dweller }

        /// <summary>A dweller the game rolls for itself, offered by rarity beside the named ones.</summary>
        private sealed class RandomDweller
        {
            public EDwellerRarity Rarity;
        }

        private Tab _tab = Tab.Resources;
        private readonly Dictionary<Tab, GameObject> _tabPages = new Dictionary<Tab, GameObject>();
        private readonly Dictionary<Tab, GameObject> _tabButtons = new Dictionary<Tab, GameObject>();

        // Value labels that have to keep up with the vault, kept so they can be rewritten rather
        // than the row rebuilt. Rebuilding widgets to change a number turns a panel into a source
        // of garbage.
        private readonly Dictionary<EResource, UILabel> _resourceLabels =
            new Dictionary<EResource, UILabel>();

        private void MeasureWindow(UIRoot root)
        {
            int virtualHeight = root.activeHeight > 0 ? root.activeHeight : 720;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            int virtualWidth = Mathf.RoundToInt(virtualHeight * aspect);

            // Text is sized from the interface, like everything else here. It scales with the
            // resolution and is the same on every run, which the borrowed size was not.
            _fontSize = Mathf.Clamp(Mathf.RoundToInt(virtualHeight * 0.0375f), 18, 34);

            // A tenth wider than a third of the screen. Every row in here is a name, a figure
            // and a control in a line, and at a third of the screen the name was the part that
            // gave way. Nothing else changes: margins, gaps and text keep their sizes, so the
            // extra width lands where it was short rather than being spread about.
            _windowWidth = Mathf.Clamp(Mathf.RoundToInt(virtualWidth / 3f * 1.1f), 418, 900);
            _windowHeight = Mathf.Max(320, virtualHeight - VerticalInset * 2);
            _windowX = -virtualWidth / 2 + _windowWidth / 2 + EdgeMargin;

            Log.LogInfo("Window sized to " + _windowWidth + "x" + _windowHeight +
                        " within a " + virtualWidth + "x" + virtualHeight +
                        " interface; text at " + _fontSize + ".");
        }

        /// <summary>
        /// Builds the window once, out of textures drawn at runtime rather than atlas sprites.
        ///
        /// Parented under the game's own UI root so it inherits the scaling that makes the interface
        /// the same size on every screen. Depth is set far above the game's panels: a widget behind
        /// another is invisible with no error, and that has already cost a round of guessing here.
        /// </summary>
        private bool BuildWindow()
        {
            try
            {
                UIRoot root = FindUiRoot();
                if (root == null)
                {
                    ReportOnce("uiroot", "No UI root yet; the window cannot be built.");
                    return false;
                }

                // Everything held from the last window belongs to widgets that no longer exist.
                // Kept, they were worse than useless: the grant list counts its rows to decide how
                // many to build, so 260 dead ones meant it built none and showed an empty tab with
                // a confident number above it, for the rest of the session.
                ForgetTheOldWindow();

                BorrowFont();

                // Everything drawn from here on is sized in interface units but rendered at the
                // screen's resolution.
                Skin.Scale = root.activeHeight > 0
                    ? Mathf.Clamp((float)Screen.height / root.activeHeight, 1f, 3f)
                    : 1f;
                Log.LogInfo("Drawing the skin at " + Skin.Scale.ToString("0.00") + " pixels per unit.");

                MeasureWindow(root);

                _nguiWindow = new GameObject("VaultAdmin_Window");
                _nguiWindow.layer = root.gameObject.layer;
                _nguiWindow.transform.SetParent(root.transform, false);
                _nguiWindow.transform.localPosition = new Vector3(_windowX, 0f, 0f);
                _nguiWindow.transform.localScale = Vector3.one;

                UIPanel windowPanel = _nguiWindow.AddComponent<UIPanel>();
                windowPanel.depth = WindowDepth;

                _frame = Plate(_nguiWindow.transform, "Frame", 0, 0, _windowWidth, _windowHeight,
                               Skin.Window(_windowWidth, _windowHeight), 0);

                // Written straight onto the top edge, in the green, as the game's own windows do
                // it — large enough to read as the window's name rather than a caption on it.
                // A tenth of its own height lower, so it rests on the frame rather than balancing
                // on it.
                UILabel title = MakeLabel(_nguiWindow.transform, "Title", "VAULT ADMIN",
                                          0, _windowHeight / 2 - 6, _windowWidth - 60, 52,
                                          Skin.Bright, 4);
                title.fontSize = TextTitle;

                // Outlined, so the frame it straddles reads as behind it rather than through it.
                title.effectStyle = UILabel.Effect.Outline;
                title.effectColor = Skin.Ink;
                title.effectDistance = new Vector2(2f, 2f);

                BuildTabs(_nguiWindow.transform);
                BuildPages(_nguiWindow.transform);
                ShowTab(_tab);
            RefreshThings();

                // On the bottom edge, as the title is on the top one: outlined rather than solid,
                // smaller than it was, and lettered large enough to read as the way out.
                GameObject close = MakeButton(_nguiWindow.transform, "Close", "CLOSE",
                                              0, -_windowHeight / 2 + 6, 148, 48, true, TogglePanel);

                UITexture closeFace = close.GetComponent<UITexture>();
                if (closeFace != null)
                    closeFace.mainTexture = Skin.SolidOutlined(closeFace.width, closeFace.height);

                UILabel closeText = close.GetComponentInChildren<UILabel>();
                if (closeText != null) closeText.fontSize = TextHeading;

                _nguiWindow.SetActive(false);
                Log.LogInfo("Built the panel window under " + root.name + ".");
                return true;
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not build the window: " + e);

                // Half a window is not something to keep. Left behind, it takes its rows and powers
                // with it and every later attempt appends to them.
                if (_nguiWindow != null) UnityEngine.Object.Destroy(_nguiWindow);
                _nguiWindow = null;
                ForgetTheOldWindow();
                return false;
            }

        }

        /// <summary>Lets go of every widget the previous window owned.</summary>
        private void ForgetTheOldWindow()
        {
            _itemRows.Clear();
            _thumbs.Clear();
            _powers.Clear();
            _tabPages.Clear();
            _tabButtons.Clear();
            _resourceLabels.Clear();
        }

        // ---- scrolling ----

        // The window is a third of the screen and the lists are longer than it. NGUI scrolls with a
        // panel that clips and a scroll view that moves it; both live on the same object, which is
        // what UIScrollView.Awake expects to find.
        private UIScrollView BeginScroll(Transform page, int width, int top, int bottom,
                                         out Transform content)
        {
            int viewHeight = Mathf.Max(80, top - bottom);
            int centre = bottom + viewHeight / 2;

            GameObject go = new GameObject("Scroll");
            go.layer = page.gameObject.layer;
            go.transform.SetParent(page, false);
            go.transform.localPosition = new Vector3(0f, centre, 0f);
            go.transform.localScale = Vector3.one;

            // Built inactive so Awake sees these settings rather than the defaults. Left to itself
            // the scroll view chooses ConstrainButDontClip, which is a list that runs off the
            // window exactly as before.
            go.SetActive(false);

            UIPanel panel = go.AddComponent<UIPanel>();
            panel.depth = WindowDepth + 1;
            panel.clipping = UIDrawCall.Clipping.SoftClip;
            panel.baseClipRegion = new Vector4(0f, 0f, width + 8, viewHeight);
            panel.clipSoftness = new Vector2(4f, 8f);

            UIScrollView view = go.AddComponent<UIScrollView>();

            // NGUI keeps a legacy field that its Awake turns back into a direction, and its default
            // stands for horizontal. Left alone it overrides whatever is set here — which is why the
            // first attempt scrolled sideways and dragged the rows out from under their labels.
            // Cleared before Awake, and the direction set again after it, so neither order matters.
            try
            {
                FieldInfo legacy = typeof(UIScrollView).GetField(
                    "scale", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (legacy != null) legacy.SetValue(view, Vector3.zero);
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not clear the scroll view's legacy direction: " + e.Message);
            }

            view.movement = UIScrollView.Movement.Vertical;
            view.dragEffect = UIScrollView.DragEffect.MomentumAndSpring;
            view.scrollWheelFactor = 0.4f;
            view.restrictWithinPanel = true;
            view.disableDragIfFits = true;
            view.showScrollBars = UIScrollView.ShowCondition.Always;

            // Where 'the beginning' is. Without this the list opened at the bottom, which for two
            // hundred outfits meant opening on the last four of them.
            view.contentPivot = UIWidget.Pivot.Top;

            go.SetActive(true);
            view.movement = UIScrollView.Movement.Vertical;

            view.verticalScrollBar = MakeScrollBar(page, width, centre, viewHeight);

            content = go.transform;
            _scrollTop = viewHeight / 2 - 4;
            _cursorY = _scrollTop;
            return view;
        }

        /// <summary>
        /// A bar down the right-hand edge, so the list says how long it is and can be dragged by it.
        ///
        /// The game's own wheel handling reaches the camera whatever the panel does about it, so the
        /// bar is not a nicety: without it a list that does not fit has no visible way out.
        /// </summary>
        private UIScrollBar MakeScrollBar(Transform page, int width, int centre, int viewHeight)
        {
            GameObject go = new GameObject("ScrollBar");
            go.layer = page.gameObject.layer;
            go.transform.SetParent(page, false);
            // Inside the content rather than on the frame: at the old offset the bar and the
            // window's own edge were drawn through each other.
            go.transform.localPosition = new Vector3(width / 2 + 7, centre, 0f);
            go.transform.localScale = Vector3.one;

            UITexture track = Plate(go.transform, "Track", 0, 0, 10, viewHeight,
                                    Skin.Frame(10, viewHeight, 5, Skin.EdgeCard, Skin.Rim, Skin.Hole), 2);

            // The scroll view resizes this one to say how much of the list is in view, so its
            // texture has to survive being stretched: a nearly square source with a small radius
            // stays a bar, where a long rounded one turns into a smear.
            UITexture thumb = Plate(go.transform, "Thumb", 0, 0, 10, viewHeight,
                                    Skin.Frame(10, viewHeight, 5, Skin.EdgeButton, Skin.Bright, Skin.Bright), 3);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(22f, viewHeight, 1f);
            box.isTrigger = true;

            UIScrollBar bar = go.AddComponent<UIScrollBar>();
            bar.backgroundWidget = track;
            bar.foregroundWidget = thumb;
            bar.fillDirection = UIProgressBar.FillDirection.TopToBottom;

            // The scroll view resizes the thumb to say how much of the list is in view. A texture
            // left to stretch across that turns into a smear, so it is redrawn at the height it is
            // actually shown at, rounded to a step so the cache does not fill with near-identical
            // copies while a drag is under way.
            _thumbs.Add(thumb);
            return bar;
        }

        /// <summary>
        /// Gives the scroll view something to be dragged by.
        ///
        /// NGUI routes drags and the wheel through colliders, so a list of plates and labels is
        /// inert. One collider over the whole content catches the empty space, and every collider
        /// already there — the buttons — is taught to pass a drag along rather than swallow it.
        /// </summary>
        private void EndScroll(UIScrollView view, int width)
        {
            int used = Mathf.Max(40, _scrollTop - _cursorY);

            GameObject area = new GameObject("DragArea");
            area.layer = view.gameObject.layer;
            area.transform.SetParent(view.transform, false);
            // Behind the rows: NGUI takes the nearest collider, and this one must not take the
            // presses meant for the buttons.
            area.transform.localPosition = new Vector3(0f, _scrollTop - used / 2, 20f);
            area.transform.localScale = Vector3.one;

            BoxCollider box = area.AddComponent<BoxCollider>();
            box.size = new Vector3(width, used, 1f);
            box.isTrigger = true;

            BoxCollider[] colliders = view.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].GetComponent<UIDragScrollView>() != null) continue;
                UIDragScrollView drag = colliders[i].gameObject.AddComponent<UIDragScrollView>();
                drag.scrollView = view;
            }

            view.ResetPosition();
        }

        // ---- icons from the game's own atlas ----

        /// <summary>
        /// The atlas the interface's own icons live in.
        ///
        /// Taken off the cloned HUD button, which carries the original sprite: that button is drawn,
        /// so its atlas is one that resolves. Reading it out of Resources by name would find several.
        /// </summary>
        private UIAtlas MenuAtlas()
        {
            if (_menuAtlas != null) return _menuAtlas;
            if (_hudButton == null) return null;

            UISprite[] sprites = _hudButton.GetComponentsInChildren<UISprite>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].atlas != null)
                {
                    _menuAtlas = sprites[i].atlas;
                    Log.LogInfo("Icons will come from atlas '" + _menuAtlas.name + "'.");
                    break;
                }
            }
            return _menuAtlas;
        }

        // Sprite names were taken from a dump of what the interface had on screen, which is not
        // the same as what the atlas holds, and several of them drew nothing. Each of these is a
        // list of candidates now, and the atlas is asked which one it actually has.
        private static string[] ResourceSprites(EResource resource)
        {
            switch (resource)
            {
                case EResource.Nuka:
                    return new[] { "Icon_nukacapsPlain", "Icon_nukacapsColor", "Icon_nukacaps",
                                   "NukaCaps", "Caps" };
                case EResource.Food:
                    return new[] { "Icon_foodPlain", "Icon_food", "Icon_FoodWater" };
                case EResource.Energy:
                    return new[] { "Icon_energyPlain", "Icon_energyGreen", "Icon_energy" };
                case EResource.Water:
                    return new[] { "Icon_WaterPlain", "Icon_Water", "Icon_WaterColorGreen" };
                case EResource.StimPack:
                    return new[] { "Icon_StimpackPlain", "Icon_Stimpack", "Stimpack" };
                case EResource.RadAway:
                    return new[] { "Icon_RadawayPlain", "Icon_Radaway", "Radaway" };
                case EResource.NukaColaQuantum:
                    return new[] { "Icon_NukaQuantum", "Icon_NukaColaQuantum",
                                   "Icon_Nuka_Quantum_Star", "NukaQuantum", "NukaColaQuantum002" };
                default:
                    return null;
            }
        }

        /// <summary>
        /// The picture the game itself uses for a box.
        ///
        /// GUIParameters carries the three sprite names outright, which settles what had been two
        /// rounds of picking between a robot, a robot with a boy beside it, and a crate.
        /// </summary>
        private string[] BoxSprites(ELunchBoxType type)
        {
            string named = null;

            try
            {
                GameParameters parameters = GameParameters.Instance;
                GUIParameters gui = parameters != null ? parameters.GUIParameters : null;

                if (gui != null)
                {
                    switch (type)
                    {
                        case ELunchBoxType.Regular:    named = gui.Lunchbox; break;
                        case ELunchBoxType.MrHandy:    named = gui.MrHandyBox; break;
                        case ELunchBoxType.PetCarrier: named = gui.PetCarrier; break;
                    }
                }
            }
            catch (Exception e)
            {
                ReportOnce("boxsprite", "Could not read the box pictures from the game: " + e.Message);
            }

            string[] fallback = BoxSpriteGuesses(type);
            if (string.IsNullOrEmpty(named)) return fallback;

            string[] all = new string[fallback.Length + 1];
            all[0] = named;
            Array.Copy(fallback, 0, all, 1, fallback.Length);
            return all;
        }

        private static string[] BoxSpriteGuesses(ELunchBoxType type)
        {
            switch (type)
            {
                // The painted article and nothing else. The same names elsewhere are pictures of
                // someone holding the article, or banners too wide to sit in a row.
                case ELunchBoxType.Regular:
                    return new[] { "Lunchbox", "LunchboxPlainColor", "LunchBox",
                                   "Icon_LunchboxesPlain" };
                case ELunchBoxType.MrHandy:
                    return new[] { "MrHandy", "MR_handy", "Icon_MrHandy" };
                case ELunchBoxType.PetCarrier:
                    return new[] { "PetCarrier", "Pet Carrier", "Icon_PetCarrier" };
                default:
                    return null;
            }
        }

        /// <summary>
        /// The sprite an atlas actually has for a name it was given.
        ///
        /// The records name their art, but the atlas often names the same picture a little
        /// differently — the same words with something else between them. Rather than fail on an
        /// exact miss, the words are matched: 'Military_FullBody' finds 'Military_Macaw_FullBody'.
        /// Every substitution is logged once, so a wrong match is visible rather than mysterious.
        /// </summary>
        private string BestSprite(UIAtlas atlas, string wanted)
        {
            if (atlas == null || string.IsNullOrEmpty(wanted)) return null;
            if (atlas.GetSprite(wanted) != null) return wanted;

            try
            {
                List<UISpriteData> list = SpritesOf(atlas);
                if (list == null || list.Count == 0) return null;

                // The same name in a different case. The interface's own dweller icon is
                // 'Icon_dweller' with a small d, and an exact lookup for 'Icon_Dweller' finds
                // nothing at all.
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == null || list[i].name == null) continue;
                    if (string.Equals(list[i].name, wanted, StringComparison.OrdinalIgnoreCase))
                        return list[i].name;
                }

                string[] parts = Meaningful(wanted);
                if (parts.Length == 0) return null;

                // Each word in turn as the one that must be present, longest first. Insisting on
                // the longest alone was too strict — 'NormalClothing' is drawn as 'Normal', and
                // 'Casual01' as 'Casual' — while insisting on none at all was how a request for the
                // dwellers icon came back with a mushroom cloud, because both are an 'icon'.
                Array.Sort(parts, LongestFirst);

                for (int k = 0; k < parts.Length; k++)
                {
                    string best = null;
                    int bestScore = 0;

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] == null || string.IsNullOrEmpty(list[i].name)) continue;
                        if (list[i].name.IndexOf(parts[k], StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        int score = 0;
                        for (int j = 0; j < parts.Length; j++)
                            if (list[i].name.IndexOf(parts[j], StringComparison.OrdinalIgnoreCase) >= 0)
                                score++;

                        if (score > bestScore) { bestScore = score; best = list[i].name; }
                    }

                    if (best != null)
                    {
                        ReportOnce("match_" + atlas.name + "_" + wanted,
                                   "'" + atlas.name + "' has no '" + wanted + "'; using '" + best + "'.");
                        return best;
                    }
                }
            }
            catch { }

            return null;
        }

        private static int LongestFirst(string a, string b)
        {
            return b.Length.CompareTo(a.Length);
        }

        // Words that say what kind of picture something is rather than which picture it is. A
        // match on these alone means nothing at all.
        private static readonly string[] EmptyWords =
        {
            "icon", "icons", "plain", "color", "colour", "sprite", "image", "img",
            "common", "default", "new", "old", "small", "big", "the"
        };

        private static string[] SplitWords(string token)
        {
            if (string.IsNullOrEmpty(token)) return new string[0];

            List<string> words = new List<string>();
            string current = "";

            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];

                if (char.IsDigit(c)) continue;                       // a variant number, not a word

                if (char.IsUpper(c) && current.Length > 1)
                {
                    words.Add(current);
                    current = "";
                }

                current += c;
            }

            if (current.Length > 0) words.Add(current);
            if (words.Count == 0) words.Add(token);

            return words.ToArray();
        }

        private static string[] Meaningful(string name)
        {
            if (string.IsNullOrEmpty(name)) return new string[0];

            string[] raw = name.Split('_', ' ', '-');
            List<string> kept = new List<string>();

            for (int i = 0; i < raw.Length; i++)
            {
                // Trailing digits number a variant rather than name it: ten Casual outfits are all
                // drawn as one 'Casual'. And a run-together name is two words to everyone but the
                // string: NormalClothing is drawn as 'Normal'.
                string[] words = SplitWords(raw[i]);

                for (int j = 0; j < words.Length; j++)
                {
                    if (words[j].Length < 3) continue;
                    if (Array.IndexOf(EmptyWords, words[j].ToLowerInvariant()) >= 0) continue;
                    kept.Add(words[j]);
                }
            }

            return kept.ToArray();
        }

        /// <summary>
        /// An atlas's sprites, following the replacement it may stand in for.
        ///
        /// An atlas can be a stand-in that points at the real one, and pet art arrives that way:
        /// the object exists from the moment it is asked for and holds nothing until the load
        /// finishes. Reading the empty list and believing it is why the drone had no picture.
        /// </summary>
        private static List<UISpriteData> SpritesOf(UIAtlas atlas)
        {
            int guard = 0;
            while (atlas != null && guard++ < 8)
            {
                List<UISpriteData> list = atlas.spriteList;
                if (list != null && list.Count > 0) return list;

                UIAtlas next = atlas.replacement;
                if (next == null || next == atlas) return null;
                atlas = next;
            }
            return null;
        }

        /// <summary>
        /// The first candidate the atlas actually holds.
        ///
        /// A UISprite given a name the atlas does not have draws nothing and says nothing about it,
        /// which is how several rows ended up with a blank where their picture should be. Asking
        /// first turns a silent gap into a line in the log.
        /// </summary>
        private string ResolveSprite(UIAtlas atlas, string[] candidates, string what)
        {
            if (atlas == null || candidates == null) return null;

            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.IsNullOrEmpty(candidates[i])) continue;
                if (atlas.GetSprite(candidates[i]) != null) return candidates[i];
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                string near = BestSprite(atlas, candidates[i]);
                if (!string.IsNullOrEmpty(near)) return near;
            }

            ReportOnce("sprite_" + what,
                       "No icon in '" + atlas.name + "' for " + what + "; tried " +
                       string.Join(", ", candidates) + ".");
            SuggestSprites(atlas, what);
            return null;
        }

        /// <summary>
        /// Writes down what the atlas does have, so the next guess is not one.
        ///
        /// A missing icon is worth one line in a log and nothing more, but a missing icon whose real
        /// name is three characters away is worth knowing about before it costs another round.
        /// </summary>
        private void SuggestSprites(UIAtlas atlas, string what)
        {
            try
            {
                List<UISpriteData> sprites = atlas.spriteList;
                if (sprites == null) return;

                string hint = what.ToLowerInvariant();
                if (hint.StartsWith("box ")) hint = hint.Substring(4);
                if (hint.Length > 5) hint = hint.Substring(0, 5);

                string found = "";
                int shown = 0;

                for (int i = 0; i < sprites.Count && shown < 12; i++)
                {
                    if (sprites[i] == null || sprites[i].name == null) continue;
                    if (Missing(sprites[i].name, hint)) continue;

                    found += (found.Length > 0 ? ", " : "") + sprites[i].name;
                    shown++;
                }

                Log.LogInfo("  '" + atlas.name + "' holds these matching '" + hint + "': " +
                            (found.Length > 0 ? found : "nothing"));

                // A miss says one name is wrong; the scheme says why, and what the right one
                // would look like.
                string sample = "";
                for (int i = 0; i < sprites.Count && i < 15; i++)
                {
                    if (sprites[i] == null || sprites[i].name == null) continue;
                    sample += (sample.Length > 0 ? ", " : "") + sprites[i].name;
                }
                Log.LogInfo("  '" + atlas.name + "' holds " + sprites.Count + " sprites, beginning: " +
                            sample);
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not list the atlas's sprites: " + e.Message);
            }
        }

        // Icon_dwellerPlain, with a small d, as the atlas itself spells it.
        private static readonly string[] DwellerSprites =
        {
            "Icon_dwellerPlain", "Icon_dweller", "new_dweller"
        };

        /// <summary>
        /// A named dweller's own portrait.
        ///
        /// The game keeps one for each of them in an atlas of its own, filed under the name with an
        /// L for legendary in front of it — sometimes with the spaces kept, sometimes without. So
        /// both are asked for, and the word matcher covers the rest.
        /// </summary>
        private UIAtlas _dwellersAtlas;
        private bool _lookedForDwellersAtlas;

        /// <summary>
        /// The atlas of portraits, one for every dweller the game has written.
        /// </summary>
        private UIAtlas DwellersAtlas()
        {
            if (_lookedForDwellersAtlas) return _dwellersAtlas;
            _lookedForDwellersAtlas = true;

            UIAtlas[] all = Resources.FindObjectsOfTypeAll<UIAtlas>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].name != "Dwellers") continue;
                if (SpritesOf(all[i]) == null) continue;

                _dwellersAtlas = all[i];
                Log.LogInfo("Portraits will come from the '" + all[i].name + "' atlas.");
                break;
            }

            if (_dwellersAtlas == null)
                ReportOnce("dwellersatlas", "No portrait atlas found; the plain icon will be used.");

            return _dwellersAtlas;
        }

        /// <summary>
        /// A named dweller's own portrait, found the way the game finds it.
        ///
        /// Not by the person's name — that was two rounds of near misses and one round of Three Dog
        /// being drawn as a dog. The game reads the sprite name off the data object itself:
        /// UISeasonPassRewardLockedPopup takes Object.name from the UniqueDwellerData and hands it
        /// straight to a UISprite on the dwellers atlas. So this does the same.
        /// </summary>
        private void ShowLegendIcon(UISprite icon, UniqueDwellerData legend)
        {
            if (icon == null) return;

            icon.atlas = null;
            icon.spriteName = "";

            UIAtlas atlas = DwellersAtlas();
            if (atlas == null || legend == null)
            {
                ShowMenuIcon(icon, DwellerSprites);
                return;
            }

            string sprite = legend.name;
            if (string.IsNullOrEmpty(sprite) || atlas.GetSprite(sprite) == null)
            {
                ReportOnce("portrait_" + sprite,
                           "The portrait atlas has nothing called '" + sprite + "'.");
                ShowMenuIcon(icon, DwellerSprites);
                return;
            }

            icon.atlas = atlas;
            icon.spriteName = sprite;
            FitSprite(icon, IconBox);
        }

        /// <summary>
        /// Sizes an icon by the whole of what is drawn, then slides it so the picture sits centred.
        ///
        /// Two things were fighting here. In Simple mode NGUI draws the padded rectangle, margins
        /// and all, so the widget has to keep the padded proportions or the picture is stretched —
        /// that was the squashing. But those margins are rarely even, so a widget sized that way
        /// puts the picture off to one side — that was the leaning arrow. Sizing by the padded box
        /// and then moving the widget by however far off-centre the picture sits answers both.
        /// </summary>
        private static void FitInk(UISprite sprite, int box)
        {
            if (sprite == null || sprite.atlas == null) return;

            try
            {
                // A sprite with a border is nine-sliced by default, and slicing stretches its
                // middle to fill whatever it is given.
                sprite.type = UIBasicSprite.Type.Simple;

                UISpriteData data = sprite.atlas.GetSprite(sprite.spriteName);
                if (data == null || data.width <= 0 || data.height <= 0)
                {
                    sprite.width = box;
                    sprite.height = box;
                    return;
                }

                float wide = data.width + data.paddingLeft + data.paddingRight;
                float tall = data.height + data.paddingTop + data.paddingBottom;
                if (wide <= 0f) wide = data.width;
                if (tall <= 0f) tall = data.height;

                float scale = Mathf.Min(box / wide, box / tall);

                sprite.width = Mathf.Max(1, Mathf.RoundToInt(wide * scale));
                sprite.height = Mathf.Max(1, Mathf.RoundToInt(tall * scale));

                // How far the picture's own centre sits from the middle of the padded box.
                float offX = (data.paddingLeft + data.width * 0.5f - wide * 0.5f) * scale;
                float offY = (data.paddingBottom + data.height * 0.5f - tall * 0.5f) * scale;

                Vector3 at = sprite.transform.localPosition;
                sprite.transform.localPosition = new Vector3(at.x - offX, at.y - offY, at.z);
            }
            catch
            {
                sprite.width = box;
                sprite.height = box;
            }
        }

        /// <summary>
        /// Whether a name fails to contain what was typed, judged the same way in every locale.
        ///
        /// ToLower and IndexOf both follow the current culture. On a Turkish system a capital I
        /// lowercases to a dotless i, so typing "i" in the FIND box matched nothing whose name
        /// contained one — which is most of them.
        /// </summary>
        private static bool Missing(string name, string wanted)
        {
            if (string.IsNullOrEmpty(wanted)) return false;
            if (string.IsNullOrEmpty(name)) return true;

            return name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>
        /// Sizes a sprite to fit a square without squashing it.
        ///
        /// A portrait is half as wide as it is tall, and forcing one into a square makes every face
        /// in the list look pressed flat.
        /// </summary>
        private static void FitSprite(UISprite sprite, int box)
        {
            if (sprite == null || sprite.atlas == null) return;

            try
            {
                // Drawn whole, exactly as FitInk does. A sprite with a border is nine-sliced by
                // default and its middle stretched to fill — the squashing that FitInk was written
                // to stop, still happening to every caller that came through here instead.
                sprite.type = UIBasicSprite.Type.Simple;
                UISpriteData data = sprite.atlas.GetSprite(sprite.spriteName);
                if (data == null || data.width <= 0 || data.height <= 0)
                {
                    sprite.width = box;
                    sprite.height = box;
                    return;
                }

                // Trimmed sprites carry their blank margins separately, and a widget is sized to the
                // whole picture rather than the ink in the middle of it. Measuring only the ink is
                // why some came out stretched tall and others stretched wide.
                float full = data.width + data.paddingLeft + data.paddingRight;
                float tall = data.height + data.paddingTop + data.paddingBottom;
                if (full <= 0f) full = data.width;
                if (tall <= 0f) tall = data.height;

                float scale = Mathf.Min(box / full, box / tall);
                sprite.width = Mathf.Max(1, Mathf.RoundToInt(full * scale));
                sprite.height = Mathf.Max(1, Mathf.RoundToInt(tall * scale));
            }
            catch
            {
                sprite.width = box;
                sprite.height = box;
            }
        }

        /// <summary>Puts one of the interface's own icons on a row that has no art of its own.</summary>
        private void ShowMenuIcon(UISprite icon, string[] candidates)
        {
            if (icon == null) return;

            icon.atlas = null;
            icon.spriteName = "";

            // Keyed on what was actually asked for. Hardcoding "dweller" meant a second caller
            // with different candidates would be handed the first caller's answer.
            Found found = FindIcon(candidates,
                                   candidates != null && candidates.Length > 0 ? candidates[0] : "icon");
            if (found == null) return;

            icon.atlas = found.Atlas;
            icon.spriteName = found.Sprite;
            icon.type = UIBasicSprite.Type.Simple;
            FitSprite(icon, IconBox);
        }

        private sealed class Found
        {
            public UIAtlas Atlas;
            public string Sprite;
        }

        private readonly Dictionary<string, Found> _foundIcons = new Dictionary<string, Found>();

        /// <summary>
        /// Hunts one picture through every atlas the game has loaded.
        ///
        /// The interface's own menu atlas holds flat pictograms; the lunchboxes, the robot and the
        /// pet carrier have proper artwork, and it lives elsewhere. Rather than name the atlas —
        /// which would be another guess — every loaded one is asked, the menu's first, and the
        /// answer is written down so the search happens once.
        /// </summary>
        private Found FindIcon(string[] candidates, string what)
        {
            return FindIcon(candidates, what, true, true);
        }

        /// <summary>
        /// Hunts one picture through the game's atlases.
        ///
        /// Two kinds of picture are wanted here and they pull in opposite directions. A resource
        /// wants the interface's own flat pictogram, in the same style as everything around it. A
        /// lunchbox wants the painted article, because that is what a lunchbox looks like and the
        /// outline of a box says nothing. So which to prefer is asked for rather than assumed.
        ///
        /// Fuzziness is also a choice: matching a portrait by words gave Three Dog a picture of a
        /// dog and Star Paladin Cross a picture of a cross.
        /// </summary>
        private Found FindIcon(string[] candidates, string what, bool preferMenu, bool allowFuzzy)
        {
            return FindIcon(candidates, what, preferMenu, allowFuzzy, null);
        }

        /// <summary>
        /// The same, told which atlas the picture ought to come from.
        ///
        /// Several atlases hold a sprite called MrHandy and they are not the same picture: one is
        /// the robot, another is the robot with a boy beside it, a third is a wide banner that
        /// arrives cropped. Naming the atlas is the difference between a lunchbox and an
        /// illustration of someone holding one.
        /// </summary>
        private Found FindIcon(string[] candidates, string what, bool preferMenu, bool allowFuzzy,
                               string preferredAtlas)
        {
            Found known;
            if (_foundIcons.TryGetValue(what, out known)) return known;

            UIAtlas[] atlases = Resources.FindObjectsOfTypeAll<UIAtlas>();
            UIAtlas menu = preferMenu ? MenuAtlas() : null;

            if (!string.IsNullOrEmpty(preferredAtlas))
            {
                for (int i = 0; i < atlases.Length; i++)
                {
                    if (atlases[i] == null || atlases[i].name != preferredAtlas) continue;
                    if (SpritesOf(atlases[i]) == null) continue;

                    for (int c = 0; c < candidates.Length; c++)
                    {
                        if (atlases[i].GetSprite(candidates[c]) == null) continue;

                        Found hit = new Found();
                        hit.Atlas = atlases[i];
                        hit.Sprite = candidates[c];
                        _foundIcons[what] = hit;

                        Log.LogInfo("Icon for " + what + ": '" + candidates[c] +
                                    "' in '" + preferredAtlas + "'.");
                        return hit;
                    }
                }
            }

            // The interface's own atlas is searched out first and in full — exactly, then by
            // words — before anything else is considered. Letting other atlases compete by name
            // order found a lunchbox, but it was the quest screen's illustration of one: a picture
            // of a Vault Boy holding a box where a small green box was wanted.
            for (int pass = 0; pass < 4; pass++)
            {
                bool menuOnly = pass < 2;
                bool exact = (pass % 2) == 0;

                if (menuOnly && menu == null) continue;
                if (!exact && !allowFuzzy) continue;

                for (int i = 0; i < candidates.Length; i++)
                {
                    for (int a = -1; a < atlases.Length; a++)
                    {
                        if (menuOnly && a >= 0) break;

                        UIAtlas atlas = a < 0 ? menu : atlases[a];
                        if (atlas == null || (a >= 0 && atlas == menu)) continue;
                        if (SpritesOf(atlas) == null) continue;

                        string sprite = exact
                            ? (atlas.GetSprite(candidates[i]) != null ? candidates[i] : null)
                            : BestSprite(atlas, candidates[i]);

                        if (string.IsNullOrEmpty(sprite)) continue;

                        Found hit = new Found();
                        hit.Atlas = atlas;
                        hit.Sprite = sprite;
                        _foundIcons[what] = hit;

                        Log.LogInfo("Icon for " + what + ": '" + sprite + "' in '" + atlas.name + "'.");
                        return hit;
                    }
                }
            }

            ReportOnce("icon_" + what,
                       "No atlas holds a picture for " + what + "; tried " +
                       string.Join(", ", candidates) + ".");
            if (menu != null) SuggestSprites(menu, candidates[0]);

            _foundIcons[what] = null;
            return null;
        }

        private void AddIcon(Transform parent, string name, string[] candidates, string what,
                             int x, int y, int size)
        {
            AddIcon(parent, name, candidates, what, x, y, size, true, null, false);
        }

        private void AddIcon(Transform parent, string name, string[] candidates, string what,
                             int x, int y, int size, bool preferMenu)
        {
            AddIcon(parent, name, candidates, what, x, y, size, preferMenu, null, false);
        }

        private void AddIcon(Transform parent, string name, string[] candidates, string what,
                             int x, int y, int size, bool preferMenu, string preferredAtlas)
        {
            AddIcon(parent, name, candidates, what, x, y, size, preferMenu, preferredAtlas, false);
        }

        private void AddIcon(Transform parent, string name, string[] candidates, string what,
                             int x, int y, int size, bool preferMenu, string preferredAtlas,
                             bool tint)
        {
            Found found = FindIcon(candidates, what, preferMenu, true, preferredAtlas);
            if (found == null) return;

            UIAtlas atlas = found.Atlas;
            string sprite = found.Sprite;

            GameObject go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one;

            Plate(parent, name + "_Well", x, y, size + 8, size + 8, Skin.Well(size + 8), 2);

            UISprite drawn = go.AddComponent<UISprite>();
            drawn.atlas = atlas;
            drawn.spriteName = sprite;
            drawn.depth = 4;

            // Only where it was asked for. A resource's picture is a nuka bottle or a drop of
            // water and it is meant to look like one; the powers are pictograms and read better in
            // the panel's own green.
            if (tint) drawn.color = Skin.Bright;

            FitInk(drawn, size);
        }

        /// <summary>
        /// A picture on its own: no recess behind it, in whatever colour it is asked for.
        ///
        /// AddIcon always sets its sprite into a dark recess, which is right on the panel and wrong
        /// on a filled button, where a dark box holding a dark picture is just a dark box.
        /// </summary>
        private void AddBareIcon(Transform parent, string name, string[] candidates, string what,
                                 int x, int y, int size, Color colour)
        {
            Found found = FindIcon(candidates, what, true, true, null);
            if (found == null) return;

            GameObject go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one;

            UISprite drawn = go.AddComponent<UISprite>();
            drawn.atlas = found.Atlas;
            drawn.spriteName = found.Sprite;
            drawn.depth = 7;
            drawn.color = colour;

            FitInk(drawn, size);
        }

        private void BuildTabs(Transform parent)
        {
            Tab[] tabs = { Tab.Resources, Tab.Grant, Tab.Create, Tab.Powers };
            // What each page is, rather than what you do to it. STOCK and GRANT were verbs
            // pretending to be places, and POWERS was a word from a different kind of game: this
            // page holds the switches that override the vault's own rules, so it says so.
            string[] names = { "RESOURCES", "ITEMS", "WORKSHOP", "OVERRIDES" };

            int usable = _windowWidth - Margin * 2;
            int width = (usable - 18) / 4;
            int y = _windowHeight / 2 - 58;
            int x = -usable / 2 + width / 2;

            for (int i = 0; i < tabs.Length; i++)
            {
                Tab captured = tabs[i];
                GameObject tab = MakeButton(parent, "Tab_" + tabs[i], names[i],
                                            x, y, width, 42, false,
                                            delegate { ShowTab(captured); });

                // Sized for the longest of the four rather than left to shrink itself. NGUI will
                // squeeze a word that does not fit, and a row of tabs each squeezed by a different
                // amount is four sizes of text pretending to be one.
                UILabel word = tab.GetComponentInChildren<UILabel>();
                if (word != null)
                {
                    // A tab is a word in a small box, so it gets the small size and nearly all of
                    // the box: at the heading size the longer names simply left through the sides.
                    word.fontSize = TextBody;
                    word.width = width - 6;
                    word.maxLineCount = 1;
                }

                _tabButtons[tabs[i]] = tab;
                x += width + 6;
            }
        }

        private void BuildPages(Transform parent)
        {
            foreach (Tab tab in new[] { Tab.Resources, Tab.Grant, Tab.Create, Tab.Powers })
            {
                GameObject page = new GameObject("Page_" + tab);
                page.layer = parent.gameObject.layer;
                page.transform.SetParent(parent, false);
                page.transform.localPosition = Vector3.zero;
                page.transform.localScale = Vector3.one;
                _tabPages[tab] = page;
            }

            BuildResourcesPage(_tabPages[Tab.Resources].transform);
            BuildGrantPage(_tabPages[Tab.Grant].transform);
            BuildCreatePage(_tabPages[Tab.Create].transform);
            BuildPowersPage(_tabPages[Tab.Powers].transform);
        }

        /// <summary>
        /// Shows one page and hides the rest.
        ///
        /// Pages are built once and switched by activation rather than rebuilt: constructing
        /// widgets on every tab press would make a panel meant to be opened often into a source of
        /// garbage.
        /// </summary>
        private void ShowTab(Tab tab)
        {
            // Reached straight from a button. NGUI's dispatch does not catch, so an
            // exception here abandons the rest of that frame's UI events -- and the spec
            // says a failure in the panel never reaches the game.
            try
            {
                _tab = tab;

                foreach (KeyValuePair<Tab, GameObject> entry in _tabPages)
                {
                    if (entry.Value != null) entry.Value.SetActive(entry.Key == tab);
                }

                // A page that has been hidden comes back where it was left, which for a list is
                // wherever the last search happened to end.
                if (tab == Tab.Grant && _grantScroll != null) _grantScroll.ResetPosition();

                // Made afresh rather than reused. A stand-in kept from last time is wearing what was
                // last applied to it, so coming back to this page showed the dweller already created
                // -- or, when the old one had gone, nothing at all until the gender was changed.
                if (tab == Tab.Create && _making == Making.Dweller && _panelOpen) RemakePreview();
                else DisposePreview();

                RefreshPreview();

                // The chosen tab is solid, the rest outlined — the game's own distinction between an
                // emphasised control and an ordinary one.
                foreach (KeyValuePair<Tab, GameObject> entry in _tabButtons)
                {
                    if (entry.Value == null) continue;

                    UITexture face = entry.Value.GetComponent<UITexture>();
                    UILabel text = entry.Value.GetComponentInChildren<UILabel>();
                    if (face == null) continue;

                    bool active = entry.Key == tab;
                    face.mainTexture = active
                        ? Skin.SolidButton(face.width, face.height)
                        : Skin.Button(face.width, face.height);
                    if (text != null) text.color = active ? Skin.Ink : Skin.Bright;
                }
        }
            catch (Exception e)
            {
                ReportOnce("showtab", "showtab failed: " + e);
            }
        }

        /// <summary>
        /// Lays a page out from the top down.
        ///
        /// The game's windows put content in outlined rows under a solid header, never loose on the
        /// background. Positions are computed once: there is no layout pass here, and recomputing
        /// geometry every frame would cost more than the rest of this mod put together.
        /// </summary>
        private void BuildResourcesPage(Transform parent)
        {
            int contentWidth = _windowWidth - Margin * 2;

            Transform content;
            UIScrollView view = BeginScroll(parent, contentWidth, ContentTop(), ContentBottom(),
                                            out content);

            foreach (EResource resource in Enum.GetValues(typeof(EResource)))
            {
                if (resource == EResource.None || resource == EResource.Count) continue;
                if (Array.IndexOf(NotRealResources, resource) >= 0) continue;
                AddResourceRow(content, resource, contentWidth);
            }

            AddHeader(content, "BOXES", contentWidth);
            foreach (ELunchBoxType type in BoxTypes) AddBoxRow(content, type, contentWidth);

            EndScroll(view, contentWidth);
        }

        // Below the title and the tab bar; above the close button.
        private int ContentTop() { return _windowHeight / 2 - 86; }
        private int ContentBottom() { return -_windowHeight / 2 + 44; }

        // ---- the items and pets page ----

        // A row is built once and rewritten as the list is paged through. Rebuilding widgets to
        // turn a page would make the longest list in the game the most expensive thing here.
        private sealed class ItemRow
        {
            public GameObject Root;
            public UISprite Icon;
            public UILabel Name;
            public UILabel Stats;
            public GameObject Give;

            // A row that has just handed something over says so where its stats were, and puts
            // them back afterwards. Kept here rather than rebuilt, because a row rebuilt to show a
            // word is a row that flickers.
            public string WasStats;
            public Color WasColour;
            public float ConfirmUntil;
            public float PulseUntil;
        }

        private static readonly Family[] Families =
        {
            Family.Weapon, Family.Outfit, Family.Junk, Family.Pet, Family.Dweller
        };

        private Family _grantFamily = Family.Weapon;

        // How many refusals have been reported. A grant says it worked by nothing having gone
        // wrong while it ran, which beats asking four different granting paths to agree on a
        // return value they do not currently have.
        private int _troubles;

        // What the armoury held last time it was counted, and how many weapons this panel had
        // handed over by then. Weapons are appearing in the vault that nobody granted, and neither
        // guessing nor reading the code has found where from -- so the mod counts them, says when
        // they arrive, and says whether it was the one that brought them.
        private int _grantsMade;
        private int _lastWeaponCount = -1;
        private int _grantsAtLastCount;

        private static EItemType ItemTypeOf(Family family)
        {
            switch (family)
            {
                case Family.Outfit: return EItemType.Outfit;
                case Family.Junk:   return EItemType.Junk;
                case Family.Pet:    return EItemType.Pet;
                default:            return EItemType.Weapon;
            }
        }

        private readonly List<ItemRow> _itemRows = new List<ItemRow>();

        // Holds CatalogueEntry for the three item families and PetEntry for pets, so one list and
        // one set of rows serve all four.
        private readonly List<object> _shown = new List<object>();

        private UIInput _filterInput;
        private UIInput _petNameInput;
        private UIInput _petValueInput;
        private UILabel _familyLabel;
        private UILabel _bonusLabel;

        private int _familyIndex;
        private string _appliedFilter = "";

        // Large enough that a portrait reads as a person rather than a smudge.
        private const int IconBox = 52;

        private const int ItemRowHeight = 62;

        private void BuildGrantPage(Transform parent)
        {
            _cursorY = ContentTop();
            int width = _windowWidth - Margin * 2;

            _familyLabel = AddPickerRow(parent, width, "FAMILY",
                                        delegate { StepFamily(-1); }, delegate { StepFamily(1); },
                                        Families[_familyIndex].ToString().ToUpper());

            // Search on the left beside its label, the two orderings on the right. Sorting a list
            // of two hundred by what the items are worth is the difference between browsing and
            // hunting.
            int filterY = _cursorY - RowHeight / 2;
            Plate(parent, "FilterRow", 0, filterY, width, RowHeight, Skin.Row(width, RowHeight), 1);

            MakeLeftLabel(parent, "FilterName", "FIND",
                          -width / 2 + 12, filterY, 62, RowHeight, Skin.Bright, 3);

            // One switch apiece, off then up then down, rather than four buttons of which two are
            // always the wrong ones to press.
            // One switch apiece, off then up then down, and a square beside the stats switch for
            // which stat it means. Everything is held clear of the frame on the right.
            int sortWidth = 76;
            int pickWidth = 32;
            int sortSpan = sortWidth * 2 + pickWidth + 12;
            int fieldWidth = width - 70 - sortSpan - 20;

            _filterInput = AddInput(parent, "Filter", -width / 2 + 70 + fieldWidth / 2, filterY,
                                    fieldWidth, "SEARCH");

            int sortX = width / 2 - 10 - sortSpan + sortWidth / 2;
            _rarityToggle = MakeButton(parent, "SortRarity", "RARITY", sortX, filterY,
                                       sortWidth, 32, false,
                                       delegate { CycleOrdering(Ordering.Rarity); });

            sortX += sortWidth + 6;
            _powerToggle = MakeButton(parent, "SortPower", "STATS", sortX, filterY,
                                      sortWidth, 32, false,
                                      delegate { CycleOrdering(Ordering.Power); });

            sortX += sortWidth / 2 + pickWidth / 2 + 6;
            _statPickButton = MakeButton(parent, "StatPick", "*", sortX, filterY,
                                         pickWidth, 32, false, CycleStatPick);

            _cursorY -= RowHeight + RowGap;

            // The whole list in one scrolling column. Paging through two hundred items four at a
            // time was a way of saying the panel could not hold them.
            _grantWidth = width;
            _grantScroll = BeginScroll(parent, width, _cursorY, ContentBottom(), out _grantContent);
            _grantTop = _scrollTop;
        }

        private enum Ordering { None, Rarity, Power }

        private UIScrollView _grantScroll;
        private Transform _grantContent;
        private GameObject _grantDragArea;
        private int _grantWidth;
        private int _grantTop;

        private Ordering _ordering = Ordering.None;
        private bool _orderingUp = true;

        private const int MaxGrantRows = 260;

        private GameObject _rarityToggle;
        private GameObject _powerToggle;
        private GameObject _statPickButton;

        // -1 means all seven together; otherwise an index into Specials.
        private int _statPick = -1;

        private void CycleStatPick()
        {
            _statPick++;
            if (_statPick >= Specials.Length) _statPick = -1;

            if (_statPick >= 0 && _ordering != Ordering.Power)
            {
                _ordering = Ordering.Power;
                _orderingUp = false;      // the strongest first, which is what a stat is asked about
            }

            RefreshThings();
        }

        /// <summary>Off, then up, then down, then off again.</summary>
        private void CycleOrdering(Ordering which)
        {
            if (_ordering != which) { _ordering = which; _orderingUp = true; }
            else if (_orderingUp) { _orderingUp = false; }
            else { _ordering = Ordering.None; }

            RefreshThings();
        }

        /// <summary>
        /// Shows each switch only where it means something.
        ///
        /// Junk has no stats to sort by — its rating is what it sells for, which says nothing about
        /// one lump of scrap against another — and neither pets nor dwellers are graded at all.
        /// </summary>
        private void UpdateOrderingSwitches()
        {
            // Everything here has a rarity, dwellers included. Only junk has nothing worth calling
            // a stat — its rating is what it sells for, which says nothing about one lump of scrap
            // against another.
            bool hasRarity = true;
            bool hasPower = _grantFamily != Family.Junk && _grantFamily != Family.Dweller;
            bool hasPick = _grantFamily == Family.Outfit;

            if (_rarityToggle != null) _rarityToggle.SetActive(hasRarity);
            if (_powerToggle != null) _powerToggle.SetActive(hasPower);
            if (_statPickButton != null) _statPickButton.SetActive(hasPick);

            if (_ordering == Ordering.Power && !hasPower) _ordering = Ordering.None;
            if (!hasPick) _statPick = -1;

            Label(_rarityToggle, "RARITY", Ordering.Rarity);
            Label(_powerToggle, "STATS", Ordering.Power);

            if (_statPickButton != null)
            {
                UILabel pick = _statPickButton.GetComponentInChildren<UILabel>();
                if (pick != null)
                    pick.text = _statPick < 0
                        ? "*"
                        : Specials[_statPick].ToString().Substring(0, 1);
            }
        }

        private void Label(GameObject button, string caption, Ordering which)
        {
            if (button == null) return;

            UILabel text = button.GetComponentInChildren<UILabel>();
            if (text == null) return;

            if (_ordering != which) text.text = caption;
            else text.text = caption + (_orderingUp ? " ^" : " v");
        }

        /// <summary>
        /// Builds as many rows as the list needs, once.
        ///
        /// Rows are kept and reused rather than destroyed with each search: a family change would
        /// otherwise throw away two hundred sets of widgets and build two hundred more.
        /// </summary>
        private void EnsureRows(int wanted)
        {
            wanted = Mathf.Min(wanted, MaxGrantRows);

            while (_itemRows.Count < wanted)
            {
                int i = _itemRows.Count;
                _itemRows.Add(BuildItemRow(_grantContent, i, _grantWidth,
                                           _grantTop - ItemRowHeight / 2 -
                                           i * (ItemRowHeight + RowGap)));
            }
        }

        /// <summary>Sizes the area the list is dragged by, and lets the new rows be dragged too.</summary>
        private void UpdateGrantArea(int rows)
        {
            if (_grantScroll == null || _grantContent == null) return;

            int used = Mathf.Max(40, rows * (ItemRowHeight + RowGap));

            if (_grantDragArea == null)
            {
                _grantDragArea = new GameObject("DragArea");
                _grantDragArea.layer = _grantContent.gameObject.layer;
                _grantDragArea.transform.SetParent(_grantContent, false);
                _grantDragArea.transform.localScale = Vector3.one;
                _grantDragArea.AddComponent<BoxCollider>().isTrigger = true;
            }

            // Behind the rows, so it catches the empty space without taking their presses.
            _grantDragArea.transform.localPosition = new Vector3(0f, _grantTop - used / 2, 20f);
            _grantDragArea.GetComponent<BoxCollider>().size = new Vector3(_grantWidth, used, 1f);

            BoxCollider[] colliders = _grantScroll.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].GetComponent<UIDragScrollView>() != null) continue;
                UIDragScrollView drag = colliders[i].gameObject.AddComponent<UIDragScrollView>();
                drag.scrollView = _grantScroll;
            }

            _grantScroll.ResetPosition();
        }

        private ItemRow BuildItemRow(Transform parent, int index, int width, int y)
        {
            ItemRow row = new ItemRow();

            row.Root = new GameObject("ItemRow" + index);
            row.Root.layer = parent.gameObject.layer;
            row.Root.transform.SetParent(parent, false);
            row.Root.transform.localPosition = new Vector3(0f, y, 0f);
            row.Root.transform.localScale = Vector3.one;

            Plate(row.Root.transform, "Plate", 0, 0, width, ItemRowHeight,
                  Skin.Row(width, ItemRowHeight), 1);

            // The game's own art, out of the game's own atlas: a UISprite needs nothing but the
            // atlas and the sprite's name, which the catalogue already carries.
            GameObject iconGo = new GameObject("Icon");
            iconGo.layer = parent.gameObject.layer;
            iconGo.transform.SetParent(row.Root.transform, false);
            iconGo.transform.localPosition = new Vector3(-width / 2 + 32, 0f, 0f);
            iconGo.transform.localScale = Vector3.one;

            Plate(row.Root.transform, "Well", -width / 2 + 32, 0, IconBox + 6, IconBox + 6,
                  Skin.Well(IconBox + 6), 2);

            row.Icon = iconGo.AddComponent<UISprite>();
            row.Icon.width = IconBox;
            row.Icon.height = IconBox;
            row.Icon.depth = 4;

            // The same margin the powers use: the picture's recess ends, and then there is air
            // before anything is said.
            int textLeft = -width / 2 + 76;
            int textWidth = width - 176;

            row.Name = MakeLeftLabel(row.Root.transform, "Name", "",
                                     textLeft, 11, textWidth, 24, Skin.Bright, 3);

            // The figures beneath the name, quieter than it: what the item does and what it is
            // worth is the reason to pick one item out of two hundred.
            row.Stats = MakeLeftLabel(row.Root.transform, "Stats", "",
                                      textLeft, -12, textWidth, 20, Skin.Bright, 3);
            row.Stats.fontSize = TextNote;
            row.Stats.color = new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.9f);

            int captured = index;
            row.Give = MakeButton(row.Root.transform, "Give", "GIVE", width / 2 - 44, 0, 76, 34,
                                  false, delegate { GiveRow(captured); });

            return row;
        }

        /// <summary>
        /// What a named dweller is called.
        ///
        /// Not "Name" — that member does not exist here, and asking for it returned nothing, which
        /// is why the dweller family listed no one at all. The data calls it DwellerFullName.
        /// </summary>
        private static string LegendName(UniqueDwellerData data)
        {
            if (data == null) return null;

            string full = ReadMember(data, "DwellerFullName");
            if (!string.IsNullOrEmpty(full)) return full;

            string first = ReadMember(data, "DwellerName");
            string last = ReadMember(data, "DwellerLastName");
            full = ((first ?? "") + " " + (last ?? "")).Trim();

            return full.Length > 0 ? full : null;
        }

        private void StepFamily(int by)
        {
            _familyIndex = (_familyIndex + by + Families.Length) % Families.Length;
            _grantFamily = Families[_familyIndex];
            _family = ItemTypeOf(_grantFamily);
            if (_familyLabel != null) _familyLabel.text = _grantFamily.ToString().ToUpper();
            RefreshThings();
        }

        private void StepBonus(int by)
        {
            _petBonusIndex = (_petBonusIndex + by + Bonuses().Length) % Bonuses().Length;
            if (_bonusLabel != null) _bonusLabel.text = Tidy(Bonuses()[_petBonusIndex].ToString()).ToUpper();
        }

        /// <summary>Rereads the catalogue for the chosen family and puts the list back to its top.</summary>
        private void RefreshThings()
        {
            // Reached straight from a button. NGUI's dispatch does not catch, so an
            // exception here abandons the rest of that frame's UI events -- and the spec
            // says a failure in the panel never reaches the game.
            try
            {
                _shown.Clear();

                string filter = _filter == null ? "" : _filter.Trim().ToLowerInvariant();

                if (_grantFamily == Family.Dweller)
                {
                    // The named dwellers the game ships: each brings its own look, stats and story, so
                    // this list hands them over whole rather than offering to edit them.
                    // Ordinary newcomers first: most of the time what is wanted is a body at the door,
                    // not one of the fifty-odd people the game has written.
                    for (int i = 0; i < Rarities.Length; i++)
                    {
                        string label = "Random " + Rarities[i] + " dweller";
                        if (filter.Length > 0 && Missing(label, filter)) continue;

                        RandomDweller random = new RandomDweller();
                        random.Rarity = Rarities[i];
                        _shown.Add(random);
                    }

                    DwellerManager manager = SafeDwellerManager();
                    UniqueDwellerData[] legends = manager != null ? manager.LegendaryDwellers : null;

                    if (legends != null)
                    {
                        for (int i = 0; i < legends.Length; i++)
                        {
                            if (legends[i] == null) continue;

                            string label = LegendName(legends[i]);
                            if (string.IsNullOrEmpty(label)) continue;
                            if (filter.Length > 0 && Missing(label, filter)) continue;

                            _shown.Add(legends[i]);
                        }
                    }
                }
                else if (_grantFamily == Family.Pet)
                {
                    PreloadPetArt();
                    if (_pets == null) BuildPetCatalogue();
                    if (_petGroups == null) GroupPets();

                    if (_pets != null)
                    {
                        // Every record, not every animal. Folding the hundred and thirty records into
                        // ninety-nine animals is the right thing for the constructor, where you pick a
                        // creature and then say what grade of it you want. Handing one over is the
                        // other question: a common mole rat and a legendary one are two different
                        // things to be given, and the list that gives them should say so.
                        for (int i = 0; i < _pets.Count; i++)
                        {
                            if (filter.Length > 0 && Missing(_pets[i].Name, filter)) continue;
                            _shown.Add(_pets[i]);
                        }
                    }
                }
                else
                {
                    if (_catalogue == null) BuildCatalogue();
                    if (_catalogue != null)
                    {
                        for (int i = 0; i < _catalogue.Count; i++)
                        {
                            CatalogueEntry entry = _catalogue[i];
                            if (entry.Type != _family) continue;
                            if (filter.Length > 0 && Missing(entry.Name, filter)) continue;
                            _shown.Add(entry);
                        }
                    }
                }

                UpdateOrderingSwitches();
                if (_ordering != Ordering.None) _shown.Sort(CompareShown);

                EnsureRows(_shown.Count);
                ClearConfirmations();
                FillRows();
                UpdateGrantArea(Mathf.Min(_shown.Count, MaxGrantRows));
        }
            catch (Exception e)
            {
                ReportOnce("refresh", "refresh failed: " + e);
            }
        }

        /// <summary>
        /// Orders the list by rarity or by what an item does.
        ///
        /// Everything that is not an item — a rolled dweller, one of the named ones — has neither,
        /// and sorts to one end rather than being scattered through the middle.
        /// </summary>
        private int CompareShown(object left, object right)
        {
            int a = SortKey(left);
            int b = SortKey(right);
            return _orderingUp ? a.CompareTo(b) : b.CompareTo(a);
        }

        private int SortKey(object thing)
        {
            CatalogueEntry item = thing as CatalogueEntry;
            if (item != null)
            {
                if (_ordering == Ordering.Rarity) return (int)item.Rarity;

                // One stat at a time when one has been picked out: an outfit worth having for its
                // Luck is not the outfit with the largest total.
                if (_statPick >= 0 && item.Stats7 != null) return item.Stats7[_statPick];
                return item.Power;
            }

            // A record, which is what the grant list now holds. Without this every pet sorted to
            // the same key and the RARITY and STATS buttons sat there doing nothing at all.
            PetEntry pet = thing as PetEntry;
            if (pet != null)
                return _ordering == Ordering.Rarity ? (int)pet.Rarity : pet.Power;

            PetGroup group = thing as PetGroup;
            if (group != null)
            {
                PetEntry best = group.Best;
                if (best == null) return -1;
                return _ordering == Ordering.Rarity ? (int)best.Rarity : best.Power;
            }

            RandomDweller random = thing as RandomDweller;
            if (random != null) return (int)random.Rarity;

            if (thing is UniqueDwellerData) return (int)EDwellerRarity.Legendary;

            return -1;
        }

        /// <summary>
        /// Takes down any confirmation still showing.
        ///
        /// Called when the list itself changes rather than when it is merely refilled: GIVEN over
        /// a row that now holds a different item is a confirmation of the wrong thing.
        /// </summary>
        private void ClearConfirmations()
        {
            for (int i = 0; i < _itemRows.Count; i++)
            {
                ItemRow row = _itemRows[i];
                if (row == null) continue;

                if (row.ConfirmUntil > 0f && row.Stats != null)
                {
                    row.Stats.text = row.WasStats == null ? "" : row.WasStats;
                    row.Stats.color = row.WasColour;
                }

                row.ConfirmUntil = 0f;

                if (row.PulseUntil > 0f && row.Give != null)
                    row.Give.transform.localScale = Vector3.one;

                row.PulseUntil = 0f;
            }
        }

        /// <summary>Writes this page of the list into rows that already exist.</summary>
        private void FillRows()
        {
            for (int i = 0; i < _itemRows.Count; i++)
            {
                ItemRow row = _itemRows[i];
                if (row == null || row.Root == null) continue;

                bool used = i < _shown.Count;
                row.Root.SetActive(used);
                if (!used) continue;

                object thing = _shown[i];

                CatalogueEntry item = thing as CatalogueEntry;
                if (item != null)
                {
                    row.Name.text = item.Name;
                    SetStats(row, item.Stats);
                    ShowIcon(row.Icon, item);
                    continue;
                }

                RandomDweller random = thing as RandomDweller;
                if (random != null)
                {
                    row.Name.text = "Random " + random.Rarity + " dweller";
                    SetStats(row, "RANDOM  a " + random.Rarity.ToString().ToLower() +
                                  " newcomer, rolled by the game");
                    ShowMenuIcon(row.Icon, DwellerSprites);
                    continue;
                }

                UniqueDwellerData legend = thing as UniqueDwellerData;
                if (legend != null)
                {
                    row.Name.text = LegendName(legend);
                    SetStats(row, "LEGENDARY  brings its own look and stats");
                    ShowLegendIcon(row.Icon, legend);
                    continue;
                }

                PetGroup group = thing as PetGroup;
                if (group != null)
                {
                    PetEntry best = group.Best;

                    row.Name.text = group.Name;
                    SetStats(row, PetStats(best) +
                                  (group.Variants.Count > 1
                                       ? "   " + group.Variants.Count + " grades"
                                       : ""));
                    ShowPetIcon(row.Icon, best);
                    continue;
                }

                {
                    PetEntry pet = (PetEntry)thing;

                    row.Name.text = pet.Name;
                    SetStats(row, RarityWord(pet.Rarity) + "   " + PetStats(pet));
                    ShowPetIcon(row.Icon, pet);
                }
            }

            // The count belongs beside the family it counts, not on a pager that no longer exists.
            // Says how many are actually on screen when the list is longer than the panel will
            // build. It used to print the full count above a list cut to 260 and let the player
            // work out the difference.
            if (_familyLabel != null)
            {
                string tally;

                if (_shown.Count <= 0) tally = "  NONE";
                else if (_shown.Count > MaxGrantRows) tally = "  " + MaxGrantRows + " OF " + _shown.Count;
                else tally = "  " + _shown.Count;

                _familyLabel.text = _grantFamily.ToString().ToUpper() + tally;
            }
        }

        // Pet art is not simply present the way item atlases are: it is loaded per type, on
        // request, and asynchronously. Asking is what a granted pet needed to have a picture at all,
        // and it is what the list needs to show one.
        private readonly Dictionary<string, UIAtlas> _petAtlases = new Dictionary<string, UIAtlas>();
        private bool _petArtPending;

        private UIAtlas PetAtlasFor(object petType)
        {
            if (petType == null) return null;

            string key = petType.ToString();
            UIAtlas known;
            if (_petAtlases.TryGetValue(key, out known) && known != null) return known;

            try
            {
                PetAtlasManager manager = PetAtlasManager.Instance;
                if (manager == null) return null;

                Array infos = ReadObject(manager, "m_atlases") as Array;
                if (infos == null) return null;

                for (int i = 0; i < infos.Length; i++)
                {
                    object info = infos.GetValue(i);
                    if (info == null) continue;

                    object type = ReadObject(info, "PetType");
                    if (type == null || type.ToString() != key) continue;

                    UIAtlas atlas = ReadObject(info, "Atlas") as UIAtlas;

                    // An atlas with no sprites in it has not finished loading. Caching one is how
                    // a pet ends up permanently without a picture: the load lands a moment later
                    // and nothing ever asks again.
                    if (atlas != null && SpritesOf(atlas) != null)
                    {
                        _petAtlases[key] = atlas;
                        return atlas;
                    }

                    // And an empty one still has to be asked for. Tightening the caching without
                    // keeping the request is how every pet but the cats — whose art the game had
                    // already loaded for itself — stayed blank.
                    object loading = ReadObject(info, "IsLoading");
                    if (loading == null || !(bool)loading) RequestPetType(petType);

                    _petArtPending = true;
                    return null;
                }
            }
            catch (Exception e)
            {
                ReportOnce("petatlas", "Could not reach the pet atlases: " + e.Message);
            }

            return null;
        }

        private bool _petArtAsked;

        /// <summary>
        /// Asks for every kind of pet's art at once.
        ///
        /// Requesting each type only when a row needing it happens to be drawn meant the pictures
        /// arrived a page at a time, in whatever order the list was read in. There are six kinds
        /// altogether; asking for all six the first time the list is opened costs one round of
        /// loading and gets them all.
        /// </summary>
        private void PreloadPetArt()
        {
            if (_petArtAsked) return;
            _petArtAsked = true;

            try
            {
                foreach (object type in Enum.GetValues(typeof(EPetType)))
                    RequestPetType(type);

                Log.LogInfo("Asked the game for every kind of pet's art.");
                _petArtPending = true;
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not ask for the pet art: " + e.Message);
            }
        }

        private void RequestPetType(object petType)
        {
            try
            {
                PetAtlasManager manager = PetAtlasManager.Instance;
                if (manager == null) return;

                MethodInfo load = typeof(PetAtlasManager).GetMethod(
                    "LoadAtlases",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { petType.GetType() }, null);

                if (load != null) load.Invoke(manager, new[] { petType });
            }
            catch (Exception e)
            {
                ReportOnce("petload", "Could not ask for pet art: " + e.Message);
            }
        }

        /// <summary>
        /// A bonus written the way the game writes it, value and all.
        ///
        /// LocalizedBonusWithValue does not append the value despite its name — it returns the whole
        /// sentence with a hole in it, and the inventory screen fills that hole with the range. The
        /// per cent sign, the plus, and the wording all live inside that sentence, which is why
        /// building the line by hand produced a number with no unit on it.
        /// </summary>
        private string BonusText(object effect, string amount)
        {
            string key = effect.ToString();

            string pattern;
            if (!_bonusWords.TryGetValue(key, out pattern))
            {
                pattern = null;

                try
                {
                    PetUniqueData spare = new PetUniqueData();
                    spare.Bonus = (EBonusEffect)effect;

                    pattern = CallText(spare, "LocalizedBonusWithValue");
                    if (!string.IsNullOrEmpty(pattern) && pattern.IndexOf("{0}") < 0) pattern = null;
                    if (!string.IsNullOrEmpty(pattern) && pattern.StartsWith("Bonus_")) pattern = null;
                }
                catch (Exception e)
                {
                    ReportOnce("bonuswords", "Could not read the bonus wording: " + e.Message);
                }

                if (string.IsNullOrEmpty(pattern))
                    pattern = string.Join(" ", SplitWords(key)) + " +{0}";

                _bonusWords[key] = pattern;
            }

            try
            {
                return string.Format(pattern, amount);
            }
            catch
            {
                return string.Join(" ", SplitWords(key)) + " +" + amount;
            }
        }

        private readonly Dictionary<string, string> _bonusWords = new Dictionary<string, string>();

        /// <summary>
        /// A number with as many places as it takes to not be zero.
        ///
        /// Several of these bonuses are small fractions, and one decimal place rendered every one of
        /// them as +0.
        /// </summary>
        private static string Figure(float value)
        {
            if (value == 0f) return "0";
            if (Mathf.Abs(value) >= 1f) return value.ToString("0.##");
            if (Mathf.Abs(value) >= 0.01f) return value.ToString("0.##");
            return value.ToString("0.####");
        }

        /// <summary>
        /// A number as someone typed it.
        ///
        /// A decimal point here is written with a comma, and a player typing 0,5 means the same
        /// thing as one typing 0.5. Accepting only one of the two is a trap set by the machine's
        /// settings rather than by anything the player did.
        /// </summary>
        private static bool TypedNumber(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(text)) return false;

            const System.Globalization.NumberStyles Style = System.Globalization.NumberStyles.Float;

            if (float.TryParse(text, Style,
                               System.Globalization.CultureInfo.InvariantCulture, out value))
                return true;

            return float.TryParse(text, Style,
                                  System.Globalization.CultureInfo.CurrentCulture, out value);
        }

        /// <summary>The best a pet's bonus reaches, as one number.</summary>
        private int PetPower(object template)
        {
            try
            {
                Array bonuses = ReadObject(template, "BonusEffectList") as Array;
                if (bonuses == null) return 0;

                float best = 0f;
                for (int i = 0; i < bonuses.Length; i++)
                {
                    object bonus = bonuses.GetValue(i);
                    if (bonus == null) continue;

                    float high = ReadFloat(bonus, "MaxValue");
                    if (high > best) best = high;
                }
                return Mathf.RoundToInt(best);
            }
            catch
            {
                return 0;
            }
        }

        private string PetStats(PetEntry pet)
        {
            try
            {
                Array bonuses = ReadObject(pet.Template, "BonusEffectList") as Array;
                if (bonuses == null || bonuses.Length == 0) return pet.Detail;

                string line = "";

                for (int i = 0; i < bonuses.Length; i++)
                {
                    object bonus = bonuses.GetValue(i);
                    if (bonus == null) continue;

                    object effect = ReadObject(bonus, "Effect");
                    if (effect == null || effect.ToString() == "None") continue;

                    float low = ReadFloat(bonus, "MinValue");
                    float high = ReadFloat(bonus, "MaxValue");

                    string amount = high > low
                        ? Figure(low) + "-" + Figure(high)
                        : Figure(low);

                    if (line.Length > 0) line += "   ";
                    line += BonusText(effect, amount);
                }

                string rarity = pet.Rarity.ToString().ToUpper();
                return rarity + "  " + (line.Length > 0 ? line : pet.Detail);
            }
            catch
            {
                return pet.Detail;
            }
        }

        /// <summary>
        /// A pet's picture, or nothing with a reason written down.
        ///
        /// Two different things can go wrong here — the type's atlas may not have loaded yet, or the
        /// name on the record may not be in it — and they need different fixes, so they are logged
        /// apart rather than both leaving the same blank square.
        /// </summary>
        private void ShowPetIcon(UISprite icon, PetEntry pet)
        {
            if (icon == null || pet == null) return;

            icon.atlas = null;
            icon.spriteName = "";

            UIAtlas atlas = PetAtlasFor(pet.PetType);
            if (atlas == null) return;

            string sprite = ReadMember(pet.Template, "Sprite");
            string head = ReadMember(pet.Template, "HeadSprite");

            string chosen = null;

            // The whole animal where there is room for one. The atlas carries a full body beside
            // every head -- Abyssinian_FullBody next to Abyssinian_Head -- and a row in a list has
            // room for a head while a bench has room for a cat.
            if (_wantWholeAnimal)
            {
                chosen = BestSprite(atlas, Whole(head));
                if (string.IsNullOrEmpty(chosen)) chosen = BestSprite(atlas, Whole(sprite));
            }

            if (string.IsNullOrEmpty(chosen)) chosen = BestSprite(atlas, head);
            if (string.IsNullOrEmpty(chosen)) chosen = BestSprite(atlas, sprite);

            if (chosen == null)
            {
                SuggestSprites(atlas, string.IsNullOrEmpty(sprite) ? pet.Name : sprite);
                ReportOnce("petsprite_" + pet.PetId,
                           "Atlas '" + atlas.name + "' has no picture for " + pet.Name +
                           " (tried '" + sprite + "' and '" + head + "').");
                return;
            }

            icon.atlas = atlas;
            icon.spriteName = chosen;
            icon.type = UIBasicSprite.Type.Simple;
            FitSprite(icon, IconBox);
        }

        private void ShowIcon(UISprite icon, CatalogueEntry entry)
        {
            UIAtlas atlas;
            if (string.IsNullOrEmpty(entry.Sprite) ||
                !_atlases.TryGetValue(entry.Type, out atlas) || atlas == null)
            {
                icon.atlas = null;
                icon.spriteName = "";
                return;
            }

            string chosen = BestSprite(atlas, entry.Sprite);

            // Ten of the outfits name no art at all. Their pictures are in the atlas under the
            // name the item goes by, so that is what is asked for when the record says nothing.
            if (string.IsNullOrEmpty(chosen)) chosen = BestSprite(atlas, entry.Name);

            if (string.IsNullOrEmpty(chosen))
            {
                ReportOnce("itemsprite_" + entry.Type,
                           "Atlas '" + atlas.name + "' has no picture for " + entry.Name +
                           " ('" + entry.Sprite + "'); other " + entry.Type + " rows may be blank too.");
                SuggestSprites(atlas, entry.Sprite);
                icon.atlas = null;
                icon.spriteName = "";
                return;
            }

            icon.atlas = atlas;
            icon.spriteName = chosen;
            icon.type = UIBasicSprite.Type.Simple;
            FitSprite(icon, _wantWholeAnimal ? _wholeAnimalBox : IconBox);
        }

        /// <summary>The full-body name that stands beside a head in the atlas.</summary>
        private static string Whole(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            int at = name.IndexOf("_Head", StringComparison.OrdinalIgnoreCase);
            return at < 0 ? name + "_FullBody" : name.Substring(0, at) + "_FullBody";
        }

        private bool _wantWholeAnimal;
        private int _wholeAnimalBox = 168;

        /// <summary>Grants whatever sits in this row, through the same paths the panel already uses.</summary>
        private void GiveRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _shown.Count) return;

            int before = _troubles;
            HandOver(rowIndex);
            if (_troubles == before) ConfirmRow(rowIndex);
        }

        /// <summary>
        /// Says on the row itself that the thing was handed over.
        ///
        /// A toast at the edge of the screen answers the question "did anything happen", but not
        /// "did anything happen to the row I pressed", and with two hundred rows that is the
        /// question being asked. So the row's own stats line says GIVEN for a moment and the
        /// button swells under the finger, and then both go back to what they were.
        /// </summary>
        private void ConfirmRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _itemRows.Count) return;

            ItemRow row = _itemRows[rowIndex];
            if (row == null || row.Root == null) return;

            if (row.Stats != null)
            {
                // Only the first press records what was there; a second press while the word is
                // still up must not remember the word as the thing to go back to.
                if (row.ConfirmUntil <= 0f)
                {
                    row.WasStats = row.Stats.text;
                    row.WasColour = row.Stats.color;
                }

                row.Stats.text = "GIVEN";
                row.Stats.color = Skin.Bright;
            }

            row.ConfirmUntil = Time.time + 1.4f;
            row.PulseUntil = Time.time + PulseFor;
        }

        private const float PulseFor = 0.28f;

        /// <summary>
        /// Writes a row's stats line, unless it is busy saying something happened.
        ///
        /// A list refilled while a confirmation is up would either wipe the word or, worse, leave
        /// the row to put back the word instead of the stats when the moment passed.
        /// </summary>
        private static void SetStats(ItemRow row, string what)
        {
            if (row.ConfirmUntil > 0f) { row.WasStats = what; return; }
            if (row.Stats != null) row.Stats.text = what;
        }

        /// <summary>Lets the swell fall and the stats come back.</summary>
        private void TickConfirmations()
        {
            float now = Time.time;

            for (int i = 0; i < _itemRows.Count; i++)
            {
                ItemRow row = _itemRows[i];
                if (row == null || row.Root == null) continue;

                if (row.PulseUntil > 0f && row.Give != null)
                {
                    if (now >= row.PulseUntil)
                    {
                        row.PulseUntil = 0f;
                        row.Give.transform.localScale = Vector3.one;
                    }
                    else
                    {
                        // One rise and one fall, rather than a jump and a snap back.
                        float left = (row.PulseUntil - now) / PulseFor;
                        float swell = Mathf.Sin(left * Mathf.PI);
                        row.Give.transform.localScale = Vector3.one * (1f + 0.16f * swell);
                    }
                }

                if (row.ConfirmUntil > 0f && now >= row.ConfirmUntil)
                {
                    row.ConfirmUntil = 0f;

                    if (row.Stats != null)
                    {
                        row.Stats.text = row.WasStats == null ? "" : row.WasStats;
                        row.Stats.color = row.WasColour;
                    }
                }
            }
        }

        private void HandOver(int rowIndex)
        {
            object thing = _shown[rowIndex];

            CatalogueEntry item = thing as CatalogueEntry;
            if (item != null) { GrantItem(item); return; }

            RandomDweller random = thing as RandomDweller;
            if (random != null)
            {
                // Borrowed for one dweller and handed straight back. Kept, granting a legendary
                // from this list quietly re-set the bench's own rarity, and the bench went on
                // showing whatever it showed before.
                int was = _rarityIndex;

                _rarityIndex = Array.IndexOf(Rarities, random.Rarity);
                if (_rarityIndex < 0) _rarityIndex = 0;

                CreateDweller(false);

                _rarityIndex = was;
                return;
            }

            UniqueDwellerData legend = thing as UniqueDwellerData;
            if (legend != null)
            {
                CreateLegendary(legend, LegendName(legend));
                return;
            }

            // Handed over as the game rolled it, and which grade of the animal is part of that.
            // Naming one and choosing its bonus is what the create tab is for.
            // The exact record the row names. Rolling a grade at random was a consequence of the
            // list showing animals rather than records, and it meant pressing GIVE on a row that
            // said LEGENDARY could hand over a common one.
            PetEntry pet = thing as PetEntry;
            if (pet != null) { GrantPet(pet, false); return; }

            PetGroup group = thing as PetGroup;
            if (group == null || group.Variants.Count == 0) return;

            GrantPet(group.Variants[UnityEngine.Random.Range(0, group.Variants.Count)], false);
        }

        private UIInput _firstNameInput;
        private UIInput _lastNameInput;
        private UILabel _rarityLabel;
        private UIInput _levelInput;
        private readonly UIInput[] _specialInputs = new UIInput[7];
        private int _dwellerLevelValue = 1;

        /// <summary>
        /// One row that picks from a list.
        ///
        /// The lists behind these change — the hair a woman can wear is not the hair a man can —
        /// so the row is built once and its contents replaced, rather than the row being rebuilt
        /// every time the gender is stepped.
        /// </summary>
        private sealed class Choice
        {
            public string Caption;
            public UILabel Title;      // the parameter's name, and how far through its list you are
            public UILabel Display;
            public UILabel Detail;     // what the thing chosen actually does
            public UILabel Grade;      // and how rare it is, on a line of its own

            // Some choices are better looked at than read: a colour is a colour, and an outfit is
            // the picture of it.
            public UITexture Swatch;
            public UISprite Picture;
            public Action OnChange;

            // Some options are colours without being one: a record that stands for a shade. This
            // asks whoever filled the list what colour an option means.
            public Func<object, Color?> SwatchOf;
            public bool SwatchIsTheAnswer;
            public readonly List<object> Options = new List<object>();
            public readonly List<string> Labels = new List<string>();
            public int Index;

            // Whether the first entry stands for nothing. Gear can honestly be absent; an
            // appearance cannot, so its lists have no such entry and count from one.
            public bool HasNone = true;

            public object Selected
            {
                // From nought, not from one. A list that begins with a real value has a real
                // value at nought, and the lists that still begin with "none" keep a null there,
                // so both answer correctly to the same question.
                get { return Index >= 0 && Index < Options.Count ? Options[Index] : null; }
            }

            public void Step(int by)
            {
                if (Options.Count == 0) return;

                Index = (Index + by + Options.Count) % Options.Count;
                Show();
            }

            public void Show()
            {
                if (Display != null)
                    Display.text = Index >= 0 && Index < Labels.Count ? Labels[Index] : "-";

                CatalogueEntry chosenItem = Selected as CatalogueEntry;

                if (Detail != null)
                    Detail.text = chosenItem == null || chosenItem.Effect == null
                        ? ""
                        : chosenItem.Effect;

                if (Grade != null)
                    Grade.text = chosenItem == null
                        ? ""
                        : chosenItem.Rarity.ToString().ToUpper();

                // The count belongs beside the thing being counted — the name of the parameter —
                // not tacked onto whichever value happens to be showing. Counted from nought,
                // because the first entry is a real choice, not the absence of one.
                if (Title != null)
                {
                    // A list with an empty first place counts its real entries and calls the
                    // empty one nought; a list without one counts from one, because its first
                    // entry is as real as its last.
                    int total = HasNone ? Options.Count - 1 : Options.Count;
                    int at = HasNone ? Index : Index + 1;

                    Title.text = total > 0 ? Caption + "   " + at + "/" + total : Caption;
                }

                object chosen = Selected;

                Color? shade = chosen is Color ? (Color)chosen : (Color?)null;
                if (shade == null && SwatchOf != null && chosen != null) shade = SwatchOf(chosen);

                if (Swatch != null)
                {
                    Swatch.gameObject.SetActive(shade != null);
                    if (shade != null) Swatch.color = shade.Value;
                }

                // Where a swatch says everything, the words are in the way — except when there is
                // no colour to show, and then the words are all there is.
                if (Display != null && SwatchIsTheAnswer)
                    Display.gameObject.SetActive(shade == null);

                if (OnChange != null) OnChange();
            }

            /// <summary>Starts the list again, with a first entry that stands for nothing.</summary>
            public void Begin(string anything)
            {
                Options.Clear();
                Labels.Clear();
                Options.Add(null);
                Labels.Add(anything);
                Index = 0;
                HasNone = true;
            }

            /// <summary>
            /// Starts the list again with nothing in front of it.
            ///
            /// An appearance has no empty option. "Random" was one for a long time, and it was not
            /// a value at all -- it meant "write nothing", so the dweller kept whatever the game
            /// had rolled for it, which was never what the figure on the bench was showing. There
            /// is no way to leave a hair colour unset, so the list does not offer one.
            /// </summary>
            public void BeginBare()
            {
                Options.Clear();
                Labels.Clear();
                Index = 0;
                HasNone = false;
            }

            public void Add(object option, string label)
            {
                Options.Add(option);
                Labels.Add(label);
            }
        }

        /// <summary>
        /// A choice in one short line, for a column beside the picture.
        ///
        /// The full-width row spends most of itself on air; at half the width there is room for the
        /// name, the thing itself and the two arrows, and nothing else is needed.
        /// </summary>
        private Choice AddCompactChoice(Transform parent, Choice choice,
                                        int centreX, int y, int width, int height, bool swatchOnly)
        {

            Plate(parent, "Compact_" + choice.Caption, centreX, y, width, height,
                  Skin.Row(width, height), 1);

            int left = centreX - width / 2;

            // The name quietly above, the choice itself plainly below: read the row once to know
            // what it is, then only ever look at the second line.
            // Bright enough to read at a glance. A caption nobody can see is a row with no name.
            UILabel caption = MakeLeftLabel(parent, "CompactName_" + choice.Caption, choice.Caption,
                                            left + 12, y + height / 2 - 13, width - 24, 16,
                                            Skin.Bright, 3);
            caption.fontSize = TextBody;
            caption.color = new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.8f);
            caption.maxLineCount = 1;
            choice.Title = caption;

            Choice captured = choice;

            // Small enough to be a control rather than the row's main event. They step a list;
            // the list is what the row is for.
            int arrow = Mathf.Min(22, height - 26);
            int lower = y - height / 2 + arrow / 2 + 8;

            // Well inside the card. At the old offset the button's own outline sat on the card's.
            MakeButton(parent, "CompactBack_" + choice.Caption, "<", left + 8 + arrow / 2, lower,
                       arrow, arrow - 2, false, delegate { captured.Step(-1); });
            MakeButton(parent, "CompactFwd_" + choice.Caption, ">",
                       centreX + width / 2 - 8 - arrow / 2, lower,
                       arrow, arrow - 2, false, delegate { captured.Step(1); });

            int span = width - 2 * (arrow + 20);

            if (swatchOnly)
            {
                // A shade has no name worth reading, but 'the one it was born with' does, and that
                // is the entry with no colour behind it.
                choice.Swatch = Plate(parent, "CompactSwatch_" + choice.Caption, centreX, lower,
                                      Mathf.Min(span, 96), Mathf.Min(22, arrow - 4),
                                      Skin.Solid(), 3);
                choice.Swatch.gameObject.SetActive(false);

                choice.Display = MakeLabel(parent, "CompactValue_" + choice.Caption, "-",
                                           centreX, lower, span, 22, Skin.Bright, 3);
                choice.Display.fontSize = TextRow;
                choice.Display.maxLineCount = 1;
                choice.SwatchIsTheAnswer = true;
            }
            else
            {
                choice.Display = MakeLabel(parent, "CompactValue_" + choice.Caption, "-",
                                           centreX, lower, span, 22, Skin.Bright, 3);
                choice.Display.fontSize = TextRow;
                choice.Display.maxLineCount = 1;
            }

            choice.Show();
            return choice;
        }

        private Choice AddChoiceRow(Transform parent, int width, Choice choice)
        {
            int y = _cursorY - RowHeight / 2;

            Plate(parent, "Choice_" + choice.Caption, 0, y, width, RowHeight,
                  Skin.Row(width, RowHeight), 1);

            choice.Title = MakeLeftLabel(parent, "ChoiceName_" + choice.Caption, choice.Caption,
                                         -width / 2 + 14, y, 190, RowHeight, Skin.Bright, 3);
            choice.Title.maxLineCount = 1;

            Choice captured = choice;
            MakeButton(parent, "ChoiceBack_" + choice.Caption, "<", width / 2 - 178, y, 40, 32,
                       false, delegate { captured.Step(-1); });

            choice.Display = MakeLabel(parent, "ChoiceValue_" + choice.Caption, "-",
                                       width / 2 - 108, y, 116, RowHeight, Skin.Bright, 3);
            choice.Display.maxLineCount = 1;

            MakeButton(parent, "ChoiceFwd_" + choice.Caption, ">", width / 2 - 38, y, 40, 32,
                       false, delegate { captured.Step(1); });

            // A place for the thing itself, left of the words: a colour swatch, or the item's own
            // picture. Naming a shade 'shade 4' told nobody anything.
            int showX = -width / 2 + 158;

            choice.Swatch = Plate(parent, "ChoiceSwatch_" + choice.Caption, showX, y, 30, 26,
                                  Skin.Solid(), 3);
            choice.Swatch.gameObject.SetActive(false);

            GameObject pictureGo = new GameObject("ChoicePic_" + choice.Caption);
            pictureGo.layer = parent.gameObject.layer;
            pictureGo.transform.SetParent(parent, false);
            pictureGo.transform.localPosition = new Vector3(showX, y, 0f);
            pictureGo.transform.localScale = Vector3.one;

            choice.Picture = pictureGo.AddComponent<UISprite>();
            choice.Picture.depth = 3;
            choice.Picture.gameObject.SetActive(false);

            _cursorY -= RowHeight + RowGap;
            choice.Show();
            return choice;
        }

        private enum Making { Dweller, Pet }

        private Making _making = Making.Dweller;
        private GameObject _dwellerSection;
        private GameObject _petSection;
        private UILabel _makingLabel;
        private UIScrollView _createView;
        private int _petIndex;
        private UILabel _petPickLabel;
        private UISprite _petPickIcon;

        /// <summary>
        /// The things that act on the whole vault at once.
        ///
        /// Everything here goes through the game's own methods — reviving, healing, levelling,
        /// setting a cap — because a vault edited around the game's back is a vault that disagrees
        /// with itself the next time it is saved.
        /// </summary>
        private void BuildPowersPage(Transform page)
        {
            int width = _windowWidth - Margin * 2;

            Transform parent;
            UIScrollView view = BeginScroll(page, width, ContentTop(), ContentBottom(), out parent);

            AddHeader(parent, "EVERYONE", width);

            AddPower(parent, width, "REVIVE THE DEAD",
                     "everyone who died, at full health", ReviveEveryone,
                     new[] { "Icon_StimpackPlain", "Icon_Stimpack", "Icon_dwellerPlain" });
            AddPower(parent, width, "HEAL EVERYONE",
                     "full health, no radiation", HealEveryone,
                     new[] { "Icon_RadawayPlain", "Icon_Radaway", "Icon_StimpackPlain" });
            AddPower(parent, width, "MAKE EVERYONE HAPPY",
                     "everyone at 100%", CheerEveryone,
                     new[] { "Icon_happiness" });
            AddPower(parent, width, "LEVEL EVERYONE",
                     "everyone to level 50", LevelEveryone,
                     new[] { "Lvl_Up", "Icon_UpgradePlain" });
            AddPower(parent, width, "MAX SPECIAL FOR EVERYONE",
                     "ten in every stat", PerfectEveryone,
                     new[] { "Icon_TrainingStrength", "Icon_TrainingPlain" });
            AddPower(parent, width, "DELIVER EVERY BABY",
                     "every pregnancy ends now", DeliverEveryBaby,
                     new[] { "Icon_Pregnant" });
            AddPower(parent, width, "GROW THE CHILDREN",
                     "every child grows up now", GrowTheChildren,
                     new[] { "Icon_ChildrenGrowthColorGreen" });
            AddPower(parent, width, "FINISH ALL TRAINING",
                     "every training done", FinishAllTraining,
                     new[] { "Icon_TrainingPlain", "Icon_Training" });

            AddHeader(parent, "THE VAULT", width);

            AddPower(parent, width, "FILL FOOD, WATER, POWER",
                     "the three, to their caps", FillTheEssentials,
                     new[] { "Icon_FoodWater", "Icon_foodPlain", "Icon_WaterPlain" });
            AddPowerWithNumber(parent, width, "POPULATION LIMIT",
                               "how many the vault takes",
                               MaxDwellersWanted.Value > 0 ? MaxDwellersWanted.Value.ToString() : "200",
                               RaisePopulation,
                               new[] { "Icon_dwellerPlain", "Icon_dweller" });
            AddPower(parent, width, "UNLOCK EVERY RECIPE",
                     "every weapon and outfit", UnlockEveryRecipe, Skin.Padlock(38));



            // On its own, and next to last. Everything else here changes a rule and is done with;
            // this moves fifty people about, and it is the only thing on the page worth a moment's
            // thought before pressing.
            AddHeader(parent, "ASSIGNMENT", width);

            AddPower(parent, width, "BEST DWELLER IN EVERY ROOM",
                     "best where it works, worst where it trains", AssignTheBest,
                     Skin.Ranked(38));

            AddHeader(parent, "PEACE AND QUIET", width);

            _rushSwitch = AddPower(parent, width, "RUSH NEVER FAILS",
                                   "no accident from rushing",
                                   ToggleRushing, Skin.Chevrons(38));

            _incidentSwitch = AddPower(parent, width, "NO INCIDENTS",
                                       "no fires, pests or raiders", ToggleIncidents,
                     new[] { "Common_icon_fire" });
            _bottleSwitch = AddPower(parent, width, "NO BOTTLE AND CAPPY",
                                     "the pair stay away", ToggleBottleAndCappy,
                     new[] { "NukaCaps", "Icon_nukacapsPlain" });

            EndScroll(view, width);
            RefreshPowerSwitches();
        }

        private GameObject _incidentSwitch;
        private GameObject _bottleSwitch;
        private GameObject _rushSwitch;

        /// <summary>One thing the vault can be told to do, with the reason it exists beside it.</summary>
        /// <summary>
        /// One power, and the line it answers on.
        ///
        /// A press with no reply is a press you make twice. Each row keeps its own description and
        /// puts the result in its place for a few seconds, so the answer appears where the question
        /// was asked rather than somewhere along the bottom of the window.
        /// </summary>
        private sealed class Power
        {
            public UILabel Note;
            public string Description;
            public float Until;
        }

        private readonly List<Power> _powers = new List<Power>();
        private Power _pressed;

        private GameObject AddPower(Transform parent, int width, string name, string what,
                                    EventDelegate.Callback action, Texture2D drawn)
        {
            _drawnPowerIcon = drawn;
            try { return AddPower(parent, width, name, what, action, (string[])null); }
            finally { _drawnPowerIcon = null; }
        }

        private Texture2D _drawnPowerIcon;

        private GameObject AddPower(Transform parent, int width, string name, string what,
                                    EventDelegate.Callback action, string[] icon)
        {
            const int cell = 66;
            const int box = 38;

            int middle = _cursorY - cell / 2;

            Plate(parent, "Power_" + name, 0, middle, width, cell, Skin.Row(width, cell), 1);

            int iconCentre = -width / 2 + 10 + (box + 6) / 2;

            if (_drawnPowerIcon != null)
            {
                Plate(parent, "PowerIcon_" + name + "_Well", iconCentre, middle, box + 6, box + 6,
                      Skin.Well(box + 6), 2);

                GameObject mark = new GameObject("PowerIcon_" + name);
                mark.layer = parent.gameObject.layer;
                mark.transform.SetParent(parent, false);
                mark.transform.localPosition = new Vector3(iconCentre, middle, 0f);
                mark.transform.localScale = Vector3.one;

                UITexture face = mark.AddComponent<UITexture>();
                face.mainTexture = _drawnPowerIcon;
                face.width = box;
                face.height = box;
                face.depth = 4;

                Shader flat = Shader.Find("Unlit/Transparent Colored");
                if (flat != null) face.shader = flat;
            }
            else
            {
                AddIcon(parent, "PowerIcon_" + name, icon, "power " + name, iconCentre, middle, box,
                        true, null, true);
            }

            // Clear of the icon's recess rather than touching it. Two units of air read as a
            // mistake; fourteen reads as a margin.
            int button = 100;
            int left = -width / 2 + 32 + box;
            int textWidth = width - button - box - 68;

            UILabel title = MakeLeftLabel(parent, "PowerName_" + name, name,
                                          left, middle + 13, textWidth, 22, Skin.Bright, 3);
            title.maxLineCount = 1;

            UILabel note = MakeLeftLabel(parent, "PowerNote_" + name, what,
                                         left, middle - 12, textWidth, 20, Skin.Bright, 3);
            note.fontSize = TextNote;
            note.color = new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.75f);
            note.maxLineCount = 1;

            Power power = new Power();
            power.Note = note;
            power.Description = what;
            _powers.Add(power);

            EventDelegate.Callback wrapped = delegate
            {
                // Cleared even when the action throws. Left set, the next power's answer was
                // written onto this row instead of its own.
                _pressed = power;
                try { action(); }
                finally { _pressed = null; }
            };

            GameObject press = MakeButton(parent, "PowerDo_" + name, "DO IT",
                                          width / 2 - button / 2 - 10, middle, button, 40,
                                          false, wrapped);

            _cursorY -= cell + RowGap;
            return press;
        }

        /// <summary>
        /// Holds the switched-on powers on.
        ///
        /// None of these is a setting the game keeps: it starts incidents again, it lets the pair
        /// wander again, and it raises the rush failure chance every time a room is rushed. A switch
        /// thrown once would come undone within the minute, so it is thrown again on a slow beat for
        /// as long as it is meant to be on.
        /// </summary>
        private void KeepThePowersOn()
        {
            CheckWhichVault();
            if (SafeVault() == null) return;

            try
            {
                if (IncidentsOff != null && IncidentsOff.Value && IncidentsOn()) SetIncidents(false);

                if (BottleAndCappyOff != null && BottleAndCappyOff.Value && !BottleAndCappyLocked())
                    SetBottleAndCappy(true);

                if (RushAlwaysWorks != null && RushAlwaysWorks.Value)
                {
                    SetRushDanger(false);
                    ClearRushChances();
                }

                WatchTheArmoury();

                if (MaxDwellersWanted != null && MaxDwellersWanted.Value > 0)
                {
                    Vault vault = SafeVault();
                    object now = vault == null ? null : ReadObject(vault, "MaxDwellers");

                    if (now != null && Convert.ToInt32(now) != MaxDwellersWanted.Value)
                    {
                        if (_wasMaxDwellers < 0) _wasMaxDwellers = Convert.ToInt32(now);
                        WriteMember(vault, "MaxDwellers", MaxDwellersWanted.Value);
                    }
                }
            }
            catch (Exception e)
            {
                ReportOnce("upkeep", "Could not hold the powers on: " + e.Message);
            }
        }

        // What the two figures were before they were set to nothing, so switching off puts the
        // game back rather than leaving it altered.
        private float _wasMinimumChance = -1f;
        private float _wasChancePerTier = -1f;

        // The rest of what this panel changes about the vault, as it was before the change. Three
        // of these switches used to be one-way: the mod could turn incidents off and then be
        // uninstalled, leaving a vault that never has an incident again and nothing to say why.
        private bool _wasIncidents, _knowIncidents;
        private bool _wasPairLocked, _knowPairLocked;
        private int _wasMaxDwellers = -1;

        // Which vault those originals came from. A figure captured in one save is not the original
        // of another, and writing it back into the wrong one is its own kind of damage.
        private object _knownVault;

        // Said once, and the sentence not even built the rest of the time. This runs every frame
        // the bench is open, and a float turned into text sixty times a second to be thrown away
        // is sixty allocations a second.
        private static bool _reportedFilm;
        private static Shader _plainShader;
        private static bool _reportedMovers;

        // Whether the stand-in has been put into its idle, and whether the camera has therefore
        // been allowed to settle on a framing. A figure measured before it stands up is measured
        // as the wrong shape.
        private static bool _posed;
        private bool _framedLocked;

        // Where each animator's clock stood last frame, so a clock that has stopped can be told
        // apart from one that is merely slow.
        private static readonly Dictionary<int, float> _lastBeat = new Dictionary<int, float>();

        /// <summary>
        /// Forgets every saved original when the vault underneath us changes.
        ///
        /// Loading a second save brings a second set of figures; the first save's are no longer the
        /// original of anything, and restoring them into the new vault would write one player's
        /// numbers over another's.
        /// </summary>
        private void CheckWhichVault()
        {
            try
            {
                Vault now = SafeVault();
                if (ReferenceEquals(now, _knownVault)) return;

                // Not _wasChancePerTier. That one is read off GameParameters, which outlives
                // every vault -- forgetting it on a vault change lost the original, so the switch
                // could never put it back and the whole process was left with a rush disaster
                // chance of zero and a switch reading OFF.
                _knownVault = now;
                _wasMinimumChance = -1f;
                _knowIncidents = false;
                _knowPairLocked = false;
                _wasMaxDwellers = -1;
            }
            catch { }
        }

        /// <summary>Puts back everything this panel changed that the vault would otherwise keep.</summary>
        private void PutTheVaultBack()
        {
            SetRushDanger(true);

            try
            {
                if (_knowIncidents) SetIncidents(_wasIncidents);
                if (_knowPairLocked) SetBottleAndCappy(_wasPairLocked);

                if (_wasMaxDwellers >= 0)
                {
                    Vault vault = SafeVault();
                    if (vault != null) WriteMember(vault, "MaxDwellers", _wasMaxDwellers);
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not put the vault back: " + e);
            }

            _knowIncidents = false;
            _knowPairLocked = false;
            _wasMaxDwellers = -1;
        }

        /// <summary>
        /// Sets the rush failure chance to nothing, or puts it back.
        ///
        /// Clearing the room's timer only removed what had been accumulated; underneath it the vault
        /// keeps a minimum chance — the ten per cent that still went wrong — and the room parameters
        /// keep a rise per tier on top. Both are plain figures, so a guarantee is a matter of
        /// setting them rather than of patching anything.
        /// </summary>
        private void SetRushDanger(bool dangerous)
        {
            try
            {
                Vault vault = SafeVault();

                if (vault != null)
                {
                    object now = ReadObject(vault, "m_minimumRushFailureChance");

                    if (!dangerous)
                    {
                        if (_wasMinimumChance < 0f && now != null)
                            _wasMinimumChance = Convert.ToSingle(now);

                        WriteMember(vault, "m_minimumRushFailureChance", 0f);
                    }
                    else if (_wasMinimumChance >= 0f)
                    {
                        WriteMember(vault, "m_minimumRushFailureChance", _wasMinimumChance);
                        _wasMinimumChance = -1f;
                    }
                }

                GameParameters parameters = GameParameters.Instance;
                object rooms = parameters == null ? null : ReadObject(parameters, "Room");

                if (rooms != null)
                {
                    object now = ReadObject(rooms, "m_rushDisasterChancePerTier");

                    if (!dangerous)
                    {
                        if (_wasChancePerTier < 0f && now != null)
                            _wasChancePerTier = Convert.ToSingle(now);

                        WriteMember(rooms, "m_rushDisasterChancePerTier", 0f);
                    }
                    else if (_wasChancePerTier >= 0f)
                    {
                        WriteMember(rooms, "m_rushDisasterChancePerTier", _wasChancePerTier);
                        _wasChancePerTier = -1f;
                    }
                }
            }
            catch (Exception e)
            {
                ReportOnce("rushdanger", "Could not set the rush chance: " + e.Message);
            }
        }

        /// <summary>Holds the failure chance down for every room that is being rushed right now.</summary>
        private void GuardTheRushes()
        {
            ResetRushChances(true);
        }

        private void ResetRushChances(bool onlyRushing)
        {
            try
            {
                if (_rushingRooms == null)
                {
                    List<Room> found = new List<Room>();
                    Room[] all = Resources.FindObjectsOfTypeAll<Room>();

                    for (int i = 0; i < all.Length; i++)
                        if (all[i] != null && all[i].gameObject.activeInHierarchy) found.Add(all[i]);

                    _rushingRooms = found.ToArray();
                }

                if (_rushReset == null)
                    _rushReset = typeof(Room).GetMethod(
                        "Cheat_ResetRushFailureChance",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (_rushReset == null) return;

                for (int i = 0; i < _rushingRooms.Length; i++)
                {
                    Room room = _rushingRooms[i];
                    if (room == null) continue;
                    if (onlyRushing && !room.IsRushing) continue;

                    // Every frame, so a shout here would fill the log; but a switch that reads ON
                    // over rushes that still fail is worse than a noisy log, and this used to say
                    // nothing at all.
                    try { _rushReset.Invoke(room, null); }
                    catch (Exception e)
                    {
                        ReportOnce("rushinvoke", "The game refused to reset a rush chance: " + e);
                    }
                }
            }
            catch (Exception e)
            {
                ReportOnce("rushguard", "Could not hold the rush chance down: " + e.Message);
            }
        }

        private MethodInfo _rushReset;

        /// <summary>
        /// Clears the accumulated rush chance on every room in the vault.
        ///
        /// The same work the per-frame guard does, over every room rather than only the ones
        /// rushing right now. It was a second copy of that method with its own lookup, its own
        /// silent catch and a count both its callers threw away; now it is the same code with the
        /// filter switched off.
        /// </summary>
        private void ClearRushChances()
        {
            _rushingRooms = null;
            ResetRushChances(false);
        }

        private UIInput _populationInput;

        /// <summary>
        /// A power with a figure of its own to be told.
        ///
        /// Nine hundred and ninety-nine was a number I chose; how large a vault should be is not my
        /// decision to make on someone else's behalf.
        /// </summary>
        private GameObject AddPowerWithNumber(Transform parent, int width, string name, string what,
                                              string figure, EventDelegate.Callback action,
                                              string[] icon)
        {
            const int cell = 66;
            const int box = 38;

            int middle = _cursorY - cell / 2;

            Plate(parent, "Power_" + name, 0, middle, width, cell, Skin.Row(width, cell), 1);

            int iconCentre = -width / 2 + 10 + (box + 6) / 2;
            AddIcon(parent, "PowerIcon_" + name, icon, "power " + name, iconCentre, middle, box,
                    true, null, true);

            // The field holds three digits and the button one short word; both had been sized
            // for a great deal more than they carry, and the words to their left paid for it.
            int button = 62;
            int field = 60;
            int left = -width / 2 + 32 + box;
            int textWidth = width - button - field - box - 74;

            UILabel title = MakeLeftLabel(parent, "PowerName_" + name, name,
                                          left, middle + 13, textWidth, 22, Skin.Bright, 3);
            title.maxLineCount = 1;

            UILabel note = MakeLeftLabel(parent, "PowerNote_" + name, what,
                                         left, middle - 12, textWidth, 20, Skin.Bright, 3);
            note.fontSize = TextNote;
            note.color = new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.75f);
            note.maxLineCount = 1;

            _populationInput = AddInput(parent, "PowerField_" + name,
                                        width / 2 - button - field / 2 - 16, middle, field,
                                        figure, true);

            Power power = new Power();
            power.Note = note;
            power.Description = what;
            _powers.Add(power);

            EventDelegate.Callback wrapped = delegate
            {
                // Cleared even when the action throws. Left set, the next power's answer was
                // written onto this row instead of its own.
                _pressed = power;
                try { action(); }
                finally { _pressed = null; }
            };

            GameObject press = MakeButton(parent, "PowerDo_" + name, "SET",
                                          width / 2 - button / 2 - 10, middle, button, 40,
                                          false, wrapped);

            _cursorY -= cell + RowGap;
            return press;
        }

        /// <summary>Puts an answer on the row that was pressed, for as long as it takes to read.</summary>
        private void Answer(string message, bool went)
        {
            if (_pressed == null || _pressed.Note == null) return;

            _pressed.Note.text = message;
            // A refusal is quieter, not a different colour. There are three greens in this
            // interface and an amber warning belongs to some other program.
            _pressed.Note.color = went
                ? Skin.Bright
                : new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.55f);

            _pressed.Until = Time.time + 6f;
        }

        /// <summary>Puts the descriptions back once their answers have been read.</summary>
        private void ForgetOldAnswers()
        {
            for (int i = 0; i < _powers.Count; i++)
            {
                Power power = _powers[i];
                if (power.Until <= 0f || Time.time < power.Until) continue;

                power.Until = 0f;

                if (power.Note == null) continue;
                power.Note.text = power.Description;
                power.Note.color = new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.75f);
            }
        }

        private void RefreshPowerSwitches()
        {
            // Each switch is a claim, and ON means the claim holds. 'No incidents' being on is
            // the opposite of the flag the game keeps, which is what made the old labels read
            // backwards.
            Switch(_incidentSwitch, !IncidentsOn());
            Switch(_bottleSwitch, BottleAndCappyLocked());
            Switch(_rushSwitch, RushAlwaysWorks != null && RushAlwaysWorks.Value);
        }

        /// <summary>
        /// Shows a switch's state, not the action that would change it.
        ///
        /// TURN OFF on a thing that is on reads as an instruction to whoever wrote it and as a state
        /// to everyone else, and the two readings are opposites. So it says ON or OFF, and what
        /// pressing does follows from that.
        /// </summary>
        private static void Switch(GameObject button, bool on)
        {
            if (button == null) return;

            UILabel text = button.GetComponentInChildren<UILabel>();
            if (text == null) return;

            // The game's own switches are a filled half and an empty one: on is solid with dark
            // lettering, off is an outline. Two states that differ only in a word are two states
            // nobody reads.
            text.text = on ? "ON" : "OFF";
            text.color = on ? Skin.Ink : Skin.Bright;

            UITexture face = button.GetComponent<UITexture>();
            if (face != null)
                face.mainTexture = on
                    ? Skin.SolidButton(face.width, face.height)
                    : Skin.Button(face.width, face.height);
        }

        private bool BottleAndCappyLocked()
        {
            try
            {
                BottleAndCappyMgr pair = BottleAndCappyMgr.Instance;
                object locked = pair == null ? null : ReadObject(pair, "m_locked");

                return locked is bool && (bool)locked;
            }
            catch { return false; }
        }

        /// <summary>
        /// Stops the bottle and the cap wandering about, or lets them back.
        ///
        /// The manager keeps a lock of its own for the times the game does not want them — it is
        /// the same switch, thrown by hand.
        /// </summary>
        private bool SetBottleAndCappy(bool locked)
        {
            try
            {
                BottleAndCappyMgr pair = BottleAndCappyMgr.Instance;
                if (pair == null) return false;

                if (!_knowPairLocked)
                {
                    object was = ReadObject(pair, "m_locked");
                    if (was is bool) { _wasPairLocked = (bool)was; _knowPairLocked = true; }
                }

                return WriteMember(pair, "m_locked", locked);
            }
            catch { return false; }
        }

        private void ToggleBottleAndCappy()
        {
            bool wanted = !BottleAndCappyLocked();

            if (!SetBottleAndCappy(wanted))
            {
                Trouble("That pair cannot be locked from here.");
                return;
            }

            BottleAndCappyOff.Value = wanted;
            RefreshPowerSwitches();

            Say(wanted ? "Bottle and Cappy will stay away." : "Bottle and Cappy may wander again.");
        }

        /// <summary>Every child grown, through the call the game makes when one comes of age.</summary>
        private void GrowTheChildren()
        {
            int grown = 0;

            foreach (Dweller one in Everyone())
            {
                if (one == null || !one.IsChild) continue;

                try
                {
                    Component child = one.GetComponentInChildren(
                        typeof(MonoBehaviour), true) as Component;

                    MonoBehaviour[] parts = one.GetComponentsInChildren<MonoBehaviour>(true);

                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i] == null || parts[i].GetType().Name != "DwellerChild") continue;

                        MethodInfo grow = parts[i].GetType().GetMethod(
                            "OnGrowUp",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (grow == null) continue;

                        grow.Invoke(parts[i], new object[] { true });
                        grown++;
                        break;
                    }
                }
                catch { }
            }

            Say(grown == 0 ? "There are no children." : "Grew " + grown + " child(ren) up.");
        }

        /// <summary>Every training slot finished, through the slot's own call.</summary>
        private void FinishAllTraining()
        {
            int finished = 0;

            try
            {
                // A training slot is not a Unity object and cannot be searched for; it belongs to
                // its room, and the rooms are what the scene holds.
                TrainingRoom[] rooms = Resources.FindObjectsOfTypeAll<TrainingRoom>();

                MethodInfo finish = null;

                for (int i = 0; i < rooms.Length; i++)
                {
                    if (rooms[i] == null || !rooms[i].gameObject.activeInHierarchy) continue;

                    System.Collections.IEnumerable slots =
                        ReadObject(rooms[i], "m_slots") as System.Collections.IEnumerable;

                    if (slots == null) continue;

                    foreach (object slot in slots)
                    {
                        if (slot == null) continue;

                        if (finish == null)
                            finish = slot.GetType().GetMethod(
                                "FinishTraining",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                                null, Type.EmptyTypes, null);

                        if (finish == null)
                        {
                            Trouble("Training cannot be finished from here.");
                            return;
                        }

                        try { finish.Invoke(slot, null); finished++; }
                        catch { }
                    }
                }
            }
            catch (Exception e)
            {
                Trouble("Could not finish the training: " + e.Message);
                return;
            }

            Say(finished == 0 ? "Nobody is training." : "Finished " + finished + " training(s).");
        }

        /// <summary>
        /// Unlocks every recipe, through the window that keeps the list of them.
        ///
        /// This unlocks what can be made, not what has been made: the game still wants the
        /// ingredients. Crafting for nothing is a different thing and is not possible without
        /// changing how the game itself behaves.
        /// </summary>
        private void UnlockEveryRecipe()
        {
            int opened = 0;

            try
            {
                VaultGUIManager gui = VaultGUIManager.Instance;
                object window = gui == null ? null : ReadObject(gui, "m_survivalWindow");

                if (window == null) { Trouble("The survival guide is not open to be written to."); return; }

                MethodInfo unlock = window.GetType().GetMethod(
                    "UnlockRecipe",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (unlock == null) { Trouble("Recipes cannot be unlocked from here."); return; }

                if (_catalogue == null) BuildCatalogue();
                if (_catalogue == null) return;

                for (int i = 0; i < _catalogue.Count; i++)
                {
                    CatalogueEntry entry = _catalogue[i];
                    if (entry.Type != EItemType.Weapon && entry.Type != EItemType.Outfit) continue;

                    try
                    {
                        unlock.Invoke(window, new object[] { new DwellerItem(entry.Type, entry.Id) });
                        opened++;
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                Trouble("Could not unlock the recipes: " + e.Message);
                return;
            }

            Say("Unlocked " + opened + " recipe(s).");
        }

        /// <summary>
        /// Counts the weapons in the vault and says so when more arrive than were asked for.
        ///
        /// This does not fix anything and is not meant to. Weapons are turning up in stacks that
        /// nobody granted; the mod's own two ways of adding one each add exactly one and write a
        /// line about it, so reading the code has got as far as it can. The next time it happens
        /// this will be in the log with a number and a time, which is worth more than another
        /// theory.
        /// </summary>
        private void WatchTheArmoury()
        {
            try
            {
                Vault vault = SafeVault();
                VaultInventory inventory = vault == null ? null : vault.Inventory;
                if (inventory == null || inventory.Items == null) return;

                int weapons = 0;
                for (int i = 0; i < inventory.Items.Count; i++)
                {
                    DwellerItem held = inventory.Items[i];
                    if (held == null) continue;

                    // The field that says what kind of item this is has no name I could find from
                    // the outside, so it is asked for by each of the names it might have -- and if
                    // none of them answer, the type writes down what it does have, once, rather
                    // than leaving the count silently wrong.
                    object kind = ReadObject(held, "ItemType");
                    if (kind == null) kind = ReadObject(held, "Type");
                    if (kind == null) kind = ReadObject(held, "m_type");
                    if (kind == null) kind = ReadObject(held, "m_itemType");

                    if (kind == null)
                    {
                        if (!_reportedItemShape)
                        {
                            _reportedItemShape = true;
                            SayWhatAnItemIs(held);
                        }
                        continue;
                    }

                    if (kind.ToString() == EItemType.Weapon.ToString()) weapons++;
                }

                if (_lastWeaponCount >= 0)
                {
                    int grew = weapons - _lastWeaponCount;
                    int granted = _grantsMade - _grantsAtLastCount;

                    if (grew > granted)
                        Log.LogWarning("The armoury grew by " + grew + " weapon(s) in the last " +
                                       "second and a half; this panel granted " + granted +
                                       " of them. It now holds " + weapons + ".");
                }

                _lastWeaponCount = weapons;
                _grantsAtLastCount = _grantsMade;
            }
            catch (Exception e)
            {
                ReportOnce("armoury", "Could not count the weapons: " + e);
            }
        }

        private static bool _reportedItemShape;
        private static bool _reportedMood;

        /// <summary>Writes down what an inventory item is made of, once, when it cannot be read.</summary>
        private static void SayWhatAnItemIs(object held)
        {
            try
            {
                const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance;

                System.Text.StringBuilder said = new System.Text.StringBuilder();
                said.Append("An inventory item is a ").Append(held.GetType().Name).Append(" with:");

                FieldInfo[] fields = held.GetType().GetFields(Flags);
                for (int i = 0; i < fields.Length; i++)
                    said.Append(" .").Append(fields[i].Name);

                PropertyInfo[] props = held.GetType().GetProperties(Flags);
                for (int i = 0; i < props.Length; i++)
                    said.Append(" .").Append(props[i].Name);

                Log.LogWarning(said.ToString());
            }
            catch { }
        }

        /// <summary>
        /// Staffs the whole vault by the one number each room runs on.
        ///
        /// A vault of fifty is an hour of dragging people about and squinting at seven figures each
        /// time. The game already knows which stat a room uses and what every dweller scores in it;
        /// all that is missing is somebody willing to do the sorting.
        ///
        /// The rules, in the order they are applied:
        ///
        ///   1. A room with no stat is left alone. Storage has nobody to place and nothing to gain.
        ///   2. Rooms that produce something are staffed first, largest first, each taking the
        ///      highest scorers left in the pool. A point of a stat is worth most in the room with
        ///      the most places, so the big merged rooms get first pick.
        ///   3. Training rooms are staffed last, and they take the LOWEST scorers left. Training
        ///      raises the stat it teaches, and a dweller already at ten learns nothing there --
        ///      putting your best in a gym is the most expensive mistake this screen can make.
        ///   4. Anyone not placed stays exactly where they were.
        ///
        /// Every one of those decisions is read from the game rather than listed here: which stat,
        /// whether anything is produced, how many places there are. A room added by an update or by
        /// another mod is classified by the same questions as the rooms that shipped, so it is
        /// staffed correctly without this code having heard of it.
        /// </summary>
        private void AssignTheBest()
        {
            try
            {
                DwellerManager manager = SafeDwellerManager();
                if (manager == null || manager.Dwellers == null)
                {
                    Trouble("The vault is not loaded.");
                    return;
                }

                MethodInfo assign = FindTheAssigner();
                if (assign == null)
                {
                    Trouble("Nothing here can assign a dweller; the log lists what the game offers.");
                    return;
                }

                // Everyone who can hold a post: no children, nobody out in the wasteland. The game
                // refuses the rest anyway, and asking it to refuse fifty times is a slower way of
                // finding that out.
                List<Dweller> pool = new List<Dweller>();

                for (int i = 0; i < manager.Dwellers.Count; i++)
                {
                    Dweller one = manager.Dwellers[i];
                    if (one == null) continue;

                    try
                    {
                        if (one.IsChild) continue;
                        if (one.IsRegisteredInWasteland) continue;
                    }
                    catch { continue; }

                    pool.Add(one);
                }

                List<Room> works = new List<Room>();
                List<Room> teaches = new List<Room>();

                Room[] all = Resources.FindObjectsOfTypeAll<Room>();

                for (int i = 0; i < all.Length; i++)
                {
                    Room room = all[i];
                    if (room == null || !room.gameObject.activeInHierarchy) continue;
                    if (!(RoomStat(room) is ESpecialStat)) continue;
                    if (RoomPlaces(room) <= 0) continue;

                    if (Teaches(room)) teaches.Add(room);
                    else works.Add(room);
                }

                if (works.Count == 0 && teaches.Count == 0)
                {
                    Trouble("No room in this vault runs on a stat.");
                    return;
                }

                works.Sort(new ByPlaces());

                int posted = Staff(works, pool, assign, manager, true) +
                             Staff(teaches, pool, assign, manager, false);

                Say("Posted " + posted + " dweller(s): " + works.Count + " working room(s) got " +
                    "their best, " + teaches.Count + " training room(s) got those with the most " +
                    "to learn.");
            }
            catch (Exception e)
            {
                Trouble("Could not assign the dwellers: " + e.Message);
            }
        }

        /// <summary>Fills a set of rooms from the pool, taking the highest scorers or the lowest.</summary>
        private int Staff(List<Room> rooms, List<Dweller> pool, MethodInfo assign,
                          DwellerManager manager, bool best)
        {
            int posted = 0;
            bool twoArgs = assign.GetParameters().Length == 2;

            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];

                ESpecialStat stat = (ESpecialStat)RoomStat(room);
                int places = RoomPlaces(room);

                pool.Sort(new ByStat(stat, best));

                for (int taken = 0; taken < places && pool.Count > 0; taken++)
                {
                    Dweller chosen = pool[0];
                    pool.RemoveAt(0);

                    try
                    {
                        if (twoArgs) assign.Invoke(manager, new object[] { chosen, room });
                        else assign.Invoke(room, new object[] { chosen });

                        posted++;
                    }
                    catch (Exception e)
                    {
                        ReportOnce("assigncall", "The game refused an assignment: " + e);
                    }
                }
            }

            return posted;
        }

        /// <summary>Rooms with more places first.</summary>
        private sealed class ByPlaces : IComparer<Room>
        {
            public int Compare(Room left, Room right)
            {
                return RoomPlaces(right).CompareTo(RoomPlaces(left));
            }
        }

        /// <summary>
        /// Whether a room teaches rather than produces.
        ///
        /// Asked three ways, weakest last, so a room this code has never heard of is still sorted
        /// correctly: the game's own flag if it has one, then the name of the type, and failing
        /// both, whether the room produces any resource at all. A room that runs on a stat and
        /// makes nothing is a gym.
        /// </summary>
        private static bool Teaches(Room room)
        {
            object flag = ReadObject(room, "IsTrainingRoom");
            if (flag == null) flag = ReadObject(room, "m_isTrainingRoom");
            if (flag is bool) return (bool)flag;

            if (room.GetType().Name.IndexOf("training", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            object made = ReadObject(room, "ProducedResource");
            if (made == null) made = ReadObject(room, "m_producedResource");
            if (made == null) made = ReadObject(room, "Resource");

            if (made != null)
            {
                string what = made.ToString();
                if (what == "None" || what == "0") return true;
            }

            return false;
        }

        /// <summary>Sorts by one stat: the ablest first, or those with the most to learn.</summary>
        private sealed class ByStat : IComparer<Dweller>
        {
            private readonly ESpecialStat _stat;
            private readonly bool _best;

            public ByStat(ESpecialStat stat, bool best) { _stat = stat; _best = best; }

            public int Compare(Dweller left, Dweller right)
            {
                return _best
                    ? Value(right).CompareTo(Value(left))
                    : Value(left).CompareTo(Value(right));
            }

            private int Value(Dweller who)
            {
                try
                {
                    DwellerStats stats = who == null ? null : who.Stats;
                    SpecialStat one = stats == null ? null : stats.GetStat(_stat);

                    return one == null ? 0 : one.Value;
                }
                catch { return 0; }
            }
        }

        /// <summary>Which stat a room runs on, asked for by each name it might have.</summary>
        private static object RoomStat(Room room)
        {
            object stat = ReadObject(room, "SpecialStat");
            if (stat == null) stat = ReadObject(room, "m_specialStat");

            if (stat == null)
            {
                object parameters = ReadObject(room, "RoomParameters");
                if (parameters == null) parameters = ReadObject(room, "m_roomParameters");
                if (parameters == null) parameters = ReadObject(room, "Parameters");

                if (parameters != null)
                {
                    stat = ReadObject(parameters, "SpecialStat");
                    if (stat == null) stat = ReadObject(parameters, "m_specialStat");
                }
            }

            return stat;
        }

        /// <summary>How many people a room has room for.</summary>
        private static int RoomPlaces(Room room)
        {
            string[] names = { "MaxDwellers", "m_maxDwellers", "Capacity", "m_capacity",
                               "MaxWorkers", "m_maxWorkers" };

            for (int i = 0; i < names.Length; i++)
            {
                object many = ReadObject(room, names[i]);
                if (many == null) continue;

                try { return Convert.ToInt32(many); }
                catch { }
            }

            // The game builds rooms two places wide per merged section; two is the smallest a room
            // ever is, so it is the safe answer when nothing will say.
            return 2;
        }

        /// <summary>
        /// The game's own way of putting a dweller in a room.
        ///
        /// Tried by name, and if none of them answer, every method that mentions assigning goes in
        /// the log. Guessing at somebody else's API and failing quietly is how this mod has wasted
        /// its afternoons; guessing and then saying exactly what was there is how it stops.
        /// </summary>
        private MethodInfo FindTheAssigner()
        {
            if (_assigner != null) return _assigner;

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance;

            _assigner = typeof(Room).GetMethod("AssignDweller", Flags, null,
                                               new[] { typeof(Dweller) }, null);

            if (_assigner == null)
                _assigner = typeof(Room).GetMethod("AddDweller", Flags, null,
                                                   new[] { typeof(Dweller) }, null);

            if (_assigner == null)
                _assigner = typeof(DwellerManager).GetMethod("AssignDwellerToRoom", Flags, null,
                                                             new[] { typeof(Dweller), typeof(Room) }, null);

            if (_assigner != null)
            {
                Log.LogInfo("Assignments go through " + _assigner.DeclaringType.Name + "." +
                            _assigner.Name + ".");
                return _assigner;
            }

            SayWhoCanAssign(typeof(Room));
            SayWhoCanAssign(typeof(Dweller));
            SayWhoCanAssign(typeof(DwellerManager));

            return null;
        }

        private MethodInfo _assigner;

        private static void SayWhoCanAssign(Type type)
        {
            try
            {
                const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance;

                System.Text.StringBuilder said = new System.Text.StringBuilder();
                said.Append(type.Name).Append(" offers:");

                MethodInfo[] all = type.GetMethods(Flags);
                for (int i = 0; i < all.Length; i++)
                {
                    string name = all[i].Name;

                    if (name.IndexOf("assign", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("room", StringComparison.OrdinalIgnoreCase) < 0) continue;

                    said.Append(" ").Append(name).Append("(");

                    ParameterInfo[] args = all[i].GetParameters();
                    for (int a = 0; a < args.Length; a++)
                        said.Append(a > 0 ? "," : "").Append(args[a].ParameterType.Name);

                    said.Append(")");
                }

                Log.LogWarning(said.ToString());
            }
            catch { }
        }

        private bool IncidentsOn()
        {
            try
            {
                Vault vault = SafeVault();
                object state = vault == null ? null : ReadObject(vault, "EmergencyState");
                object on = state == null ? null : ReadObject(state, "Enabled");

                return on is bool && (bool)on;
            }
            catch { return true; }
        }

        private bool SetIncidents(bool on)
        {
            try
            {
                Vault vault = SafeVault();
                object state = vault == null ? null : ReadObject(vault, "EmergencyState");
                if (state == null) return false;

                if (!_knowIncidents)
                {
                    object was = ReadObject(state, "Enabled");
                    if (was is bool) { _wasIncidents = (bool)was; _knowIncidents = true; }
                }

                return WriteMember(state, "Enabled", on);
            }
            catch { return false; }
        }

        private void ToggleIncidents()
        {
            bool wanted = !IncidentsOn();

            if (!SetIncidents(wanted))
            {
                Trouble("The emergency state cannot be switched here.");
                return;
            }

            IncidentsOff.Value = !wanted;
            RefreshPowerSwitches();

            Say(wanted ? "Incidents are on again." : "Incidents are off, and will stay off.");
        }

        /// <summary>Everyone the vault knows about, dead ones included.</summary>
        private List<Dweller> Everyone()
        {
            List<Dweller> all = new List<Dweller>();

            try
            {
                Vault vault = SafeVault();
                if (vault == null) return all;

                List<Dweller> living = ReadObject(vault, "Dwellers") as List<Dweller>;
                if (living != null) all.AddRange(living);
            }
            catch (Exception e)
            {
                ReportOnce("everyone", "Could not list the dwellers: " + e.Message);
            }

            return all;
        }

        private void ReviveEveryone()
        {
            int brought = 0;

            foreach (Dweller one in Everyone())
            {
                if (one == null || !one.IsDead) continue;

                try
                {
                    MethodInfo revive = typeof(Dweller).GetMethod(
                        "TryReviveInVaultWithFullHealth",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (revive != null && (bool)revive.Invoke(one, new object[] { true })) brought++;
                }
                catch { }
            }

            Say(brought == 0 ? "Nobody was dead." : "Brought back " + brought + " dweller(s).");
        }

        private void HealEveryone()
        {
            int mended = 0;

            foreach (Dweller one in Everyone())
            {
                if (one == null) continue;

                try
                {
                    object health = ReadObject(one, "Health");
                    if (health == null) continue;

                    object most = ReadObject(health, "HealthMax");
                    if (most != null) WriteMember(health, "HealthValue", Convert.ToSingle(most));

                    WriteMember(health, "RadiationValue", 0f);
                    mended++;
                }
                catch { }
            }

            Say("Healed " + mended + " dweller(s).");
        }

        private void CheerEveryone()
        {
            int cheered = 0;

            foreach (Dweller one in Everyone())
            {
                if (one == null) continue;

                try
                {
                    object mood = ReadObject(one, "Happiness");
                    if (mood == null) continue;

                    WriteMember(mood, "HappinessValue", 100f);
                    cheered++;
                }
                catch { }
            }

            Say("Cheered up " + cheered + " dweller(s).");
        }

        private void LevelEveryone()
        {
            int raised = 0;

            foreach (Dweller one in Everyone())
            {
                if (one == null) continue;

                try
                {
                    ApplyLevel(one, 50);
                    raised++;
                }
                catch { }
            }

            Say("Took " + raised + " dweller(s) to level 50.");
        }

        private void PerfectEveryone()
        {
            int improved = 0;

            foreach (Dweller one in Everyone())
            {
                if (one == null) continue;

                try
                {
                    object stats = ReadObject(one, "Stats");
                    if (stats == null) continue;

                    for (int i = 0; i < Specials.Length; i++)
                    {
                        MethodInfo find = stats.GetType().GetMethod(
                            "GetStat",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        object stat = find == null ? null : find.Invoke(stats, new object[] { Specials[i] });
                        if (stat == null) continue;

                        MethodInfo set = stat.GetType().GetMethod(
                            "SetValueAndMinExp",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (set != null) set.Invoke(stat, new object[] { 10 });
                    }

                    improved++;
                }
                catch { }
            }

            Say("Gave " + improved + " dweller(s) ten in everything.");
        }

        private void DeliverEveryBaby()
        {
            int born = 0;

            foreach (Dweller one in Everyone())
            {
                if (one == null) continue;

                try
                {
                    object expecting = ReadObject(one, "Pregnant");
                    if (!(expecting is bool) || !(bool)expecting) continue;

                    object relations = ReadObject(one, "Relations");
                    object bond = relations == null ? null : ReadObject(relations, "Partnership");
                    if (bond == null) continue;

                    MethodInfo deliver = bond.GetType().GetMethod(
                        "BabyBirth",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (deliver != null) { deliver.Invoke(bond, new object[] { true }); born++; }
                }
                catch { }
            }

            Say(born == 0 ? "Nobody is expecting." : "Delivered " + born + " baby(ies).");
        }

        // The three a vault lives or dies by. Caps, quantum, stimpaks and radaway are stock, not
        // upkeep, and a button that topped those up as well would be handing over the game rather
        // than keeping the lights on.
        private static readonly EResource[] Essentials =
        {
            EResource.Food, EResource.Water, EResource.Energy
        };

        private void FillTheEssentials()
        {
            for (int i = 0; i < Essentials.Length; i++) FillToCap(Essentials[i]);

            Say("Food, water and power are full.");
        }

        /// <summary>
        /// Rushing that never goes wrong, without patching the game.
        ///
        /// The failure chance lives in a timer the room holds; the game's own cheat releases it, and
        /// the chance climbs again the moment another room is rushed. So it is released again every
        /// second and a half for as long as the switch is on. Nothing about the game is rewritten —
        /// the same call it ships is simply made often enough to be a guarantee.
        /// </summary>
        private void ToggleRushing()
        {
            bool wanted = !RushAlwaysWorks.Value;
            RushAlwaysWorks.Value = wanted;

            SetRushDanger(!wanted);
            if (wanted) ClearRushChances();

            RefreshPowerSwitches();
            Say(wanted ? "Rushing cannot fail." : "Rushing can fail again.");
        }

        private void RaisePopulation()
        {
            try
            {
                Vault vault = SafeVault();
                if (vault == null) return;

                int wanted;
                if (_populationInput == null ||
                    !int.TryParse(_populationInput.value, out wanted) || wanted < 1)
                {
                    Trouble("Type how many dwellers the vault should take.");
                    return;
                }

                if (_wasMaxDwellers < 0)
                {
                    object was = ReadObject(vault, "MaxDwellers");
                    if (was != null) _wasMaxDwellers = Convert.ToInt32(was);
                }

                if (!WriteMember(vault, "MaxDwellers", wanted))
                {
                    Trouble("The population limit cannot be set from here.");
                    return;
                }

                // Written down, so the vault is still this size after a restart.
                MaxDwellersWanted.Value = wanted;
                Say("The vault will take " + wanted + " dwellers.");
            }
            catch (Exception e)
            {
                Trouble("Could not set the population limit: " + e.Message);
            }
        }

        /// <summary>Writes a property or a field, whichever the member turns out to be.</summary>
        private static bool WriteMember(object target, string member, object value)
        {
            if (target == null) return false;

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance;

            PropertyInfo property = target.GetType().GetProperty(member, Flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value, null);
                return true;
            }

            FieldInfo field = target.GetType().GetField(member, Flags);
            if (field == null) return false;

            field.SetValue(target, value);
            return true;
        }

        /// <summary>
        /// The constructor: everything that is built to order rather than handed over as it stands.
        ///
        /// Both halves start at the same height and only one is ever shown, so neither leaves a gap
        /// where the other would have been.
        /// </summary>
        private void BuildCreatePage(Transform page)
        {
            int width = _windowWidth - Margin * 2;

            Transform parent;
            _createView = BeginScroll(page, width, ContentTop(), ContentBottom(), out parent);

            _makingLabel = AddPickerRow(parent, width, "MAKING",
                                        delegate { StepMaking(-1); }, delegate { StepMaking(1); },
                                        _making.ToString().ToUpper());

            int sectionTop = _cursorY;

            _dwellerSection = MakeSection(parent, "DwellerSection");
            BuildDwellerSection(_dwellerSection.transform, width);
            int afterDweller = _cursorY;

            _cursorY = sectionTop;
            _petSection = MakeSection(parent, "PetSection");
            BuildPetSection(_petSection.transform, width);

            _cursorY = Mathf.Min(afterDweller, _cursorY);
            EndScroll(_createView, width);

            ShowMaking(_making);
        }

        private GameObject MakeSection(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static readonly Making[] Makings = { Making.Dweller, Making.Pet };

        private void StepMaking(int by)
        {
            int at = Array.IndexOf(Makings, _making);
            if (at < 0) at = 0;

            at = (at + by + Makings.Length) % Makings.Length;
            ShowMaking(Makings[at]);
        }

        private void ShowMaking(Making making)
        {
            _making = making;
            if (_makingLabel != null) _makingLabel.text = making.ToString().ToUpper();

            if (_dwellerSection != null) _dwellerSection.SetActive(making == Making.Dweller);
            if (_petSection != null) _petSection.SetActive(making == Making.Pet);

            // Only when the bench is the thing being looked at. Building the window used to make
            // one and throw it away in the same breath.
            // The animal bench is one screenful. A bar down the side of a list that does not
            // move is a control that says something untrue about the page.
            if (_createView != null && _createView.verticalScrollBar != null)
                _createView.verticalScrollBar.gameObject.SetActive(making != Making.Pet);

            if (making == Making.Dweller && _tab == Tab.Create && _panelOpen) RemakePreview();
            else DisposePreview();

            RefreshPreview();

            if (_createView != null) _createView.ResetPosition();
        }

        /// <summary>A pet built to order: which one, called what, carrying which bonus.</summary>
        private void BuildPetSection(Transform parent, int width)
        {
            AddHeader(parent, "PET", width);

            // The same shape as the dweller's bench: the animal in a box down the left, and the
            // two things you choose about it in rows beside it. Choosing a pet by a photograph of
            // its head was choosing a pet by its passport.
            const int pad = 8;
            const int petBlock = 210;

            int blockY = _cursorY - petBlock / 2;

            int wellWidth = 186;
            int wellHeight = petBlock - pad * 2;

            Plate(parent, "PetBlock", 0, blockY, width, petBlock, Skin.Row(width, petBlock), 1);

            int wellX = -width / 2 + pad + wellWidth / 2;
            Plate(parent, "PetWell", wellX, blockY, wellWidth, wellHeight,
                  Skin.Well(wellWidth, wellHeight), 2);

            GameObject iconGo = new GameObject("PetPickIcon");
            iconGo.layer = parent.gameObject.layer;
            iconGo.transform.SetParent(parent, false);
            iconGo.transform.localPosition = new Vector3(wellX, blockY, 0f);
            iconGo.transform.localScale = Vector3.one;

            _petPickIcon = iconGo.AddComponent<UISprite>();
            _petPickIcon.depth = 3;

            // Named before the row is made: AddChoiceRow writes the caption into a label there and
            // then, and an empty caption stays empty. It is a rarity, the same word a dweller uses.
            _petGrade.Caption = "RARITY";

            int columnLeft = -width / 2 + pad + wellWidth + pad;
            int columnWidth = width - wellWidth - pad * 3;
            int columnCentre = columnLeft + columnWidth / 2;

            int rowHeight = (wellHeight - RowGap) / 2;
            int upper = blockY + wellHeight / 2 - rowHeight / 2;

            Plate(parent, "PetBreedRow", columnCentre, upper, columnWidth, rowHeight,
                  Skin.Row(columnWidth, rowHeight), 2);

            int arrow = 26;

            MakeButton(parent, "PetBack", "<", columnLeft + pad + arrow / 2, upper - 10,
                       arrow, arrow - 2, false, delegate { StepPet(-1); });
            MakeButton(parent, "PetFwd", ">", columnLeft + columnWidth - pad - arrow / 2, upper - 10,
                       arrow, arrow - 2, false, delegate { StepPet(1); });

            UILabel breed = MakeLeftLabel(parent, "PetBreedCaption", "BREED",
                                          columnLeft + 12, upper + rowHeight / 2 - 13,
                                          columnWidth - 24, 16, Skin.Bright, 3);
            breed.fontSize = TextBody;
            breed.color = new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.8f);

            _petPickLabel = MakeLabel(parent, "PetPickName", "-", columnCentre, upper - 10,
                                      columnWidth - 2 * (arrow + 20), 22, Skin.Bright, 3);
            _petPickLabel.maxLineCount = 1;

            _cursorY = blockY - petBlock / 2 + rowHeight + RowGap;
            AddCompactChoice(parent, _petGrade, columnCentre,
                             blockY - wellHeight / 2 + rowHeight / 2, columnWidth, rowHeight, false);

            _cursorY = blockY - petBlock / 2 - RowGap;

            AddHeader(parent, "NAME AND BONUS", width);

            int nameY = _cursorY - RowHeight / 2;
            Plate(parent, "PetNameRow", 0, nameY, width, RowHeight, Skin.Row(width, RowHeight), 1);
            MakeLeftLabel(parent, "PetNameCaption", "NAME", -width / 2 + 14, nameY, 110,
                          RowHeight, Skin.Bright, 3);
            _petNameInput = AddInput(parent, "PetName", 34, nameY, width - 150, "RANDOM");
            _cursorY -= RowHeight + RowGap;

            int bonusY = _cursorY - RowHeight / 2;
            Plate(parent, "PetBonusRow", 0, bonusY, width, RowHeight, Skin.Row(width, RowHeight), 1);
            MakeButton(parent, "PetBonusBack", "<", -width / 2 + 28, bonusY, 40, 32, false,
                       delegate { StepBonus(-1); });
            MakeButton(parent, "PetBonusFwd", ">", -width / 2 + 72, bonusY, 40, 32, false,
                       delegate { StepBonus(1); });
            _bonusLabel = MakeLeftLabel(parent, "PetBonusName",
                                        Tidy(Bonuses()[_petBonusIndex].ToString()).ToUpper(),
                                        -width / 2 + 98, bonusY, width - 200, RowHeight,
                                        Skin.Bright, 3);
            _petValueInput = AddInput(parent, "PetValue", width / 2 - 96, bonusY, 76, "10");

            // The strongest the game itself ever gives for this effect. A pet's bonus is one number
            // and it can be any number, but the highest the game uses is the one worth knowing.
            MakeButton(parent, "PetValueMax", "MAX", width / 2 - 32, bonusY, 52, 32, false,
                       MaxOutPetBonus);

            _cursorY -= RowHeight + RowGap;

            // The same mark as the dweller bench, in the animal's shape: the game keeps a matching
            // pair of blanks, and a blank is the right picture for a button that fills one in.
            GameObject makePet = MakeButton(parent, "CreatePet", "CREATE PET", 0, _cursorY - 22,
                                            width, 44, true, CreatePetFromPanel);

            AddBareIcon(makePet.transform, "CreatePetMark",
                        new[] { "Silhouette_Pet", "Icon_Pet", "PetCarrier" },
                        "silhouette pet", width / 2 - 32, 0, 30, Skin.Ink);

            _cursorY -= 44 + RowGap;

            MakeLabel(parent, "PetNote", "Goes straight into the vault's storage.",
                      0, _cursorY - 13, width, 26, Skin.Rim, 3);
            _cursorY -= 26 + RowGap;

            RefreshPetPick();
        }

        private readonly Choice _petGrade = new Choice();

        private void StepPet(int by)
        {
            if (_petGroups == null) { BuildPetCatalogue(); GroupPets(); }
            if (_petGroups == null || _petGroups.Count == 0) return;

            _petIndex = (_petIndex + by + _petGroups.Count) % _petGroups.Count;
            RefreshPetPick();
        }

        /// <summary>The versions of the chosen animal, strongest last.</summary>
        /// <summary>
        /// The rarities this animal comes in.
        ///
        /// The same animal is filed once per rarity, and 'grade' was a word for that filing rather
        /// than for anything a player thinks about. It is a rarity, chosen the way a dweller's is,
        /// and which copy sits behind it is this side's business.
        /// </summary>
        private void RefillGrades()
        {
            _petGrade.Caption = "RARITY";
            _petGrade.Begin("any");

            PetGroup group = CurrentPetGroup();
            if (group == null) return;

            for (int i = 0; i < PetRarities.Length; i++)
            {
                PetEntry match = null;

                for (int j = 0; j < group.Variants.Count; j++)
                {
                    if (group.Variants[j].Rarity != PetRarities[i]) continue;
                    match = group.Variants[j];
                    break;
                }

                if (match == null) continue;
                // The rarity, and nothing else. What the animal does is the bonus row's business; this
                // row was asked twice to say only how the game grades it.
                _petGrade.Add(match, RarityWord(PetRarities[i]));
            }

            _petGrade.Show();
        }

        private static readonly EItemRarity[] PetRarities =
        {
            EItemRarity.Common, EItemRarity.Normal, EItemRarity.Rare, EItemRarity.Legendary
        };

        /// <summary>
        /// The three words a player knows a pet by.
        ///
        /// The table has four names and two of them mean the same thing to anyone looking at a cat:
        /// Common and Normal are both the plain one. Three words is what the game shows and three is
        /// what this shows.
        /// </summary>
        private static string RarityWord(EItemRarity rarity)
        {
            switch (rarity)
            {
                case EItemRarity.Rare:      return "RARE";
                case EItemRarity.Legendary: return "LEGENDARY";
                default:                    return "COMMON";
            }
        }

        private PetGroup CurrentPetGroup()
        {
            if (_petGroups == null || _petGroups.Count == 0) return null;

            _petIndex = Mathf.Clamp(_petIndex, 0, _petGroups.Count - 1);
            return _petGroups[_petIndex];
        }

        private void RefreshPetPick()
        {
            PreloadPetArt();
            if (_pets == null) BuildPetCatalogue();
            if (_petGroups == null) GroupPets();

            PetGroup group = CurrentPetGroup();
            if (group == null)
            {
                if (_petPickLabel != null) _petPickLabel.text = "no pets in the catalogue";
                return;
            }

            // The animal and where you are in the list, and nothing about grade. This row picks a
            // creature; the row beneath it picks how rare that creature is, and a rarity printed
            // here was the best grade the animal happens to come in -- which is neither what is
            // being chosen nor what will be made.
            if (_petPickLabel != null)
                _petPickLabel.text = group.Name + "   " + (_petIndex + 1) + "/" + _petGroups.Count;

            _wantWholeAnimal = true;
            try { ShowPetIcon(_petPickIcon, group.Best); }
            finally { _wantWholeAnimal = false; }

            RefillGrades();
        }

        /// <summary>
        /// Fills the value in with the best the game gives for this effect.
        ///
        /// A pet carries exactly one bonus and one number — the game compares its single Bonus with
        /// the effect being asked about and returns its single BonusValue, so a second bonus would
        /// never be read. What can be maxed is the number, and the honest ceiling is the largest one
        /// the game hands out for that effect anywhere in its own catalogue.
        /// </summary>
        private void MaxOutPetBonus()
        {
            if (_petValueInput == null) return;

            try
            {
                if (_pets == null) BuildPetCatalogue();

                EBonusEffect wanted = Bonuses()[_petBonusIndex];
                float best = 0f;

                if (_pets != null)
                {
                    for (int i = 0; i < _pets.Count; i++)
                    {
                        Array bonuses = ReadObject(_pets[i].Template, "BonusEffectList") as Array;
                        if (bonuses == null) continue;

                        for (int j = 0; j < bonuses.Length; j++)
                        {
                            object bonus = bonuses.GetValue(j);
                            if (bonus == null) continue;

                            object effect = ReadObject(bonus, "Effect");
                            if (effect == null || !effect.Equals(wanted)) continue;

                            float high = ReadFloat(bonus, "MaxValue");
                            if (high > best) best = high;
                        }
                    }
                }

                if (best <= 0f)
                {
                    Trouble("The game never gives " + BonusText(wanted, "?") +
                            " to a pet; type a number instead.");
                    return;
                }

                _petValueInput.value = Figure(best);
                _petBonusValue = _petValueInput.value;

                Say("The most the game gives for that is " + Figure(best) + ".");
            }
            catch (Exception e)
            {
                Trouble("Could not find the best value: " + e.Message);
            }
        }

        private void CreatePetFromPanel()
        {
            PetGroup group = CurrentPetGroup();
            if (group == null || group.Variants.Count == 0) return;

            if (_petNameInput != null) _petName = _petNameInput.value;
            if (_petValueInput != null && !string.IsNullOrEmpty(_petValueInput.value))
                _petBonusValue = _petValueInput.value;

            PetEntry chosen = _petGrade.Selected as PetEntry;
            if (chosen == null)
                chosen = group.Variants[UnityEngine.Random.Range(0, group.Variants.Count)];

            GrantPet(chosen, true);
        }

        private readonly Choice _hair = new Choice();
        private readonly Choice _face = new Choice();
        private readonly Choice _hairColour = new Choice();
        private readonly Choice _helmet = new Choice();
        private readonly Choice _skin = new Choice();
        private readonly Choice _outfit = new Choice();
        private readonly Choice _weapon = new Choice();

        /// <summary>
        /// Fills the appearance lists from the game's own customisation catalogue.
        ///
        /// This is the same table the barbershop works from, so everything offered here is something
        /// the game already knows how to put on a dweller — and applying it goes through the game's
        /// own ApplyCustomization rather than writing to fields and hoping the picture catches up.
        /// </summary>
        private void RebuildLookOptions()
        {
            EGender gender = Genders[_genderIndex];

            _hair.Caption = "HAIR";
            _face.Caption = "FACE";
            _hairColour.Caption = "HAIR COLOUR";
            _helmet.Caption = "HEADGEAR";
            _skin.Caption = "SKIN";
            _outfit.Caption = "OUTFIT";
            _weapon.Caption = "WEAPON";

            _hair.BeginBare();
            _face.BeginBare();
            _hairColour.BeginBare();
            _colourWordsUsed.Clear();
            _helmet.Begin("none");
            _skin.BeginBare();

            try
            {
                Catalog catalog = Catalog.Instance;
                object data = catalog != null ? ReadObject(catalog, "m_dwellerCustomizationData") : null;
                Array all = data != null
                    ? ReadObject(data, "DwellerCustomizationAttributeDataList") as Array
                    : null;

                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        object entry = all.GetValue(i);
                        if (entry == null) continue;

                        object entryGender = ReadObject(entry, "Gender");
                        if (entryGender != null && entryGender.ToString() != gender.ToString() &&
                            entryGender.ToString() != "None")
                            continue;

                        string kind = ReadAsText(entry, "Attribute");
                        string label = LookLabel(entry);

                        if (kind == "Hair") _hair.Add(entry, StyleName(label));
                        else if (kind == "Face") _face.Add(entry, label);
                        else if (kind == "HairColor") _hairColour.Add(entry, HairColourName(entry, label));
                        else if (kind == "Helmet") _helmet.Add(entry, label);
                    }
                }

                // Skin is not one of the barbershop's four; the palette lives with the body pieces.
                DwellerPieceList pieces = catalog != null ? catalog.GetCatalogForGender(gender) : null;
                Array skins = pieces != null ? ReadObject(pieces, "m_skinColors") as Array : null;

                if (skins != null)
                {
                    // Every shade the game has, and nothing standing for "leave it alone" --
                    // leaving it alone is what made the figure and the dweller disagree.
                    for (int i = 0; i < skins.Length; i++)
                        _skin.Add(skins.GetValue(i), "SHADE " + (i + 1));
                }
            }
            catch (Exception e)
            {
                ReportOnce("looks", "Could not read the appearance catalogue: " + e.Message);
            }

            _hair.Show();
            _face.Show();
            _hairColour.Show();
            _helmet.Show();
            _skin.Show();

            // A fresh set of lists has put every slot back to nought. Rolling here means the page
            // is never sitting on the value that applies nothing, including the very first time it
            // is opened and after every change of gender.
            if (_rollWhenReady) RollTheLooksQuietly();

            Log.LogInfo("Appearance for " + gender + ": " + _hair.Options.Count + " hair, " +
                        _face.Options.Count + " face, " +
                        _hairColour.Options.Count + " hair colour, " +
                        (_helmet.Options.Count - 1) + " headgear, " +
                        _skin.Options.Count + " skin.");
        }

        /// <summary>Puts the chosen item's own picture in the row that chose it.</summary>
        private void ShowChoicePicture(Choice choice)
        {
            if (choice.Picture == null) return;

            CatalogueEntry entry = choice.Selected as CatalogueEntry;
            if (entry == null)
            {
                choice.Picture.gameObject.SetActive(false);
                return;
            }

            choice.Picture.gameObject.SetActive(true);
            ShowIcon(choice.Picture, entry);

            if (choice == _outfit || choice == _weapon) FitSprite(choice.Picture, 46);
        }

        /// <summary>
        /// A hairstyle's name, given that half of them have none.
        ///
        /// The game has no word for these: _DwellerCustomization_Hair_Male_01 localises to "01" and
        /// that is the whole of it. A bare 03 in a row captioned HAIR is not wrong so much as
        /// unhelpful, so the number is given something to be the number of. The one whose piece is
        /// literally called null is the absence of hair, and says so.
        /// </summary>
        private static string StyleName(string label)
        {
            if (string.IsNullOrEmpty(label)) return "STYLE ?";
            if (label.Equals("null", StringComparison.OrdinalIgnoreCase)) return "BALD";

            for (int i = 0; i < label.Length; i++)
                if (!char.IsDigit(label[i])) return label;

            return "STYLE " + label;
        }

        private string LookLabel(object entry)
        {
            string key = ReadMember(entry, "TitleTextId");

            string title = GameText(key);
            if (string.IsNullOrEmpty(title)) title = Localised(key);
            if (!string.IsNullOrEmpty(title)) return title;

            // Whatever is left is a file name. Splitting it into words and dropping the parts that
            // only mean something to whoever filed it is better than showing it raw.
            string piece = ReadMember(entry, "PieceName");
            if (string.IsNullOrEmpty(piece)) piece = ReadMember(entry, "Id");
            if (string.IsNullOrEmpty(piece)) return "?";

            return Tidy(piece);
        }

        /// <summary>Turns a file name into something worth reading.</summary>
        private static string Tidy(string name)
        {
            string[] words = Meaningful(name);
            if (words.Length == 0) words = SplitWords(name);
            if (words.Length == 0) return name;

            string line = "";
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (word.Length == 0) continue;

                line += (line.Length > 0 ? " " : "") +
                        char.ToUpper(word[0]) + (word.Length > 1 ? word.Substring(1) : "");
            }

            return line.Length > 0 ? line : name;
        }

        /// <summary>
        /// A hair colour named after the colour it is.
        ///
        /// The catalogue's own label is the name of the group the entry belongs to, not of the
        /// entry: twenty-one shades all called "Hair Male" and "Hair Male Customization Only" in
        /// turn. The colour itself is right there on the record and already resolved for the
        /// swatch, so the name is worked out from that instead of read off a heading.
        /// </summary>
        private string HairColourName(object entry, string fallback)
        {
            Color? shade = ColourOf(entry);
            if (shade == null) return fallback;

            string word = ColourWord(shade.Value);

            // Twenty-one shades will not yield twenty-one words, and three rows all reading BROWN
            // is the same complaint in a different form. The repeats are numbered in the order the
            // catalogue lists them, so each row names a colour and no two rows name it the same.
            int seen;
            _colourWordsUsed.TryGetValue(word, out seen);
            _colourWordsUsed[word] = seen + 1;

            return seen == 0 ? word : word + " " + (seen + 1);
        }

        private readonly Dictionary<string, int> _colourWordsUsed = new Dictionary<string, int>();

        /// <summary>
        /// The word a person would use for a colour, leaning towards the words used about hair.
        ///
        /// Hue alone is not enough: brown, ginger and blonde are all the same orange, separated
        /// only by how dark and how strong it is. Grey and white have no hue worth reading at all,
        /// so they are settled on brightness before hue is consulted.
        /// </summary>
        private static string ColourWord(Color shade)
        {
            float hue, sat, value;
            Color.RGBToHSV(shade, out hue, out sat, out value);

            float degrees = hue * 360f;

            if (sat < 0.12f)
            {
                if (value < 0.18f) return "BLACK";
                if (value < 0.40f) return "DARK GREY";
                if (value < 0.65f) return "GREY";
                if (value < 0.88f) return "SILVER";
                return "WHITE";
            }

            if (value < 0.16f) return "BLACK";

            if (degrees < 16f || degrees >= 345f) return value < 0.45f ? "AUBURN" : "RED";

            if (degrees < 42f)
            {
                if (value < 0.30f) return "DARK BROWN";
                if (value < 0.55f) return "BROWN";
                if (sat > 0.55f) return "GINGER";
                return "LIGHT BROWN";
            }

            if (degrees < 68f) return value > 0.75f ? "BLONDE" : "DARK BLONDE";
            if (degrees < 165f) return "GREEN";
            if (degrees < 200f) return "TEAL";
            if (degrees < 260f) return "BLUE";
            if (degrees < 300f) return "PURPLE";

            return "PINK";
        }

        /// <summary>
        /// The colour an appearance entry stands for.
        ///
        /// A hair colour is filed either as a shade of its own or as an index into the palette for
        /// this gender, and either way it is a colour — which is the only useful way to show it.
        /// </summary>
        private Color? ColourOf(object entry)
        {
            if (entry == null) return null;

            try
            {
                object custom = ReadObject(entry, "IsCustomColor");
                if (custom is bool && (bool)custom)
                {
                    object shade = ReadObject(entry, "CustomColor");
                    if (shade is Color) return (Color)shade;
                }

                object indexed = ReadObject(entry, "ColorId");
                if (indexed == null) return null;

                int index = Convert.ToInt32(indexed);

                Catalog catalog = Catalog.Instance;
                DwellerPieceList pieces = catalog == null
                    ? null
                    : catalog.GetCatalogForGender(Genders[_genderIndex]);

                object only = ReadObject(entry, "IsOnlyInCustomization");
                bool forCustomisation = only is bool && (bool)only;

                Array palette = pieces == null
                    ? null
                    : ReadObject(pieces, forCustomisation
                                     ? "m_hairColorsForCustomization"
                                     : "m_hairColors") as Array;

                if (palette != null && index >= 0 && index < palette.Length)
                {
                    object shade = palette.GetValue(index);
                    if (shade is Color) return (Color)shade;
                }
            }
            catch { }

            return null;
        }

        /// <summary>The gear lists, which do not depend on the gender.</summary>
        private void RebuildGearOptions()
        {
            if (_catalogue == null) BuildCatalogue();

            _outfit.Begin("none");
            _weapon.Begin("none");

            if (_catalogue == null) return;

            for (int i = 0; i < _catalogue.Count; i++)
            {
                CatalogueEntry entry = _catalogue[i];
                if (entry.Type == EItemType.Outfit) _outfit.Add(entry, entry.Name);
                else if (entry.Type == EItemType.Weapon) _weapon.Add(entry, entry.Name);
            }

            _outfit.Show();
            _weapon.Show();
        }

        /// <summary>Puts the chosen look and gear on a dweller that has just been made.</summary>
        private void ApplyLooks(Dweller dweller)
        {
            if (dweller == null) return;

            ApplyOneLook(dweller, _hair);
            ApplyOneLook(dweller, _face);
            ApplyOneLook(dweller, _hairColour);

            // Headgear is the one look that can honestly be nothing, and nothing has to be written
            // as well as chosen: ApplyCustomization is only ever told what to put on, so a dweller
            // asked for no helmet would otherwise keep whatever the game had given it.
            if (_helmet.Selected == null)
            {
                try { WriteMember(dweller, "m_helmet", null); }
                catch { }
            }
            else ApplyOneLook(dweller, _helmet);

            try
            {
                object shade = _skin.Selected;
                if (shade is Color) dweller.SkinColor = (Color)shade;
            }
            catch (Exception e)
            {
                Log.LogWarning("Setting the skin colour failed: " + e.Message);
            }

            Equip(dweller, _outfit, EItemType.Outfit);
            Equip(dweller, _weapon, EItemType.Weapon);
        }

        private void ApplyOneLook(Dweller dweller, Choice choice)
        {
            object chosen = choice.Selected;
            if (chosen == null) return;

            try
            {
                MethodInfo apply = typeof(Dweller).GetMethod(
                    "ApplyCustomization",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { chosen.GetType() }, null);

                if (apply == null)
                {
                    ReportOnce("applylook", "The game has no ApplyCustomization for " +
                                            chosen.GetType().Name + ".");
                    return;
                }

                apply.Invoke(dweller, new[] { chosen });
            }
            catch (Exception e)
            {
                Log.LogWarning("Applying " + choice.Caption + " failed: " + e.Message);
            }
        }

        /// <summary>
        /// Counts what the vault is holding, or -1 if it cannot be counted.
        /// </summary>
        private int CountStorage()
        {
            try
            {
                Vault vault = SafeVault();
                VaultInventory inventory = vault == null ? null : vault.Inventory;

                return inventory == null || inventory.Items == null ? -1 : inventory.Items.Count;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Takes back anything that appeared in the vault's storage since it was counted.
        ///
        /// This is the whole of the weapon bug. Dressing the stand-in calls the game's own
        /// EquipWeapon, and the game does what it always does when a dweller's weapon is swapped:
        /// it puts the old one back in storage. The old one was fabricated by this panel and never
        /// came from storage, so every re-dress left a real weapon behind -- and the bench
        /// re-dresses on every gender change, every visit to the page, and every creation. Thirty
        /// or fifty of them is an afternoon's work.
        ///
        /// The fix is not to stop dressing the figure but to leave the storage as it was found.
        /// </summary>
        private void PutStorageBack(int was, string mintedId)
        {
            if (was < 0) return;

            try
            {
                Vault vault = SafeVault();
                VaultInventory inventory = vault == null ? null : vault.Inventory;
                if (inventory == null || inventory.Items == null) return;

                int extra = inventory.Items.Count - was;
                if (extra <= 0) return;

                int taken = 0;

                for (int i = 0; i < extra; i++)
                {
                    int last = inventory.Items.Count - 1;
                    if (last < was) break;

                    DwellerItem leftover = inventory.Items[last];

                    // Only what this bench minted. Deleting by position alone assumed the newest
                    // row was ours, and the newest row belongs to whoever put it there -- a
                    // finished craft, a squad home from the wasteland. This code deletes from the
                    // player's save, so it is going to be sure first.
                    string id = ReadAsText(leftover, "Id");

                    if (id != mintedId)
                    {
                        Log.LogWarning("Something else reached storage while the bench was " +
                                       "dressing ('" + id + "', expected '" + mintedId +
                                       "'); leaving it alone.");
                        break;
                    }

                    int before = inventory.Items.Count;

                    // The game's own removal if it has one that takes an item; the list itself if
                    // not -- and either way, believed only if the count actually falls.
                    if (!TookItBack(inventory, leftover) && inventory.Items.Count == before)
                        inventory.Items.RemoveAt(last);

                    if (inventory.Items.Count >= before) break;
                    taken++;
                }

                if (taken > 0)
                    Log.LogInfo("Took back " + taken + " item(s) the dressing table left in storage.");
            }
            catch (Exception e)
            {
                ReportOnce("putback", "Could not take back what the bench left in storage: " + e);
            }
        }

        private bool TookItBack(VaultInventory inventory, DwellerItem leftover)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance;

            string[] names = { "RemoveItem", "Remove", "DestroyItem", "DeleteItem" };

            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    MethodInfo go = inventory.GetType().GetMethod(
                        names[i], Flags, null, new[] { typeof(DwellerItem) }, null);

                    if (go == null) continue;

                    // Believed only if it says so. A removal that returns false, or that removes
                    // by reference and finds nothing, used to be reported as a success -- and the
                    // caller then skipped its own fallback, so nothing was removed and the log
                    // said otherwise.
                    object said = go.Invoke(inventory, new object[] { leftover });
                    if (said is bool && !(bool)said) continue;

                    return true;
                }
                catch { }
            }

            if (!_reportedInventoryShape)
            {
                _reportedInventoryShape = true;

                try
                {
                    System.Text.StringBuilder said = new System.Text.StringBuilder();
                    said.Append("VaultInventory offers no removal taking a DwellerItem; it has:");

                    MethodInfo[] all = inventory.GetType().GetMethods(Flags);
                    for (int i = 0; i < all.Length; i++)
                        said.Append(" ").Append(all[i].Name);

                    Log.LogWarning(said.ToString());
                }
                catch { }
            }

            return false;
        }

        private static bool _reportedInventoryShape;

        /// <summary>
        /// Puts a stand-in back to a plain random person before the bench's choices go on it.
        ///
        /// A stand-in is kept per gender and dressed again each time the page is shown, and what
        /// it was wearing last time survived every slot the bench had since set back to random --
        /// ApplyCustomization is told what to put on, never what to take off. So the figure went
        /// on showing the dweller that had already been created and walked away, while the fields
        /// beneath it described somebody else entirely.
        ///
        /// Randomising first makes random mean random. Anything actually chosen is applied over
        /// the top a moment later and wins, as it should.
        /// </summary>
        private void StartFromScratch(Dweller who)
        {
            if (who == null) return;

            // This method changes what the dweller is made of, so what was drawn of it is stale by
            // definition.
            _texturedOnce = false;

            // Emptied first. GenerateRandomCustomization fills a dweller in, and a slot that is
            // already filled is not a slot it has to fill -- which is how the created dweller's
            // hair and face survived being randomised and went on standing there after the bench
            // had been cleared. These are the field names the mod's own piece report prints, not
            // guesses.
            string[] slots = { "m_hair", "m_face", "m_faceMask", "m_body",
                               "m_helmet", "m_overrideFace", "m_helmetCoverCustomization" };

            for (int i = 0; i < slots.Length; i++)
            {
                try { WriteMember(who, slots[i], null); }
                catch { }
            }

            try
            {
                MethodInfo dress = typeof(Dweller).GetMethod(
                    "GenerateRandomCustomization",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (dress != null)
                    dress.Invoke(who, new object[] { true, null, null, null });
                else
                    ReportOnce("dressup", "The game has no GenerateRandomCustomization.");
            }
            catch (Exception e)
            {
                ReportOnce("dressup", "Could not give the stand-in a body: " + e.Message);
            }

            // Nothing chosen means nothing held. Taking it off hands it to storage, which is why
            // this is counted like every other dressing the bench does.
            try
            {
                if (_weapon.Selected == null && who.EquippedWeapon != null)
                {
                    int was = CountStorage();
                    string mintedId = ReadAsText(who.EquippedWeapon, "Id");

                    who.EquipWeapon(null);
                    PutStorageBack(was, mintedId);
                }
            }
            catch (Exception e)
            {
                ReportOnce("disarm", "Could not take the stand-in's weapon off: " + e.Message);
            }

            // UpdateTexture draws the body out of the outfit being worn: with m_outfit null it
            // composes the head and stops, which is exactly what the picture showed. The vault's
            // own default is what a dweller wears when it wears nothing.
            try
            {
                // Nothing chosen means the vault's own plain outfit, not whatever the figure
                // happened to be wearing. A dweller has to wear something or the shader composes a
                // head and stops -- but "something" after a reset is a jumpsuit, not the coat the
                // last creation went out in.
                if (string.IsNullOrEmpty(_defaultOutfitId)) BuildCatalogue();

                string plain = string.IsNullOrEmpty(_defaultOutfitId)
                    ? "jumpsuit"
                    : _defaultOutfitId;

                DwellerItem worn = who.EquippedOutfit;
                bool wrong = worn == null || (_outfit.Selected == null &&
                                              ReadAsText(worn, "Id") != plain);

                if (wrong)
                {
                    int was = CountStorage();
                    string mintedId = worn == null ? null : ReadAsText(worn, "Id");

                    who.EquipOutfit(new DwellerItem(EItemType.Outfit, plain), false);
                    PutStorageBack(was, mintedId);
                }
            }
            catch (Exception e)
            {
                ReportOnce("dressplain", "Could not dress the stand-in: " + e.Message);
            }
        }

        private void Equip(Dweller dweller, Choice choice, EItemType type)
        {
            CatalogueEntry entry = choice.Selected as CatalogueEntry;
            if (entry == null) return;

            try
            {
                // Already wearing it. Re-equipping the same thing is not free: the game hands the
                // old copy back to storage, so the cheapest fix for most of the churn is not to do
                // the work at all.
                DwellerItem worn = type == EItemType.Outfit
                    ? dweller.EquippedOutfit
                    : dweller.EquippedWeapon;

                if (worn != null && ReadAsText(worn, "Id") == entry.Id) return;

                DwellerItem item = new DwellerItem(type, entry.Id);

                // Counted before and put back after, because the game returns whatever was being
                // worn to the vault -- and on the bench, what was being worn never came from there.
                int was = dweller == _previewDweller ? CountStorage() : -1;
                string mintedId = worn == null ? null : ReadAsText(worn, "Id");

                if (type == EItemType.Outfit) dweller.EquipOutfit(item, false);
                else dweller.EquipWeapon(item);

                PutStorageBack(was, mintedId);
            }
            catch (Exception e)
            {
                Log.LogWarning("Equipping " + entry.Name + " failed: " + e.Message);
            }
        }

        private Dweller _previewDweller;
        private bool _reportedPieces;
        private UITexture _previewPicture;
        private UITexture _previewHeadgear;

        /// <summary>
        /// Makes the person in the picture — who is nobody, and never joins the vault.
        ///
        /// A dweller's picture is composed at runtime from the pieces being worn, so something has
        /// to exist to be drawn. Committing a real one to the queue first was the wrong way round;
        /// the game keeps a second kind for exactly this, an NPC dweller that belongs to no vault
        /// and is thrown away by name when it is done with. The one in the picture is that. Pressing
        /// create makes the real one, and this one is torn down when the bench is left.
        /// </summary>
        private Dweller EnsurePreview()
        {
            if (_previewDweller != null) return _previewDweller;

            try
            {
                DwellerManager manager = SafeDwellerManager();
                if (manager == null) return null;

                // Where the game gets the figures for its own display windows:
                // CreateDwellerToDisplay is nothing but DwellerPool.GetInstance and a customisation
                // applied on top. A dweller built any other way came out as a head, because a
                // pooled one is the one that arrives already assembled.
                DwellerPool pool = DwellerPool.Instance;
                if (pool == null)
                {
                    ReportOnce("previewmake", "The dweller pool is not up yet; nothing to draw.");
                    return null;
                }

                MethodInfo make = typeof(DwellerPool).GetMethod(
                    "GetInstance",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(EGender) }, null);

                if (make == null)
                {
                    ReportOnce("previewmake", "The pool has no GetInstance; nothing to draw.");
                    return null;
                }

                // The one made for this gender last time, if it is still around.
                string kind = Genders[_genderIndex].ToString();

                Dweller kept;
                if (_standIns.TryGetValue(kind, out kept) && kept != null)
                {
                    _previewDweller = kept;
                    // Dressed again from the panel: it was put away wearing whatever was chosen
                    // last time, and the choices on screen are what it should be wearing now.
                    Silence(kept.gameObject, "DwellerVisibilityDetector");

                    StartFromScratch(_previewDweller);
                    ApplyLooks(_previewDweller);

                    Log.LogInfo("Reusing the stand-in for " + kind + ".");
                    return _previewDweller;
                }

                _previewDweller = make.Invoke(pool, new object[] { Genders[_genderIndex] }) as Dweller;

                if (_previewDweller != null)
                {
                    _standIns[kind] = _previewDweller;

                    int taken = Silence(_previewDweller.gameObject, "DwellerVisibilityDetector");
                    Log.LogInfo("Took " + taken + " visibility watcher(s) off the stand-in.");
                }

                // A pooled dweller has no pieces on it. UpdateTexture does not compose one picture —
                // it hands the shader a texture per piece, hair here, face there — so with nothing
                // assigned the shader has nothing to assemble and draws white. This is the call the
                // spawner makes to fill them in.
                if (_previewDweller != null) StartFromScratch(_previewDweller);

                if (_previewDweller == null)
                {
                    ReportOnce("previewmake", "The game did not make anyone to draw.");
                    return null;
                }

                // It exists to be looked at in a panel, not to be seen standing in the vault.
                try
                {
                    MethodInfo hide = typeof(Dweller).GetMethod(
                        "SetInVisible",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null, Type.EmptyTypes, null);

                    if (hide != null) hide.Invoke(_previewDweller, null);
                }
                catch (Exception e)
                {
                    ReportOnce("previewhide", "Could not hide the stand-in: " + e.Message);
                }

                ApplyLooks(_previewDweller);
                Log.LogInfo("Made a stand-in to draw: " + Genders[_genderIndex] + ", " +
                            Rarities[_rarityIndex] + ".");
            }
            catch (Exception e)
            {
                ReportOnce("previewmake", "Could not make anyone to draw: " + e.Message);
            }

            return _previewDweller;
        }

        /// <summary>Puts a stand-in back on its own layer, where it stood.</summary>
        private void PutTheFigureBack(GameObject body)
        {
            if (body == null || _standInLayer < 0) return;

            SetLayer(body.transform, _standInLayer);
            body.transform.position = _standInHome;
            _standInLayer = -1;
        }

        /// <summary>
        /// Puts the stand-in away without giving it back.
        ///
        /// Handing it to the pool crashed the game twice. The pool switches the object off as it
        /// takes it, that makes DwellerVisibilityDetector fire, and on a borrowed dweller that
        /// detector has nothing to work with — restoring the layer and silencing the component did
        /// not stop it. So it is not handed back at all: it is put to sleep and kept, one per
        /// gender, to be woken again the next time the bench is opened. Two objects held for a
        /// session is a small price for a game that does not stop.
        /// </summary>
        private void DisposePreview()
        {
            if (_previewDweller == null) return;

            try
            {
                GameObject body = _previewDweller.gameObject;

                // The next stand-in is a different figure and deserves its own framing. Kept, the
                // camera held the first one's height and centre for the rest of the session, so a
                // change of gender put the new figure slightly off.
                _framedSize = -1f;

                PutTheFigureBack(body);
                body.SetActive(false);
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not put the stand-in away: " + e.Message);
            }

            // The next figure needs its picture composed from scratch, whether it is a new one
            // or the same one wearing something else. Without this the bench was dressed anew and
            // then drawn as it used to be.
            _texturedOnce = false;

            // A new stand-in is a new set of animators, and the old readings belong to figures
            // that no longer exist.
            _lastBeat.Clear();
            _posed = false;
            _framedLocked = false;

            _previewDweller = null;
            RefreshPreview();
        }

        /// <summary>The gender and rarity are settled when someone is made, so a change starts again.</summary>
        private void RemakePreview()
        {
            DisposePreview();

            if (_tab == Tab.Create && _making == Making.Dweller && _panelOpen) EnsurePreview();
            RefreshPreview();
        }

        /// <summary>
        /// Draws the dweller being built, the way the game draws one.
        ///
        /// SetUITex is the game's own call for this: it is what the character window, the dweller
        /// list and the room panels all use to turn a dweller into a picture.
        /// </summary>
        private Camera _previewCamera;
        private RenderTexture _previewFilm;

        // What the stand-in was before it was borrowed. Handing it back in the state we found it
        // is the difference between a returned dweller and a crash.
        private readonly Dictionary<string, Dweller> _standIns = new Dictionary<string, Dweller>();
        private int _standInLayer = -1;
        private Vector3 _standInHome;

        // A layer of its own, so the camera that films the stand-in sees nothing else and nothing
        // else sees the stand-in.
        // Chosen by asking, not by picking a number that looked unused: an occupied layer would
        // put whatever else lives on it into the picture.
        private int _previewLayer = -1;

        private int PreviewLayer()
        {
            if (_previewLayer >= 0) return _previewLayer;

            for (int layer = 31; layer >= 8; layer--)
            {
                if (!string.IsNullOrEmpty(LayerMask.LayerToName(layer))) continue;

                _previewLayer = layer;
                Log.LogInfo("The stand-in will be filmed on layer " + layer + ", which is unnamed.");
                return _previewLayer;
            }

            _previewLayer = 31;
            Log.LogWarning("Every layer is named; filming on 31 and hoping it is quiet.");
            return _previewLayer;
        }

        /// <summary>
        /// Sets up a camera pointed at nothing in particular, to film the stand-in.
        ///
        /// SetUITex draws a head and is meant to: the game's own picture widget for it is ninety by
        /// ninety, a square, and a square is a portrait. The full-height figure in the character
        /// window and the barbershop is the actual dweller, filmed. So this films one too.
        /// </summary>
        private void EnsurePreviewCamera()
        {
            if (_previewCamera != null) return;

            try
            {
                // The camera's object dies with the scene; this does not. Without letting the
                // old one go first, every scene load stranded a quarter of a megabyte.
                if (_previewFilm != null)
                {
                    _previewFilm.Release();
                    UnityEngine.Object.Destroy(_previewFilm);
                }

                _previewFilm = new RenderTexture(256, 512, 16, RenderTextureFormat.ARGB32);
                _previewFilm.name = "VaultAdmin_Preview";
                _previewFilm.Create();

                GameObject go = new GameObject("VaultAdmin_PreviewCamera");
                go.transform.position = new Vector3(0f, -8000f, -10f);

                _previewCamera = go.AddComponent<Camera>();
                _previewCamera.orthographic = true;
                _previewCamera.orthographicSize = 1.2f;
                _previewCamera.nearClipPlane = 0.01f;
                _previewCamera.farClipPlane = 100f;
                _previewCamera.clearFlags = CameraClearFlags.SolidColor;
                _previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _previewCamera.cullingMask = 1 << PreviewLayer();
                _previewCamera.targetTexture = _previewFilm;

                // Filmed on demand, not sixty times a second for a picture that rarely changes.
                _previewCamera.enabled = false;

                Log.LogInfo("A camera is standing by to film the stand-in.");
            }
            catch (Exception e)
            {
                ReportOnce("previewcamera", "Could not set up the camera: " + e.Message);
            }
        }

        /// <summary>Moves the stand-in in front of the camera, frames it, and takes one picture.</summary>
        private bool FilmStandIn()
        {
            if (_previewDweller == null) return false;

            EnsurePreviewCamera();
            if (_previewCamera == null || _previewFilm == null) return false;

            // A render texture can be dropped from under you when the graphics device resets.
            if (!_previewFilm.IsCreated() && !_previewFilm.Create()) return false;

            GameObject body = _previewDweller.gameObject;

            try
            {
                if (_standInLayer < 0)
                {
                    _standInLayer = body.layer;
                    _standInHome = body.transform.position;
                }

                // Far from the vault, on a layer only this camera looks at.
                body.transform.position = new Vector3(0f, -8000f, 0f);
                body.transform.rotation = Quaternion.identity;
                SetLayer(body.transform, PreviewLayer());

                if (!body.activeSelf) body.SetActive(true);

                // Rebuilt when something about the look changes, not sixty times a second: the
                // composition does not move on its own, only the animation does.
                if (!_texturedOnce)
                {
                    _texturedOnce = true;
                    Call(_previewDweller, "SetupTexture");
                    Call(_previewDweller, "ForceUpdateTexture", true);
                }

                // Standing before measuring. This used to run the other way about, and a dweller
                // out of the pool arrives in whatever pose it was left in — the one in the report
                // was lying back with its legs out. The camera framed that, locked to it, and then
                // the figure stood up inside a frame cut for someone lying down: too small, and
                // then suddenly the right size when a change of gender built a new one.
                KeepItCheerful();
                KeepItMoving(body);

                // Framed on what is actually there, so a tall dweller and a child both fit. Held
                // loosely until the idle is running, and fixed from then on: re-framing every shot
                // made the figure breathe in and out, and framing it before it had stood up was
                // worse.
                if (_framedSize <= 0f || !_framedLocked)
                {
                    Bounds seen;
                    if (!MeasureRenderers(body, out seen)) return false;

                    _framedSize = Mathf.Max(0.2f, seen.extents.y * 1.12f);
                    _framedAt = new Vector3(seen.center.x, seen.center.y, seen.center.z - 10f);

                    if (_posed) _framedLocked = true;
                }

                _previewCamera.transform.position = _framedAt;
                _previewCamera.orthographicSize = _framedSize;

                // Last look before the shutter. Anything that throws when it is seen must be gone
                // by now, and if it is not, no picture is worth the game stopping for.
                int late = Silence(body, "DwellerVisibilityDetector");
                if (late > 0)
                    Log.LogWarning("Took " + late + " more visibility watcher(s) off just in time.");

                if (StillWatching(body))
                {
                    ReportOnce("watcher", "A visibility watcher is still on the stand-in; " +
                                          "not filming, because it stops the game when it is seen.");
                    return false;
                }

                _previewCamera.Render();

                if (!_reportedFilm)
                {
                    _reportedFilm = true;
                    Log.LogInfo("Filmed the stand-in at camera size " +
                                _previewCamera.orthographicSize + ".");
                }
                return true;
            }
            catch (Exception e)
            {
                ReportOnce("filming", "Could not film the stand-in: " + e.Message);
                return false;
            }
            finally
            {
                // Left awake on purpose. A dweller that is switched off does not animate, and the
                // figure was a photograph; kept running it idles the way one in the vault does. It
                // is out of sight regardless — far from the vault, on a layer only this camera
                // looks at — and it is put to sleep when the bench is left.
            }
        }

        /// <summary>
        /// Takes a component off the stand-in for good, by name.
        ///
        /// DwellerVisibilityDetector throws the moment anything sees the stand-in or stops seeing
        /// it — it threw when the pool switched the object off, and it threw again when this mod's
        /// own camera looked at it. Disabling was not enough and would not have been honest anyway:
        /// the stand-in is never given back, so the watcher on it has nothing left to watch.
        /// </summary>
        private static int Silence(GameObject body, string component)
        {
            int taken = 0;

            try
            {
                MonoBehaviour[] parts = body.GetComponentsInChildren<MonoBehaviour>(true);

                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null) continue;
                    if (parts[i].GetType().Name != component) continue;

                    // Immediately, not at the end of the frame. Destroy is deferred, and the
                    // camera looks at the stand-in inside the same frame the component is taken
                    // off — so a deferred removal is a component that is still there when it
                    // matters, which is exactly how this crashed again.
                    parts[i].enabled = false;
                    UnityEngine.Object.DestroyImmediate(parts[i]);
                    taken++;
                }
            }
            catch (Exception e)
            {
                ReportOnce("silence", "Could not take " + component + " off the stand-in: " + e);
            }

            return taken;
        }

        private static bool StillWatching(GameObject body)
        {
            try
            {
                MonoBehaviour[] parts = body.GetComponentsInChildren<MonoBehaviour>(true);

                for (int i = 0; i < parts.Length; i++)
                    if (parts[i] != null && parts[i].GetType().Name == "DwellerVisibilityDetector")
                        return true;
            }
            catch (Exception e)
            {
                // Fails closed. This is the last thing standing between the camera and a crash
                // that has already happened twice, and it used to answer "nothing is watching"
                // whenever it could not tell — which is the one answer it must never guess.
                ReportOnce("watchcheck", "Could not tell whether a watcher is still on the " +
                                         "stand-in, so assuming there is one: " + e);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Keeps the idle playing instead of letting it stop on its last frame.
        ///
        /// A dweller standing in the vault is driven by the state machine that gives it something to
        /// do. This one has nothing to do and nobody driving it, so its animation ran once and
        /// stopped — a second of life and then a photograph. Rewinding the state it is in when it
        /// reaches the end is enough to keep it going.
        /// </summary>
        private static void KeepItMoving(GameObject body)
        {
            try
            {
                // Not Mecanim. The stand-in has no Animator at all -- it is driven by the old
                // Animation component and the game's own AnimationController on top of it, which
                // is why every previous attempt here changed nothing: they were winding a clock
                // that was not in the room.
                HushTheDriver(body);

                Animation[] reels = body.GetComponentsInChildren<Animation>(true);

                if (!_reportedMovers)
                {
                    _reportedMovers = true;
                    ReportMovers(body, reels);
                }

                for (int i = 0; i < reels.Length; i++)
                {
                    Animation reel = reels[i];
                    if (reel == null) continue;

                    if (!reel.enabled) reel.enabled = true;

                    // Nothing here is on screen the way Unity means it: the stand-in sits on a
                    // layer of its own, watched by a disabled camera that is told to render by
                    // hand. Anything culled by visibility is culled for good.
                    reel.cullingType = AnimationCullingType.AlwaysAnimate;

                    // The idle, chosen outright rather than accepted. Taking whatever happened to
                    // be running and putting it on a loop is how a dweller came to sit in the
                    // panel for half a minute: the pool had left a sitting clip playing, and all
                    // this did was make sure it never ended.
                    AnimationState playing = PickIdle(reel);
                    if (playing == null) continue;

                    if (!reel.IsPlaying(playing.name))
                    {
                        playing.wrapMode = WrapMode.Loop;
                        playing.time = 0f;
                        reel.Play(playing.name);
                        reel.Sample();

                        _lastBeat.Remove(reel.GetInstanceID());
                    }

                    _posed = true;

                    playing.enabled = true;
                    playing.weight = 1f;
                    if (playing.speed <= 0f) playing.speed = 1f;
                    if (playing.wrapMode != WrapMode.Loop) playing.wrapMode = WrapMode.Loop;

                    int who = reel.GetInstanceID();
                    float now = playing.time;

                    float before;
                    bool seenBefore = _lastBeat.TryGetValue(who, out before);

                    // The same honest test as before, now aimed at the thing that is actually
                    // there: did the clock move since the last frame, and if not, move it. Setting
                    // the time is only bookkeeping until Sample puts it on the bones.
                    if (seenBefore && now == before)
                    {
                        now += Time.deltaTime;
                        if (playing.length > 0f && now > playing.length) now %= playing.length;

                        playing.time = now;
                        reel.Sample();
                    }

                    _lastBeat[who] = now;
                }
            }
            catch (Exception e)
            {
                ReportOnce("keepmoving", "Could not keep the stand-in moving: " + e);
            }
        }

        /// <summary>Whichever clip this component is running, or nothing if it is running none.</summary>
        private static AnimationState NowPlaying(Animation reel)
        {
            foreach (AnimationState state in reel)
                if (state != null && reel.IsPlaying(state.name)) return state;

            return null;
        }

        /// <summary>
        /// The clip to stand about in.
        ///
        /// By name first, because a dweller has a great many clips and only some of them are a
        /// person standing still; the component's own default next, since that is what the game
        /// would have played; and failing both, the first one there is, on the grounds that a
        /// figure doing something is better than a figure doing nothing.
        /// </summary>
        private static AnimationState PickIdle(Animation reel)
        {
            AnimationState first = null;
            AnimationState named = null;

            foreach (AnimationState state in reel)
            {
                if (state == null || state.clip == null) continue;
                if (first == null) first = state;

                // The component's own default is the game's answer to this question, and the game
                // is right: it reads ANI_Dweller_Woman_Idle, which is a person standing about.
                // Searching the clip names for "idle" was asking a question that had already been
                // answered, and answering it worse.
                if (reel.clip != null && state.clip == reel.clip) return state;

                if (named == null &&
                    state.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0)
                    named = state;
            }

            return named != null ? named : first;
        }

        /// <summary>
        /// Keeps the stand-in in a good mood.
        ///
        /// A dweller's face is its happiness: the one out of the pool arrives with whatever it was
        /// feeling when it was last put away, and a miserable figure is a poor advertisement for
        /// the person you are about to make. Written every frame because it costs one field, and
        /// because whatever set it low is still running.
        /// </summary>
        private void KeepItCheerful()
        {
            try
            {
                if (_previewDweller == null) return;

                object mood = ReadObject(_previewDweller, "Happiness");
                if (mood != null) WriteMember(mood, "HappinessValue", 100f);
            }
            catch (Exception e)
            {
                ReportOnce("cheerful", "Could not cheer the stand-in up: " + e);
            }
        }

        /// <summary>
        /// Switches off the game's own animation driver on the stand-in.
        ///
        /// It runs every frame and plays whatever it thinks the dweller should be doing, which for
        /// one that has no job and no room is not standing about. It was overwriting the clip this
        /// panel had just chosen, one frame later, every frame -- which is why the figure kept its
        /// pose while plainly being animated.
        /// </summary>
        private static void HushTheDriver(GameObject body)
        {
            try
            {
                MonoBehaviour[] parts = body.GetComponentsInChildren<MonoBehaviour>(true);

                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null || !parts[i].enabled) continue;

                    string kind = parts[i].GetType().Name;
                    if (kind != "AnimationController" && kind != "DwellerAnimationController") continue;

                    parts[i].enabled = false;
                    ReportOnce("hushed", "Switched off " + kind + " on the stand-in so the idle sticks.");
                }
            }
            catch (Exception e)
            {
                ReportOnce("hush", "Could not switch off the stand-in's animation driver: " + e);
            }
        }

        /// <summary>
        /// Writes down what actually animates the stand-in, once.
        ///
        /// This is the line that ended four rounds of guessing: it said zero animators and one
        /// legacy Animation, and every attempt before it had been aimed at Mecanim.
        /// </summary>
        private static void ReportMovers(GameObject body, Animation[] reels)
        {
            try
            {
                System.Text.StringBuilder said = new System.Text.StringBuilder();
                said.Append("The stand-in has ").Append(reels.Length).Append(" Animation(s)");

                for (int i = 0; i < reels.Length; i++)
                {
                    Animation reel = reels[i];
                    if (reel == null) continue;

                    said.Append("; [").Append(reel.name).Append("] enabled=").Append(reel.enabled)
                        .Append(" playing=").Append(reel.isPlaying)
                        .Append(" default=")
                        .Append(reel.clip == null ? "none" : reel.clip.name)
                        .Append(" clips:");

                    int shown = 0;
                    foreach (AnimationState state in reel)
                    {
                        if (state == null) continue;
                        if (++shown > 24) { said.Append(" ..."); break; }

                        said.Append(" ").Append(state.name)
                            .Append("(").Append(state.length.ToString("0.00")).Append("s")
                            .Append(reel.IsPlaying(state.name) ? ",playing" : "")
                            .Append(")");
                    }

                    if (shown == 0) said.Append(" none");
                }

                Log.LogInfo(said.ToString());
            }
            catch { }
        }

        private static void SetLayer(Transform branch, int layer)
        {
            branch.gameObject.layer = layer;
            for (int i = 0; i < branch.childCount; i++) SetLayer(branch.GetChild(i), layer);
        }

        private static bool MeasureRenderers(GameObject body, out Bounds seen)
        {
            seen = new Bounds();

            Renderer[] parts = body.GetComponentsInChildren<Renderer>(true);
            bool any = false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null || !parts[i].enabled) continue;

                if (!any) { seen = parts[i].bounds; any = true; }
                else seen.Encapsulate(parts[i].bounds);
            }

            return any;
        }

        private void RefreshPreview()
        {
            if (_previewPicture == null || _previewHeadgear == null) return;

            bool have = _previewDweller != null;

            _previewPicture.gameObject.SetActive(have);
            _previewHeadgear.gameObject.SetActive(false);

            if (!have) return;

            // Setting a piece writes a field and nothing more — ApplyCustomization has no effect on
            // what is drawn until the texture is built again. This is the pair of calls the game
            // makes for itself after a change, and leaving them out is why the hair colour never
            // moved and why a face came back wrong once a helmet went on.
            if (FilmStandIn())
            {
                // The film is an ordinary texture, so the widget goes back to drawing one plainly
                // rather than wearing the dweller's own material.
                // Both of these are per-frame work for a result that never changes: assigning
                // the material marks the widget dirty every frame, and Shader.Find is a
                // string-keyed lookup. Done once, when the picture is actually still wearing the
                // dweller's own material.
                if (_previewPicture.material != null)
                {
                    _previewPicture.material = null;

                    if (_plainShader == null) _plainShader = Shader.Find("Unlit/Transparent Colored");
                    if (_plainShader != null) _previewPicture.shader = _plainShader;
                }

                _previewPicture.mainTexture = _previewFilm;
                _previewPicture.uvRect = new Rect(0f, 0f, 1f, 1f);

                // Set once and left alone. The film is always the same shape, so working the size
                // out again on every refresh only made the figure jump about as the bench opened.
                // The width follows the film's shape, and it has to be set even when the height
                // already matches -- which it did from the moment the widget was built, so the
                // guard meant the width was never corrected and the figure was drawn into whatever
                // box the layout had reserved. That is the squashing.
                int tall = PreviewHeight;
                int wide = Mathf.Max(8, Mathf.RoundToInt(
                    tall * (float)_previewFilm.width / _previewFilm.height));

                if (_previewPicture.height != tall || _previewPicture.width != wide)
                {
                    _previewPicture.height = tall;
                    _previewPicture.width = wide;
                }

                if (!_reportedPieces) { _reportedPieces = true; ReportPieces(); }
                return;
            }

            try
            {
                MethodInfo draw = typeof(Dweller).GetMethod(
                    "SetUITex",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(UITexture), typeof(UITexture) }, null);

                if (draw == null)
                {
                    ReportOnce("preview", "The game has no SetUITex; the picture cannot be drawn.");
                    return;
                }

                draw.Invoke(_previewDweller, new object[] { _previewPicture, _previewHeadgear });

                // Every piece is bound — body, outfit, hands, face — so nothing is missing from the
                // composition. What was missing was the part of it being looked at: SetUITex leaves
                // the widget sampling a corner of the sheet, and that corner is the head. The whole
                // sheet is shown instead, which is where the rest of the dweller was all along.
                // The whole sheet is the raw atlas — the dweller in pieces — so this is off by
                // default now. It stays because seeing the atlas is what proved that.
                if (PreviewWholeSheet.Value)
                {
                    _previewPicture.uvRect = new Rect(0f, 0f, 1f, 1f);
                    if (_previewHeadgear != null) _previewHeadgear.uvRect = new Rect(0f, 0f, 1f, 1f);
                }

                FitPreview();

                if (!_reportedPieces) { _reportedPieces = true; ReportPieces(); }
            }
            catch (Exception e)
            {
                ReportOnce("preview", "Drawing the dweller failed: " + e.Message);
            }
        }

        /// <summary>Calls one of the game's own methods on a dweller, by name.</summary>
        private static void Call(object target, string method, params object[] arguments)
        {
            if (target == null) return;

            Type[] shapes = new Type[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
                shapes[i] = arguments[i] == null ? typeof(object) : arguments[i].GetType();

            MethodInfo found = target.GetType().GetMethod(
                method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, shapes, null);

            if (found != null) found.Invoke(target, arguments);
        }

        /// <summary>
        /// Puts every choice back onto the dweller, then redraws.
        ///
        /// Every choice, not the one that moved: the pieces are not independent — putting a helmet
        /// on rewrites the face beneath it — so applying one at a time left the rest behind. The
        /// whole set is cheap to reapply, because each is a field being written.
        /// </summary>
        private void LookChanged(Choice choice)
        {
            if (_previewDweller == null) return;

            try
            {
                ApplyLooks(_previewDweller);
                _texturedOnce = false;
                RefreshPreview();
            }
            catch (Exception e)
            {
                Trouble("Could not change " + choice.Caption + ": " + e.Message);
            }
        }

        /// <summary>
        /// Writes down every piece the dweller is made of and every texture the shader was handed.
        ///
        /// Four attempts at the head have each been a theory about which piece was missing. This
        /// stops the theorising: the material's own texture properties say what is bound and what is
        /// empty, and the dweller's fields say which pieces it thinks it has.
        /// </summary>
        private void ReportPieces()
        {
            if (_previewDweller == null) return;

            try
            {
                string pieces = "";
                string[] names = { "m_hair", "m_face", "m_faceMask", "m_body", "m_outfit",
                                   "m_helmet", "m_overrideFace", "m_helmetCoverCustomization" };

                for (int i = 0; i < names.Length; i++)
                {
                    object piece = ReadObject(_previewDweller, names[i]);
                    UnityEngine.Object asset = piece as UnityEngine.Object;

                    pieces += (pieces.Length > 0 ? ", " : "") + names[i] + "=" +
                              (asset == null ? "none" : asset.name);
                }

                Log.LogInfo("The stand-in is made of: " + pieces);

                DwellerItem worn = _previewDweller.EquippedOutfit;
                Log.LogInfo("  it is wearing " + (worn == null ? "nothing" : worn.Id) +
                            ", child=" + ReadAsText(_previewDweller, "IsChild") +
                            ", active=" + _previewDweller.gameObject.activeSelf);

                Material paint = _previewPicture == null ? null : _previewPicture.material;
                if (paint == null) { Log.LogInfo("  the picture has no material."); return; }

                string bound = "";
                string[] properties = paint.GetTexturePropertyNames();

                for (int i = 0; i < properties.Length; i++)
                {
                    Texture held = paint.GetTexture(properties[i]);
                    bound += "\n      " + properties[i] + " = " +
                             (held == null
                                  ? "empty"
                                  : held.name + " " + held.width + "x" + held.height +
                                    " offset " + paint.GetTextureOffset(properties[i]) +
                                    " scale " + paint.GetTextureScale(properties[i]));
                }

                Log.LogInfo("  the shader was handed:" + bound);
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not read the stand-in apart: " + e.Message);
            }
        }

        private void FitPreview()
        {
            if (_previewPicture == null) return;

            try
            {
                Material paint = _previewPicture.material;
                Texture sheet = paint != null ? paint.mainTexture : null;
                Rect uv = _previewPicture.uvRect;

                // Said out loud whatever happens. The last run left no line at all, which told me
                // only that something was missing and not which thing.
                ReportOnce("previewwhat",
                           "The picture has material=" +
                           (paint == null ? "none" : paint.name + " shader " +
                                                     (paint.shader == null ? "none" : paint.shader.name)) +
                           ", texture=" +
                           (sheet == null ? "none" : sheet.name + " " + sheet.width + "x" + sheet.height) +
                           ", uv=" + uv +
                           ", widget=" + _previewPicture.mainTexture);

                float wide = PreviewWidth;
                float high = PreviewHeight;

                if (sheet != null && sheet.width > 0 && sheet.height > 0 &&
                    uv.width > 0f && uv.height > 0f)
                {
                    float shownWidth = sheet.width * uv.width;
                    float shownHeight = sheet.height * uv.height;
                    float scale = Mathf.Min(PreviewWidth / shownWidth, PreviewHeight / shownHeight);

                    wide = shownWidth * scale;
                    high = shownHeight * scale;

                    ReportOnce("previewsize",
                               "The dweller's sheet is " + sheet.width + "x" + sheet.height +
                               ", shown through " + uv + " as " + Mathf.RoundToInt(wide) + "x" +
                               Mathf.RoundToInt(high) + ".");
                }

                _previewPicture.width = Mathf.Max(8, Mathf.RoundToInt(wide));
                _previewPicture.height = Mathf.Max(8, Mathf.RoundToInt(high));

                if (_previewHeadgear != null)
                {
                    _previewHeadgear.width = _previewPicture.width;
                    _previewHeadgear.height = _previewPicture.height;
                }
            }
            catch (Exception e)
            {
                ReportOnce("previewsize", "Could not size the picture: " + e.Message);
            }
        }

        private const int PreviewWidth = 138;
        private const int PreviewHeight = 214;

        /// <summary>
        /// The dweller down the left, the choices about it down the right.
        ///
        /// Full height, because a person adjusting a face wants to see the person, and the choices
        /// beside them rather than under them so that changing one does not move the picture out of
        /// sight.
        /// </summary>
        private void BuildLooksBlock(Transform parent, int width)
        {
            Choice[] rows = { _hair, _face, _hairColour, _skin, _helmet };

            const int rowGap = 4;

            // One box holding two compartments, divided by a line, the way a handset used to
            // put the screen above the keys. Two separate boxes made the figure and its die look
            // like two unrelated things that happened to be stacked; one box with a line in it
            // says they belong together and that the lower half does something.
            //
            // Everything inside is measured from a single padding, so the gap above the figure,
            // below the die and either side of both is the same number.
            const int pad = 8;
            const int dieRoom = 50;
            const int rollHeight = dieRoom - 8;

            int wellWidth = PreviewWidth + pad * 2;
            int wellHeight = PreviewHeight + dieRoom + pad * 3 + 2;

            int block = wellHeight + pad * 2;
            int middle = _cursorY - block / 2;

            // The rows share the box's height between them rather than bunching at the top and
            // leaving a hole underneath it.
            int rowHeight = (block - pad * 2 - (rows.Length - 1) * rowGap) / rows.Length;

            Plate(parent, "LooksPlate", 0, middle, width, block, Skin.Row(width, block), 1);

            // The box, down the left, and everything inside it measured from its own top edge.
            int pictureX = -width / 2 + pad + wellWidth / 2;

            Plate(parent, "PreviewWell", pictureX, middle, wellWidth, wellHeight,
                  Skin.Well(wellWidth, wellHeight), 2);

            // The figure sits a little below the top of its half rather than against it, and the
            // line that divides the box sits lower with it, which leaves the die where the eye
            // expects a control to be.
            int wellTop = middle + wellHeight / 2;
            int pictureY = wellTop - pad - 12 - PreviewHeight / 2;

            GameObject picture = new GameObject("PreviewPicture");
            picture.layer = parent.gameObject.layer;
            picture.transform.SetParent(parent, false);
            picture.transform.localPosition = new Vector3(pictureX, pictureY, 0f);
            picture.transform.localScale = Vector3.one;

            _previewPicture = picture.AddComponent<UITexture>();
            _previewPicture.width = PreviewWidth;
            _previewPicture.height = PreviewHeight;
            _previewPicture.depth = 3;

            GameObject headgear = new GameObject("PreviewHeadgear");
            headgear.layer = parent.gameObject.layer;
            headgear.transform.SetParent(parent, false);
            headgear.transform.localPosition = new Vector3(pictureX, pictureY, 0f);
            headgear.transform.localScale = Vector3.one;

            _previewHeadgear = headgear.AddComponent<UITexture>();
            _previewHeadgear.width = PreviewWidth;
            _previewHeadgear.height = PreviewHeight;
            _previewHeadgear.depth = 4;

            // The line, and the die below it: the same padding above the line as below it.
            // The line sits lower and the die rides higher in what is left, so the lower half
            // is the die rather than the die and a gap under it.
            int lineY = pictureY - PreviewHeight / 2 - pad - 4;
            // Centred in what is left below the line, rather than placed by an offset somebody
            // adjusted until it looked about right. Half of a remainder is exactly the middle; a
            // number chosen by eye is exactly the middle only by accident.
            int wellFloor = middle - wellHeight / 2;
            int dieY = (lineY - 1 + wellFloor) / 2 + 7;

            // No rule across the middle. The two halves are told apart by the figure filling one
            // of them, which is enough, and a line inside a small box is one more edge in an
            // interface that already has a great many.

            GameObject die = new GameObject("RollLooks");
            die.layer = parent.gameObject.layer;
            die.transform.SetParent(parent, false);
            die.transform.localPosition = new Vector3(pictureX, dieY, 0f);
            die.transform.localScale = Vector3.one;

            _dieFace = die.AddComponent<UITexture>();
            _dieFace.mainTexture = Skin.Die(rollHeight, 6);
            _dieFace.width = rollHeight;
            _dieFace.height = rollHeight;
            _dieFace.depth = 5;

            Shader flat = Shader.Find("Unlit/Transparent Colored");
            if (flat != null) _dieFace.shader = flat;

            BoxCollider hit = die.AddComponent<BoxCollider>();
            hit.size = new Vector3(rollHeight + 10, rollHeight + 10, 1f);
            hit.isTrigger = true;

            UIButton press = die.AddComponent<UIButton>();
            press.tweenTarget = die;
            press.onClick.Add(new EventDelegate(RollTheLooks));

            // The choices, down the right, with the same padding from the box as the box has
            // from the plate and as the rows have from each other's ends.
            int columnLeft = -width / 2 + pad + wellWidth + pad;
            int columnWidth = width - wellWidth - pad * 3;
            int columnCentre = columnLeft + columnWidth / 2;

            int top = middle + block / 2 - pad - rowHeight / 2;

            for (int i = 0; i < rows.Length; i++)
                AddCompactChoice(parent, rows[i], columnCentre,
                                 top - i * (rowHeight + rowGap), columnWidth, rowHeight,
                                 rows[i] == _skin);

            // Five rows beside a picture two hundred and sixty tall leaves a gap; the picture is
            // what the gap is for.

            _cursorY -= block + RowGap;

            // What the dweller carries, both on one line: arrows level with the pictures, names
            // under them. Two tall panels for two items was a lot of room to say very little.
            const int slotHeight = 92;
            int slotWidth = (width - 6) / 2;
            int slotY = _cursorY - slotHeight / 2;

            AddGearSlot(parent, _outfit, -width / 2 + slotWidth / 2, slotY, slotWidth, slotHeight);
            AddGearSlot(parent, _weapon, width / 2 - slotWidth / 2, slotY, slotWidth, slotHeight);

            _cursorY -= slotHeight + RowGap;

            _previewPicture.gameObject.SetActive(false);
            _previewHeadgear.gameObject.SetActive(false);
        }

        /// <summary>
        /// One thing the dweller carries, laid out the way the game lays out a card.
        ///
        /// What it is and where you are in the list go in the top left, the way to change it goes
        /// in the top right, and the rest of the card belongs to the thing itself: its picture in a
        /// recess down the left, and three lines beside it that all start in the same place. The
        /// recess stops short of the bottom edge, because a card with its contents pressed against
        /// the frame reads as one that ran out of room.
        /// </summary>
        private void AddGearSlot(Transform parent, Choice choice, int centreX, int y,
                                 int width, int height)
        {
            Plate(parent, "Slot_" + choice.Caption, centreX, y, width, height,
                  Skin.Row(width, height), 1);

            int left = centreX - width / 2;
            int right = centreX + width / 2;
            int top = y + height / 2 - 15;

            Choice captured = choice;

            MakeButton(parent, "SlotBack_" + choice.Caption, "<", right - 52, top, 24, 22,
                       false, delegate { captured.Step(-1); });
            MakeButton(parent, "SlotFwd_" + choice.Caption, ">", right - 22, top, 24, 22,
                       false, delegate { captured.Step(1); });

            UILabel caption = MakeLeftLabel(parent, "SlotName_" + choice.Caption, choice.Caption,
                                            left + 12, top, width - 78, 18, Skin.Bright, 3);
            caption.fontSize = TextBody;
            caption.color = new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.85f);
            caption.maxLineCount = 1;
            choice.Title = caption;

            // Measured from the card's own edges rather than placed by eye: the gap below the
            // recess is the gap to its left, which is what makes a box inside a box look deliberate
            // rather than dropped in.
            const int inset = 8;

            int well = height - 34;
            int middle = y - height / 2 + inset + well / 2;

            Plate(parent, "SlotWell_" + choice.Caption, left + inset + well / 2, middle, well, well,
                  Skin.Well(well), 2);

            GameObject pictureGo = new GameObject("SlotPic_" + choice.Caption);
            pictureGo.layer = parent.gameObject.layer;
            pictureGo.transform.SetParent(parent, false);
            pictureGo.transform.localPosition = new Vector3(left + inset + well / 2, middle, 0f);
            pictureGo.transform.localScale = Vector3.one;

            choice.Picture = pictureGo.AddComponent<UISprite>();
            choice.Picture.depth = 4;
            choice.Picture.gameObject.SetActive(false);

            // Three lines of one size, set lower so they sit against the picture rather than
            // above it. Three sizes over three lines that say three parts of the same thing is
            // hierarchy invented where there is none.
            int lineLeft = left + inset * 2 + well;
            int lineWidth = Mathf.Max(48, right - 8 - lineLeft);

            choice.Display = MakeLeftLabel(parent, "SlotValue_" + choice.Caption, "-",
                                           lineLeft, middle + 15, lineWidth, 20, Skin.Bright, 3);
            choice.Display.fontSize = TextBody;
            choice.Display.maxLineCount = 1;

            UILabel effect = MakeLeftLabel(parent, "SlotStats_" + choice.Caption, "",
                                           lineLeft, middle - 5, lineWidth, 20, Skin.Bright, 3);
            effect.fontSize = TextBody;
            effect.color = new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.9f);
            effect.maxLineCount = 1;
            choice.Detail = effect;

            UILabel grade = MakeLeftLabel(parent, "SlotRarity_" + choice.Caption, "",
                                          lineLeft, middle - 25, lineWidth, 22, Skin.Bright, 3);
            grade.fontSize = TextNote;
            grade.color = new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.7f);
            grade.maxLineCount = 1;
            choice.Grade = grade;

            choice.Show();
        }

        private void BuildDwellerSection(Transform parent, int width)
        {
            RebuildLookOptions();
            RebuildGearOptions();

            AddHeader(parent, "NAME", width);

            int nameY = _cursorY - RowHeight / 2;
            Plate(parent, "NameRow", 0, nameY, width, RowHeight, Skin.Row(width, RowHeight), 1);
            // Three things in a line with the same margin at each end and the same gap between
            // them. It had eight units of margin on the left, four on the right and two before the
            // switch, which is not a row so much as three things that happened to stop where they
            // stopped.
            const int nameGap = 8;
            const int genderWidth = 92;

            int nameWidth = (width - nameGap * 4 - genderWidth) / 2;
            int nameLeft = -width / 2 + nameGap;

            _firstNameInput = AddInput(parent, "First", nameLeft + nameWidth / 2, nameY,
                                       nameWidth, "FIRST");
            _lastNameInput = AddInput(parent, "Last",
                                      nameLeft + nameWidth + nameGap + nameWidth / 2, nameY,
                                      nameWidth, "LAST");

            // Gender belongs on the name row: it is one of two things, so a switch says it in the
            // space a whole row was taking. It decides what the looks can be, so it comes first.
            _genderSwitch = MakeButton(parent, "Gender", Genders[_genderIndex].ToString().ToUpper(),
                                       width / 2 - nameGap - genderWidth / 2, nameY,
                                       genderWidth, RowHeight - 10, false,
                                       delegate { StepGender(1); });

            _cursorY -= RowHeight + RowGap;

            AddHeader(parent, "LOOKS", width);
            BuildLooksBlock(parent, width);

            _hair.OnChange = delegate { LookChanged(_hair); };
            _face.OnChange = delegate { LookChanged(_face); };
            _hairColour.OnChange = delegate { LookChanged(_hairColour); };
            _helmet.OnChange = delegate { LookChanged(_helmet); };
            _skin.OnChange = delegate { LookChanged(_skin); };
            _outfit.OnChange = delegate { ShowChoicePicture(_outfit); LookChanged(_outfit); };
            _weapon.OnChange = delegate { ShowChoicePicture(_weapon); LookChanged(_weapon); };

            _hairColour.SwatchOf = ColourOf;

            _hair.Show();
            _face.Show();
            _hairColour.Show();
            _skin.Show();
            _helmet.Show();
            _outfit.Show();
            _weapon.Show();

            AddHeader(parent, "RARITY AND LEVEL", width);

            // Both on one line: two short answers do not need two rows between them.
            int gradeY = _cursorY - RowHeight / 2;
            Plate(parent, "GradeRow", 0, gradeY, width, RowHeight, Skin.Row(width, RowHeight), 1);

            MakeButton(parent, "RarityBack", "<", -width / 2 + 26, gradeY, 34, 30, false,
                       delegate { StepRarity(-1); });
            _rarityLabel = MakeLabel(parent, "RarityValue", Rarities[_rarityIndex].ToString(),
                                     -width / 2 + 118, gradeY, 120, RowHeight, Skin.Bright, 3);
            _rarityLabel.maxLineCount = 1;
            MakeButton(parent, "RarityFwd", ">", -width / 2 + 210, gradeY, 34, 30, false,
                       delegate { StepRarity(1); });

            MakeLeftLabel(parent, "LevelCaption", "LEVEL", -width / 2 + 244, gradeY, 70,
                          RowHeight, Skin.Bright, 3);
            _levelInput = AddInput(parent, "Level", width / 2 - 46, gradeY, 76,
                                   _dwellerLevelValue.ToString(), true);
            _cursorY -= RowHeight + RowGap;

            AddHeader(parent, "SPECIAL", width);

            // Seven stats across one row: a row apiece would not fit, and the letters are the
            // game's own shorthand for them.
            const int specialHeight = 92;
            int cell = (width - 16) / 7;
            int specialY = _cursorY - specialHeight / 2;

            Plate(parent, "SpecialRow", 0, specialY, width, specialHeight,
                  Skin.Row(width, specialHeight), 1);

            for (int i = 0; i < Specials.Length; i++)
            {
                int index = i;
                int x = -width / 2 + cell / 2 + 8 + i * cell;

                // The letter is the name of the stat and the loudest thing in the cell; the box
                // beneath holds one or two digits and had been sized as though it held a word.
                // Down the cell: the letter, air, the box, air, the two keys. The letter names
                // the stat and is the loudest thing here; the box holds one or two digits; the
                // keys are keys.
                UILabel letter = MakeLabel(parent, "SpecLetter" + i,
                                           Specials[i].ToString().Substring(0, 1),
                                           x, specialY + 29, cell, 26, Skin.Bright, 3);
                letter.fontSize = TextTitle;

                UIInput box = AddInput(parent, "Spec" + i, x, specialY + 4, cell - 24,
                                       _special[i].ToString(), true, 24);
                _specialInputs[i] = box;

                UILabel typed = box.GetComponentInChildren<UILabel>();
                if (typed != null) typed.fontSize = TextBody;

                MakeSignButton(parent, "SpecDown" + i, false, x - cell / 4 - 1, specialY - 26,
                               cell / 2 - 8, 22, delegate { StepSpecial(index, -1); });
                MakeSignButton(parent, "SpecUp" + i, true, x + cell / 4 + 1, specialY - 26,
                               cell / 2 - 8, 22, delegate { StepSpecial(index, 1); });
            }
            _cursorY -= specialHeight + RowGap;

            // The one button on the bench that does anything. A typed plus sat wherever the
            // font's baseline put it, which is not the middle of anything; the game has a blank
            // person in its own art, which is both straight and a better answer to the question
            // of what this button makes.
            GameObject make = MakeButton(parent, "CreateDweller", "CREATE DWELLER", 0,
                                         _cursorY - 22, width, 44, true, CreateDwellerFromPanel);

            AddBareIcon(make.transform, "CreateMark",
                        new[] { "Silhouette_Dweller", "Icon_dwellerPlain", "Icon_dweller" },
                        "silhouette dweller", width / 2 - 32, 0, 30, Skin.Ink);
            _cursorY -= 44 + RowGap;
        }

        /// <summary>A label, a value, and a pair of arrows — the game's own way of offering a choice.</summary>
        private UILabel AddPickerRow(Transform parent, int width, string caption,
                                     EventDelegate.Callback back, EventDelegate.Callback forward,
                                     string initial)
        {
            int y = _cursorY - RowHeight / 2;

            Plate(parent, "Pick_" + caption, 0, y, width, RowHeight, Skin.Row(width, RowHeight), 1);

            MakeLeftLabel(parent, "PickName_" + caption, caption,
                          -width / 2 + 14, y, 170, RowHeight, Skin.Bright, 3);

            MakeButton(parent, "PickBack_" + caption, "<", width / 2 - 178, y, 40, 32, false, back);
            UILabel value = MakeLabel(parent, "PickValue_" + caption, initial,
                                      width / 2 - 108, y, 116, RowHeight, Skin.Bright, 3);
            value.fontSize = TextBody;
            value.maxLineCount = 1;
            MakeButton(parent, "PickFwd_" + caption, ">", width / 2 - 38, y, 40, 32, false, forward);

            _cursorY -= RowHeight + RowGap;
            return value;
        }

        private static char RefuseLineBreaks(string text, int index, char typed)
        {
            if (typed == '\n' || typed == '\r' || typed == '\t') return (char)0;
            return typed;
        }

        private UIInput AddInput(Transform parent, string name, int x, int y, int width, string hint)
        {
            return AddInput(parent, name, x, y, width, hint, false);
        }

        private UIInput AddInput(Transform parent, string name, int x, int y, int width, string hint,
                                 bool numeric)
        {
            return AddInput(parent, name, x, y, width, hint, numeric, RowHeight - 12);
        }

        /// <summary>A field of a stated height, for the cells that hold two digits rather than a word.</summary>
        private UIInput AddInput(Transform parent, string name, int x, int y, int width, string hint,
                                 bool numeric, int fieldHeight)
        {
            GameObject go = new GameObject("Input_" + name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one;

            // A field has to look like one. Without a sunken plate behind it a place to type is
            // indistinguishable from a label, and the search box read as the word ALL.
            Plate(go.transform, "Field", 0, 0, width, fieldHeight,
                  Skin.Field(width, fieldHeight), 2);

            // The hint goes on the label, not on defaultText: UIInput.Init takes its placeholder
            // from the label's text and overwrites whatever defaultText held.
            UILabel label = MakeLabel(go.transform, "Text", hint, 0, 0, width - 18, fieldHeight - 6,
                                      Skin.Bright, 4);

            // NGUI routes typing through a collider, exactly as it routes clicks.
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, RowHeight - 8, 1f);
            box.isTrigger = true;

            label.multiLine = false;
            label.maxLineCount = 1;

            UIInput input = go.AddComponent<UIInput>();
            input.label = label;
            input.characterLimit = 24;
            input.inputType = UIInput.InputType.Standard;
            if (numeric) input.validation = UIInput.Validation.Integer;

            // A line break in a name is not a name, and NGUI will happily take one.
            input.onValidate = RefuseLineBreaks;

            // This line is the whole reason the dwellers tab took the game down with it. UIInput.Start
            // runs mValue.Replace(...) with no null check; on a component built here mValue is null,
            // and the game's own inputs only escape it because a serialised field comes back as "".
            // Nothing in the log said so — the crash reporter did.
            input.value = "";

            return input;
        }

        private void StepRarity(int by)
        {
            _rarityIndex = (_rarityIndex + by + Rarities.Length) % Rarities.Length;
            if (_rarityLabel != null) _rarityLabel.text = Rarities[_rarityIndex].ToString().ToUpper();

            RemakePreview();
        }

        private GameObject _genderSwitch;

        /// <summary>Makes the gender button say what the bench is actually set to.</summary>
        private void ShowGender()
        {
            if (_genderSwitch == null) return;

            UILabel text = _genderSwitch.GetComponentInChildren<UILabel>();
            if (text != null) text.text = Genders[_genderIndex].ToString().ToUpper();
        }

        private void StepGender(int by)
        {
            _genderIndex = (_genderIndex + by + Genders.Length) % Genders.Length;

            ShowGender();
            RebuildLookOptions();
            RemakePreview();
        }

        private void ReadLevelInput()
        {
            if (_levelInput == null) return;

            int parsed;
            if (int.TryParse(_levelInput.value, out parsed) && parsed >= 1)
                _dwellerLevelValue = Mathf.Min(parsed, 50);

            _dwellerLevel = _dwellerLevelValue.ToString();
        }

        // Ten is the figure the game shows, not a rule it enforces: higher values are kept and do
        // work. The ceiling here is only high enough to stop a slipped keystroke.
        private const int MaxSpecial = 100;

        private void StepSpecial(int index, int by)
        {
            _special[index] = Mathf.Clamp(_special[index] + by, 1, MaxSpecial);
            if (_specialInputs[index] != null)
                _specialInputs[index].value = _special[index].ToString();

        }

        /// <summary>Takes the typed figures, so a stat can be set rather than clicked up to.</summary>
        private void ReadSpecialInputs()
        {
            for (int i = 0; i < _specialInputs.Length; i++)
            {
                if (_specialInputs[i] == null) continue;

                int parsed;
                if (int.TryParse(_specialInputs[i].value, out parsed) && parsed >= 1)
                    _special[i] = Mathf.Min(parsed, MaxSpecial);
            }
        }

        /// <summary>Reads the panel's fields and hands them to the creation the game already uses.</summary>
        private void CreateDwellerFromPanel()
        {
            if (_firstNameInput != null) _dwellerFirst = _firstNameInput.value;
            if (_lastNameInput != null) _dwellerLast = _lastNameInput.value;
            ReadLevelInput();
            ReadSpecialInputs();
            _dwellerLevel = _dwellerLevelValue.ToString();
            CreateDweller();
        }

        // Everything on a page is placed against _cursorY as the top edge of the next element and
        // drawn about its own centre. Mixing the two conventions is what pushed the first row of
        // every page half out of the window.
        private void AddHeader(Transform parent, string text, int width)
        {
            int y = _cursorY - 17;
            Plate(parent, "Header_" + text, 0, y, width, 34, Skin.Header(width, 34), 1);
            UILabel heading = MakeLabel(parent, "HeaderText_" + text, text, 0, y, width - 20, 34,
                                        Skin.Ink, 3);
            heading.fontSize = TextHeading;
            _cursorY -= 34 + RowGap;
        }

        /// <summary>
        /// One resource as a two-line cell: what it is and how much of it above, what can be done
        /// about it below.
        ///
        /// A single line was written for a window twice this wide. At a third of the screen the
        /// name, the figure and four buttons do not fit across, and squeezing them makes a row that
        /// belongs to no interface at all.
        /// </summary>
        // Room for a picture that is worth looking at, with everything else to the right of it.
        private const int ResourceCell = 80;
        private const int ResourceIcon = 62;

        /// <summary>
        /// What a resource is called, rather than what the enum calls it.
        ///
        /// StimPack, RadAway and NukaColaQuantum are the names of fields in somebody's code. The
        /// things themselves are a Stimpak, a RadAway and a Nuka-Cola Quantum, and a panel that
        /// says otherwise is showing the player the inside of the game rather than the game.
        /// Anything not in this list is at least split into words rather than left as one.
        /// </summary>
        private static string ResourceName(EResource resource)
        {
            // By what the name contains rather than by a member I have to spell right. Two
            // guesses at this enum have already been wrong, and the compiler catching them is
            // luckier than it sounds -- the third would have been a row labelled with a field name
            // in front of a player.
            string raw = resource.ToString();
            if (raw.IndexOf("cap", StringComparison.OrdinalIgnoreCase) >= 0 &&
                raw.IndexOf("carrier", StringComparison.OrdinalIgnoreCase) < 0) return "CAPS";

            switch (resource)
            {
                // Caps live in the enum under Nuka, which is why every rule aimed at the word
                // "cap" walked straight past them.
                case EResource.Nuka:             return "CAPS";
                case EResource.Energy:           return "POWER";
                case EResource.Food:             return "FOOD";
                case EResource.Water:            return "WATER";
                case EResource.StimPack:         return "STIMPAK";
                case EResource.RadAway:          return "RADAWAY";
                case EResource.NukaColaQuantum:  return "NUKA-COLA QUANTUM";
                case EResource.Lunchbox:         return "LUNCHBOX";
                case EResource.MrHandy:          return "MR HANDY";
                case EResource.PetCarrier:       return "PET CARRIER";
                default:                         return Tidy(raw).ToUpper();
            }
        }

        private void AddResourceRow(Transform parent, EResource resource, int width)
        {
            int middle = _cursorY - ResourceCell / 2;
            int top = middle + 17;
            int bottom = middle - 19;

            Plate(parent, "Row_" + resource, 0, middle, width, ResourceCell,
                  Skin.Row(width, ResourceCell), 1);

            // The picture takes the full height of the row: at a third of the screen these are the
            // only things in the panel large enough to be recognised at a glance.
            int iconCentre = -width / 2 + 8 + (ResourceIcon + 8) / 2;
            AddIcon(parent, "Icon_" + resource, ResourceSprites(resource), resource.ToString(),
                    iconCentre, middle, ResourceIcon);

            int left = -width / 2 + 16 + ResourceIcon + 8;
            int right = width / 2 - 8;
            int span = right - left;

            MakeLeftLabel(parent, "Name_" + resource, ResourceName(resource),
                          left, top, span - 150, 26, Skin.Bright, 3);

            _resourceLabels[resource] = MakeRightLabel(parent, "Value_" + resource, "-",
                                                       right, top, 160, 26, Skin.Bright, 3);

            // A copy the handler owns. The hazard the comment here used to describe — C# 5
            // sharing a foreach variable — is real, but not at this line: resource is a parameter,
            // already one per call. The real one is the amount below, declared inside its loop.
            EResource captured = resource;

            float[] amounts = AmountsFor(resource);

            int count = amounts.Length + 1;
            int buttonWidth = (span - (count - 1) * 4) / count;
            int x = left + buttonWidth / 2;

            for (int i = 0; i < amounts.Length; i++)
            {
                float amount = amounts[i];
                MakeButton(parent, "Grant_" + resource + "_" + amount,
                           "+" + Short(amount), x, bottom, buttonWidth, 28, false,
                           delegate { Grant(captured, amount); });
                x += buttonWidth + 4;
            }

            MakeButton(parent, "Fill_" + resource, "MAX", x, bottom, buttonWidth, 28, false,
                       delegate { FillToCap(captured); });

            _cursorY -= ResourceCell + RowGap;
        }

        /// <summary>1000 is wider than the button. 1K is not.</summary>
        private static string Short(float amount)
        {
            if (amount >= 1000f) return (amount / 1000f).ToString("0") + "K";
            return amount.ToString("0");
        }

        /// <summary>What a box is called: Regular is the lunchbox everyone means by lunchbox.</summary>
        private static string BoxName(ELunchBoxType type)
        {
            switch (type)
            {
                case ELunchBoxType.MrHandy:     return "MR HANDY";
                case ELunchBoxType.PetCarrier:  return "PET CARRIER";
                default:                        return "LUNCHBOX";
            }
        }

        private void AddBoxRow(Transform parent, ELunchBoxType type, int width)
        {
            int middle = _cursorY - ResourceCell / 2;
            int top = middle + 17;
            int bottom = middle - 19;

            Plate(parent, "BoxRow_" + type, 0, middle, width, ResourceCell,
                  Skin.Row(width, ResourceCell), 1);

            int iconCentre = -width / 2 + 8 + (ResourceIcon + 8) / 2;
            AddIcon(parent, "BoxIcon_" + type, BoxSprites(type), "box " + type,
                    iconCentre, middle, ResourceIcon, false, "VaultTec");

            int left = -width / 2 + 16 + ResourceIcon + 8;
            int right = width / 2 - 8;
            int span = right - left;

            MakeLeftLabel(parent, "BoxName_" + type, BoxName(type),
                          left, top, span, 26, Skin.Bright, 3);

            ELunchBoxType captured = type;

            int buttonWidth = (span - (BoxAmounts.Length - 1) * 4) / BoxAmounts.Length;
            int x = left + buttonWidth / 2;

            for (int i = 0; i < BoxAmounts.Length; i++)
            {
                int quantity = BoxAmounts[i];
                MakeButton(parent, "Box_" + type + "_" + quantity, "+" + quantity,
                           x, bottom, buttonWidth, 28, false,
                           delegate { GrantBoxes(captured, quantity); });
                x += buttonWidth + 4;
            }

            _cursorY -= ResourceCell + RowGap;
        }

        /// <summary>Rewrites the figures while the window is open, without rebuilding anything.</summary>
        private void RefreshValues()
        {
            Vault vault = SafeVault();
            if (vault == null || !vault.Loaded || vault.Storage == null) return;

            GameResources held = vault.Storage.Resources;
            GameResources cap = vault.Storage.MaxResources;
            if (held == null) return;

            foreach (KeyValuePair<EResource, UILabel> entry in _resourceLabels)
            {
                if (entry.Value == null) continue;
                try
                {
                    string line = held[entry.Key].ToString("0");
                    if (cap != null)
                    {
                        float max = cap[entry.Key];
                        if (max > 0f) line += " / " + max.ToString("0");
                    }
                    entry.Value.text = line;
                }
                catch { entry.Value.text = "-"; }
            }
        }

        /// <summary>Says whether the window reached the screen, and what stopped it if it did not.</summary>
        private void ReportDrawing()
        {
            try
            {
                if (_frame == null)
                {
                    Log.LogWarning("The window has no frame widget; falling back to the scaffold.");
                    return;
                }

                bool drawn = _frame.drawCall != null;
                _nguiDrawing = drawn && _frame.isVisible;

                Log.LogInfo("Window drawing check: drawCall=" + (drawn ? "yes" : "NO") +
                            " isVisible=" + _frame.isVisible +
                            " alpha=" + _frame.alpha +
                            " depth=" + _frame.depth +
                            " size=" + _frame.width + "x" + _frame.height +
                            " texture=" + (_frame.mainTexture != null ? "yes" : "NO") +
                            " material=" + (_frame.material != null ? _frame.material.name : "none") +
                            " shader=" + (_frame.shader != null ? _frame.shader.name : "none") +
                            " layer=" + LayerMask.LayerToName(_frame.gameObject.layer) +
                            " under " + Path(_nguiWindow));

                if (!_nguiDrawing)
                    Log.LogWarning("The window is not reaching the screen; the scaffold will be " +
                                   "drawn instead so the panel stays usable.");
            }
            catch (Exception e)
            {
                Log.LogWarning("The drawing check itself failed: " + e.Message);
            }
        }

        // The panel used to keep a running commentary along its foot. Watching the resources fly
        // into the vault says the same thing better, and the log still has every word of it.
        private void Say(string message)
        {
            Log.LogInfo(message);
            Answer(message, true);
        }

        private void Trouble(string message)
        {
            _troubles++;
            Log.LogWarning(message);
            Answer(message, false);
        }

        /// <summary>
        /// Finds the root the vault interface is actually drawn under.
        ///
        /// Matching on the name was wrong and cost a launch: the survey named the branch
        /// MainScene_Root, but the UIRoot component sits on a child called "UI Root", so the name
        /// test never matched and the search settled for whichever root came last — one nothing
        /// renders. The HUD button is the answer, because it is the one thing here already proven
        /// to be on screen: whatever root it hangs off is the root that draws.
        /// </summary>
        private UIRoot FindUiRoot()
        {
            if (_hudButton != null)
            {
                UIRoot fromButton = NGUITools.FindInParents<UIRoot>(_hudButton);
                if (fromButton != null)
                {
                    Log.LogInfo("Root taken from the HUD button: '" + Path(fromButton.gameObject) + "'.");
                    return fromButton;
                }
            }

            // No button to follow. Report every candidate rather than settling silently, so a wrong
            // choice is visible in the log instead of being an invisible window.
            UIRoot[] roots = Resources.FindObjectsOfTypeAll<UIRoot>();
            UIRoot best = null;

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null) continue;
                bool live = roots[i].gameObject.activeInHierarchy;
                Log.LogInfo("  candidate root '" + Path(roots[i].gameObject) + "' active=" + live +
                            " layer=" + LayerMask.LayerToName(roots[i].gameObject.layer) +
                            " activeHeight=" + roots[i].activeHeight);

                if (!live) continue;
                if (roots[i].name.IndexOf("World", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (best == null) best = roots[i];

                if (Path(roots[i].gameObject).IndexOf("MainScene", StringComparison.OrdinalIgnoreCase) >= 0)
                    best = roots[i];
            }

            if (best != null) Log.LogInfo("Root chosen by search: '" + Path(best.gameObject) + "'.");
            return best;
        }

        /// <summary>The full path of an object, which is the only way to tell two 'UI Root's apart.</summary>
        private static string Path(GameObject go)
        {
            if (go == null) return "(none)";

            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        /// <summary>
        /// Takes a font off a label the game already draws.
        ///
        /// A UILabel with no font renders nothing at all and says nothing about it, and there is no
        /// font of ours to give it. Borrowing one also means the panel reads in the game's own face
        /// rather than in something that looks imported.
        /// </summary>
        private void BorrowFont()
        {
            if (_font != null) return;

            UILabel[] labels = Resources.FindObjectsOfTypeAll<UILabel>();
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel label = labels[i];
                if (label == null) continue;

                object bitmap = ReadAny(label, "bitmapFont");
                object dynamic = ReadAny(label, "trueTypeFont");
                if (bitmap == null && dynamic == null) continue;

                // The typeface only. Taking the size as well made the whole panel a lottery:
                // this walks every label in memory and stops at the first one with a font, and
                // what that label happens to be depends on what the game has loaded. One session
                // it was a caption of size 20; the next it was 'AdditionalText' at 40, and every
                // word in the panel came out half again as large.
                _font = bitmap != null ? bitmap : dynamic;
                Log.LogInfo("Borrowed a font from '" + label.name + "': " +
                            _font.GetType().Name + ".");
                return;
            }

            Log.LogWarning("No font found on any label; the panel's text will not draw.");
        }

        /// <summary>A drawn texture, positioned in the window's own space.</summary>
        /// <summary>A button whose word is a drawn sign, so it sits where the button's middle is.</summary>
        private GameObject MakeSignButton(Transform parent, string name, bool plus,
                                          int x, int y, int width, int height,
                                          EventDelegate.Callback onClick)
        {
            GameObject button = MakeButton(parent, name, "", x, y, width, height, false, onClick);

            int mark = Mathf.Min(width, height) - 8;

            GameObject drawn = new GameObject("Sign");
            drawn.layer = button.layer;
            drawn.transform.SetParent(button.transform, false);
            drawn.transform.localPosition = Vector3.zero;
            drawn.transform.localScale = Vector3.one;

            UITexture face = drawn.AddComponent<UITexture>();
            face.mainTexture = Skin.Sign(mark, plus);
            face.width = mark;
            face.height = mark;
            face.depth = button.GetComponent<UITexture>().depth + 2;

            Shader flat = Shader.Find("Unlit/Transparent Colored");
            if (flat != null) face.shader = flat;

            return button;
        }

        private UITexture Plate(Transform parent, string name, int x, int y, int width, int height,
                                Texture2D texture, int depthOffset)
        {
            GameObject go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one;

            UITexture drawn = go.AddComponent<UITexture>();
            drawn.mainTexture = texture;
            drawn.width = width;
            drawn.height = height;
            drawn.depth = depthOffset;

            // A drawn plate is only even if it is drawn at the size it is shown at. Stretching one
            // pulls its corners out of round, which is the artefact this is here to catch -- and
            // eyeballing nine thousand lines for the next one is not a method. Solid fills have no
            // corners to pull, so they are allowed to stretch.
            WarnIfStretched(name, texture, width, height);

            Shader shader = Shader.Find("Unlit/Transparent Colored");
            if (shader != null) drawn.shader = shader;

            return drawn;
        }

        private static void WarnIfStretched(string name, Texture2D texture, int width, int height)
        {
            // A fill with no corners has nothing to pull out of round, and Solid is drawn small
            // on purpose so it can be stretched over anything.
            if (texture == null || texture.width <= 16 || texture.height <= 16) return;

            int wantWide = Mathf.RoundToInt(width * Skin.Scale);
            int wantTall = Mathf.RoundToInt(height * Skin.Scale);

            if (Mathf.Abs(texture.width - wantWide) <= 1 && Mathf.Abs(texture.height - wantTall) <= 1)
                return;

            ReportOnce("stretch_" + name,
                       "'" + name + "' is drawn " + texture.width + "x" + texture.height +
                       " and shown at " + wantWide + "x" + wantTall +
                       "; its corners will be pulled out of round.");
        }

        private UILabel MakeLabel(Transform parent, string name, string text,
                                  int x, int y, int width, int height, Color colour, int depthOffset)
        {
            GameObject go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one;

            UILabel label = go.AddComponent<UILabel>();

            UIFont bitmap = _font as UIFont;
            if (bitmap != null) label.bitmapFont = bitmap;
            else
            {
                Font dynamic = _font as Font;
                if (dynamic != null) label.trueTypeFont = dynamic;
            }

            label.text = text;
            label.color = colour;
            label.width = width;
            label.height = height;
            label.depth = depthOffset;
            label.alignment = NGUIText.Alignment.Center;
            label.overflowMethod = UILabel.Overflow.ShrinkContent;

            // The tier everything starts on. Left unset, a label takes the borrowed font's own
            // size -- which is how a panel of forty labels came to have no relationship at all
            // between what a thing was and how loudly it said so. The few places that want
            // quieter or louder ask for it straight after this.
            label.fontSize = TextRow;

            return label;
        }

        /// <summary>
        /// A left-aligned label placed by the edge its text starts at.
        ///
        /// NGUI positions a label by its centre, so a left-aligned one placed where its text should
        /// begin actually begins half its own width further left. That is how every resource name
        /// ended up outside the window with its first letters cut off.
        /// </summary>
        private UILabel MakeLeftLabel(Transform parent, string name, string text,
                                      int left, int y, int width, int height, Color colour, int depth)
        {
            UILabel label = MakeLabel(parent, name, text, left + width / 2, y, width, height,
                                      colour, depth);
            label.alignment = NGUIText.Alignment.Left;
            return label;
        }

        /// <summary>The same, placed by the edge its text ends at.</summary>
        private UILabel MakeRightLabel(Transform parent, string name, string text,
                                       int right, int y, int width, int height, Color colour, int depth)
        {
            UILabel label = MakeLabel(parent, name, text, right - width / 2, y, width, height,
                                      colour, depth);
            label.alignment = NGUIText.Alignment.Right;
            return label;
        }

        private GameObject MakeButton(Transform parent, string name, string text,
                                      int x, int y, int width, int height,
                                      bool solid, EventDelegate.Callback onClick)
        {
            GameObject go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one;

            UITexture face = go.AddComponent<UITexture>();
            face.mainTexture = solid ? Skin.SolidButton(width, height) : Skin.Button(width, height);
            face.width = width;
            face.height = height;
            face.depth = 4;

            Shader shader = Shader.Find("Unlit/Transparent Colored");
            if (shader != null) face.shader = shader;

            // NGUI routes clicks through colliders; without one the button is decoration.
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, height, 1f);
            box.isTrigger = true;

            UIButton button = go.AddComponent<UIButton>();
            button.tweenTarget = go;
            button.onClick.Add(new EventDelegate(onClick));

            MakeLabel(go.transform, "Text", text, 0, 0, width - 16, height,
                      solid ? Skin.Ink : Skin.Bright, 6);

            return go;
        }

        private void Update()
        {
            if (!Enabled.Value) return;

            EnsureHudButton();
            UpdateCameraHold();

            if (_panelOpen)
            {
                ForgetOldAnswers();
                if (_tab == Tab.Grant) TickConfirmations();
                if (_tab == Tab.Create) TickTheDie();
            }

            if (++_upkeepFrames >= 90)
            {
                _upkeepFrames = 0;
                KeepThePowersOn();
                _rushingRooms = null;      // rooms are built and sold; the list goes stale
            }

            // A slow beat leaves a gap, and a rush that finishes inside that gap can still go
            // wrong. While anything is being rushed the chance is cleared every frame, which costs
            // one call per rushing room — and a vault rarely rushes more than one at a time.
            if (RushAlwaysWorks != null && RushAlwaysWorks.Value) GuardTheRushes();

            // A window that builds without error and draws nothing is the failure this mod has
            // already paid for once. Rather than trust that it appeared, look: a widget that is
            // being drawn has a draw call. If it has none after a few frames, say so and let the
            // scaffold take over, so the panel is never simply missing.
            if (_panelOpen && _nguiWindow != null && !_drawChecked && ++_drawCheckFrames >= 10)
            {
                _drawChecked = true;
                ReportDrawing();
            }

            // Filmed every frame while the bench is open, which makes it a living picture rather
            // than three stills that never quite agreed. It also settles the older complaint by
            // itself: a render texture is not kept for you — the game reclaims it whenever it is
            // busy — and a picture taken every frame cannot go stale. One camera and one object is
            // a small price for a figure that stands there breathing.
            if (_panelOpen && _tab == Tab.Create && _making == Making.Dweller &&
                _previewDweller != null)
            {
                RefreshPreview();
            }

            if (_panelOpen && _nguiWindow != null && ++_refreshFrames >= 30)
            {
                _refreshFrames = 0;
                try
                {
                    RefreshValues();

                    for (int i = 0; i < _thumbs.Count; i++)
                    {
                        UITexture thumb = _thumbs[i];
                        if (thumb == null) continue;

                        // The thumb takes the height its texture was drawn at, rather than the
                        // texture being drawn at a rounded-off height and then stretched to the
                        // thumb's real one. Eight units of stretch on a bar ten wide is what
                        // squared off its ends.
                        int step = Mathf.Max(16, (thumb.height / 8) * 8);

                        if (thumb.mainTexture != null && thumb.height == step &&
                            thumb.mainTexture.height == Mathf.RoundToInt(step * Skin.Scale)) continue;

                        thumb.height = step;
                        thumb.mainTexture = Skin.Frame(10, step, 5, Skin.EdgeButton,
                                                       Skin.Bright, Skin.Bright);
                    }

                    if (_petArtPending)
                    {
                        _petArtPending = false;
                        if (_grantFamily == Family.Pet) FillRows();
                        if (_making == Making.Pet) RefreshPetPick();
                    }

                    if (_filterInput != null)
                    {
                        string typed = _filterInput.value == null ? "" : _filterInput.value;
                        if (typed != _appliedFilter)
                        {
                            _appliedFilter = typed;
                            _filter = typed;
                                            RefreshThings();
                        }
                    }
                }
                catch (Exception e) { ReportOnce("refresh", "Refreshing the figures failed: " + e.Message); }
            }

            try
            {
                // Null with no keyboard attached, and null again between some scene loads, so it is
                // checked every frame rather than resolved once.
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null) return;

                KeyControl key = keyboard[_toggleKey];
                if (key != null && key.wasPressedThisFrame) TogglePanel();
            }
            catch (Exception e)
            {
                // An exception escaping Update is a crash in the player's game, not a bug in a
                // debug panel.
                ReportOnce("update", "Hotkey handling failed: " + e.Message);
            }
        }

        private void OnGUI()
        {
            if (!Enabled.Value || !_panelOpen) return;

            // The panel proper is the NGUI window; this scaffold is what is left of the one that
            // came before it. It stays for the case where the window cannot be built or, having
            // been built, never reaches the screen — so the mod is never simply unreachable.
            if (_nguiWindow != null && (!_drawChecked || _nguiDrawing)) return;

            try
            {
                _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow,
                                           PluginName + " " + PluginVersion);
            }
            catch (Exception e)
            {
                ReportOnce("ongui", "Drawing the panel failed: " + e.Message);
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Grants go through the game's own methods.");
            if (GUILayout.Button("Survey UI", GUILayout.Width(80f))) SurveyInterface();
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            Vault vault = SafeVault();
            if (vault == null || !vault.Loaded)
            {
                GUILayout.Label("No vault loaded.");
                GUI.DragWindow();
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawResources(vault);
            GUILayout.Space(8f);
            DrawBoxes();
            GUILayout.Space(8f);
            DrawItems();
            GUILayout.Space(8f);
            DrawPets();
            GUILayout.Space(8f);
            DrawDwellers();
            GUILayout.Space(8f);
            DrawInventory(vault);

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void DrawResources(Vault vault)
        {
            GUILayout.Label("Resources");

            VaultStorage storage = vault.Storage;
            if (storage == null || storage.Resources == null)
            {
                GUILayout.Label("    unavailable");
                return;
            }

            GameResources held = storage.Resources;
            GameResources cap = storage.MaxResources;

            foreach (EResource resource in Enum.GetValues(typeof(EResource)))
            {
                if (resource == EResource.None || resource == EResource.Count) continue;
                if (Array.IndexOf(NotRealResources, resource) >= 0) continue;

                float amount;
                try { amount = held[resource]; }
                catch { continue; }   // not every enum member is a resource the vault stores

                string line = resource + ": " + amount.ToString("0");
                if (cap != null)
                {
                    float max;
                    try { max = cap[resource]; } catch { max = 0f; }
                    if (max > 0f) line += " / " + max.ToString("0");
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label(line, GUILayout.Width(190f));

                for (int i = 0; i < GrantAmounts.Length; i++)
                {
                    if (GUILayout.Button("+" + GrantAmounts[i].ToString("0"), GUILayout.Width(60f)))
                        Grant(resource, GrantAmounts[i]);
                }
                if (GUILayout.Button("Fill", GUILayout.Width(44f))) FillToCap(resource);

                GUILayout.EndHorizontal();
            }
        }

        private void DrawBoxes()
        {
            GUILayout.Label("Boxes");

            foreach (ELunchBoxType type in BoxTypes)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(type.ToString(), GUILayout.Width(190f));

                for (int i = 0; i < BoxAmounts.Length; i++)
                {
                    if (GUILayout.Button("+" + BoxAmounts[i], GUILayout.Width(60f)))
                        GrantBoxes(type, BoxAmounts[i]);
                }

                GUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Writes down every name the catalogue asks for and every name the atlases hold.
        ///
        /// Four rounds have now been spent on sprite names, each one a guess corrected by the next.
        /// A file with both lists in it ends that: whatever is still blank can be looked up rather
        /// than guessed at. Off by default — it is a page of text nobody playing the game needs.
        /// </summary>
        private void WriteReport()
        {
            try
            {
                string path = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Info.Location), "VaultAdmin-icons.txt");

                System.Text.StringBuilder text = new System.Text.StringBuilder();
                text.AppendLine("What the catalogue asks for, and what the atlases hold.");
                text.AppendLine();

                text.AppendLine("== items ==");
                for (int i = 0; i < _catalogue.Count; i++)
                {
                    CatalogueEntry entry = _catalogue[i];
                    UIAtlas atlas;
                    _atlases.TryGetValue(entry.Type, out atlas);

                    bool have = atlas != null && !string.IsNullOrEmpty(entry.Sprite) &&
                                atlas.GetSprite(entry.Sprite) != null;

                    text.AppendLine((have ? "  ok   " : "  MISS ") + entry.Type + "  id=" + entry.Id +
                                    "  name=" + entry.Name + "  sprite=" + entry.Sprite);
                }

                // What the constructor has to offer, per gender. A category that comes back empty
                // is a filter that is wrong, and the only way to see that without launching twice
                // is to write it down the first time.
                text.AppendLine();
                text.AppendLine("== appearance ==");

                // Borrowed, and given back. Walking both catalogues means moving the bench's own
                // gender, and this used to end by setting it to Male rather than to whatever it
                // found -- which is how choosing a woman produced a man.
                int wasGender = _genderIndex;
                _rollWhenReady = false;

                foreach (EGender gender in Genders)
                {
                    _genderIndex = Array.IndexOf(Genders, gender);
                    if (_genderIndex < 0) _genderIndex = 0;

                    RebuildLookOptions();

                    text.AppendLine("  " + gender);
                    AppendRawNames(text, _hair);
                    AppendChoice(text, _hair);
                    AppendChoice(text, _face);
                    AppendChoice(text, _hairColour);
                    AppendChoice(text, _helmet);
                    AppendChoice(text, _skin);
                }

                _rollWhenReady = true;

                _genderIndex = wasGender;
                RebuildLookOptions();
                ShowGender();

                text.AppendLine();
                text.AppendLine("== atlases ==");

                UIAtlas[] all = Resources.FindObjectsOfTypeAll<UIAtlas>();
                for (int i = 0; i < all.Length; i++)
                {
                    List<UISpriteData> sprites = SpritesOf(all[i]);
                    if (sprites == null) continue;

                    text.AppendLine("  " + all[i].name + " (" + sprites.Count + ")");
                    for (int j = 0; j < sprites.Count; j++)
                    {
                        if (sprites[j] == null) continue;
                        text.AppendLine("      " + sprites[j].name);
                    }
                }

                System.IO.File.WriteAllText(path, text.ToString());
                Log.LogInfo("Wrote the icon report to " + path + ".");
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not write the icon report: " + e.Message);
            }
        }

        /// <summary>
        /// Every field a hairstyle could be named from, and what each of them actually says.
        ///
        /// The list currently reads 24, 9 and null, which are three different failures wearing the
        /// same clothes. Rather than guess which field to prefer -- a habit that has cost several
        /// rounds already -- this writes down what is on the record and lets the answer be read off
        /// it. The whole member list of the first entry goes down once, so a field nobody thought
        /// to try is still visible.
        /// </summary>
        private static void AppendRawNames(System.Text.StringBuilder text, Choice choice)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance;

            try
            {
                text.AppendLine("      " + choice.Caption + " -- raw:");

                for (int i = choice.HasNone ? 1 : 0; i < choice.Options.Count && i <= 40; i++)
                {
                    object entry = choice.Options[i];
                    if (entry == null) { text.AppendLine("          " + i + ": <null entry>"); continue; }

                    text.AppendLine("          " + i +
                                    ": TitleTextId=" + Show(ReadMember(entry, "TitleTextId")) +
                                    "  PieceName=" + Show(ReadMember(entry, "PieceName")) +
                                    "  Id=" + Show(ReadMember(entry, "Id")) +
                                    "  shown=" + Show(i < choice.Labels.Count ? choice.Labels[i] : null));
                }

                for (int i = choice.HasNone ? 1 : 0; i < choice.Options.Count; i++)
                {
                    object entry = choice.Options[i];
                    if (entry == null) continue;

                    text.AppendLine("          all members of entry " + i + " (" +
                                    entry.GetType().Name + "):");

                    FieldInfo[] fields = entry.GetType().GetFields(Flags);
                    for (int f = 0; f < fields.Length; f++)
                        text.AppendLine("              ." + fields[f].Name + " = " +
                                        Show(SafeText(fields[f].GetValue(entry))));

                    PropertyInfo[] props = entry.GetType().GetProperties(Flags);
                    for (int f = 0; f < props.Length; f++)
                    {
                        if (props[f].GetIndexParameters().Length > 0) continue;

                        string got;
                        try { got = SafeText(props[f].GetValue(entry, null)); }
                        catch { got = "<threw>"; }

                        text.AppendLine("              ." + props[f].Name + " = " + Show(got));
                    }

                    break;
                }
            }
            catch (Exception e)
            {
                text.AppendLine("          could not read the raw names: " + e.Message);
            }
        }

        private static string Show(string what)
        {
            if (what == null) return "<null>";
            return what.Length == 0 ? "<empty>" : "\"" + what + "\"";
        }

        private static string SafeText(object what)
        {
            if (what == null) return null;

            try { return what.ToString(); }
            catch { return "<threw>"; }
        }

        private static void AppendChoice(System.Text.StringBuilder text, Choice choice)
        {
            int count = choice.HasNone ? choice.Options.Count - 1 : choice.Options.Count;
            text.AppendLine("      " + choice.Caption + ": " + count);

            for (int i = choice.HasNone ? 1 : 0; i < choice.Labels.Count && i <= 8; i++)
                text.AppendLine("          " + choice.Labels[i]);

            if (count > 8) text.AppendLine("          ... and " + (count - 8) + " more");
        }

        /// <summary>
        /// Reads the game's own item tables.
        ///
        /// The identifier differs per family and neither Name nor CodeId is it. Weapons are found
        /// by a search comparing WeaponId; outfits by a dictionary keyed on the private field
        /// m_outfitId, which has no Id-suffixed property at all. Both were read out of the IL of
        /// ItemParameters rather than guessed, because an id the game cannot resolve produces an
        /// item with no data behind it.
        /// </summary>
        private void BuildCatalogue()
        {
            _catalogue = new List<CatalogueEntry>();

            try
            {
                GameParameters parameters = GameParameters.Instance;
                if (parameters == null || parameters.Items == null)
                {
                    ReportOnce("catalogue", "The game's item tables are not available yet.");
                    _catalogue = null;   // try again next time the section is drawn
                    return;
                }

                ItemParameters items = parameters.Items;

                _defaultOutfitId = ReadMember(items, "m_vaultDefaultOutfit");
                _defaultWeaponId = ReadMember(items, "m_vaultDefaultWeapon");
                Log.LogInfo("The default kit is outfit '" + _defaultOutfitId +
                            "' and weapon '" + _defaultWeaponId + "'; neither is offered.");

                int weapons = Collect(items.WeaponsList, EItemType.Weapon,
                                      "WeaponId", "WeaponSprite", "m_NameLocalizationId");
                int outfits = Collect(items.OutfitList, EItemType.Outfit,
                                      "m_outfitId", "OutfitSprite", "m_outfitNameLocalizationId");
                int junk = Collect(items.JunksList, EItemType.Junk,
                                   "JunkId", "JunkSprite", "m_NameLocalizationId");

                _atlases[EItemType.Weapon] = items.WeaponAtlas;
                _atlases[EItemType.Outfit] = items.OutfitAtlas;
                _atlases[EItemType.Junk] = items.JunkAtlas;

                Log.LogInfo("Item catalogue read from the game: " + weapons + " weapons, " +
                            outfits + " outfits, " + junk + " junk.");

                if (WriteIconReport.Value) WriteReport();
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not read the item catalogue: " + e.Message);
            }
        }

        /// <summary>
        /// Pulls one family into the catalogue, taking the id off whichever member that family is
        /// keyed by. Reflection rather than a direct call because the outfit id is a private field.
        /// </summary>
        private string _defaultOutfitId;
        private string _defaultWeaponId;

        private bool IsDefaultKit(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            return id == _defaultOutfitId || id == _defaultWeaponId;
        }

        private int Collect(Array table, EItemType type, string idMember, string spriteMember,
                            string nameMember)
        {
            if (table == null) return 0;

            int added = 0;
            int nameless = 0;

            for (int i = 0; i < table.Length; i++)
            {
                try
                {
                    DwellerBaseItem data = table.GetValue(i) as DwellerBaseItem;
                    if (data == null || data.IsHiddenItem) continue;

                    string id = ReadMember(data, idMember);
                    if (string.IsNullOrEmpty(id)) continue;

                    // The bare hand and the vault suit are what a dweller has when they have
                    // nothing. Offering to hand someone their own fists is not a thing the panel
                    // should do, and the game names both of them for us.
                    if (IsDefaultKit(id)) continue;

                    // The ten costume_NN outfits list as Casual01 to Casual10, name no art, and do
                    // not arrive when granted. They are dressing for something else in the game,
                    // not stock a vault can hold.
                    if (id.StartsWith("costume_", StringComparison.OrdinalIgnoreCase)) continue;

                    // The plain clothes a dweller arrives in. Like the jumpsuit and the fist, it is
                    // what someone has when they have nothing, and granting it does nothing.
                    if (id == "NormalClothing") continue;

                    // The Name property returns the data object's own name, which in these tables
                    // is a bare number. Every family carries a GetName that does the lookup properly.
                    string label = CallText(data, "GetName");
                    if (string.IsNullOrEmpty(label)) label = Localised(ReadMember(data, nameMember));

                    // Every item the vault can actually hold has a written name. The handful that
                    // have none are leftovers — the police baton with one point of damage and no
                    // picture, the plain clothes a dweller arrives in — and listing them by their
                    // row number offered the player something they could not use.
                    if (string.IsNullOrEmpty(label))
                    {
                        nameless++;
                        continue;
                    }

                    CatalogueEntry entry = new CatalogueEntry();
                    entry.Type = type;
                    entry.Id = id;
                    entry.Name = label;
                    entry.Rarity = data.ItemRarity;
                    entry.Sprite = ReadMember(data, spriteMember);
                    entry.Stats = Describe(data, type);
                    entry.Effect = Describe(data, type, false);
                    entry.Power = Rate(data, type);
                    if (type == EItemType.Outfit) entry.Stats7 = OutfitStats(data);
                    _catalogue.Add(entry);
                    added++;
                }
                catch { }   // one unreadable row must not cost the whole family
            }

            if (nameless > 0)
                Log.LogInfo("Left out " + nameless + " " + type + " row(s) with no written name.");

            return added;
        }

        /// <summary>
        /// Calls a no-argument method and returns what it says.
        ///
        /// Each item family has its own GetName, and each one runs the localisation lookup itself.
        /// Reading the key and looking it up here was the wrong way round: the table those keys
        /// belong to is not the one NGUI keeps, which is why every item was listed by its id.
        /// </summary>
        private static string CallText(object target, string method)
        {
            if (target == null) return null;

            try
            {
                MethodInfo found = target.GetType().GetMethod(
                    method,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);

                if (found == null) return null;

                object answer = found.Invoke(target, null);
                return answer == null ? null : answer.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads a number without turning it into words first.
        ///
        /// Going through ToString and back was quietly wrong: the text is written in the machine's
        /// own culture, which here writes 0,05, and it was being read back as invariant, which
        /// expects 0.05. Every fraction in the game came out as nought, and only whole numbers
        /// survived the trip.
        /// </summary>
        private static float ReadFloat(object target, string member)
        {
            object value = ReadObject(target, member);
            if (value == null) return 0f;

            try
            {
                return Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Reads a member as text, whatever type it is.
        ///
        /// ReadMember casts to string and answers null for everything else, so every number asked
        /// for through it came back empty — which is why no weapon showed its damage and no outfit
        /// its bonus.
        /// </summary>
        private static string ReadAsText(object target, string member)
        {
            object value = ReadObject(target, member);
            return value == null ? null : value.ToString();
        }

        private static string ReadMember(object target, string member)
        {
            Type t = target.GetType();
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            PropertyInfo prop = t.GetProperty(member, Flags);
            if (prop != null) return prop.GetValue(target, null) as string;

            FieldInfo field = t.GetField(member, Flags);
            if (field != null) return field.GetValue(target) as string;

            return null;
        }

        private void DrawItems()
        {
            GUILayout.Label("Items");

            if (_catalogue == null) BuildCatalogue();
            if (_catalogue == null) { GUILayout.Label("    catalogue unavailable"); return; }

            GUILayout.BeginHorizontal();
            foreach (EItemType type in new[] { EItemType.Weapon, EItemType.Outfit, EItemType.Junk })
            {
                bool active = _family == type;
                if (GUILayout.Toggle(active, type.ToString(), "Button", GUILayout.Width(80f)) && !active)
                    _family = type;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter", GUILayout.Width(44f));
            _filter = GUILayout.TextField(_filter == null ? "" : _filter);
            GUILayout.EndHorizontal();

            _itemScroll = GUILayout.BeginScrollView(_itemScroll, GUILayout.Height(200f));

            int shown = 0;
            int matched = 0;
            for (int i = 0; i < _catalogue.Count; i++)
            {
                CatalogueEntry entry = _catalogue[i];
                if (entry.Type != _family) continue;
                if (_filter.Length > 0 &&
                    entry.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                matched++;
                if (shown >= MaxRowsShown) continue;
                shown++;

                GUILayout.BeginHorizontal();
                DrawIcon(entry);
                GUILayout.Label(entry.Name + "  (" + entry.Rarity + ")", GUILayout.Width(258f));
                if (GUILayout.Button("Grant", GUILayout.Width(60f))) GrantItem(entry);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            if (matched > shown)
                GUILayout.Label("    " + matched + " match; showing " + shown + ". Narrow the filter.");
        }

        /// <summary>
        /// Turns a localisation key into words.
        ///
        /// The table answers with the key itself when it holds nothing for it, so that case is
        /// treated as a miss rather than shown to the player as a name.
        /// </summary>
        private static MethodInfo _gameText;
        private static bool _lookedForGameText;

        /// <summary>
        /// The game's own text for a key.
        ///
        /// Not NGUI's table — that is a different one, and asking it is what left every appearance
        /// option named after a file, with underscores showing. The game keeps its strings in
        /// ScriptLocalization, which lives in another assembly, so it is found by name once.
        /// </summary>
        private static string GameText(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (!_lookedForGameText)
            {
                _lookedForGameText = true;

                try
                {
                    // I2.Loc.ScriptLocalization, with its namespace. Asking for the short name found
                    // nothing at all, which is why every option was still called after its file —
                    // and why saying that had been fixed was wrong.
                    Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();

                    for (int i = 0; i < loaded.Length && _gameText == null; i++)
                    {
                        Type table = loaded[i].GetType("I2.Loc.ScriptLocalization");
                        if (table == null) table = loaded[i].GetType("ScriptLocalization");
                        if (table == null) continue;

                        // Get(key) or Get(key, fallback) — take whichever this build carries.
                        _gameText = table.GetMethod("Get", BindingFlags.Public | BindingFlags.Static,
                                                    null, new[] { typeof(string), typeof(bool) }, null);

                        if (_gameText == null)
                            _gameText = table.GetMethod("Get",
                                                        BindingFlags.Public | BindingFlags.Static,
                                                        null, new[] { typeof(string) }, null);

                        if (_gameText != null && Log != null)
                            Log.LogInfo("Names will come from " + table.FullName + ".");
                    }

                    if (_gameText == null && Log != null)
                        Log.LogWarning("No localisation table was found; names will be file names.");
                }
                catch { }
            }

            if (_gameText == null) return null;

            try
            {
                object[] arguments = _gameText.GetParameters().Length == 2
                    ? new object[] { key, true }
                    : new object[] { key };

                string text = _gameText.Invoke(null, arguments) as string;
                if (!string.IsNullOrEmpty(text) && text != key) return text;
            }
            catch { }

            return null;
        }

        private static string Localised(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            try
            {
                string text = Localization.Get(key);
                if (!string.IsNullOrEmpty(text) && text != key) return text;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// What an item is worth knowing about, in one line.
        ///
        /// Every family keeps its figures somewhere different, and none of it is on the base type
        /// except the rarity and the price, so each is read where it lives.
        /// </summary>
        private string Describe(DwellerBaseItem data, EItemType type)
        {
            return Describe(data, type, true);
        }

        private string Describe(DwellerBaseItem data, EItemType type, bool withRarity)
        {
            try
            {
                string line = withRarity ? data.ItemRarity.ToString().ToUpper() : "";

                if (type == EItemType.Weapon)
                {
                    if (line.Length > 0) line += " ";

                    // The game writes its own damage line; there is no reason to write another.
                    string damage = CallText(data, "GetDamageAsString");
                    if (string.IsNullOrEmpty(damage))
                    {
                        string min = ReadAsText(data, "DamageMin");
                        string max = ReadAsText(data, "DamageMax");
                        if (!string.IsNullOrEmpty(min) && !string.IsNullOrEmpty(max))
                            damage = min == max ? min : min + "-" + max;
                    }
                    if (!string.IsNullOrEmpty(damage)) line += "  " + damage + " DMG";

                    string kind = ReadAsText(data, "WeaponType");
                    if (!string.IsNullOrEmpty(kind) && kind != "None") line += "  " + kind.ToUpper();
                }
                else if (type == EItemType.Outfit)
                {
                    string bonus = OutfitBonus(data);
                    if (!string.IsNullOrEmpty(bonus)) line += "  " + bonus;
                }
                else if (type == EItemType.Junk)
                {
                    string part = ReadAsText(data, "m_linkedComponent");
                    if (!string.IsNullOrEmpty(part) && part != "None") line += "  " + part.ToUpper();
                }

                // No price. Nothing here is being bought, and it crowded out the figures that
                // actually decide which of two hundred items to pick.
                return line;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>An outfit's seven bonuses in the order the letters are read.</summary>
        private int[] OutfitStats(DwellerBaseItem data)
        {
            try
            {
                object stats = ReadObject(data, "m_specialStats");
                if (stats == null) return null;

                int[] values = new int[Specials.Length];
                for (int i = 0; i < Specials.Length; i++)
                {
                    object entry = ReadObject(stats, Specials[i].ToString());
                    int value;
                    if (entry != null && int.TryParse(ReadAsText(entry, "Value"), out value))
                        values[i] = value;
                }
                return values;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// One number for how much an item does, so a list of them can be put in order.
        ///
        /// A weapon's is its damage and an outfit's is the sum of what it adds; junk has neither and
        /// falls back on what it sells for, which is the only thing that separates one lump of scrap
        /// from another.
        /// </summary>
        private int Rate(DwellerBaseItem data, EItemType type)
        {
            try
            {
                if (type == EItemType.Weapon)
                {
                    int most;
                    if (int.TryParse(ReadAsText(data, "DamageMax"), out most)) return most;
                }
                else if (type == EItemType.Outfit)
                {
                    object stats = ReadObject(data, "m_specialStats");
                    if (stats != null)
                    {
                        int total = 0;
                        for (int i = 0; i < Specials.Length; i++)
                        {
                            object entry = ReadObject(stats, Specials[i].ToString());
                            int value;
                            if (entry != null && int.TryParse(ReadAsText(entry, "Value"), out value))
                                total += value;
                        }
                        return total;
                    }
                }

                return data.SellPrice;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// An outfit's SPECIAL bonuses, written the way the game writes them.
        ///
        /// ModificationStats is an array of the game's own stat records; the two members worth
        /// reading off each are which stat it is and by how much, and neither resolves by name from
        /// this assembly.
        /// </summary>
        private string OutfitBonus(DwellerBaseItem data)
        {
            // The bonuses are not a list to be walked but seven named fields, one per stat, each
            // holding a single Value. ModificationStats is a different thing and is usually empty,
            // which is why every outfit came back with nothing to say for itself.
            try
            {
                object stats = ReadObject(data, "m_specialStats");
                if (stats == null) stats = ReadObject(data, "SpecialStats");
                if (stats == null) return null;

                string line = "";

                for (int i = 0; i < Specials.Length; i++)
                {
                    object entry = ReadObject(stats, Specials[i].ToString());
                    if (entry == null) continue;

                    string value = ReadAsText(entry, "Value");
                    int amount;
                    if (!int.TryParse(value, out amount) || amount == 0) continue;

                    if (line.Length > 0) line += " ";
                    line += (amount > 0 ? "+" : "") + amount + Specials[i].ToString().Substring(0, 1);
                }

                return line.Length > 0 ? line : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Reads a member without turning it into a string first.</summary>
        private static object ReadObject(object target, string member)
        {
            if (target == null) return null;

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance;
            Type type = target.GetType();

            PropertyInfo property = type.GetProperty(member, Flags);
            if (property != null && property.CanRead) return property.GetValue(target, null);

            FieldInfo field = type.GetField(member, Flags);
            if (field != null) return field.GetValue(target);

            return null;
        }

        /// <summary>
        /// Draws one item's own icon out of its family's atlas.
        ///
        /// An atlas is a texture plus a table of pixel rectangles, which is exactly what
        /// DrawTextureWithTexCoords takes once the rectangle is normalised. NGUI measures y
        /// downwards from the top of the texture and the drawing call measures upwards from the
        /// bottom, so it is flipped.
        ///
        /// The lookup is the part worth keeping: when the panel becomes real game UI the sprite
        /// name and atlas go straight into a UISprite and only this drawing call is replaced.
        /// </summary>
        private void DrawIcon(CatalogueEntry entry)
        {
            Rect box = GUILayoutUtility.GetRect(40f, 40f, GUILayout.Width(40f), GUILayout.Height(40f));

            try
            {
                if (string.IsNullOrEmpty(entry.Sprite)) return;

                UIAtlas atlas;
                if (!_atlases.TryGetValue(entry.Type, out atlas) || atlas == null) return;

                Texture texture = atlas.texture;
                if (texture == null || texture.width == 0 || texture.height == 0) return;

                UISpriteData sprite = atlas.GetSprite(entry.Sprite);
                if (sprite == null || sprite.width <= 0 || sprite.height <= 0) return;

                float w = texture.width;
                float h = texture.height;

                Rect coords = new Rect(sprite.x / w,
                                       1f - (sprite.y + sprite.height) / h,
                                       sprite.width / w,
                                       sprite.height / h);

                GUI.DrawTextureWithTexCoords(box, texture, coords);
            }
            catch
            {
                // A missing sprite leaves a gap. Hiding items that cannot be illustrated would be
                // worse than a picker with a few blanks in it.
            }
        }

        /// <summary>
        /// Reads the game's pet catalogue.
        ///
        /// Reached as Catalog.Instance.m_petsCustomizationData.PetItems — the route the game itself
        /// takes in GenerateRandomPet. The field is public but its type does not resolve by name
        /// from this assembly, so the list comes back through reflection.
        /// </summary>
        private void BuildPetCatalogue()
        {
            _pets = new List<PetEntry>();

            try
            {
                Catalog catalog = Catalog.Instance;
                if (catalog == null) { _pets = null; return; }

                object customisation = catalog.m_petsCustomizationData;
                if (customisation == null) { _pets = null; return; }

                object list = ReadAny(customisation, "PetItems");
                System.Collections.IEnumerable items = list as System.Collections.IEnumerable;
                if (items == null)
                {
                    ReportOnce("petlist", "The pet catalogue is not a list this build understands.");
                    return;
                }

                foreach (object template in items)
                {
                    if (template == null) continue;
                    try
                    {
                        string id = ReadMember(template, "PetId");
                        if (string.IsNullOrEmpty(id)) continue;

                        PetEntry entry = new PetEntry();
                        entry.Template = template;
                        entry.PetId = id;

                        string label = ReadMember(template, "BaseName");
                        entry.Name = string.IsNullOrEmpty(label) ? id : label;

                        object type = ReadAny(template, "Type");
                        object breed = ReadAny(template, "Breed");
                        entry.PetType = type;
                        entry.Detail = (type == null ? "?" : type.ToString()) + " / " +
                                       (breed == null ? "?" : breed.ToString());

                        // Pets are graded like items — they descend from the same record — and the
                        // best their bonus can do is what separates one from another.
                        DwellerBaseItem asItem = template as DwellerBaseItem;
                        if (asItem != null) entry.Rarity = asItem.ItemRarity;
                        entry.Power = PetPower(template);

                        _pets.Add(entry);
                    }
                    catch { }
                }

                Log.LogInfo("Pet catalogue read from the game: " + _pets.Count + " pets.");
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not read the pet catalogue: " + e.Message);
            }
        }

        private void DrawPets()
        {
            GUILayout.Label("Pets");

            if (_pets == null) BuildPetCatalogue();
            if (_pets == null) { GUILayout.Label("    catalogue unavailable"); return; }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(44f));
            _petName = GUILayout.TextField(_petName == null ? "" : _petName, GUILayout.Width(140f));
            GUILayout.Label("Value", GUILayout.Width(40f));
            _petBonusValue = GUILayout.TextField(_petBonusValue == null ? "" : _petBonusValue, GUILayout.Width(50f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Bonus", GUILayout.Width(44f));
            if (GUILayout.Button("<", GUILayout.Width(24f)))
                _petBonusIndex = (_petBonusIndex - 1 + Bonuses().Length) % Bonuses().Length;
            GUILayout.Label(Tidy(Bonuses()[_petBonusIndex].ToString()), GUILayout.Width(180f));
            if (GUILayout.Button(">", GUILayout.Width(24f)))
                _petBonusIndex = (_petBonusIndex + 1) % Bonuses().Length;
            GUILayout.EndHorizontal();

            GUILayout.Label("    An empty name keeps the one the game generates.");

            _petScroll = GUILayout.BeginScrollView(_petScroll, GUILayout.Height(140f));
            int shown = 0;
            for (int i = 0; i < _pets.Count && shown < MaxRowsShown; i++)
            {
                PetEntry entry = _pets[i];
                if (_filter.Length > 0 &&
                    entry.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown++;

                GUILayout.BeginHorizontal();
                GUILayout.Label(entry.Name + "  (" + entry.Detail + ")", GUILayout.Width(300f));
                if (GUILayout.Button("Grant", GUILayout.Width(60f))) GrantPet(entry);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// Creates a pet the way the game creates one, then overwrites the three fields the panel
        /// offers.
        ///
        /// The order is the point. Letting GenerateRandomData run first means everything it fills
        /// in stays filled in, and what lands in the save is a pet the game built with three fields
        /// changed — not a record assembled by this mod and hoped over.
        /// </summary>
        private void GrantPet(PetEntry entry) { GrantPet(entry, true); }

        private void GrantPet(PetEntry entry, bool customise)
        {
            try
            {
                Vault vault = SafeVault();
                if (vault == null || !vault.Loaded || vault.Inventory == null) return;

                if (vault.Inventory.EmptySpace() <= 0)
                {
                    Trouble("The inventory is full; " + entry.Name + " was not granted.");
                    return;
                }

                // The game asks for this pet type's atlas before it builds the item, and pet art
                // loads asynchronously per type rather than being simply present the way item
                // atlases are. Skipping it is why a granted pet appeared with no picture: the
                // pet existed, its art had never been requested.
                RequestPetAtlas(entry);

                DwellerItem item = new DwellerItem(EItemType.Pet, entry.PetId);

                object unique = InvokeGenerateRandomData(entry.Template);
                if (unique == null)
                {
                    Log.LogWarning("The game did not generate data for " + entry.Name + "; nothing granted.");
                    return;
                }

                PetUniqueData data = unique as PetUniqueData;
                if (data != null && customise)
                {
                    if (!string.IsNullOrEmpty(_petName)) data.Name = _petName;

                    data.Bonus = Bonuses()[_petBonusIndex];

                    float value;
                    if (TypedNumber(_petBonusValue, out value)) data.BonusValue = value;
                }

                item.ExtraData = unique as ItemExtraData;
                vault.Inventory.AddItem(item, false, false);

                Say(customise
                    ? "Created " + (string.IsNullOrEmpty(_petName) ? entry.Name : _petName) +
                      ", a " + entry.Name + "."
                    : "Granted " + entry.Name + ".");
            }
            catch (Exception e)
            {
                Log.LogWarning("Granting pet " + entry.Name + " failed: " + e.Message);
            }
        }

        /// <summary>
        /// Asks the game to load the atlas holding this pet's art.
        ///
        /// Taken from the IL of GenerateRandomPet, which calls
        /// PetAtlasManager.Instance.LoadAtlases(petItem.Type) before constructing anything. The
        /// call returns a Coroutine: the art arrives a moment later, which is fine, because the
        /// pet is not looked at until the player opens it.
        /// </summary>
        private void RequestPetAtlas(PetEntry entry)
        {
            try
            {
                if (entry.PetType == null) return;

                PetAtlasManager manager = PetAtlasManager.Instance;
                if (manager == null) return;

                const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                MethodInfo load = typeof(PetAtlasManager).GetMethod("LoadAtlases", Flags,
                                                                    null, new[] { entry.PetType.GetType() }, null);
                if (load == null) return;

                load.Invoke(manager, new object[] { entry.PetType });
            }
            catch (Exception e)
            {
                // No atlas means no picture, not a broken pet.
                ReportOnce("petatlas", "Could not request the pet atlas: " + e.Message);
            }
        }

        private static object InvokeGenerateRandomData(object template)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            MethodInfo method = template.GetType().GetMethod("GenerateRandomData", Flags);
            if (method == null) return null;

            // The game passes null for the Random, so this does too.
            return method.Invoke(template, new object[] { null });
        }

        /// <summary>Reads any member, of any type, by name. The string-only helper cannot do enums.</summary>
        private static object ReadAny(object target, string member)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type t = target.GetType();

            PropertyInfo prop = t.GetProperty(member, Flags);
            if (prop != null) return prop.GetValue(target, null);

            FieldInfo field = t.GetField(member, Flags);
            if (field != null) return field.GetValue(target);

            return null;
        }

        private void GrantItem(CatalogueEntry entry)
        {
            try
            {
                Vault vault = SafeVault();
                if (vault == null || !vault.Loaded || vault.Inventory == null) return;

                if (vault.Inventory.EmptySpace() <= 0)
                {
                    // Say so rather than calling into an add that may quietly drop it.
                    Log.LogWarning("The inventory is full; " + entry.Name + " was not granted.");
                    return;
                }

                DwellerItem item = new DwellerItem(entry.Type, entry.Id);
                vault.Inventory.AddItem(item, false, false);
                if (entry.Type == EItemType.Weapon) _grantsMade++;

                Say("Granted " + entry.Name + ".");
            }
            catch (Exception e)
            {
                Trouble("Could not grant " + entry.Name + ": " + e.Message);
            }
        }

        /// <summary>
        /// Adds a resource through the game's own method.
        ///
        /// capped lets the game clamp to the vault's own limit, so this never has to know what the
        /// cap is. fireCallbacks raises the event the interface listens to; without it the figure
        /// on screen stays stale until something else refreshes it.
        /// </summary>
        private void Grant(EResource resource, float amount)
        {
            try
            {
                Vault vault = SafeVault();
                if (vault == null || !vault.Loaded || vault.Storage == null) return;

                vault.Storage.AddResource(new GameResources(resource, amount), true, true);
                ShowResourceFlight(resource, amount);

                Log.LogInfo("Granted " + amount.ToString("0") + " " + resource + ".");
            }
            catch (Exception e)
            {
                // Named so a failure says which grant failed, not merely that one did.
                Log.LogWarning("Granting " + amount.ToString("0") + " " + resource + " failed: " + e.Message);
            }
        }

        private void FillToCap(EResource resource)
        {
            try
            {
                Vault vault = SafeVault();
                if (vault == null || !vault.Loaded || vault.Storage == null) return;

                GameResources space = vault.Storage.GetAvailableSpace();
                if (space == null) return;

                float room = space[resource];
                if (room <= 0f) return;

                vault.Storage.AddResource(new GameResources(resource, room), true, true);
                ShowResourceFlight(resource, room);

                Log.LogInfo("Filled " + resource + " to its cap (+" + room.ToString("0") + ").");
            }
            catch (Exception e)
            {
                Log.LogWarning("Filling " + resource + " failed: " + e.Message);
            }
        }

        /// <summary>
        /// The same for boxes, which the game carries with a call of their own.
        /// </summary>
        private void ShowBoxFlight(ELunchBoxType type, int quantity)
        {
            if (quantity <= 0) return;

            try
            {
                ResourceParticleMgr particles = ResourceParticleMgr.Instance;
                if (particles == null) return;

                Camera view = Camera.main;
                Vector3 from = view != null
                    ? view.ViewportToWorldPoint(new Vector3(0.5f, 0.42f, 12f))
                    : Vector3.zero;

                MethodInfo fly = typeof(ResourceParticleMgr).GetMethod(
                    "AddLunchboxParticlesAt",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (fly == null)
                {
                    ReportOnce("boxflight", "The game has no AddLunchboxParticlesAt.");
                    return;
                }

                fly.Invoke(particles, new object[] { from, quantity, type, true, null });
            }
            catch (Exception e)
            {
                ReportOnce("boxflight", "Could not send the boxes flying: " + e.Message);
            }
        }

        /// <summary>
        /// Sends the granted resources flying into the vault, the way everything else in the game
        /// arrives.
        ///
        /// This is the game's own delivery: a parcel of resources handed to the particle manager,
        /// which carries them up to the counters. Nothing else in Fallout Shelter gives you
        /// something without showing it arrive, and a number that changes silently reads as a bug.
        /// The parcel is cosmetic — the resource is already in the vault by the time it flies —
        /// so nothing is counted twice.
        /// </summary>
        private void ShowResourceFlight(EResource resource, float amount)
        {
            if (amount <= 0f) return;

            try
            {
                ResourceParticleMgr particles = ResourceParticleMgr.Instance;
                if (particles == null) return;

                Storage parcel = new Storage();
                parcel.AddResource(new GameResources(resource, amount), true, true);

                Camera view = Camera.main;
                Vector3 from = view != null
                    ? view.ViewportToWorldPoint(new Vector3(0.5f, 0.42f, 12f))
                    : Vector3.zero;

                MethodInfo fly = typeof(ResourceParticleMgr).GetMethod(
                    "CollectResourcesWorld",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (fly == null)
                {
                    ReportOnce("flight", "The game has no CollectResourcesWorld; nothing will fly.");
                    return;
                }

                // capped, sound, transfer, ignore the ratio. Transfer is off: the vault already has
                // them, and these are the same resources arriving, not more of them.
                fly.Invoke(particles, new object[] { from, parcel, true, true, false, true });
            }
            catch (Exception e)
            {
                ReportOnce("flight", "Could not send the resources flying: " + e.Message);
            }
        }

        private void GrantBoxes(ELunchBoxType type, int quantity)
        {
            try
            {
                Vault vault = SafeVault();
                if (vault == null || !vault.Loaded) return;

                // AddLunchBox(type, n) does not add n boxes. All three of its overloads insert a
                // single one; the number is handed to the LunchBox constructor, not counted. So the
                // count is kept here, where it means what it says.
                for (int i = 0; i < quantity; i++) vault.AddLunchBox(type);

                ShowBoxFlight(type, quantity);
                Say("Granted " + quantity + " " + type + " box" + (quantity == 1 ? "" : "es") + ".");
            }
            catch (Exception e)
            {
                Trouble("Could not grant that box: " + e.Message);
            }
        }

        private void DrawDwellers()
        {
            GUILayout.Label("Dwellers");

            DwellerManager dwellers = SafeDwellerManager();
            if (dwellers == null || dwellers.Dwellers == null)
            {
                GUILayout.Label("    unavailable");
                return;
            }

            GUILayout.Label("    in vault: " + dwellers.Dwellers.Count +
                            " / " + dwellers.MaximumDwellerCount);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(44f));
            _dwellerFirst = GUILayout.TextField(_dwellerFirst == null ? "" : _dwellerFirst, GUILayout.Width(110f));
            _dwellerLast = GUILayout.TextField(_dwellerLast == null ? "" : _dwellerLast, GUILayout.Width(110f));
            GUILayout.Label("Lvl", GUILayout.Width(28f));
            _dwellerLevel = GUILayout.TextField(_dwellerLevel == null ? "" : _dwellerLevel, GUILayout.Width(36f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(24f)))
                _rarityIndex = (_rarityIndex - 1 + Rarities.Length) % Rarities.Length;
            GUILayout.Label(Rarities[_rarityIndex].ToString(), GUILayout.Width(80f));
            if (GUILayout.Button(">", GUILayout.Width(24f)))
                _rarityIndex = (_rarityIndex + 1) % Rarities.Length;

            // Through the same door as the panel's own button. Setting the field here left the
            // looks built for the other gender and the panel's caption saying the opposite, which
            // is the drift this whole change is about.
            if (GUILayout.Button("<", GUILayout.Width(24f))) StepGender(-1);
            GUILayout.Label(Genders[_genderIndex].ToString(), GUILayout.Width(60f));
            if (GUILayout.Button(">", GUILayout.Width(24f))) StepGender(1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int i = 0; i < Specials.Length; i++)
            {
                GUILayout.Label(Specials[i].ToString().Substring(0, 1), GUILayout.Width(12f));
                string text = GUILayout.TextField(_special[i].ToString(), GUILayout.Width(26f));
                int parsed;

                // The same bounds the panel applies. This is the screen a player only ever sees
                // when something has already gone wrong, and it was the one that would put a
                // strength of minus five into their save.
                if (int.TryParse(text, out parsed))
                    _special[i] = Mathf.Clamp(parsed, 1, MaxSpecial);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create dweller")) CreateDweller();
            if (GUILayout.Button("Diagnose", GUILayout.Width(80f))) DiagnoseNewest();
            GUILayout.EndHorizontal();

            GUILayout.Label("    Legendary — brings its own name, look and stats:");
            _legendScroll = GUILayout.BeginScrollView(_legendScroll, GUILayout.Height(90f));
            UniqueDwellerData[] legends = dwellers.LegendaryDwellers;
            if (legends != null)
            {
                for (int i = 0; i < legends.Length; i++)
                {
                    if (legends[i] == null) continue;

                    string label = ReadMember(legends[i], "Name");
                    if (string.IsNullOrEmpty(label)) label = "legendary " + i;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(label, GUILayout.Width(300f));
                    if (GUILayout.Button("Create", GUILayout.Width(60f))) CreateLegendary(legends[i], label);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// What a dweller ended up as, read off the dweller itself.
        ///
        /// Every piece here is one that was just set through the game's own methods; naming them
        /// back turns a silent failure into a line that says which part did not take.
        /// </summary>
        private string Describe(Dweller dweller)
        {
            string line = (dweller.Name + " " + dweller.LastName).Trim();
            if (line.Length == 0) line = "a dweller";

            try
            {
                line += " (" + Rarities[_rarityIndex] + ", level " + LevelOf(dweller) + ")";

                string hair = PieceName(ReadObject(dweller, "m_hair"));
                if (!string.IsNullOrEmpty(hair)) line += ", hair " + hair;

                string face = PieceName(ReadObject(dweller, "m_face"));
                if (!string.IsNullOrEmpty(face)) line += ", face " + face;

                DwellerItem worn = dweller.EquippedOutfit;
                if (worn != null && !string.IsNullOrEmpty(worn.Id)) line += ", wearing " + worn.Id;

                DwellerItem held = dweller.EquippedWeapon;
                if (held != null && !string.IsNullOrEmpty(held.Id)) line += ", holding " + held.Id;
            }
            catch (Exception e)
            {
                ReportOnce("describe", "Could not read the dweller back: " + e.Message);
            }

            return line;
        }

        private static string PieceName(object piece)
        {
            UnityEngine.Object asset = piece as UnityEngine.Object;
            return asset == null ? null : asset.name;
        }

        private static int LevelOf(Dweller dweller)
        {
            try
            {
                object experience = ReadObject(dweller, "Experience");
                object level = experience != null ? ReadObject(experience, "CurrentLevel") : null;
                if (level != null) return Convert.ToInt32(level);
            }
            catch { }

            return 1;
        }

        /// <summary>
        /// Writes down the whole of what the bench is set to, in one line.
        ///
        /// The panel showed a woman and produced a man, and the useful question is which of the
        /// two the code believed. Reading the state out where it is used answers that without
        /// another round of reasoning about which call might have reset what.
        /// </summary>
        private void SayTheState()
        {
            try
            {
                string onTheButton = "?";

                if (_genderSwitch != null)
                {
                    UILabel text = _genderSwitch.GetComponentInChildren<UILabel>();
                    if (text != null) onTheButton = text.text;
                }

                Log.LogInfo("Creating: gender=" + Genders[_genderIndex] +
                            " (index " + _genderIndex + ", button says " + onTheButton + ")" +
                            ", rarity=" + Rarities[_rarityIndex] +
                            ", level=" + _dwellerLevelValue +
                            ", " + Picked(_hair) +
                            ", " + Picked(_face) +
                            ", " + Picked(_hairColour) +
                            ", " + Picked(_skin) +
                            ", " + Picked(_helmet) +
                            ", " + Picked(_outfit) +
                            ", " + Picked(_weapon));
            }
            catch { }
        }

        private static string Picked(Choice choice)
        {
            string label = choice.Index >= 0 && choice.Index < choice.Labels.Count
                ? choice.Labels[choice.Index]
                : "?";

            return choice.Caption + "=" + choice.Index + ":" + label +
                   (choice.Selected == null ? "(nothing)" : "");
        }

        /// <summary>
        /// Puts the bench back to empty, and the figure on it with them.
        ///
        /// After a dweller has been made, everything on this page describes somebody who has
        /// already gone to the vault door. Leaving the figure standing there while the fields go
        /// back to random is the worst of both: the picture promises a person the next press will
        /// not produce. So the fields and the figure are cleared together, and what is shown is
        /// again what would be made.
        /// </summary>
        /// <summary>
        /// Puts a real value in every appearance slot, chosen at random.
        ///
        /// This is the fix for the oldest complaint about this bench, and the player found the
        /// cause before I did. A slot left on "random" applied nothing at all: the figure on the
        /// bench had been given a random look of its own, the spawner rolled a different one for
        /// the dweller that walked away, and the two had never been the same thing. "Random" was
        /// never a value; it was an instruction not to write one, and the picture above it was a
        /// picture of somebody else.
        ///
        /// So there is no such instruction any more. Every appearance slot holds something real
        /// from the moment the page is opened, the figure is dressed in exactly that, and the
        /// dweller who arrives at the door is wearing what was on the screen.
        ///
        /// Gear is not rolled: an outfit and a weapon are things you choose, and a dweller with a
        /// random one of each is not what anybody opened this page for. Headgear is left alone for
        /// the same reason -- it sits under LOOKS, but a helmet on every newcomer is a costume,
        /// not an appearance. Say the word and it joins the roll.
        /// </summary>
        // Whether the appearance slots should be filled in as soon as there is something to fill
        // them with. False only while the catalogue is being walked for the icon report, which
        // rebuilds the lists for both genders and has no business rolling anything.
        private bool _rollWhenReady = true;

        /// <summary>The roll without remaking the figure, for when the caller is about to do that.</summary>
        private void RollTheLooksQuietly()
        {
            Choice[] rolled = { _hair, _face, _hairColour, _skin };

            for (int i = 0; i < rolled.Length; i++)
            {
                Choice choice = rolled[i];
                if (choice.Options.Count == 0) continue;

                choice.Index = UnityEngine.Random.Range(0, choice.Options.Count);
                choice.Show();
            }
        }

        private UITexture _dieFace;
        private float _dieSpinUntil;
        private float _dieNextFace;

        private const float DieSpin = 0.55f;

        /// <summary>
        /// Turns the die while it settles.
        ///
        /// The look changes the instant it is pressed -- waiting half a second to see the figure
        /// would be a worse button, not a better one -- so the spin is what the press felt like
        /// rather than what it is waiting on. Two turns, slowing as they go, with the face changing
        /// under them.
        /// </summary>
        private void TickTheDie()
        {
            if (_dieFace == null || _dieSpinUntil <= 0f) return;

            float now = Time.time;

            if (now >= _dieSpinUntil)
            {
                _dieSpinUntil = 0f;
                _dieFace.transform.localRotation = Quaternion.identity;
                return;
            }

            float left = (_dieSpinUntil - now) / DieSpin;

            // Squared, so it comes to rest rather than stopping.
            _dieFace.transform.localRotation = Quaternion.Euler(0f, 0f, left * left * 720f);

            if (now < _dieNextFace) return;

            _dieNextFace = now + 0.06f;
            _dieFace.mainTexture = Skin.Die(_dieFace.width, UnityEngine.Random.Range(1, 7));
        }

        private void RollTheLooks()
        {
            if (_dieFace != null)
            {
                _dieSpinUntil = Time.time + DieSpin;
                _dieNextFace = 0f;
            }

            Choice[] rolled = { _hair, _face, _hairColour, _skin };

            for (int i = 0; i < rolled.Length; i++)
            {
                Choice choice = rolled[i];

                if (choice.Options.Count == 0) continue;

                choice.Index = UnityEngine.Random.Range(0, choice.Options.Count);
                choice.Show();
            }

            RemakePreview();
        }

        private void ResetTheBench()
        {
            Choice[] all = { _hair, _face, _hairColour, _skin, _helmet, _outfit, _weapon };

            for (int i = 0; i < all.Length; i++)
            {
                all[i].Index = 0;
                all[i].Show();
            }

            _dwellerFirst = "";
            _dwellerLast = "";
            if (_firstNameInput != null) _firstNameInput.value = "";
            if (_lastNameInput != null) _lastNameInput.value = "";

            _dwellerLevelValue = 1;
            _dwellerLevel = "1";
            if (_levelInput != null) _levelInput.value = "1";

            for (int i = 0; i < _special.Length; i++)
            {
                _special[i] = 1;
                if (_specialInputs[i] != null) _specialInputs[i].value = "1";
            }

            _rarityIndex = 0;
            if (_rarityLabel != null) _rarityLabel.text = Rarities[_rarityIndex].ToString().ToUpper();

            // Cleared and then rolled, so the bench is never showing a person it would not make.
            // RollTheLooks remakes the figure itself.
            RollTheLooks();
        }

        private void CreateDweller() { CreateDweller(true); }

        /// <summary>
        /// A newcomer at the door.
        ///
        /// Handed over plain when it comes from the grant list — the point of a rolled dweller is
        /// that the game rolled it — and dressed in the panel's fields when it comes from the
        /// constructor.
        /// </summary>
        private void CreateDweller(bool customise)
        {
            try
            {
                DwellerManager manager = SafeDwellerManager();
                if (manager == null) return;

                DwellerSpawner spawner = DwellerSpawner.Instance;
                if (spawner == null)
                {
                    Log.LogWarning("The dweller spawner is unavailable; nothing was created.");
                    return;
                }

                if (manager.VaultIsWithMaxPopulation)
                {
                    Trouble("The vault is full; no dweller was created.");
                    return;
                }

                // The game's own call for a newcomer at the door. It creates the dweller and adds
                // them to the waiting line in one go, which is the half that was missing when this
                // was built by hand: setting the waiting-approval state without registering with
                // the queue left someone waiting at a door that did not know they were there.
                //
                // forceCreate is true so the panel is not silently refused by the same throttle
                // that paces normal arrivals; the population limit is checked above instead.
                // Written down at the moment it is used, not reasoned about afterwards. Twice now
                // the panel has shown one thing and created another, and both times the guess about
                // which half was lying was wrong. This says which.
                if (customise) SayTheState();

                Dweller dweller = spawner.CreateWaitingDweller(
                    Genders[_genderIndex], false, 0, Rarities[_rarityIndex], true);

                if (dweller == null)
                {
                    Trouble("The game refused to create a dweller.");
                    return;
                }

                int level = 1;

                if (customise)
                {
                    if (!string.IsNullOrEmpty(_dwellerFirst)) dweller.Name = _dwellerFirst;
                    if (!string.IsNullOrEmpty(_dwellerLast)) dweller.LastName = _dwellerLast;

                    if (!int.TryParse(_dwellerLevel, out level) || level < 1) level = 1;
            level = Mathf.Min(level, 50);
                    ApplyLevel(dweller, level);

                    ApplySpecial(dweller);
                    ApplyLooks(dweller);

                    // Chosen on the left, what the dweller ended up with on the right -- read
                    // here, before the bench is cleared. Printed after the clearing, the left
                    // half was the empty bench and the line proved nothing at all.
                    Log.LogInfo("Asked for " + Picked(_hair) + ", " + Picked(_face) + ", " +
                                Picked(_hairColour) + ", " + Picked(_skin) + ", " +
                                Picked(_helmet) + ", " + Picked(_outfit) + ", " + Picked(_weapon) +
                                "  ->  got " + Describe(dweller));
                }

                _created.Add(dweller.GetInstanceID());

                // The figure on the bench was kept cheerful; the person who walks away from it is
                // a different object entirely and got whatever mood the game hands a newcomer. A
                // dweller's face is its happiness, so they arrived at the door looking as though
                // they regretted it. The number the game gave them goes in the log once, in case
                // it is worth knowing what that default actually is.
                try
                {
                    object mood = ReadObject(dweller, "Happiness");

                    if (mood != null)
                    {
                        if (!_reportedMood)
                        {
                            _reportedMood = true;
                            Log.LogInfo("A newcomer arrives at " +
                                        ReadAsText(mood, "HappinessValue") + " happiness.");
                        }

                        WriteMember(mood, "HappinessValue", 100f);
                    }
                }
                catch (Exception e)
                {
                    ReportOnce("newmood", "Could not cheer the newcomer up: " + e);
                }

                // If the spawner has handed back the very object this panel was using as its
                // stand-in -- it came out of the same pool and was never given back -- then the
                // figure on the bench has just walked off to the vault door, which would explain
                // both the avatar going and the newcomer arriving as something else.
                // Against every stand-in, not just the one on screen. One is kept per gender, and
                // checking only the current one left the other in the cache -- so the next time
                // the gender was stepped, the bench adopted a dweller the player had created and
                // re-randomised their face. Which is exactly what a newcomer arriving as somebody
                // else looks like.
                string standingFor = null;

                foreach (KeyValuePair<string, Dweller> kept in _standIns)
                    if (ReferenceEquals(kept.Value, dweller)) { standingFor = kept.Key; break; }

                if (standingFor != null)
                {
                    Log.LogWarning("The spawner handed back the stand-in itself; letting go of it.");

                    // Given back whole. Detecting this and then dropping the reference was worse
                    // than not detecting it at all: the layer and the position were captured when
                    // the figure was borrowed, and forgetting them left a real dweller eight
                    // thousand units under the vault on a layer nothing draws. Worse, the cache
                    // still held it, so the next visit to this page adopted the player's new
                    // dweller all over again and re-randomised their face.
                    PutTheFigureBack(dweller.gameObject);
                    dweller.gameObject.SetActive(true);

                    _standIns.Remove(standingFor);

                    if (ReferenceEquals(dweller, _previewDweller)) _previewDweller = null;
                    _texturedOnce = false;
                    _framedSize = -1f;
                    _framedLocked = false;
                    _posed = false;
                    _lastBeat.Clear();
                }

                if (customise)
                    Log.LogInfo("Created a " + ReadAsText(dweller, "Gender") +
                                " named " + dweller.Name + " " + dweller.LastName + ".");

                // Cleared, figure and fields together, so the page goes on describing what the
                // next press would make rather than what the last one did.
                if (customise) ResetTheBench();
                else if (_tab == Tab.Create && _making == Making.Dweller && _panelOpen)
                {
                    EnsurePreview();
                    RefreshPreview();
                }

                // Read back rather than assume. Applying a look is a chain of reflection calls into
                // somebody else's object graph, and the only honest way to know it took is to ask
                // the dweller afterwards what it is actually wearing.
                Say("Created " + Describe(dweller) + " — waiting at the vault door.");
            }
            catch (Exception e)
            {
                Trouble("Creating a dweller failed: " + e.Message);
            }
        }

        /// <summary>
        /// Writes a description of the game's own interface to the log.
        ///
        /// The panel has to become part of that interface, and an NGUI widget renders as nothing
        /// when its depth is below what it sits on, when its parent is wrong, when its atlas lacks
        /// the sprite it names, or when its label has no font. None of those produce an error, and
        /// from this side of the screen they all look the same.
        ///
        /// The dwellers established what guessing costs: three attempts, three launches, then a
        /// diagnostic that answered it in one. So this comes first, and reads rather than assumes —
        /// member names are looked up and reported as found, because writing panel.depth would
        /// compile against whatever this NGUI version happens to call it and report nothing if the
        /// name is different.
        ///
        /// Reads only. Creates nothing, changes nothing, adds no component.
        /// </summary>
        private void SurveyInterface()
        {
            Log.LogInfo("=== interface survey ===");
            SurveySection("roots", delegate { SurveyRoots(); });
            SurveySection("panels", delegate { SurveyPanels(); });
            SurveySection("windows", delegate { SurveyWindows(); });
            SurveySection("buttons", delegate { SurveyButtons(); });
            SurveySection("atlases", delegate { SurveyAtlases(); });
            SurveySection("fonts", delegate { SurveyFonts(); });
            Log.LogInfo("=== end of survey ===");
        }

        private delegate void Section();

        private void SurveySection(string name, Section body)
        {
            // One unreadable section must not cost the rest of the survey.
            try { body(); }
            catch (Exception e) { Log.LogWarning("  [" + name + "] could not be read: " + e.Message); }
        }

        private const int SurveyCap = 40;

        private void SurveyRoots()
        {
            UIRoot[] roots = Resources.FindObjectsOfTypeAll<UIRoot>();
            Log.LogInfo("  UI roots: " + roots.Length);

            for (int i = 0; i < roots.Length && i < SurveyCap; i++)
            {
                UIRoot r = roots[i];
                if (r == null) continue;
                Log.LogInfo("    " + Path(r.transform) +
                            "  scaling=" + Member(r, "scalingStyle") +
                            "  manualHeight=" + Member(r, "manualHeight") +
                            "  active=" + r.gameObject.activeInHierarchy);
            }
        }

        private void SurveyPanels()
        {
            UIPanel[] panels = Resources.FindObjectsOfTypeAll<UIPanel>();
            int shown = 0;
            Log.LogInfo("  panels: " + panels.Length);

            for (int i = 0; i < panels.Length; i++)
            {
                UIPanel p = panels[i];
                if (p == null || !p.gameObject.activeInHierarchy) continue;   // only what is on screen
                if (shown++ >= SurveyCap) continue;

                Log.LogInfo("    " + Path(p.transform) +
                            "  depth=" + Member(p, "depth") +
                            "  sorting=" + Member(p, "sortingOrder") +
                            "  clipping=" + Member(p, "clipping"));
            }
            if (panels.Length > shown) Log.LogInfo("    (" + (panels.Length - shown) + " more not listed)");
        }

        private void SurveyWindows()
        {
            // A window the game already built is what the real panel should be cloned from: it is
            // the only way to inherit its look, depth and parenting without seeing them.
            MonoBehaviour[] all = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            int shown = 0;

            for (int i = 0; i < all.Length; i++)
            {
                MonoBehaviour m = all[i];
                if (m == null) continue;

                string type = m.GetType().Name;
                if (!type.EndsWith("Window") && !type.EndsWith("HUD") && !type.EndsWith("Popup")) continue;
                if (shown++ >= SurveyCap) continue;

                Log.LogInfo("    " + type + "  at " + Path(m.transform) +
                            "  active=" + m.gameObject.activeInHierarchy);
            }
            Log.LogInfo("  windows, huds and popups listed: " + shown);
        }

        /// <summary>
        /// Every button on screen, with where it lives.
        ///
        /// The menu on the right — settings, stats, boxes, missions, storage — is assembled in the
        /// scene rather than declared in code, so no amount of reading the assembly finds it. Its
        /// buttons are on screen though, and a button's parent path is what says which menu it
        /// belongs to and therefore where another one would go.
        /// </summary>
        private void SurveyButtons()
        {
            UIButton[] buttons = Resources.FindObjectsOfTypeAll<UIButton>();
            int shown = 0;
            int active = 0;

            for (int i = 0; i < buttons.Length; i++)
            {
                UIButton b = buttons[i];
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                active++;
                if (shown++ >= SurveyCap * 3) continue;

                Log.LogInfo("    " + Path(b.transform));
            }

            Log.LogInfo("  buttons on screen: " + active + " (of " + buttons.Length + " loaded)");
            if (active > shown) Log.LogInfo("    (" + (active - shown) + " more not listed)");
        }

        private void SurveyAtlases()
        {
            UIAtlas[] atlases = Resources.FindObjectsOfTypeAll<UIAtlas>();
            Log.LogInfo("  atlases: " + atlases.Length);

            for (int i = 0; i < atlases.Length && i < SurveyCap; i++)
            {
                UIAtlas a = atlases[i];
                if (a == null) continue;

                string size = "?";
                try { if (a.texture != null) size = a.texture.width + "x" + a.texture.height; }
                catch { }

                string sample = "";
                int count = 0;
                try
                {
                    List<UISpriteData> sprites = a.spriteList;
                    if (sprites != null)
                    {
                        count = sprites.Count;
                        for (int k = 0; k < sprites.Count && k < 3; k++)
                        {
                            if (sprites[k] == null) continue;
                            if (sample.Length > 0) sample += ", ";
                            sample += sprites[k].name;
                        }
                    }
                }
                catch { }

                Log.LogInfo("    " + a.name + "  " + size + "  sprites=" + count +
                            (sample.Length > 0 ? "  e.g. " + sample : ""));
            }
        }

        private void SurveyFonts()
        {
            UILabel[] labels = Resources.FindObjectsOfTypeAll<UILabel>();
            HashSet<string> fonts = new HashSet<string>();

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                object bitmap = ReadAny(labels[i], "bitmapFont");
                object dynamic = ReadAny(labels[i], "trueTypeFont");
                if (bitmap != null) fonts.Add("bitmap:" + bitmap);
                if (dynamic != null) fonts.Add("dynamic:" + dynamic);
            }

            Log.LogInfo("  labels: " + labels.Length + ", distinct fonts: " + fonts.Count);
            int shown = 0;
            foreach (string f in fonts)
            {
                if (shown++ >= SurveyCap) break;
                Log.LogInfo("    " + f);
            }
        }

        /// <summary>The transform's path, which is what tells you where to parent something.</summary>
        private static string Path(Transform t)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(t.name);
            Transform up = t.parent;
            int guard = 0;
            while (up != null && guard++ < 12)
            {
                sb.Insert(0, up.name + "/");
                up = up.parent;
            }
            return sb.ToString();
        }

        /// <summary>Reports a member as found, or says it is not there under that name.</summary>
        private static string Member(object target, string name)
        {
            object v = ReadAny(target, name);
            return v == null ? "<no member '" + name + "'>" : v.ToString();
        }

        /// <summary>
        /// Logs every condition that gates interaction with a dweller, for the one this mod made
        /// last beside one that came from the game.
        ///
        /// Dweller.CanDoAction runs through several checks and returning false anywhere kills every
        /// button on the dweller's card. Guessing which one fails has now cost three attempts, so
        /// this reads them all and prints them side by side. The line that differs is the answer.
        /// </summary>
        private void DiagnoseNewest()
        {
            try
            {
                DwellerManager manager = SafeDwellerManager();
                if (manager == null || manager.Dwellers == null || manager.Dwellers.Count == 0)
                {
                    Log.LogWarning("No dwellers to diagnose.");
                    return;
                }

                Dweller mine = null;
                Dweller theirs = null;

                for (int i = 0; i < manager.Dwellers.Count; i++)
                {
                    Dweller d = manager.Dwellers[i];
                    if (d == null) continue;
                    if (_created.Contains(d.GetInstanceID())) { if (mine == null) mine = d; }
                    else if (theirs == null) theirs = d;
                }

                if (mine == null)
                {
                    Log.LogWarning("Nothing created this session to compare; create a dweller first.");
                    return;
                }

                Log.LogInfo("=== dweller gate comparison ===");
                Log.LogInfo("  created by this mod : " + Describe(mine, manager));
                Log.LogInfo("  made by the game    : " +
                            (theirs == null ? "(none in the vault to compare against)" : Describe(theirs, manager)));
                Log.LogInfo("  Any line that differs is what stops the interface working.");
            }
            catch (Exception e)
            {
                Log.LogWarning("Diagnosing failed: " + e.Message);
            }
        }

        private string Describe(Dweller d, DwellerManager manager)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(Safe(d, "Name")).Append(" | ");
            sb.Append("state=").Append(StateName(d)).Append(" | ");
            sb.Append("canDoAction1=").Append(Try(delegate { return d.CanDoAction((EDwellerAction)1).ToString(); })).Append(" | ");
            sb.Append("canBe3DSelected=").Append(Try(delegate { return manager.CanBe3DSelected(d).ToString(); })).Append(" | ");
            sb.Append("canDoAnySelectionNow=").Append(Try(delegate { return manager.CanDoAnySelectionNow(d).ToString(); })).Append(" | ");
            sb.Append("inWasteland=").Append(Try(delegate { return d.IsRegisteredInWasteland.ToString(); })).Append(" | ");
            sb.Append("isChild=").Append(Try(delegate { return d.IsChild.ToString(); })).Append(" | ");
            sb.Append("willBeEvicted=").Append(Field(d, "m_willBeEvicted")).Append(" | ");
            sb.Append("savedRoomId=").Append(Field(d, "m_savedRoomId")).Append(" | ");
            sb.Append("serializeId=").Append(Safe(d, "SerializeID")).Append(" | ");
            sb.Append("assigned=").Append(Field(d, "m_assigned")).Append(" | ");
            sb.Append("gameObjectActive=").Append(Try(delegate { return d.gameObject.activeInHierarchy.ToString(); }));
            return sb.ToString();
        }

        private static string StateName(Dweller d)
        {
            object state = ReadAny(d, "m_currentState");
            return state == null ? "<null>" : state.GetType().Name;
        }

        private static string Safe(Dweller d, string member)
        {
            object v = ReadAny(d, member);
            return v == null ? "<null>" : v.ToString();
        }

        private static string Field(Dweller d, string member)
        {
            object v = ReadAny(d, member);
            return v == null ? "<absent>" : v.ToString();
        }

        private delegate string Probe();

        private static string Try(Probe probe)
        {
            try { return probe(); }
            catch (Exception e) { return "<threw " + e.GetType().Name + ">"; }
        }

        /// <summary>
        /// Sets the dweller's level, and the experience that belongs with it.
        ///
        /// CreateWaitingDweller does not take a level, so it is applied afterwards. The game uses
        /// this same call inside CreateDweller for the same purpose — it moves the level and the
        /// experience together, which is the pairing the save keeps.
        /// </summary>
        private void ApplyLevel(Dweller dweller, int level)
        {
            try
            {
                DwellerExperience experience = dweller.Experience;
                if (experience != null) experience.SetLevelAndMinExp(level);
            }
            catch (Exception e)
            {
                Log.LogWarning("Setting the level failed: " + e.Message);
            }
        }

        /// <summary>
        /// Writes the seven SPECIAL values.
        ///
        /// SpecialStat.Value cannot be assigned. Of the methods that can change it,
        /// SetValueAndMinExp moves the stored experience along with the value; SetValueOnly leaves
        /// it behind. The save keeps both side by side, so moving only one produces a record
        /// describing two different things.
        /// </summary>
        private void ApplySpecial(Dweller dweller)
        {
            DwellerStats stats = dweller.Stats;
            if (stats == null) return;

            for (int i = 0; i < Specials.Length; i++)
            {
                try
                {
                    SpecialStat stat = stats.GetStat(Specials[i]);
                    if (stat != null) stat.SetValueAndMinExp(_special[i]);
                }
                catch (Exception e)
                {
                    Log.LogWarning("Setting " + Specials[i] + " failed: " + e.Message);
                }
            }

            // The game calls this after every stat change — CreateDweller does it twice in its own
            // body. Modified stats are what equipment bonuses are applied on top of, so leaving them
            // stale after rewriting all seven values leaves the dweller describing itself wrongly.
            try { stats.CalculateModStats(); }
            catch (Exception e) { Log.LogWarning("Recalculating modified stats failed: " + e.Message); }
        }

        private void CreateLegendary(UniqueDwellerData data, string label)
        {
            try
            {
                DwellerManager manager = SafeDwellerManager();
                if (manager == null) return;

                DwellerSpawner spawner = DwellerSpawner.Instance;
                if (spawner == null)
                {
                    Log.LogWarning("The dweller spawner is unavailable; " + label + " was not created.");
                    return;
                }

                if (manager.VaultIsWithMaxPopulation)
                {
                    Log.LogWarning("The vault is at its population limit; " + label + " was not created.");
                    return;
                }

                Dweller dweller = spawner.CreateUniqueWaitingDweller(data, false, false, 0, true);

                if (dweller == null)
                {
                    Trouble("The game refused to create " + label + ".");
                    return;
                }

                Say("Created " + label + " — waiting at the vault door.");

                // Deliberately not edited: a legendary dweller brings its own name, look and stats,
                // and overwriting them produces something that looks legendary and is not.
                _created.Add(dweller.GetInstanceID());

                Log.LogInfo("Created legendary dweller " + label + " — waiting at the door.");
            }
            catch (Exception e)
            {
                Log.LogWarning("Creating " + label + " failed: " + e.Message);
            }
        }

        private void DrawInventory(Vault vault)
        {
            GUILayout.Label("Inventory");

            VaultInventory inventory = vault.Inventory;
            if (inventory == null || inventory.Items == null)
            {
                GUILayout.Label("    unavailable");
                return;
            }

            GUILayout.Label("    items: " + inventory.Items.Count + " / " + inventory.ItemCountMax);
        }

        /// <summary>
        /// A singleton's Instance is null before the game has built it and again between scenes,
        /// and reading through it is the most likely thing in this panel to throw.
        /// </summary>
        private Vault SafeVault()
        {
            try { return Vault.Instance; }
            catch (Exception e) { ReportOnce("vault", "Could not reach the vault: " + e.Message); return null; }
        }

        private DwellerManager SafeDwellerManager()
        {
            try { return DwellerManager.Instance; }
            catch (Exception e) { ReportOnce("dwellers", "Could not reach the dwellers: " + e.Message); return null; }
        }

        private static void ReportOnce(string key, string message)
        {
            if (_reported.Add(key)) Log.LogWarning(message);
        }
    }
}
