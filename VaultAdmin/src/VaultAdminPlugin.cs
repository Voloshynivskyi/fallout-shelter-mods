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
        public const string PluginVersion = "0.10.2";

        internal static ManualLogSource Log;

        private static ConfigEntry<bool> Enabled;
        private static ConfigEntry<string> ToggleKey;
        private static ConfigEntry<bool> ShowHudButton;
        private static ConfigEntry<float> HudButtonOffsetX;

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

        private const string CameraButtonPath =
            "MainScene_Root/GUI/VaultHUDWindow/VaultHUDPanel/7 BottomLeft/BTN Camera";

        private int _hudFrames;
        private const int HudCheckInterval = 120;   // frames, about twice every two seconds
        private bool _hudPathReported;

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
            if (++_hudFrames < HudCheckInterval) return;
            _hudFrames = 0;

            try
            {
                GameObject source = GameObject.Find(CameraButtonPath);
                if (source == null)
                {
                    // Not an error: outside a vault this part of the interface simply is not there.
                    if (!_hudPathReported)
                    {
                        _hudPathReported = true;
                        Log.LogInfo("The HUD button host is not present yet. Looking for: " + CameraButtonPath);
                    }
                    return;
                }

                Transform parent = source.transform.parent;
                if (parent == null) return;
                if (parent.Find(HudButtonName) != null) return;   // already placed

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
        }

        private void Update()
        {
            if (!Enabled.Value) return;

            EnsureHudButton();

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
