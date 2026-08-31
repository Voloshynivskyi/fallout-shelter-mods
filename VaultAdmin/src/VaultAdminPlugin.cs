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

        public static Texture2D Frame(int width, int height, int radius, int thickness,
                                      Color edge, Color inside)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            radius = Mathf.Clamp(radius, 0, Mathf.Min(width, height) / 2);

            string key = width + "x" + height + "r" + radius + "t" + thickness +
                         "e" + ColorUtility.ToHtmlStringRGBA(edge) +
                         "i" + ColorUtility.ToHtmlStringRGBA(inside);

            Texture2D cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) return cached;

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // How far this pixel lies outside the rounded core, in pixels.
                    float dx = 0f;
                    float dy = 0f;
                    if (x < radius) dx = radius - x;
                    else if (x >= width - radius) dx = x - (width - radius - 1);
                    if (y < radius) dy = radius - y;
                    else if (y >= height - radius) dy = y - (height - radius - 1);

                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    Color colour;
                    if (d > radius)
                    {
                        colour = Clear;                       // outside the rounded corner
                    }
                    else
                    {
                        int near = Mathf.Min(Mathf.Min(x, y), Mathf.Min(width - 1 - x, height - 1 - y));
                        bool onEdge = near < thickness || d > radius - thickness;
                        colour = onEdge ? edge : inside;
                    }

                    pixels[y * width + x] = colour;
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
            return Frame(width, height, 18, 3, Bright, Plate);
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
        public const string PluginVersion = "0.20.0";

        internal static ManualLogSource Log;

        private static ConfigEntry<bool> Enabled;
        private static ConfigEntry<string> ToggleKey;
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
        private static readonly EResource[] NotRealResources =
        {
            EResource.Lunchbox, EResource.MrHandy, EResource.PetCarrier
        };

        private static readonly float[] GrantAmounts = { 100f, 1000f, 10000f };
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

        private static readonly ELunchBoxType[] BoxTypes =
        {
            ELunchBoxType.Regular, ELunchBoxType.MrHandy, ELunchBoxType.PetCarrier,
            ELunchBoxType.NukaColaQuantum
        };

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", false,
                "Master switch, off by default. While this is false the mod reads nothing, draws " +
                "nothing and binds no key: the game behaves exactly as it does without the plugin. " +
                "This is a debug tool, so it stays out of the way until it is asked for.");

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

        /// <summary>Opens or closes the panel. Shared by the hotkey and the HUD button.</summary>
        public void TogglePanel()
        {
            _panelOpen = !_panelOpen;

            if (_nguiWindow == null) BuildWindow();
            if (_nguiWindow != null) _nguiWindow.SetActive(_panelOpen);
        }

        // ---- the window, built from the game's own widget types ----

        private GameObject _nguiWindow;
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
        private int _refreshFrames;

        private enum Tab { Resources, Dwellers, Things }

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

                MeasureWindow(root);

                _nguiWindow = new GameObject("VaultAdmin_Window");
                _nguiWindow.layer = root.gameObject.layer;
                _nguiWindow.transform.SetParent(root.transform, false);
                _nguiWindow.transform.localPosition = new Vector3(_windowX, 0f, 0f);
                _nguiWindow.transform.localScale = Vector3.one;

                _windowPanel = _nguiWindow.AddComponent<UIPanel>();
                _windowPanel.depth = WindowDepth;

                Plate(_nguiWindow.transform, "Frame", 0, 0, _windowWidth, _windowHeight,
                      Skin.Window(_windowWidth, _windowHeight), 0);

                // The title sits on the top edge rather than inside the frame, as the game's do.
                MakeLabel(_nguiWindow.transform, "Title", "VAULT ADMIN",
                          0, _windowHeight / 2, _windowWidth, 40, Skin.Bright, 2);

                BuildTabs(_nguiWindow.transform);
                BuildPages(_nguiWindow.transform);
                ShowTab(_tab);
            RefreshThings();

                MakeButton(_nguiWindow.transform, "Close", "CLOSE",
                           _windowWidth / 2 - 78, -_windowHeight / 2 + 34, 128, 46,
                           true, TogglePanel);

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

        private void BuildTabs(Transform parent)
        {
            Tab[] tabs = { Tab.Resources, Tab.Dwellers, Tab.Things };
            string[] names = { "RESOURCES", "DWELLERS", "ITEMS" };

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
            foreach (Tab tab in new[] { Tab.Resources, Tab.Dwellers, Tab.Things })
            {
                GameObject page = new GameObject("Page_" + tab);
                page.layer = parent.gameObject.layer;
                page.transform.SetParent(parent, false);
                page.transform.localPosition = Vector3.zero;
                page.transform.localScale = Vector3.one;
                _tabPages[tab] = page;
            }

            BuildResourcesPage(_tabPages[Tab.Resources].transform);
            BuildDwellersPage(_tabPages[Tab.Dwellers].transform);
            BuildThingsPage(_tabPages[Tab.Things].transform);
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
            _cursorY = _windowHeight / 2 - 96;     // below the title and the tab bar
            int contentWidth = _windowWidth - Margin * 2;

            foreach (EResource resource in Enum.GetValues(typeof(EResource)))
            {
                if (resource == EResource.None || resource == EResource.Count) continue;
                if (Array.IndexOf(NotRealResources, resource) >= 0) continue;
                AddResourceRow(parent, resource, contentWidth);
            }

            AddHeader(parent, "BOXES", contentWidth);
            foreach (ELunchBoxType type in BoxTypes) AddBoxRow(parent, type, contentWidth);
        }

        // ---- the items and pets page ----

        // A row is built once and rewritten as the list is paged through. Rebuilding widgets to
        // turn a page would make the longest list in the game the most expensive thing here.
        private sealed class ItemRow
        {
            public GameObject Root;
            public UISprite Icon;
            public UILabel Name;
            public GameObject Give;
        }

        private static readonly EItemType[] Families =
        {
            EItemType.Weapon, EItemType.Outfit, EItemType.Junk, EItemType.Pet
        };

        private readonly List<ItemRow> _itemRows = new List<ItemRow>();

        // Holds CatalogueEntry for the three item families and PetEntry for pets, so one list and
        // one set of rows serve all four.
        private readonly List<object> _shown = new List<object>();

        private GameObject _petStrip;
        private UIInput _filterInput;
        private UIInput _petNameInput;
        private UIInput _petValueInput;
        private UILabel _familyLabel;
        private UILabel _bonusLabel;
        private UILabel _pageLabel;

        private int _familyIndex;
        private int _itemPage;
        private int _rowsPerPage;
        private string _appliedFilter = "";

        private const int ItemRowHeight = 40;
        private const int MaxItemRows = 9;

        private void BuildThingsPage(Transform parent)
        {
            _cursorY = _windowHeight / 2 - 96;
            int width = _windowWidth - Margin * 2;

            _familyLabel = AddPickerRow(parent, width, "FAMILY",
                                        delegate { StepFamily(-1); }, delegate { StepFamily(1); },
                                        Families[_familyIndex].ToString().ToUpper());

            Plate(parent, "FilterRow", 0, _cursorY, width, RowHeight, Skin.Row(width, RowHeight), 1);
            UILabel filterName = MakeLabel(parent, "FilterName", "FILTER",
                                           -width / 2 + 70, _cursorY, 120, RowHeight, Skin.Bright, 3);
            filterName.alignment = NGUIText.Alignment.Left;
            _filterInput = AddInput(parent, "Filter", 30, _cursorY, width - 160, "ALL");
            _cursorY -= RowHeight + RowGap;

            // The list occupies whatever is left between here and the pager above the close button.
            int listTop = _cursorY;
            int listBottom = -_windowHeight / 2 + 96;

            _rowsPerPage = Mathf.Clamp((listTop - listBottom) / (ItemRowHeight + RowGap), 1, MaxItemRows);

            for (int i = 0; i < _rowsPerPage; i++)
                _itemRows.Add(BuildItemRow(parent, i, width, listTop - i * (ItemRowHeight + RowGap)));

            BuildPetStrip(parent, width, listBottom + 2 * (ItemRowHeight + RowGap));

            // The pager sits at the foot of the page, clear of the window's close button.
            int pagerY = listBottom - 8;
            MakeButton(parent, "PagePrev", "<", -width / 2 + 40, pagerY, 70, 34, false,
                       delegate { StepPage(-1); });
            _pageLabel = MakeLabel(parent, "PageLabel", "-", 0, pagerY, width - 200, 34, Skin.Bright, 3);
            MakeButton(parent, "PageNext", ">", width / 2 - 40, pagerY, 70, 34, false,
                       delegate { StepPage(1); });
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
            iconGo.transform.localPosition = new Vector3(-width / 2 + 28, 0f, 0f);
            iconGo.transform.localScale = Vector3.one;

            row.Icon = iconGo.AddComponent<UISprite>();
            row.Icon.width = 34;
            row.Icon.height = 34;
            row.Icon.depth = 3;

            row.Name = MakeLabel(row.Root.transform, "Name", "", 20, 0, width - 190, ItemRowHeight,
                                 Skin.Bright, 3);
            row.Name.alignment = NGUIText.Alignment.Left;

            int captured = index;
            row.Give = MakeButton(row.Root.transform, "Give", "GIVE", width / 2 - 48, 0, 84, 32,
                                  false, delegate { GiveRow(captured); });

            return row;
        }

        /// <summary>
        /// The two controls that only mean anything for a pet.
        ///
        /// Pets are the one family that carries data per copy, so they are the one the panel can
        /// name and tune. The strip appears only for them, and the list gives up its last two rows
        /// to make room rather than the page growing past the window.
        /// </summary>
        private void BuildPetStrip(Transform parent, int width, int y)
        {
            _petStrip = new GameObject("PetStrip");
            _petStrip.layer = parent.gameObject.layer;
            _petStrip.transform.SetParent(parent, false);
            _petStrip.transform.localPosition = new Vector3(0f, y, 0f);
            _petStrip.transform.localScale = Vector3.one;

            Plate(_petStrip.transform, "NamePlate", 0, 0, width, RowHeight, Skin.Row(width, RowHeight), 1);
            UILabel caption = MakeLabel(_petStrip.transform, "NameCaption", "PET NAME",
                                        -width / 2 + 80, 0, 150, RowHeight, Skin.Bright, 3);
            caption.alignment = NGUIText.Alignment.Left;
            _petNameInput = AddInput(_petStrip.transform, "PetName", 40, 0, width - 180, "RANDOM");

            int second = -(RowHeight + RowGap);
            Plate(_petStrip.transform, "BonusPlate", 0, second, width, RowHeight,
                  Skin.Row(width, RowHeight), 1);

            MakeButton(_petStrip.transform, "BonusBack", "<", -width / 2 + 34, second, 44, 32, false,
                       delegate { StepBonus(-1); });
            _bonusLabel = MakeLabel(_petStrip.transform, "BonusName",
                                    BonusEffects[_petBonusIndex].ToString(),
                                    -20, second, width - 220, RowHeight, Skin.Bright, 3);
            MakeButton(_petStrip.transform, "BonusFwd", ">", -width / 2 + 82, second, 44, 32, false,
                       delegate { StepBonus(1); });

            _petValueInput = AddInput(_petStrip.transform, "PetValue", width / 2 - 60, second, 96, "10");

            _petStrip.SetActive(false);
        }

        private void StepFamily(int by)
        {
            _familyIndex = (_familyIndex + by + Families.Length) % Families.Length;
            _family = Families[_familyIndex];
            _itemPage = 0;
            if (_familyLabel != null) _familyLabel.text = _family.ToString().ToUpper();
            RefreshThings();
        }

        private void StepBonus(int by)
        {
            _petBonusIndex = (_petBonusIndex + by + BonusEffects.Length) % BonusEffects.Length;
            if (_bonusLabel != null) _bonusLabel.text = BonusEffects[_petBonusIndex].ToString();
        }

        private void StepPage(int by)
        {
            int rows = VisibleRowCount();
            int pages = Mathf.Max(1, (_shown.Count + rows - 1) / rows);
            _itemPage = Mathf.Clamp(_itemPage + by, 0, pages - 1);
            FillRows();
        }

        private int VisibleRowCount()
        {
            bool pets = _family == EItemType.Pet;
            return Mathf.Max(1, pets ? _rowsPerPage - 2 : _rowsPerPage);
        }

        /// <summary>Rereads the catalogue for the chosen family and puts the list back to its top.</summary>
        private void RefreshThings()
        {
            _shown.Clear();

            string filter = _filter == null ? "" : _filter.Trim().ToLower();

            if (_family == EItemType.Pet)
            {
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

            int rows = VisibleRowCount();
            int pages = Mathf.Max(1, (_shown.Count + rows - 1) / rows);
            _itemPage = Mathf.Clamp(_itemPage, 0, pages - 1);

            if (_petStrip != null) _petStrip.SetActive(_family == EItemType.Pet);
            FillRows();
        }

        /// <summary>Writes this page of the list into rows that already exist.</summary>
        private void FillRows()
        {
            int rows = VisibleRowCount();
            int first = _itemPage * rows;

            for (int i = 0; i < _itemRows.Count; i++)
            {
                ItemRow row = _itemRows[i];
                if (row == null || row.Root == null) continue;

                int index = first + i;
                bool used = i < rows && index < _shown.Count;
                row.Root.SetActive(used);
                if (!used) continue;

                object thing = _shown[index];

                CatalogueEntry item = thing as CatalogueEntry;
                if (item != null)
                {
                    row.Name.text = item.Name;
                    ShowIcon(row.Icon, item);
                }
                else
                {
                    PetEntry pet = (PetEntry)thing;
                    row.Name.text = pet.Name;
                    // Pet art loads per type on demand, so there is nothing to show here until the
                    // pet is granted and the game fetches its atlas.
                    row.Icon.atlas = null;
                    row.Icon.spriteName = "";
                }
            }

            int pages = Mathf.Max(1, (_shown.Count + rows - 1) / rows);
            if (_pageLabel != null)
                _pageLabel.text = _shown.Count == 0
                    ? "NOTHING MATCHES"
                    : (_itemPage + 1) + " / " + pages + "   (" + _shown.Count + ")";
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

            icon.atlas = atlas;
            icon.spriteName = entry.Sprite;
        }

        /// <summary>Grants whatever sits in this row, through the same paths the panel already uses.</summary>
        private void GiveRow(int rowIndex)
        {
            int index = _itemPage * VisibleRowCount() + rowIndex;
            if (index < 0 || index >= _shown.Count) return;

            object thing = _shown[index];

            CatalogueEntry item = thing as CatalogueEntry;
            if (item != null) { GrantItem(item); return; }

            if (_petNameInput != null) _petName = _petNameInput.value;
            if (_petValueInput != null && !string.IsNullOrEmpty(_petValueInput.value))
                _petBonusValue = _petValueInput.value;

            GrantPet((PetEntry)thing);
        }

        private UIInput _firstNameInput;
        private UIInput _lastNameInput;
        private UILabel _rarityLabel;
        private UILabel _genderLabel;
        private UILabel _levelLabel;
        private readonly UILabel[] _specialLabels = new UILabel[7];
        private int _dwellerLevelValue = 1;

        private void BuildDwellersPage(Transform parent)
        {
            _cursorY = _windowHeight / 2 - 96;
            int width = _windowWidth - Margin * 2;

            AddHeader(parent, "NAME", width);

            Plate(parent, "NameRow", 0, _cursorY, width, RowHeight, Skin.Row(width, RowHeight), 1);
            _firstNameInput = AddInput(parent, "First", -width / 4, _cursorY, width / 2 - 12, "FIRST");
            _lastNameInput = AddInput(parent, "Last", width / 4, _cursorY, width / 2 - 12, "LAST");
            _cursorY -= RowHeight + RowGap;

            AddHeader(parent, "RARITY, GENDER, LEVEL", width);

            _rarityLabel = AddPickerRow(parent, width, "RARITY",
                                        delegate { StepRarity(-1); }, delegate { StepRarity(1); },
                                        Rarities[_rarityIndex].ToString());

            _genderLabel = AddPickerRow(parent, width, "GENDER",
                                        delegate { StepGender(-1); }, delegate { StepGender(1); },
                                        Genders[_genderIndex].ToString());

            _levelLabel = AddPickerRow(parent, width, "LEVEL",
                                       delegate { StepLevel(-1); }, delegate { StepLevel(1); },
                                       _dwellerLevelValue.ToString());

            AddHeader(parent, "SPECIAL", width);

            // Seven stats across one row: a row apiece would not fit, and the letters are the
            // game's own shorthand for them.
            int cell = (width - 16) / 7;
            Plate(parent, "SpecialRow", 0, _cursorY, width, RowHeight + 22,
                  Skin.Row(width, RowHeight + 22), 1);

            for (int i = 0; i < Specials.Length; i++)
            {
                int index = i;
                int x = -width / 2 + cell / 2 + 8 + i * cell;

                MakeLabel(parent, "SpecLetter" + i, Specials[i].ToString().Substring(0, 1),
                          x, _cursorY + 20, cell, 24, Skin.Bright, 3);

                _specialLabels[i] = MakeLabel(parent, "SpecValue" + i, _special[i].ToString(),
                                              x, _cursorY - 2, cell, 24, Skin.Bright, 3);

                MakeButton(parent, "SpecDown" + i, "-", x - cell / 4, _cursorY - 24, cell / 2 - 4, 26,
                           false, delegate { StepSpecial(index, -1); });
                MakeButton(parent, "SpecUp" + i, "+", x + cell / 4, _cursorY - 24, cell / 2 - 4, 26,
                           false, delegate { StepSpecial(index, 1); });
            }
            _cursorY -= RowHeight + 22 + RowGap;

            MakeButton(parent, "CreateDweller", "CREATE DWELLER", 0, _cursorY, width, 44, true,
                       CreateDwellerFromPanel);
            _cursorY -= 44 + RowGap;

            MakeLabel(parent, "DwellerNote",
                      "Arrives at the vault door, waiting to be let in.",
                      0, _cursorY, width, 26, Skin.Bright, 3);
        }

        /// <summary>A label, a value, and a pair of arrows — the game's own way of offering a choice.</summary>
        private UILabel AddPickerRow(Transform parent, int width, string caption,
                                     EventDelegate.Callback back, EventDelegate.Callback forward,
                                     string initial)
        {
            Plate(parent, "Pick_" + caption, 0, _cursorY, width, RowHeight,
                  Skin.Row(width, RowHeight), 1);

            UILabel name = MakeLabel(parent, "PickName_" + caption, caption,
                                     -width / 2 + 90, _cursorY, 160, RowHeight, Skin.Bright, 3);
            name.alignment = NGUIText.Alignment.Left;

            MakeButton(parent, "PickBack_" + caption, "<", width / 2 - 190, _cursorY, 44, 34, false, back);
            UILabel value = MakeLabel(parent, "PickValue_" + caption, initial,
                                      width / 2 - 118, _cursorY, 120, RowHeight, Skin.Bright, 3);
            MakeButton(parent, "PickFwd_" + caption, ">", width / 2 - 46, _cursorY, 44, 34, false, forward);

            _cursorY -= RowHeight + RowGap;
            return value;
        }

        private UIInput AddInput(Transform parent, string name, int x, int y, int width, string hint)
        {
            GameObject go = new GameObject("Input_" + name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one;

            UILabel label = MakeLabel(go.transform, "Text", "", 0, 0, width - 16, RowHeight - 8,
                                      Skin.Bright, 3);

            // NGUI routes typing through a collider, exactly as it routes clicks.
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, RowHeight - 8, 1f);
            box.isTrigger = true;

            UIInput input = go.AddComponent<UIInput>();
            input.label = label;
            input.defaultText = hint;
            input.characterLimit = 24;
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

        private void StepLevel(int by)
        {
            _dwellerLevelValue = Mathf.Clamp(_dwellerLevelValue + by, 1, 50);
            _dwellerLevel = _dwellerLevelValue.ToString();
            if (_levelLabel != null) _levelLabel.text = _dwellerLevel;
        }

        private void StepSpecial(int index, int by)
        {
            _special[index] = Mathf.Clamp(_special[index] + by, 1, 10);
            if (_specialLabels[index] != null) _specialLabels[index].text = _special[index].ToString();
        }

        /// <summary>Reads the panel's fields and hands them to the creation the game already uses.</summary>
        private void CreateDwellerFromPanel()
        {
            if (_firstNameInput != null) _dwellerFirst = _firstNameInput.value;
            if (_lastNameInput != null) _dwellerLast = _lastNameInput.value;
            _dwellerLevel = _dwellerLevelValue.ToString();
            CreateDweller();
        }

        private void AddHeader(Transform parent, string text, int width)
        {
            Plate(parent, "Header_" + text, 0, _cursorY, width, 34, Skin.Header(width, 34), 1);
            MakeLabel(parent, "HeaderText_" + text, text, 0, _cursorY, width - 20, 34, Skin.Ink, 3);
            _cursorY -= 34 + RowGap;
        }

        private void AddResourceRow(Transform parent, EResource resource, int width)
        {
            Plate(parent, "Row_" + resource, 0, _cursorY, width, RowHeight,
                  Skin.Row(width, RowHeight), 1);

            UILabel name = MakeLabel(parent, "Name_" + resource, resource.ToString(),
                                     -width / 2 + 90, _cursorY, 160, RowHeight, Skin.Bright, 3);
            name.alignment = NGUIText.Alignment.Left;

            UILabel value = MakeLabel(parent, "Value_" + resource, "-",
                                      -width / 2 + 250, _cursorY, 130, RowHeight, Skin.Bright, 3);
            value.alignment = NGUIText.Alignment.Right;
            _resourceLabels[resource] = value;

            // C# 5 shares a foreach variable across iterations, so each handler needs its own copy.
            EResource captured = resource;
            int buttonWidth = 74;
            int x = width / 2 - buttonWidth / 2 - 8;

            MakeButton(parent, "Fill_" + resource, "MAX", x, _cursorY, buttonWidth, 34, false,
                       delegate { FillToCap(captured); });
            x -= buttonWidth + 6;

            for (int i = GrantAmounts.Length - 1; i >= 0; i--)
            {
                float amount = GrantAmounts[i];
                MakeButton(parent, "Grant_" + resource + "_" + amount,
                           "+" + amount.ToString("0"), x, _cursorY, buttonWidth, 34, false,
                           delegate { Grant(captured, amount); });
                x -= buttonWidth + 6;
            }

            _cursorY -= RowHeight + RowGap;
        }

        private void AddBoxRow(Transform parent, ELunchBoxType type, int width)
        {
            Plate(parent, "BoxRow_" + type, 0, _cursorY, width, RowHeight,
                  Skin.Row(width, RowHeight), 1);

            UILabel name = MakeLabel(parent, "BoxName_" + type, type.ToString(),
                                     -width / 2 + 110, _cursorY, 200, RowHeight, Skin.Bright, 3);
            name.alignment = NGUIText.Alignment.Left;

            ELunchBoxType captured = type;
            int buttonWidth = 74;
            int x = width / 2 - buttonWidth / 2 - 8;

            for (int i = BoxAmounts.Length - 1; i >= 0; i--)
            {
                int quantity = BoxAmounts[i];
                MakeButton(parent, "Box_" + type + "_" + quantity, "+" + quantity,
                           x, _cursorY, buttonWidth, 34, false,
                           delegate { GrantBoxes(captured, quantity); });
                x -= buttonWidth + 6;
            }

            _cursorY -= RowHeight + RowGap;
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

        private static UIRoot FindUiRoot()
        {
            UIRoot[] roots = Resources.FindObjectsOfTypeAll<UIRoot>();
            UIRoot best = null;

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null || !roots[i].gameObject.activeInHierarchy) continue;
                // The scene root, not the world-space one the dweller icons hang off.
                if (roots[i].name.IndexOf("World", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                best = roots[i];
                if (roots[i].name.IndexOf("MainScene", StringComparison.OrdinalIgnoreCase) >= 0) break;
            }
            return best;
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

            if (_panelOpen && _nguiWindow != null && ++_refreshFrames >= 30)
            {
                _refreshFrames = 0;
                try
                {
                    RefreshValues();

                    if (_filterInput != null)
                    {
                        string typed = _filterInput.value == null ? "" : _filterInput.value;
                        if (typed != _appliedFilter)
                        {
                            _appliedFilter = typed;
                            _filter = typed;
                            _itemPage = 0;
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
            // came before it, and it stays only for the case where the window could not be built —
            // no UI root, no font — so the mod is never unreachable.
            if (_nguiWindow != null) return;

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

                int weapons = Collect(items.WeaponsList, EItemType.Weapon, "WeaponId", "WeaponSprite");
                int outfits = Collect(items.OutfitList, EItemType.Outfit, "m_outfitId", "OutfitSprite");
                int junk = Collect(items.JunksList, EItemType.Junk, "JunkId", "JunkSprite");

                _atlases[EItemType.Weapon] = items.WeaponAtlas;
                _atlases[EItemType.Outfit] = items.OutfitAtlas;
                _atlases[EItemType.Junk] = items.JunkAtlas;

                Log.LogInfo("Item catalogue read from the game: " + weapons + " weapons, " +
                            outfits + " outfits, " + junk + " junk.");
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
        private int Collect(Array table, EItemType type, string idMember, string spriteMember)
        {
            if (table == null) return 0;

            int added = 0;
            for (int i = 0; i < table.Length; i++)
            {
                try
                {
                    DwellerBaseItem data = table.GetValue(i) as DwellerBaseItem;
                    if (data == null || data.IsHiddenItem) continue;

                    string id = ReadMember(data, idMember);
                    if (string.IsNullOrEmpty(id)) continue;

                    // Name is a non-public property, so it comes through the same reflection
                    // helper as the id. CodeId is public and readable, and stands in when the
                    // display name is missing.
                    string label = ReadMember(data, "Name");
                    if (string.IsNullOrEmpty(label)) label = data.CodeId;
                    if (string.IsNullOrEmpty(label)) label = id;

                    CatalogueEntry entry = new CatalogueEntry();
                    entry.Type = type;
                    entry.Id = id;
                    entry.Name = label;
                    entry.Rarity = data.ItemRarity;
                    entry.Sprite = ReadMember(data, spriteMember);
                    _catalogue.Add(entry);
                    added++;
                }
                catch { }   // one unreadable row must not cost the whole family
            }
            return added;
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
        private void GrantPet(PetEntry entry)
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
                if (data != null)
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

                vault.AddLunchBox(type, quantity);
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

        private void CreateDweller()
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

                if (!string.IsNullOrEmpty(_dwellerFirst)) dweller.Name = _dwellerFirst;
                if (!string.IsNullOrEmpty(_dwellerLast)) dweller.LastName = _dwellerLast;

                int level;
                if (!int.TryParse(_dwellerLevel, out level) || level < 1) level = 1;
                ApplyLevel(dweller, level);

                ApplySpecial(dweller);
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
