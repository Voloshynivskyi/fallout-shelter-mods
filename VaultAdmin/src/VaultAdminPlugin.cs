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
        public static readonly Color Bright = new Color32(0x14, 0xFF, 0x17, 0xFF);
        public static readonly Color Rim = new Color32(0x08, 0x60, 0x0A, 0xFF);
        public static readonly Color Ink = new Color32(0x04, 0x28, 0x04, 0xFF);

        // A window is a dimmed plate, not an opaque one: the vault shows through the game's own.
        public static readonly Color Plate = new Color32(0x08, 0x51, 0x08, 0xC8);
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
            return Frame(width, height, 18, 3, Bright, Plate, Ink, 2);
        }

        /// <summary>An ordinary button: outlined, nothing behind it. Frequent, reversible actions.</summary>
        public static Texture2D Button(int width, int height)
        {
            return Frame(width, height, 8, 3, Bright, Clear);
        }

        /// <summary>An emphasis button: solid, with dark text on it. Close, save, confirm.</summary>
        public static Texture2D SolidButton(int width, int height)
        {
            return Frame(width, height, 8, 3, Bright, Bright);
        }

        /// <summary>The same, edged the way the window is: filled, outlined dark, then bright.</summary>
        public static Texture2D SolidOutlined(int width, int height)
        {
            return Frame(width, height, 10, 3, Ink, Bright, Bright, 2);
        }

        /// <summary>
        /// The recess an icon sits in.
        ///
        /// A picture laid straight onto a row runs into the words beside it. A dark square behind it
        /// says where the picture ends and the row begins.
        /// </summary>
        public static Texture2D Well(int size)
        {
            return Frame(size, size, 6, 2, Rim, Ink);
        }

        /// <summary>A place to type: outlined bright, sunk dark, so it reads as a field.</summary>
        public static Texture2D Field(int width, int height)
        {
            return Frame(width, height, 6, 2, Bright, Ink);
        }

        /// <summary>A content row: a quieter outline, dimmed inside.</summary>
        public static Texture2D Row(int width, int height)
        {
            return Frame(width, height, 6, 2, Rim, Plate);
        }

        /// <summary>A section header: solid, inverted against the rows beneath it.</summary>
        public static Texture2D Header(int width, int height)
        {
            return Frame(width, height, 6, 2, Bright, Bright);
        }
    }

    /// <summary>
    /// Vault Admin — a debug panel for Fallout Shelter.
    ///
    /// Reads live vault state, and grants resources and boxes.
    ///
    /// Everything is written through the game's own methods rather than by assigning fields.
    /// Storage.AddResource clamps to the vault's cap and raises the callbacks the interface
    /// listens to; a field assignment would leave the number on screen stale and skip whatever
    /// else the game does when a resource changes.
    ///
    /// The panel is drawn with IMGUI here and only here. The finished panel is built from the
    /// game's own NGUI widgets so it belongs to the interface rather than floating over it; doing
    /// that is the next change. Separating the two means a failure there is a UI failure and
    /// nothing else.
    ///
    /// Disabled by default. Installing this without deliberately switching it on changes nothing.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ovolo.falloutshelter.vaultadmin";
        public const string PluginName = "Vault Admin";
        public const string PluginVersion = "0.38.0";

        internal static ManualLogSource Log;

        private static ConfigEntry<bool> Enabled;
        private static ConfigEntry<string> ToggleKey;
        private static ConfigEntry<bool> WriteIconReport;
        private static ConfigEntry<bool> ShowHudButton;
        private static ConfigEntry<float> HudButtonOffsetX;
        private static ConfigEntry<string> HudButtonSprite;
        private static ConfigEntry<string> HudButtonTint;
        private static ConfigEntry<string> HudButtonImage;
        private static ConfigEntry<float> HudButtonIconScale;

        private Key _toggleKey = Key.F8;
        private bool _panelOpen;

        // Failures are logged once each. A panel that writes sixty lines a second destroys the very
        // evidence needed to work out what it is failing at.
        private readonly HashSet<string> _reported = new HashSet<string>();

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
            public string Stats;     // rarity, what it does, what it sells for
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

        private List<PetEntry> _pets;
        private Vector2 _petScroll;
        private string _petName = "";
        private string _petBonusValue = "10";
        private int _petBonusIndex;
        private static readonly EBonusEffect[] BonusEffects =
            (EBonusEffect[])Enum.GetValues(typeof(EBonusEffect));

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
            if (_hudButton != null) return;

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

                Transform found = parent.Find(CameraButtonName);
                if (found == null)
                {
                    Log.LogWarning("No '" + CameraButtonName + "' under " + AnchorPath +
                                   "; nothing to copy the button from.");
                    return;
                }
                GameObject source = found.gameObject;

                GameObject clone = UnityEngine.Object.Instantiate(source);
                clone.name = HudButtonName;

                // false, because keeping world position puts the clone somewhere off screen: NGUI
                // lays out in its own scaled space, not the world's.
                clone.transform.SetParent(parent, false);
                clone.transform.localPosition =
                    source.transform.localPosition + new Vector3(HudButtonOffsetX.Value, 0f, 0f);
                clone.transform.localRotation = source.transform.localRotation;
                clone.transform.localScale = source.transform.localScale;

                StripClonedBehaviour(clone);
                ReleaseAnchors(clone);
                WireButton(clone);
                MakeVisible(clone, source);

                // After the anchors are gone, so the position actually holds.
                clone.transform.localPosition =
                    source.transform.localPosition + new Vector3(HudButtonOffsetX.Value, 0f, 0f);

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

                ReportSpriteNames(sprite);

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

                string path = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Info.Location), file);

                if (!System.IO.File.Exists(path))
                {
                    ReportOnce("buttonimage", "No button image at " + path + "; keeping the borrowed sprite.");
                    return false;
                }

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
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
                UnityEngine.Object.Destroy(part);
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

        private void OnDisable()
        {
            // Leaving the camera switched off because the mod went away would be unforgivable.
            HoldCamera(false);
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
            _panelOpen = !_panelOpen;
            if (!_panelOpen) HoldCamera(false);

            if (_nguiWindow == null) BuildWindow();

            if (_panelOpen)
            {
                _drawChecked = false;
                _drawCheckFrames = 0;
            }
            if (_nguiWindow != null) _nguiWindow.SetActive(_panelOpen);
        }

        // ---- the window, built from the game's own widget types ----

        private GameObject _nguiWindow;
        private UITexture _frame;      // the window's own backing, and the proof it is being drawn
        private int _drawCheckFrames;
        private bool _drawChecked;
        private bool _nguiDrawing;     // false until the frame is seen with a draw call
        private UIPanel _windowPanel;
        private object _font;            // UIFont or Font, whichever the game's labels use
        private int _fontSize = 28;

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
        private UIAtlas _menuAtlas;
        private readonly List<UITexture> _thumbs = new List<UITexture>();

        // What the panel does falls into three jobs, not three kinds of thing: top the vault up,
        // hand something over, or build something that does not exist yet. A dweller belongs in two
        // of those, which is why splitting by kind put its two halves in one place and nothing in
        // the other.
        private enum Tab { Resources, Grant, Create }

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

            _windowWidth = Mathf.Clamp(virtualWidth / 3, 380, 900);
            _windowHeight = Mathf.Max(320, virtualHeight - VerticalInset * 2);
            _windowX = -virtualWidth / 2 + _windowWidth / 2 + EdgeMargin;

            Log.LogInfo("Window sized to " + _windowWidth + "x" + _windowHeight +
                        " within a " + virtualWidth + "x" + virtualHeight + " interface.");
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

                _windowPanel = _nguiWindow.AddComponent<UIPanel>();
                _windowPanel.depth = WindowDepth;

                _frame = Plate(_nguiWindow.transform, "Frame", 0, 0, _windowWidth, _windowHeight,
                               Skin.Window(_windowWidth, _windowHeight), 0);

                // Written straight onto the top edge, in the green, as the game's own windows do
                // it — large enough to read as the window's name rather than a caption on it.
                // A tenth of its own height lower, so it rests on the frame rather than balancing
                // on it.
                UILabel title = MakeLabel(_nguiWindow.transform, "Title", "VAULT ADMIN",
                                          0, _windowHeight / 2 - 6, _windowWidth - 60, 52,
                                          Skin.Bright, 4);
                title.fontSize = Mathf.RoundToInt(_fontSize * 1.6f);

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
                if (closeText != null) closeText.fontSize = Mathf.RoundToInt(_fontSize * 1.25f);

                _nguiWindow.SetActive(false);
                Log.LogInfo("Built the panel window under " + root.name + ".");
                return true;
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not build the window: " + e.Message);
                _nguiWindow = null;
                return false;
            }
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
            go.transform.localPosition = new Vector3(width / 2 + 2, centre, 0f);
            go.transform.localScale = Vector3.one;

            UITexture track = Plate(go.transform, "Track", 0, 0, 10, viewHeight,
                                    Skin.Frame(10, viewHeight, 5, 1, Skin.Rim, Skin.Plate), 2);

            // The scroll view resizes this one to say how much of the list is in view, so its
            // texture has to survive being stretched: a nearly square source with a small radius
            // stays a bar, where a long rounded one turns into a smear.
            UITexture thumb = Plate(go.transform, "Thumb", 0, 0, 10, viewHeight,
                                    Skin.Frame(10, viewHeight, 5, 5, Skin.Bright, Skin.Bright), 3);

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
                    if (Array.IndexOf(EmptyWords, words[j].ToLower()) >= 0) continue;
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

                string hint = what.ToLower();
                if (hint.StartsWith("box ")) hint = hint.Substring(4);
                if (hint.Length > 5) hint = hint.Substring(0, 5);

                string found = "";
                int shown = 0;

                for (int i = 0; i < sprites.Count && shown < 12; i++)
                {
                    if (sprites[i] == null || sprites[i].name == null) continue;
                    if (sprites[i].name.ToLower().IndexOf(hint) < 0) continue;

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

            Found found = FindIcon(candidates, "dweller");
            if (found == null) return;

            icon.atlas = found.Atlas;
            icon.spriteName = found.Sprite;
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
            AddIcon(parent, name, candidates, what, x, y, size, true);
        }

        private void AddIcon(Transform parent, string name, string[] candidates, string what,
                             int x, int y, int size, bool preferMenu)
        {
            AddIcon(parent, name, candidates, what, x, y, size, preferMenu, null);
        }

        private void AddIcon(Transform parent, string name, string[] candidates, string what,
                             int x, int y, int size, bool preferMenu, string preferredAtlas)
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
            FitSprite(drawn, size);
        }

        private void BuildTabs(Transform parent)
        {
            Tab[] tabs = { Tab.Resources, Tab.Grant, Tab.Create };
            string[] names = { "RESOURCES", "GRANT", "CREATE" };

            int usable = _windowWidth - Margin * 2;
            int width = (usable - 12) / 3;
            int y = _windowHeight / 2 - 58;
            int x = -usable / 2 + width / 2;

            for (int i = 0; i < tabs.Length; i++)
            {
                Tab captured = tabs[i];
                _tabButtons[tabs[i]] = MakeButton(parent, "Tab_" + tabs[i], names[i],
                                                  x, y, width, 42, false,
                                                  delegate { ShowTab(captured); });
                x += width + 6;
            }
        }

        private void BuildPages(Transform parent)
        {
            foreach (Tab tab in new[] { Tab.Resources, Tab.Grant, Tab.Create })
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
            _tab = tab;

            foreach (KeyValuePair<Tab, GameObject> entry in _tabPages)
            {
                if (entry.Value != null) entry.Value.SetActive(entry.Key == tab);
            }

            // A page that has been hidden comes back where it was left, which for a list is
            // wherever the last search happened to end.
            if (tab == Tab.Grant && _grantScroll != null) _grantScroll.ResetPosition();

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
        private int ContentBottom() { return -_windowHeight / 2 + 70; }

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
        }

        private static readonly Family[] Families =
        {
            Family.Weapon, Family.Outfit, Family.Junk, Family.Pet, Family.Dweller
        };

        private Family _grantFamily = Family.Weapon;

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

            int textLeft = -width / 2 + 62;
            int textWidth = width - 160;

            row.Name = MakeLeftLabel(row.Root.transform, "Name", "",
                                     textLeft, 11, textWidth, 24, Skin.Bright, 3);

            // The figures beneath the name, quieter than it: what the item does and what it is
            // worth is the reason to pick one item out of two hundred.
            row.Stats = MakeLeftLabel(row.Root.transform, "Stats", "",
                                      textLeft, -12, textWidth, 20, Skin.Bright, 3);
            row.Stats.color = new Color(Skin.Bright.r, Skin.Bright.g, Skin.Bright.b, 0.7f);

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
            _petBonusIndex = (_petBonusIndex + by + BonusEffects.Length) % BonusEffects.Length;
            if (_bonusLabel != null) _bonusLabel.text = BonusEffects[_petBonusIndex].ToString();
        }

        /// <summary>Rereads the catalogue for the chosen family and puts the list back to its top.</summary>
        private void RefreshThings()
        {
            _shown.Clear();

            string filter = _filter == null ? "" : _filter.Trim().ToLower();

            if (_grantFamily == Family.Dweller)
            {
                // The named dwellers the game ships: each brings its own look, stats and story, so
                // this list hands them over whole rather than offering to edit them.
                // Ordinary newcomers first: most of the time what is wanted is a body at the door,
                // not one of the fifty-odd people the game has written.
                for (int i = 0; i < Rarities.Length; i++)
                {
                    string label = "Random " + Rarities[i] + " dweller";
                    if (filter.Length > 0 && label.ToLower().IndexOf(filter) < 0) continue;

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
                        if (filter.Length > 0 && label.ToLower().IndexOf(filter) < 0) continue;

                        _shown.Add(legends[i]);
                    }
                }
            }
            else if (_grantFamily == Family.Pet)
            {
                PreloadPetArt();
                if (_pets == null) BuildPetCatalogue();
                if (_pets != null)
                {
                    for (int i = 0; i < _pets.Count; i++)
                    {
                        if (filter.Length > 0 &&
                            _pets[i].Name.ToLower().IndexOf(filter) < 0) continue;
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
                        if (filter.Length > 0 && entry.Name.ToLower().IndexOf(filter) < 0) continue;
                        _shown.Add(entry);
                    }
                }
            }

            UpdateOrderingSwitches();
            if (_ordering != Ordering.None) _shown.Sort(CompareShown);

            EnsureRows(_shown.Count);
            FillRows();
            UpdateGrantArea(Mathf.Min(_shown.Count, MaxGrantRows));
        }

        /// <summary>Writes this page of the list into rows that already exist.</summary>
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

            PetEntry pet = thing as PetEntry;
            if (pet != null)
                return _ordering == Ordering.Rarity ? (int)pet.Rarity : pet.Power;

            RandomDweller random = thing as RandomDweller;
            if (random != null) return (int)random.Rarity;

            if (thing is UniqueDwellerData) return (int)EDwellerRarity.Legendary;

            return -1;
        }

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
                    row.Stats.text = item.Stats;
                    ShowIcon(row.Icon, item);
                    continue;
                }

                RandomDweller random = thing as RandomDweller;
                if (random != null)
                {
                    row.Name.text = "Random " + random.Rarity + " dweller";
                    row.Stats.text = "RANDOM  a " + random.Rarity.ToString().ToLower() +
                                     " newcomer, rolled by the game";
                    ShowMenuIcon(row.Icon, DwellerSprites);
                    continue;
                }

                UniqueDwellerData legend = thing as UniqueDwellerData;
                if (legend != null)
                {
                    row.Name.text = LegendName(legend);
                    row.Stats.text = "LEGENDARY  brings its own look and stats";
                    ShowLegendIcon(row.Icon, legend);
                    continue;
                }

                {
                    PetEntry pet = (PetEntry)thing;
                    row.Name.text = pet.Name;
                    row.Stats.text = PetStats(pet);

                    ShowPetIcon(row.Icon, pet);
                }
            }

            // The count belongs beside the family it counts, not on a pager that no longer exists.
            if (_familyLabel != null)
                _familyLabel.text = _grantFamily.ToString().ToUpper() +
                                    (_shown.Count > 0 ? "  " + _shown.Count : "  NONE");
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
        /// What a pet actually does, which is the reason to pick one.
        ///
        /// Each carries a list of effects with a range apiece — happiness by so much, damage by so
        /// much — and the breed alone said none of it.
        /// </summary>
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

                    float high;
                    float.TryParse(ReadAsText(bonus, "MaxValue"),
                                   System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out high);
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

                    string effect = ReadAsText(bonus, "Effect");
                    if (string.IsNullOrEmpty(effect) || effect == "None") continue;

                    float low, high;
                    float.TryParse(ReadAsText(bonus, "MinValue"),
                                   System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out low);
                    float.TryParse(ReadAsText(bonus, "MaxValue"),
                                   System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out high);

                    if (line.Length > 0) line += "   ";
                    line += effect.ToUpper() + " +" + low.ToString("0.#") +
                            (high > low ? "-" + high.ToString("0.#") : "");
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

            string chosen = BestSprite(atlas, head);
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
            FitSprite(icon, IconBox);
        }

        /// <summary>Grants whatever sits in this row, through the same paths the panel already uses.</summary>
        private void GiveRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _shown.Count) return;

            object thing = _shown[rowIndex];

            CatalogueEntry item = thing as CatalogueEntry;
            if (item != null) { GrantItem(item); return; }

            RandomDweller random = thing as RandomDweller;
            if (random != null)
            {
                _rarityIndex = Array.IndexOf(Rarities, random.Rarity);
                if (_rarityIndex < 0) _rarityIndex = 0;
                CreateDweller(false);
                return;
            }

            UniqueDwellerData legend = thing as UniqueDwellerData;
            if (legend != null)
            {
                CreateLegendary(legend, LegendName(legend));
                return;
            }

            // Handed over as the game rolled it. Naming one and choosing its bonus is what the
            // create tab is for.
            GrantPet((PetEntry)thing, false);
        }

        private UIInput _firstNameInput;
        private UIInput _lastNameInput;
        private UILabel _rarityLabel;
        private UILabel _genderLabel;
        private UIInput _levelInput;
        private readonly UIInput[] _specialInputs = new UIInput[7];
        private int _dwellerLevelValue = 1;

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

        private void StepMaking(int by)
        {
            _making = _making == Making.Dweller ? Making.Pet : Making.Dweller;
            ShowMaking(_making);
        }

        private void ShowMaking(Making making)
        {
            _making = making;
            if (_makingLabel != null) _makingLabel.text = making.ToString().ToUpper();
            if (_dwellerSection != null) _dwellerSection.SetActive(making == Making.Dweller);
            if (_petSection != null) _petSection.SetActive(making == Making.Pet);
            if (_createView != null) _createView.ResetPosition();
        }

        /// <summary>A pet built to order: which one, called what, carrying which bonus.</summary>
        private void BuildPetSection(Transform parent, int width)
        {
            AddHeader(parent, "PET", width);

            int pickY = _cursorY - RowHeight / 2;
            Plate(parent, "PetPick", 0, pickY, width, RowHeight, Skin.Row(width, RowHeight), 1);

            GameObject iconGo = new GameObject("PetPickIcon");
            iconGo.layer = parent.gameObject.layer;
            iconGo.transform.SetParent(parent, false);
            iconGo.transform.localPosition = new Vector3(-width / 2 + 78, pickY, 0f);
            iconGo.transform.localScale = Vector3.one;
            _petPickIcon = iconGo.AddComponent<UISprite>();
            _petPickIcon.width = 34;
            _petPickIcon.height = 34;
            _petPickIcon.depth = 3;

            MakeButton(parent, "PetBack", "<", -width / 2 + 28, pickY, 40, 32, false,
                       delegate { StepPet(-1); });
            MakeButton(parent, "PetFwd", ">", -width / 2 + 128, pickY, 40, 32, false,
                       delegate { StepPet(1); });

            _petPickLabel = MakeLeftLabel(parent, "PetPickName", "-",
                                          -width / 2 + 154, pickY, width - 164, RowHeight,
                                          Skin.Bright, 3);
            _cursorY -= RowHeight + RowGap;

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
                                        BonusEffects[_petBonusIndex].ToString(),
                                        -width / 2 + 98, bonusY, width - 200, RowHeight,
                                        Skin.Bright, 3);
            _petValueInput = AddInput(parent, "PetValue", width / 2 - 52, bonusY, 80, "10");
            _cursorY -= RowHeight + RowGap;

            MakeButton(parent, "CreatePet", "CREATE PET", 0, _cursorY - 22, width, 44, true,
                       CreatePetFromPanel);
            _cursorY -= 44 + RowGap;

            MakeLabel(parent, "PetNote", "Goes straight into the vault's storage.",
                      0, _cursorY - 13, width, 26, Skin.Rim, 3);
            _cursorY -= 26 + RowGap;

            RefreshPetPick();
        }

        private void StepPet(int by)
        {
            if (_pets == null) BuildPetCatalogue();
            if (_pets == null || _pets.Count == 0) return;

            _petIndex = (_petIndex + by + _pets.Count) % _pets.Count;
            RefreshPetPick();
        }

        private void RefreshPetPick()
        {
            PreloadPetArt();
            if (_pets == null) BuildPetCatalogue();

            if (_pets == null || _pets.Count == 0)
            {
                if (_petPickLabel != null) _petPickLabel.text = "no pets in the catalogue";
                return;
            }

            _petIndex = Mathf.Clamp(_petIndex, 0, _pets.Count - 1);
            PetEntry pet = _pets[_petIndex];

            if (_petPickLabel != null)
                _petPickLabel.text = pet.Name + "   " + (_petIndex + 1) + "/" + _pets.Count;

            ShowPetIcon(_petPickIcon, pet);
        }

        private void CreatePetFromPanel()
        {
            if (_pets == null || _pets.Count == 0) return;

            if (_petNameInput != null) _petName = _petNameInput.value;
            if (_petValueInput != null && !string.IsNullOrEmpty(_petValueInput.value))
                _petBonusValue = _petValueInput.value;

            GrantPet(_pets[Mathf.Clamp(_petIndex, 0, _pets.Count - 1)], true);
        }

        private void BuildDwellerSection(Transform parent, int width)
        {
            AddHeader(parent, "NAME", width);

            int nameY = _cursorY - RowHeight / 2;
            Plate(parent, "NameRow", 0, nameY, width, RowHeight, Skin.Row(width, RowHeight), 1);
            _firstNameInput = AddInput(parent, "First", -width / 4, nameY, width / 2 - 12, "FIRST");
            _lastNameInput = AddInput(parent, "Last", width / 4, nameY, width / 2 - 12, "LAST");
            _cursorY -= RowHeight + RowGap;

            AddHeader(parent, "RARITY, GENDER, LEVEL", width);

            _rarityLabel = AddPickerRow(parent, width, "RARITY",
                                        delegate { StepRarity(-1); }, delegate { StepRarity(1); },
                                        Rarities[_rarityIndex].ToString());

            _genderLabel = AddPickerRow(parent, width, "GENDER",
                                        delegate { StepGender(-1); }, delegate { StepGender(1); },
                                        Genders[_genderIndex].ToString());

            int levelY = _cursorY - RowHeight / 2;
            Plate(parent, "LevelRow", 0, levelY, width, RowHeight, Skin.Row(width, RowHeight), 1);
            MakeLeftLabel(parent, "LevelCaption", "LEVEL", -width / 2 + 14, levelY, 120,
                          RowHeight, Skin.Bright, 3);
            _levelInput = AddInput(parent, "Level", width / 2 - 60, levelY, 96,
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

                MakeLabel(parent, "SpecLetter" + i, Specials[i].ToString().Substring(0, 1),
                          x, specialY + 32, cell, 22, Skin.Bright, 3);

                _specialInputs[i] = AddInput(parent, "Spec" + i, x, specialY + 10, cell - 6,
                                             _special[i].ToString(), true);

                MakeButton(parent, "SpecDown" + i, "-", x - cell / 4, specialY - 22, cell / 2 - 3, 26,
                           false, delegate { StepSpecial(index, -1); });
                MakeButton(parent, "SpecUp" + i, "+", x + cell / 4, specialY - 22, cell / 2 - 3, 26,
                           false, delegate { StepSpecial(index, 1); });
            }
            _cursorY -= specialHeight + RowGap;

            MakeButton(parent, "CreateDweller", "CREATE DWELLER", 0, _cursorY - 22, width, 44, true,
                       CreateDwellerFromPanel);
            _cursorY -= 44 + RowGap;

            MakeLabel(parent, "DwellerNote",
                      "Arrives at the vault door, waiting to be let in.",
                      0, _cursorY - 13, width, 26, Skin.Rim, 3);
            _cursorY -= 26 + RowGap;
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
            GameObject go = new GameObject("Input_" + name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one;

            // A field has to look like one. Without a sunken plate behind it a place to type is
            // indistinguishable from a label, and the search box read as the word ALL.
            int fieldHeight = RowHeight - 12;
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
            if (_rarityLabel != null) _rarityLabel.text = Rarities[_rarityIndex].ToString();
        }

        private void StepGender(int by)
        {
            _genderIndex = (_genderIndex + by + Genders.Length) % Genders.Length;
            if (_genderLabel != null) _genderLabel.text = Genders[_genderIndex].ToString();
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
            MakeLabel(parent, "HeaderText_" + text, text, 0, y, width - 20, 34, Skin.Ink, 3);
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

            MakeLeftLabel(parent, "Name_" + resource, resource.ToString(),
                          left, top, span - 150, 26, Skin.Bright, 3);

            _resourceLabels[resource] = MakeRightLabel(parent, "Value_" + resource, "-",
                                                       right, top, 160, 26, Skin.Bright, 3);

            // C# 5 shares a foreach variable across iterations, so each handler needs its own copy.
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

            MakeLeftLabel(parent, "BoxName_" + type, type.ToString(),
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

                _font = bitmap != null ? bitmap : dynamic;
                _fontSize = label.fontSize > 0 ? label.fontSize : _fontSize;
                Log.LogInfo("Borrowed a font from '" + label.name + "': " +
                            _font.GetType().Name + ", size " + _fontSize + ".");
                return;
            }

            Log.LogWarning("No font found on any label; the panel's text will not draw.");
        }

        /// <summary>A drawn texture, positioned in the window's own space.</summary>
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

            Shader shader = Shader.Find("Unlit/Transparent Colored");
            if (shader != null) drawn.shader = shader;

            return drawn;
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

            // A window that builds without error and draws nothing is the failure this mod has
            // already paid for once. Rather than trust that it appeared, look: a widget that is
            // being drawn has a draw call. If it has none after a few frames, say so and let the
            // scaffold take over, so the panel is never simply missing.
            if (_panelOpen && _nguiWindow != null && !_drawChecked && ++_drawCheckFrames >= 10)
            {
                _drawChecked = true;
                ReportDrawing();
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

                        int step = Mathf.Max(16, (thumb.height / 8) * 8);
                        if (thumb.mainTexture != null &&
                            thumb.mainTexture.height == Mathf.RoundToInt(step * Skin.Scale)) continue;

                        thumb.mainTexture = Skin.Frame(10, step, 5, 5, Skin.Bright, Skin.Bright);
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
        /// Reads the game's own item tables.
        ///
        /// The identifier differs per family and neither Name nor CodeId is it. Weapons are found
        /// by a search comparing WeaponId; outfits by a dictionary keyed on the private field
        /// m_outfitId, which has no Id-suffixed property at all. Both were read out of the IL of
        /// ItemParameters rather than guessed, because an id the game cannot resolve produces an
        /// item with no data behind it.
        /// </summary>
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
            try
            {
                string line = data.ItemRarity.ToString().ToUpper();

                if (type == EItemType.Weapon)
                {
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
                _petBonusIndex = (_petBonusIndex - 1 + BonusEffects.Length) % BonusEffects.Length;
            GUILayout.Label(BonusEffects[_petBonusIndex].ToString(), GUILayout.Width(180f));
            if (GUILayout.Button(">", GUILayout.Width(24f)))
                _petBonusIndex = (_petBonusIndex + 1) % BonusEffects.Length;
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
                    Log.LogWarning("The inventory is full; " + entry.Name + " was not granted.");
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

                    data.Bonus = BonusEffects[_petBonusIndex];

                    float value;
                    if (float.TryParse(_petBonusValue, System.Globalization.NumberStyles.Float,
                                       System.Globalization.CultureInfo.InvariantCulture, out value))
                        data.BonusValue = value;
                }

                item.ExtraData = unique as ItemExtraData;
                vault.Inventory.AddItem(item, false, false);

                Log.LogInfo("Granted pet " + entry.Name + " ('" + entry.PetId + "') with bonus " +
                            BonusEffects[_petBonusIndex] + " " + _petBonusValue + ".");
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

                Log.LogInfo("Granted " + entry.Name + " (" + entry.Type + " '" + entry.Id + "').");
            }
            catch (Exception e)
            {
                Log.LogWarning("Granting " + entry.Name + " failed: " + e.Message);
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
                Log.LogInfo("Filled " + resource + " to its cap (+" + room.ToString("0") + ").");
            }
            catch (Exception e)
            {
                Log.LogWarning("Filling " + resource + " failed: " + e.Message);
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

                Log.LogInfo("Granted " + quantity + " " + type + " box(es).");
            }
            catch (Exception e)
            {
                Log.LogWarning("Granting " + quantity + " " + type + " box(es) failed: " + e.Message);
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

            if (GUILayout.Button("<", GUILayout.Width(24f)))
                _genderIndex = (_genderIndex - 1 + Genders.Length) % Genders.Length;
            GUILayout.Label(Genders[_genderIndex].ToString(), GUILayout.Width(60f));
            if (GUILayout.Button(">", GUILayout.Width(24f)))
                _genderIndex = (_genderIndex + 1) % Genders.Length;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int i = 0; i < Specials.Length; i++)
            {
                GUILayout.Label(Specials[i].ToString().Substring(0, 1), GUILayout.Width(12f));
                string text = GUILayout.TextField(_special[i].ToString(), GUILayout.Width(26f));
                int parsed;
                if (int.TryParse(text, out parsed)) _special[i] = parsed;
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
                    Log.LogWarning("The vault is at its population limit; no dweller was created.");
                    return;
                }

                // The game's own call for a newcomer at the door. It creates the dweller and adds
                // them to the waiting line in one go, which is the half that was missing when this
                // was built by hand: setting the waiting-approval state without registering with
                // the queue left someone waiting at a door that did not know they were there.
                //
                // forceCreate is true so the panel is not silently refused by the same throttle
                // that paces normal arrivals; the population limit is checked above instead.
                Dweller dweller = spawner.CreateWaitingDweller(
                    Genders[_genderIndex], false, 0, Rarities[_rarityIndex], true);

                if (dweller == null)
                {
                    Log.LogWarning("The game did not create a dweller; nothing was added.");
                    return;
                }

                int level = 1;

                if (customise)
                {
                    if (!string.IsNullOrEmpty(_dwellerFirst)) dweller.Name = _dwellerFirst;
                    if (!string.IsNullOrEmpty(_dwellerLast)) dweller.LastName = _dwellerLast;

                    if (!int.TryParse(_dwellerLevel, out level) || level < 1) level = 1;
                    ApplyLevel(dweller, level);

                    ApplySpecial(dweller);
                }

                _created.Add(dweller.GetInstanceID());

                Log.LogInfo("Created dweller " + dweller.Name + " " + dweller.LastName +
                            " (" + Rarities[_rarityIndex] + ", level " + level + ") — waiting at the door.");
            }
            catch (Exception e)
            {
                Log.LogWarning("Creating a dweller failed: " + e.Message);
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
                    Log.LogWarning("The game did not create " + label + "; nothing was added.");
                    return;
                }

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

        private void ReportOnce(string key, string message)
        {
            if (_reported.Add(key)) Log.LogWarning(message);
        }
    }
}
