using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CapsFoundry
{
    /// <summary>
    /// Adds a caps-producing room to Fallout Shelter.
    ///
    /// The room is a runtime clone of the Geothermal prefab, re-labelled as ERoomType.ProteinBar —
    /// a value the game ships in its enum but has no prefab for, so nothing in vanilla can create
    /// or reference it. Reconnaissance established that:
    ///   * GetRoomInfoForType(ProteinBar) returns no prefab, so the game cannot build one;
    ///   * "ProteinBar" occurs in the game files only as an enum name, nowhere else;
    ///   * it appears in no save.
    ///
    /// Existing rooms are safe by construction rather than by inspection: no shipped RoomInfo is
    /// modified, the registry array is only appended to, and Instantiate deep-copies the prefab so
    /// the clone's levels and materials are its own. Real Geothermal rooms keep their art, their
    /// levels and their output.
    ///
    /// Failure policy: creation happens in one guarded step that either completes or registers
    /// nothing at all, so the game is never left half-modified.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ovolo.falloutshelter.capsfoundry";
        public const string PluginName = "Caps Foundry";
        public const string PluginVersion = "1.3.2";  // BepInEx parses this with System.Version — no suffixes

        /// <summary>Enum value adopted for the new room. See the class remarks for why this one.</summary>
        internal const ERoomType AdoptedType = ERoomType.ProteinBar;

        /// <summary>Prefab whose art, levels and merge behaviour the new room borrows.</summary>
        internal const ERoomType DonorType = ERoomType.Geothermal;

        internal const string NameLocId = "CapsFoundry_Name";
        internal const string DescriptionLocId = "CapsFoundry_Desc";

        /// <summary>
        /// Resource the room actually accumulates during its work cycle.
        ///
        /// NOT caps. The game treats caps as a bonus that rooms never produce: GetPositiveResources
        /// excludes them by default and MaxResourceValue skips their slot, so a caps-producing room
        /// left the icon lookup with an empty resource list and crashed while a save containing it
        /// was being deserialised. A normal resource carries the cycle, and the amount is converted
        /// to caps at collection time by CollectConversionPatch.
        /// </summary>
        internal const EResource CarrierResource = EResource.Energy;

        // Scene and prefab ids are the enum member names as strings.
        internal static readonly string AdoptedTypeName = AdoptedType.ToString();

        /// <summary>
        /// Name the scene and pool redirects point at. Comes from the VisualDonor setting so the
        /// room's art can be changed without a rebuild; falls back to the structural donor.
        /// </summary>
        internal static string DonorTypeName
        {
            get
            {
                string configured = VisualDonor == null ? null : VisualDonor.Value;
                if (string.IsNullOrEmpty(configured)) return DonorType.ToString();

                try { Enum.Parse(typeof(ERoomType), configured, true); }
                catch { return DonorType.ToString(); }

                return configured;
            }
        }

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> CapsPerBatch;
        internal static ConfigEntry<float> HoursLevel1;
        internal static ConfigEntry<float> HoursLevel2;
        internal static ConfigEntry<float> HoursLevel3;
        internal static ConfigEntry<int> BuildPriceCaps;
        internal static ConfigEntry<string> PriceSourceRoom;
        internal static ConfigEntry<string> VisualDonor;
        internal static ConfigEntry<string> UnlockLikeRoom;
        internal static ConfigEntry<string> TintColor;
        internal static ConfigEntry<float> TintStrength;
        internal static ConfigEntry<float> TintBrightness;
        internal static ConfigEntry<string> RoomName;
        internal static ConfigEntry<bool> VerboseLogging;

        internal static ManualLogSource Log;
        internal static bool RoomRegistered;

        private bool _bootstrapped;
        private float _elapsed;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch. When false the room is never created and nothing is changed.");

            RoomName = Config.Bind("General", "RoomName", "Caps Foundry",
                "Name shown for the room in game.");

            CapsPerBatch = Config.Bind("Rate", "CapsPerBatch", 200,
                "Caps produced per completed cycle by a ONE-SEGMENT room. A wider room produces " +
                "proportionally more per cycle rather than cycling faster.");

            HoursLevel1 = Config.Bind("Rate", "HoursLevel1", 4.0f,
                "Hours a LEVEL 1 room takes to complete a batch at full worker efficiency.");
            HoursLevel2 = Config.Bind("Rate", "HoursLevel2", 3.0f,
                "Hours a LEVEL 2 room takes to complete a batch at full worker efficiency.");
            HoursLevel3 = Config.Bind("Rate", "HoursLevel3", 2.0f,
                "Hours a LEVEL 3 room takes to complete a batch at full worker efficiency.");

            BuildPriceCaps = Config.Bind("General", "BuildPriceCaps", 0,
                "Override for the build price in caps. 0 means copy the price AND the per-room " +
                "price escalation from the room named by PriceSourceRoom.");

            PriceSourceRoom = Config.Bind("General", "PriceSourceRoom", "NukaCola",
                "Room whose build price and price escalation this room copies. The visual donor's " +
                "price is inherited otherwise, which made the room far too cheap.");

            UnlockLikeRoom = Config.Bind("General", "UnlockLikeRoom", "NukaCola",
                "Room whose unlock condition this room copies, by ERoomType name. The adopted room " +
                "type has no unlock objective of its own, and a locked entry needs one to draw its " +
                "progress, so it borrows this room's. NukaCola unlocks at 100 dwellers. Leave empty " +
                "to have the room always available.");

            VisualDonor = Config.Bind("Appearance", "VisualDonor", "Energy2",
                "Room whose 3D art this room borrows, by ERoomType name. Energy2 is the Nuclear " +
                "Reactor; Geothermal is the Power Plant. Must be a Production room, or the game " +
                "hands back the wrong kind of room object and production never starts. The art is " +
                "fixed when a room is built, so change this before building.");

            TintStrength = Config.Bind("Appearance", "TintStrength", 0.55f,
                "How far the room is pushed towards TintColor, 0 to 1. Full strength on a saturated " +
                "colour swallows most of the light, which reads as a dark room rather than a " +
                "coloured one.");

            TintBrightness = Config.Bind("Appearance", "TintBrightness", 1.15f,
                "Brightness applied after tinting, relative to the room's original brightness. " +
                "Above 1 makes the room lighter than stock; the tint itself no longer darkens it.");

            TintColor = Config.Bind("Appearance", "TintColor", "#C0392B",
                "Hex colour multiplied into the cloned room's materials so it reads differently " +
                "from a Geothermal plant. Only the clone is tinted. Empty disables tinting.");

            VerboseLogging = Config.Bind("Logging", "VerboseLogging", false,
                "Log the per-room detail as well: what was painted, which room had its identity or " +
                "level data restored, and what the upgrade costs were copied as. These lines repeat " +
                "for every room and every level change, so they are off unless you are diagnosing " +
                "something. One-time facts and all warnings are logged either way.");

            ApplyPatches();
        }

        /// <summary>
        /// Whether per-room detail is wanted. Test this BEFORE building a message: the argument to
        /// <see cref="LogDetail"/> is concatenated whether or not the call ends up logging anything,
        /// so an unguarded call on a path the game runs often allocates a string every time for
        /// nothing.
        /// </summary>
        internal static bool Verbose
        {
            get { return VerboseLogging != null && VerboseLogging.Value; }
        }

        /// <summary>Per-room detail, logged only when the player has asked for it.</summary>
        internal static void LogDetail(string message)
        {
            if (Verbose) Log.LogInfo(message);
        }

        private void Update()
        {
            if (!Enabled.Value) return;

            ProcessTintQueue();

            if (_bootstrapped) return;

            // The registry is filled by the game's own singletons, which do not exist at Awake.
            _elapsed += Time.deltaTime;
            if (_elapsed < 2f) return;
            _elapsed = 0f;

            try
            {
                if (TryCreateRoom()) _bootstrapped = true;
            }
            catch (Exception e)
            {
                // Never let this reach Unity's update loop; give up rather than retry into a
                // half-built state.
                _bootstrapped = true;
                Log.LogError("Room creation failed, so the room was not added. Existing rooms are " +
                             "untouched. Details: " + e);
            }
        }

        /// <summary>
        /// Applies each patch class separately instead of PatchAll().
        ///
        /// PatchAll aborts on the first failure, so a single method renamed by a game update would
        /// take the whole mod down. Patching one class at a time means an update costs only the
        /// features that actually broke, and the log names them.
        /// </summary>
        private void ApplyPatches()
        {
            Harmony harmony = new Harmony(PluginGuid);
            int applied = 0;
            System.Collections.Generic.List<string> failures = new System.Collections.Generic.List<string>();

            foreach (Type type in System.Reflection.Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception e)
                {
                    failures.Add(type.Name);
                    Log.LogError("Patch '" + type.Name + "' could not be applied: " + e.Message);
                }
            }

            if (failures.Count == 0)
            {
                Log.LogInfo(PluginName + " " + PluginVersion +
                            (Enabled.Value ? " ready (" : " loaded but disabled (") + applied + " patches).");
                return;
            }

            Log.LogWarning("Applied " + applied + " patches; " + failures.Count + " failed (" +
                           string.Join(", ", failures.ToArray()) + "). The room may misbehave — " +
                           "this usually means the game updated.");
        }

        /// <summary>
        /// Clones the donor prefab and appends it to the room registry. Returns false (quietly)
        /// while the game is not ready yet; returns true once there is nothing more to try.
        /// </summary>
        internal static bool TryCreateRoom()
        {
            ParameterDataMgr mgr = ParameterDataMgr.Instance;
            if (mgr == null) return false;

            RoomInfo[] prefabs = mgr.RoomDataPrefabs;
            if (prefabs == null || prefabs.Length == 0) return false;

            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null && prefabs[i].m_eRoomType == AdoptedType)
                {
                    Log.LogWarning("A prefab for " + AdoptedType + " already exists — not adding another.");
                    RoomRegistered = true;
                    return true;
                }
            }

            RoomInfo donor = null;
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null && prefabs[i].m_eRoomType == DonorType) { donor = prefabs[i]; break; }
            }
            if (donor == null)
            {
                Log.LogError("Donor room " + DonorType + " not found in the registry; aborting.");
                return true;
            }

            RoomInfo clone = UnityEngine.Object.Instantiate(donor);
            if (clone == null)
            {
                Log.LogError("Instantiate returned null; aborting.");
                return true;
            }

            UnityEngine.Object.DontDestroyOnLoad(clone.gameObject);
            clone.gameObject.SetActive(false);
            clone.name = "CapsFoundry";
            clone.m_eRoomType = AdoptedType;

            // Caps are earned, so Luck drives this room's worker efficiency.
            TrySet(clone, "m_eSpecialStat", ESpecialStat.Luck);
            TrySet(clone, "m_SpecialStatLetter", "L");

            _visualDonorControllers = donor.m_LevelControllers;
            ApplyPricing(clone, prefabs);

            // The build-menu thumbnail must match the art the room actually gets, which comes from
            // the visual donor rather than the structural one. Copied dynamically so changing
            // VisualDonor keeps the picture in step.
            RoomInfo visualDonor = FindByTypeName(prefabs, DonorTypeName);
            if (visualDonor != null && visualDonor != donor)
            {
                string donorIcon = Traverse.Create(visualDonor).Field("m_Icon").GetValue<string>();
                if (!string.IsNullOrEmpty(donorIcon))
                {
                    TrySet(clone, "m_Icon", donorIcon);
                    if (Verbose) LogDetail("Build-menu thumbnail taken from " + DonorTypeName + " ('" + donorIcon + "').");
                }
            }

            // m_MainIcon is the small resource sprite on the production readouts — that one
            // becomes caps. m_Icon is left alone: it is the room's THUMBNAIL in the build menu,
            // drawn from a different atlas, so putting a resource sprite there just showed nothing.
            TrySet(clone, "m_MainIcon", "Icon_nukacapsPlain");

            // Localisation keys of our own, resolved by ScriptLocalizationPatch. Reusing the
            // donor's keys is what made the room read "Power Generator" everywhere.
            TrySet(clone, "m_roomNamePerLevelLocId", new string[] { NameLocId, NameLocId, NameLocId });
            TrySet(clone, "m_buildLocId", NameLocId);
            TrySet(clone, "m_descriptionLocId", DescriptionLocId);

            RoomInfo[] extended = new RoomInfo[prefabs.Length + 1];
            Array.Copy(prefabs, extended, prefabs.Length);
            extended[prefabs.Length] = clone;

            // Assigned through the backing field: only the getter is public.
            Traverse.Create(mgr).Field("m_roomDataPrefabs").SetValue(extended);

            if (mgr.GetRoomInfoForType(AdoptedType) == null)
            {
                Log.LogError("The registry did not take the clone; the room is unavailable.");
                return true;
            }

            RoomRegistered = true;
            Log.LogInfo("Registered '" + RoomName.Value + "' as " + AdoptedType +
                        " (cloned from " + DonorType + "); registry " +
                        prefabs.Length + " -> " + extended.Length + " entries.");
            return true;
        }

        /// <summary>
        /// Gives the room a sensible price. Cloning the visual donor also cloned its price, which is
        /// why the room cost almost nothing; copying from a comparable room brings across both the
        /// base cost and the escalation factor that makes each further copy dearer.
        /// </summary>
        private static void ApplyPricing(RoomInfo clone, RoomInfo[] prefabs)
        {
            if (BuildPriceCaps.Value > 0)
            {
                TrySet(clone, "m_Price", new GameResources(EResource.Nuka, BuildPriceCaps.Value));
                TrySet(clone, "m_InstantBuildPrice", new GameResources(EResource.Nuka, BuildPriceCaps.Value));
                return;
            }

            ERoomType sourceType;
            try { sourceType = (ERoomType)Enum.Parse(typeof(ERoomType), PriceSourceRoom.Value, true); }
            catch { Log.LogWarning("PriceSourceRoom '" + PriceSourceRoom.Value + "' is not a room type; keeping the donor price."); return; }

            RoomInfo source = null;
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null && prefabs[i].m_eRoomType == sourceType) { source = prefabs[i]; break; }
            }
            if (source == null) { Log.LogWarning("Price source " + sourceType + " not found; keeping the donor price."); return; }

            Traverse from = Traverse.Create(source);
            GameResources price = from.Field("m_Price").GetValue<GameResources>();
            GameResources instant = from.Field("m_InstantBuildPrice").GetValue<GameResources>();
            float factor = from.Field("m_additionalPriceFactor").GetValue<float>();

            // Built from the (EResource, value) constructor rather than the copy constructor:
            // GameResources keeps both named fields and an array, and a copy that filled only one
            // of them is the likeliest reason the room ended up free.
            if (price != null) TrySet(clone, "m_Price", new GameResources(EResource.Nuka, price[EResource.Nuka]));
            if (instant != null) TrySet(clone, "m_InstantBuildPrice", new GameResources(EResource.Nuka, instant[EResource.Nuka]));
            TrySet(clone, "m_additionalPriceFactor", factor);

            CopyUpgradeCosts(clone, source);

            Log.LogInfo("Copied pricing from " + sourceType + " (" +
                        (price == null ? "?" : price[EResource.Nuka].ToString("0")) +
                        " caps, escalation " + factor + ").");
        }

        /// <summary>
        /// Copies the per-level upgrade and sell values from the price source room.
        ///
        /// Build price alone is not enough: upgrade costs live inside each RoomLevel, so the clone
        /// inherited the visual donor's — a 250-cap upgrade on a room priced like a Bottler.
        ///
        /// Editing these is safe because Instantiate deep-copied the level controllers, so these are
        /// the clone's own objects and the donor room is untouched.
        /// </summary>
        internal static void CopyUpgradeCosts(RoomInfo clone, RoomInfo source)
        {
            try
            {
                LevelController[] mine = clone.m_LevelControllers;
                LevelController[] theirs = source.m_LevelControllers;
                if (mine == null || theirs == null) return;

                // Instantiate clones a GameObject hierarchy, but level controllers may be referenced
                // assets shared with the room this clone came from. Writing into those would rewrite
                // the ORIGINAL room's upgrade costs for the whole game — so check first and refuse.
                LevelController[] donorControllers = _visualDonorControllers;
                if (donorControllers != null)
                {
                    for (int c = 0; c < mine.Length && c < donorControllers.Length; c++)
                    {
                        if (mine[c] != null && ReferenceEquals(mine[c], donorControllers[c]))
                        {
                            Log.LogWarning("Level controllers are SHARED with the donor room — " +
                                           "upgrade costs left untouched to avoid altering it.");
                            return;
                        }
                    }
                }

                int copied = 0;
                float sample = 0f;
                for (int c = 0; c < mine.Length && c < theirs.Length; c++)
                {
                    if (mine[c] == null || theirs[c] == null) continue;

                    RoomLevel[] myLevels = mine[c].m_roomLevels;
                    RoomLevel[] theirLevels = theirs[c].m_roomLevels;
                    if (myLevels == null || theirLevels == null) continue;

                    for (int l = 0; l < myLevels.Length && l < theirLevels.Length; l++)
                    {
                        if (myLevels[l] == null || theirLevels[l] == null) continue;

                        // Built from the (EResource, value) constructor, never the copy constructor:
                        // GameResources keeps both named fields and an array, and a copy that fills
                        // only one of them reads back as zero. That is what made the build price free
                        // earlier, and it did the same to upgrade costs here.
                        if (theirLevels[l].m_upgradeCost != null)
                            myLevels[l].m_upgradeCost =
                                new GameResources(EResource.Nuka, theirLevels[l].m_upgradeCost[EResource.Nuka]);

                        if (theirLevels[l].m_acceleretedUpgradeCost != null)
                            myLevels[l].m_acceleretedUpgradeCost =
                                new GameResources(EResource.Nuka, theirLevels[l].m_acceleretedUpgradeCost[EResource.Nuka]);

                        myLevels[l].m_upgradeTimeCost = theirLevels[l].m_upgradeTimeCost;
                        myLevels[l].m_sellValue = theirLevels[l].m_sellValue;

                        // The clone inherited the donor's vault storage bonus, so a Caps Foundry was
                        // quietly raising the vault's ENERGY capacity. It produces caps; it should
                        // add no storage at all.
                        // m_storageModifier is private, hence Traverse.
                        ProductionLevel production = myLevels[l] as ProductionLevel;
                        if (production != null)
                            Traverse.Create(production).Field("m_storageModifier").SetValue(new GameResources());

                        if (copied == 0 && myLevels[l].m_upgradeCost != null)
                            sample = myLevels[l].m_upgradeCost[EResource.Nuka];
                        copied++;
                    }
                }

                if (Verbose) LogDetail("Copied upgrade costs for " + copied + " room level(s) from " +
                          source.m_eRoomType + "; sample level-1 upgrade = " + sample.ToString("0") + " caps.");
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not copy upgrade costs: " + e.Message);
            }
        }

        /// <summary>
        /// Confirms the room still carries a real price, restoring it if not. Logs what it found so
        /// a future regression is visible rather than silent.
        /// </summary>
        internal static void VerifyPrice(RoomInfo clone)
        {
            try
            {
                GameResources price = Traverse.Create(clone).Field("m_Price").GetValue<GameResources>();
                float caps = price == null ? 0f : price[EResource.Nuka];

                if (caps > 0f)
                {
                    return;
                }

                Log.LogWarning("Build price was " + caps.ToString("0") + " — re-applying.");

                ParameterDataMgr mgr = ParameterDataMgr.Instance;
                if (mgr != null) ApplyPricing(clone, mgr.RoomDataPrefabs);

                price = Traverse.Create(clone).Field("m_Price").GetValue<GameResources>();
            }
            catch (Exception e)
            {
                Log.LogWarning("Price verification failed: " + e.Message);
            }
        }

        /// <summary>Finds a registered room by its ERoomType name, or null.</summary>
        private static RoomInfo FindByTypeName(RoomInfo[] prefabs, string typeName)
        {
            ERoomType type;
            try { type = (ERoomType)Enum.Parse(typeof(ERoomType), typeName, true); }
            catch { return null; }

            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null && prefabs[i].m_eRoomType == type) return prefabs[i];
            }
            return null;
        }

        private static void TrySet(RoomInfo info, string field, object value)
        {
            try { Traverse.Create(info).Field(field).SetValue(value); }
            catch (Exception e) { Log.LogWarning("Could not set " + field + ": " + e.Message); }
        }

        /// <summary>Caps yielded per cycle, scaled by how wide the room is.</summary>
        /// <summary>Donor's level controllers, kept only to detect shared references.</summary>
        private static LevelController[] _visualDonorControllers;

        private static bool _donorFallbackWarned;

        internal static void WarnDonorFallback(string usedPool)
        {
            if (_donorFallbackWarned) return;
            _donorFallbackWarned = true;
            Log.LogWarning("VisualDonor '" + VisualDonor.Value + "' has no loaded object pool, so the " +
                           "room falls back to '" + usedPool + "'. Seasonal rooms are not available " +
                           "in a normal game.");
        }

        /// <summary>
        /// Re-applies level data when the game has wiped it.
        ///
        /// Registration sets the upgrade costs correctly, but something between then and the vault
        /// loading resets them — the same value reads 7500 at registration and 0 by the time a room
        /// is restored. Rather than guess at the culprit, the values are restored on demand whenever
        /// a room finds them missing.
        /// </summary>
        internal static void EnsureLevelData(RoomInfo clone)
        {
            try
            {
                ParameterDataMgr mgr = ParameterDataMgr.Instance;
                if (mgr == null) return;

                RoomInfo source = FindByTypeName(mgr.RoomDataPrefabs, PriceSourceRoom.Value);
                if (source == null) return;

                CopyUpgradeCosts(clone, source);
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not restore level data: " + e.Message);
            }
        }

        /// <summary>Entry point for the early registration hook.</summary>
        internal static void EnsureRoomRegistered()
        {
            if (RoomRegistered) return;
            TryCreateRoom();
        }

        internal static float BatchFor(Room room)
        {
            int size = room.MergeLevel;
            if (size < 1) size = 1;

            int perSegment = CapsPerBatch.Value;
            if (perSegment < 1) perSegment = 1;

            return perSegment * size;
        }

        // Rooms waiting for their first coat, with how many frames we have tried. Their section
        // meshes come from an object pool and are not present the instant the room starts, so a
        // single attempt finds nothing to colour.
        private static readonly List<Room> _tintQueue = new List<Room>();
        private static readonly List<int> _tintAttempts = new List<int>();
        private const int MaxTintAttempts = 900;   // ~15 seconds at 60fps

        internal static void QueueTint(Room room)
        {
            if (room == null || _tintQueue.Contains(room)) return;
            _tintQueue.Add(room);
            _tintAttempts.Add(0);
        }

        private void ProcessTintQueue()
        {
            for (int i = _tintQueue.Count - 1; i >= 0; i--)
            {
                Room room = _tintQueue[i];
                bool done;

                if (room == null) done = true;
                else
                {
                    int painted = TryTint(room);
                    _tintAttempts[i] = _tintAttempts[i] + 1;
                    done = painted != 0 || _tintAttempts[i] >= MaxTintAttempts;

                    if (done && painted == 0)
                        Log.LogWarning("Gave up tinting a " + RoomName.Value +
                                       ": no colourable materials found after " + MaxTintAttempts + " attempts.");
                }

                if (done) { _tintQueue.RemoveAt(i); _tintAttempts.RemoveAt(i); }
            }
        }

        /// <summary>
        /// Colours one room's own material instances. Renderer.materials hands back per-instance
        /// copies, so this never touches the shared assets other rooms draw with.
        /// Returns how many materials were changed.
        /// </summary>
        // Shaders differ in what they call their tint slot; "_Color" is only the most common.
        /// <summary>
        /// Colour slots to try, body first.
        ///
        /// The room body uses the "Underground/Rooms/FakeDynamicLightmap" shader, whose tint lives in
        /// _LightmapModulation — it has no _Color at all. Earlier attempts only ever hit the
        /// electricity and sparkle particle effects (_TintColor), which is why the log claimed
        /// success while nothing on screen changed.
        /// </summary>
        private static readonly string[] ColorProperties =
        {
            "_LightmapModulation",   // room body
            "_Color", "_BaseColor", "_Tint",
            "_TintColor"             // particle effects, last so the body wins
        };

        // Marker written into the name of every material this mod has coloured.
        //
        // The colour is applied by multiplication, so painting the same material twice compounds and
        // drives it towards black. The guard used to be a set of material instance ids, which does
        // not hold: assigning Renderer.materials makes Unity mint fresh instances with fresh ids, so
        // the very next pass no longer recognised its own work. A name survives that, and survives
        // Unity's own cloning, which only appends " (Instance)".
        private const string TintMarker = " [CapsFoundry]";

        /// <summary>
        /// Colours one room's own material instances, at most once per material.
        ///
        /// The tint is normalised so its brightest channel is 1. A raw multiply darkens as well as
        /// recolours — the room's lightmap is already below full brightness — and the result read as
        /// a power generator standing in deep shadow rather than a differently coloured room.
        /// </summary>
        private static int TryTint(Room room)
        {
            try
            {
                Color tint;
                string hex = TintColor.Value;
                if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString(hex, out tint)) return -1;

                float peak = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
                if (peak > 0.001f) tint = new Color(tint.r / peak, tint.g / peak, tint.b / peak, tint.a);

                float strength = Mathf.Clamp01(TintStrength.Value);
                float brightness = Mathf.Max(0.05f, TintBrightness.Value);

                // The room's meshes are NOT children of the Room object — they live in its
                // RoomSections, which is why searching the room's own hierarchy found nothing.
                List<Renderer> found = new List<Renderer>();
                found.AddRange(room.GetComponentsInChildren<Renderer>(true));

                List<RoomSectionBase> sections = room.RoomSections;
                if (sections != null)
                {
                    for (int sIdx = 0; sIdx < sections.Count; sIdx++)
                    {
                        if (sections[sIdx] == null) continue;
                        found.AddRange(sections[sIdx].GetComponentsInChildren<Renderer>(true));
                    }
                }

                Renderer[] renderers = found.ToArray();
                if (renderers.Length == 0) return 0;   // sections stream in later; let the caller retry

                int tinted = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    // Reading sharedMaterials does not instantiate anything, so a renderer that is
                    // already done costs nothing. Renderer.materials is only touched when there is
                    // actually something new to paint — it clones every material it hands back.
                    if (!NeedsTinting(renderers[i])) continue;

                    Material[] mats = renderers[i].materials;
                    bool changed = false;

                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] == null || mats[m].name.Contains(TintMarker)) continue;

                        for (int p = 0; p < ColorProperties.Length; p++)
                        {
                            if (!mats[m].HasProperty(ColorProperties[p])) continue;
                            mats[m].SetColor(ColorProperties[p],
                                              Recolour(mats[m].GetColor(ColorProperties[p]), tint, strength, brightness));
                            mats[m].name = mats[m].name + TintMarker;
                            tinted++;
                            changed = true;
                            break;
                        }
                    }

                    if (changed) renderers[i].materials = mats;
                }

                return tinted;
            }
            catch (Exception e)
            {
                Log.LogWarning("Room tinting failed (the room still works): " + e.Message);
                return -1;
            }
        }

        /// <summary>True when this renderer has at least one material the mod has not coloured yet.</summary>
        private static bool NeedsTinting(Renderer renderer)
        {
            Material[] shared = renderer.sharedMaterials;
            for (int m = 0; m < shared.Length; m++)
            {
                if (shared[m] != null && !shared[m].name.Contains(TintMarker)) return true;
            }
            return false;
        }

        /// <summary>
        /// Shifts a colour towards the tint without losing light.
        ///
        /// A plain multiply is why the room came out almost black: multiplying a lightmap by a
        /// saturated colour removes most of two channels. Here the tinted colour is renormalised
        /// back to the original's luminance, blended by strength, then scaled by brightness — so the
        /// hue changes while the room stays as lit as it was, or lighter.
        /// </summary>
        private static Color Recolour(Color original, Color tint, float strength, float brightness)
        {
            Color multiplied = original * tint;

            float originalLuma = Luminance(original);
            float tintedLuma = Luminance(multiplied);
            if (tintedLuma > 0.001f)
            {
                float restore = originalLuma / tintedLuma;
                multiplied = new Color(multiplied.r * restore, multiplied.g * restore, multiplied.b * restore, original.a);
            }

            Color blended = Color.Lerp(original, multiplied, strength);
            return new Color(blended.r * brightness, blended.g * brightness, blended.b * brightness, original.a);
        }

        private static float Luminance(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        /// <summary>Cycle length for an explicit room level, used by the upgrade preview.</summary>
        internal static float HoursFor(Room room, int levelNumber)
        {
            float hours;
            switch (levelNumber)
            {
                case 2: hours = HoursLevel2.Value; break;
                case 3: hours = HoursLevel3.Value; break;
                default: hours = HoursLevel1.Value; break;
            }
            return hours < 0.0005f ? 0.0005f : hours;
        }

        internal static float HoursFor(Room room)
        {
            float hours;
            switch (room.CurrentLevelNumber)
            {
                case 2: hours = HoursLevel2.Value; break;
                case 3: hours = HoursLevel3.Value; break;
                default: hours = HoursLevel1.Value; break;
            }
            // Floor only guards against a divide-by-zero; kept low enough that very short
            // cycles can be configured for testing.
            return hours < 0.0005f ? 0.0005f : hours;
        }
    }

    /// <summary>
    /// Produces caps instead of the donor's energy.
    ///
    /// GetProducedResources returns a per-second rate which the game accumulates into the room's own
    /// Storage until full; collecting drains that storage into the vault.
    /// </summary>
    [HarmonyPatch(typeof(ProductionRoom), "GetProducedResources")]
    internal static class GetProducedResourcesPatch
    {
        private static void Postfix(ProductionRoom __instance, ref GameResourcesBuilder __result)
        {
            if (!Plugin.RoomRegistered || __instance.RoomType != Plugin.AdoptedType) return;

            // The stats screen totals this to report production per minute. Report nothing: the
            // carrier never reaches the vault, and caps are not part of that readout.
            if (StatsExclusionPatch.Summing)
            {
                __result.Clear();
                return;
            }

            EnsureCarrierOnlyCapacity(__instance);

            float batch = Plugin.BatchFor(__instance);
            float efficiency = __instance.GetWorkingEfficiency(__instance.GetRoomSpecialStat());
            float perSecond = (batch * efficiency) / (Plugin.HoursFor(__instance) * 3600f);

            __result.Clear();
            __result.Add(new GameResources(Plugin.CarrierResource, perSecond));
        }

        /// <summary>
        /// IsWorkCompleted() is RoomStorage.IsFilled(), which requires EVERY resource to reach its
        /// cap — so the donor's energy cap could never be met once energy production is removed,
        /// stalling the room forever. Note also that SetMaxResources stores the reference it is
        /// handed, and the game hands it the level's shared asset, so a fresh object is assigned
        /// rather than the existing one edited.
        /// </summary>
        internal static void EnsureCarrierOnlyCapacity(ProductionRoom room)
        {
            Storage storage = room.RoomStorage;
            if (storage == null) return;

            float capacity = Mathf.Round(Plugin.BatchFor(room));
            if (capacity < 1f) capacity = 1f;

            GameResources max = storage.MaxResources;
            if (max != null
                && max[Plugin.CarrierResource] == capacity
                && max[EResource.Nuka] == 0f
                && max[EResource.Food] == 0f
                && max[EResource.Water] == 0f)
            {
                return;
            }

            storage.SetMaxResources(new GameResources(Plugin.CarrierResource, capacity));
        }
    }

    /// <summary>
    /// OnChangeRoomLevel restores the donor's caps from the level asset; re-apply ours afterwards so
    /// loading a save, upgrading or merging does not stall the room.
    /// </summary>
    [HarmonyPatch(typeof(ProductionRoom), "OnChangeRoomLevel")]
    internal static class OnChangeRoomLevelPatch
    {
        private static void Postfix(ProductionRoom __instance)
        {
            if (!Plugin.RoomRegistered || __instance.RoomType != Plugin.AdoptedType) return;
            GetProducedResourcesPatch.EnsureCarrierOnlyCapacity(__instance);

            // Upgrading rebuilds the room's sections, so the meshes carry brand-new material
            // instances and the colour applied to the old ones is gone. Queue the room again;
            // materials are tracked by instance id, so nothing already coloured is touched twice.
            Plugin.QueueTint(__instance);
        }
    }

    /// <summary>
    /// The production timer, collect button and management panel all display
    /// GetCurrentReserve().MaxResourceValue(), which otherwise reports the donor's energy figures.
    ///
    /// MaxResourceValue skips index 0 (EResource.Nuka) because caps are normally a bonus rather
    /// than a room's output, so putting the batch there would display zero. The reserve therefore
    /// carries the amount on a non-Nuka slot purely so the on-screen number is right — this object
    /// is UI-only and never added to any storage.
    /// </summary>
    [HarmonyPatch(typeof(ProductionRoom), "GetCurrentReserve")]
    internal static class GetCurrentReservePatch
    {
        private static GameResources _reserve;

        private static void Postfix(ProductionRoom __instance, ref GameResources __result)
        {
            if (!Plugin.RoomRegistered || __instance.RoomType != Plugin.AdoptedType) return;

            if (_reserve == null) _reserve = new GameResources();
            _reserve[Plugin.CarrierResource] = Mathf.Round(Plugin.BatchFor(__instance));
            __result = _reserve;
        }
    }

    /// <summary>
    /// Makes the room load the donor's assets instead of crashing.
    ///
    /// Every room type has its own Unity scene named "Logic" + type, built by
    /// AssetManager.StartSceneLoad. There is no LogicProteinBar scene in the shipped game, so
    /// placing the room threw:
    ///     Scene 'LogicProteinBar' couldn't be loaded because it has not been added to the
    ///     active build profile ... ArgumentException: The scene is invalid.
    ///
    /// Redirecting the id to the donor makes the room load LogicGeothermal — the same assets it is
    /// cloned from — which is exactly the intent. Only our own id is ever rewritten; every other
    /// room resolves its scene untouched.
    /// </summary>
    [HarmonyPatch(typeof(AssetManager), "StartSceneLoad")]
    internal static class SceneNamePatch
    {
        private static void Prefix(ref string __0)
        {
            if (!Plugin.RoomRegistered) return;
            if (__0 == Plugin.AdoptedTypeName) __0 = Plugin.DonorTypeName;
        }
    }

    /// <summary>
    /// Redirects the room's object-pool lookups to the donor's pools.
    ///
    /// BaseConstructionMgr.PreloadRoom builds a pool name as roomType + mergeCount ("ProteinBar1")
    /// and calls PoolMgr.GetPool. Those pools are created by the room's scene, so ours never
    /// existed. Worse, the game only *logs* the miss and then dereferences the null pool anyway:
    ///     Error: PreloadRoom could not find the room pool named: 'ProteinBar1'
    ///     NullReferenceException ... BaseConstructionMgr`1[T].PreloadRoom
    /// which is what force-crashed the game on placement.
    ///
    /// The name is prefix-matched so every merge size (1, 2, 3) maps to the donor's matching pool.
    /// </summary>
    [HarmonyPatch(typeof(PoolMgr), "GetPool")]
    internal static class PoolNamePatch
    {
        private static void Prefix(ref string poolType, out string __state)
        {
            __state = null;
            if (!Plugin.RoomRegistered || string.IsNullOrEmpty(poolType)) return;
            if (!poolType.StartsWith(Plugin.AdoptedTypeName, StringComparison.Ordinal)) return;

            __state = poolType.Substring(Plugin.AdoptedTypeName.Length);
            poolType = Plugin.DonorTypeName + __state;
        }

        /// <summary>
        /// Falls back to the structural donor when the configured art donor has no pool.
        ///
        /// Not every room's assets are loaded in a normal game — seasonal rooms such as
        /// UltraciteMining have no pools unless their event content is active. PreloadRoom does not
        /// check the pool it gets back before dereferencing it, so an unavailable donor crashed the
        /// game on placement. Answering with the fallback keeps a bad VisualDonor setting to a
        /// cosmetic disappointment instead of a crash.
        /// </summary>
        private static void Postfix(PoolMgr __instance, string __state, ref ObjectPool __result)
        {
            if (__state == null || __result != null) return;

            string fallback = Plugin.DonorType + __state;

            // Safe from recursion: the fallback name does not start with our room type, so the
            // prefix above leaves it alone.
            __result = __instance.GetPool(fallback);

            if (__result != null)
                Plugin.WarnDonorFallback(fallback);
        }
    }

    /// <summary>
    /// The same redirect for the room-prefab cache, which is keyed by the very same type-name id.
    /// Without this the prefab would be looked up under a key nothing ever loads.
    /// </summary>
    [HarmonyPatch(typeof(RoomAssetManager), "LoadRoom")]
    internal static class RoomPrefabIdPatch
    {
        private static void Prefix(ref string __0)
        {
            if (!Plugin.RoomRegistered) return;
            if (__0 == Plugin.AdoptedTypeName) __0 = Plugin.DonorTypeName;
        }
    }

    /// <summary>
    /// Puts the room into the build menu.
    ///
    /// Registering with ParameterDataMgr is not enough: FillAvailableBuildList builds the menu from
    /// UIRoomBuildList's own serialised m_roomInfo array, so the clone has to be appended there too.
    ///
    /// Two parallel arrays are indexed by the room type and must be able to hold our slot, or the
    /// menu would throw while sorting:
    ///   * CompareItems reads m_order[(int)type - 1];
    ///   * IsRoomAvailable reads m_roomAvailableConstruction[(int)type].
    /// Both are checked and grown rather than assumed. Our slot is unused by any real room, so
    /// writing to it cannot disturb another room's ordering or lock state.
    /// </summary>
    [HarmonyPatch(typeof(UIRoomBuildList), "FillAvailableBuildList")]
    internal static class BuildListInjectionPatch
    {
        private static void Prefix(UIRoomBuildList __instance)
        {
            if (!Plugin.RoomRegistered) return;

            try { Inject(__instance); }
            catch (Exception e)
            {
                // A broken build menu would be far worse than a missing room.
                Plugin.Log.LogError("Build-list injection failed; the room will not be listed, but " +
                                    "the menu is unchanged. Details: " + e);
            }
        }

        /// <summary>
        /// Gives the room a real locked state by borrowing another room's unlock objective.
        ///
        /// A locked entry is drawn by SetNotAvailable(Objective), which calls GetProgress() on it —
        /// and UIRoomBuildList resolves that objective from m_unlockRoomObjectives, a table our
        /// adopted room type is absent from. Passing null crashed the game the moment the entry
        /// scrolled into view. Registering a pairing that points at an existing room's objective
        /// makes the entry lock, show progress and unlock exactly like that room does.
        /// </summary>
        private static void BorrowUnlockObjective(UIRoomBuildList list, Traverse t,
                                                  ERoomBuildLockState[] avail, int typeIndex)
        {
            string like = Plugin.UnlockLikeRoom == null ? null : Plugin.UnlockLikeRoom.Value;
            if (string.IsNullOrEmpty(like))
            {
                avail[typeIndex] = ERoomBuildLockState.Unlocked;
                return;
            }

            ERoomType source;
            try { source = (ERoomType)Enum.Parse(typeof(ERoomType), like, true); }
            catch
            {
                Plugin.Log.LogWarning("UnlockLikeRoom '" + like + "' is not a room type; leaving the room unlocked.");
                avail[typeIndex] = ERoomBuildLockState.Unlocked;
                return;
            }

            PairRoomObjective[] pairs = t.Field("m_unlockRoomObjectives").GetValue<PairRoomObjective[]>();
            if (pairs == null)
            {
                avail[typeIndex] = ERoomBuildLockState.Unlocked;
                return;
            }

            bool alreadyPaired = false;
            ObjectiveID borrowed = default(ObjectiveID);
            bool foundSource = false;

            for (int i = 0; i < pairs.Length; i++)
            {
                if (pairs[i].RoomType == Plugin.AdoptedType) alreadyPaired = true;
                if (pairs[i].RoomType == source) { borrowed = pairs[i].Objective; foundSource = true; }
            }

            if (!foundSource)
            {
                Plugin.Log.LogWarning("No unlock objective found for " + source + "; leaving the room unlocked.");
                avail[typeIndex] = ERoomBuildLockState.Unlocked;
                return;
            }

            if (!alreadyPaired)
            {
                PairRoomObjective[] extended = new PairRoomObjective[pairs.Length + 1];
                Array.Copy(pairs, extended, pairs.Length);
                extended[pairs.Length].RoomType = Plugin.AdoptedType;
                extended[pairs.Length].Objective = borrowed;
                t.Field("m_unlockRoomObjectives").SetValue(extended);
            }

            // Mirror the source room's current state so the two unlock together.
            int sourceIndex = (int)source;
            avail[typeIndex] = (sourceIndex >= 0 && sourceIndex < avail.Length)
                ? avail[sourceIndex]
                : ERoomBuildLockState.Unlocked;

            if (Plugin.Verbose) Plugin.LogDetail("Unlock borrowed from " + source + "; state = " + avail[typeIndex] + ".");
        }

        private static void Inject(UIRoomBuildList list)
        {
            Traverse t = Traverse.Create(list);

            RoomInfo[] infos = t.Field("m_roomInfo").GetValue<RoomInfo[]>();
            if (infos == null) return;

            for (int i = 0; i < infos.Length; i++)
            {
                if (infos[i] != null && infos[i].m_eRoomType == Plugin.AdoptedType) return; // already injected
            }

            ParameterDataMgr mgr = ParameterDataMgr.Instance;
            if (mgr == null) return;

            RoomInfo clone = mgr.GetRoomInfoForType(Plugin.AdoptedType);
            if (clone == null) return;

            int typeIndex = (int)Plugin.AdoptedType;
            int orderIndex = typeIndex - 1;

            int[] order = t.Field("m_order").GetValue<int[]>();
            if (order == null) return;
            if (order.Length <= orderIndex)
            {
                int[] grown = new int[orderIndex + 1];
                Array.Copy(order, grown, order.Length);
                order = grown;
                t.Field("m_order").SetValue(order);
            }
            // Sit at the very end of the menu. CompareItems sorts on m_order[(int)type - 1], so
            // one past the highest existing value puts this room last, after Nuka-Cola.
            int highest = int.MinValue;
            for (int i = 0; i < order.Length; i++)
            {
                if (i != orderIndex && order[i] > highest) highest = order[i];
            }
            order[orderIndex] = (highest == int.MinValue) ? 0 : highest + 1;

            ERoomBuildLockState[] avail = t.Field("m_roomAvailableConstruction").GetValue<ERoomBuildLockState[]>();
            if (avail == null) return;
            if (avail.Length <= typeIndex)
            {
                ERoomBuildLockState[] grown = new ERoomBuildLockState[typeIndex + 1];
                Array.Copy(avail, grown, avail.Length);
                avail = grown;
                t.Field("m_roomAvailableConstruction").SetValue(avail);
            }
            BorrowUnlockObjective(list, t, avail, typeIndex);

            RoomInfo[] extended = new RoomInfo[infos.Length + 1];
            Array.Copy(infos, extended, infos.Length);
            extended[infos.Length] = clone;
            t.Field("m_roomInfo").SetValue(extended);

            // The price is verified here rather than trusted: something between registration and
            // the menu was zeroing it, and a free room is worse than a wrongly priced one.
            Plugin.VerifyPrice(clone);

            Plugin.Log.LogInfo("Injected '" + Plugin.RoomName.Value + "' into the build menu (" +
                               infos.Length + " -> " + extended.Length + " entries).");
        }
    }

    /// <summary>
    /// Turns the collected carrier resource into caps.
    ///
    /// CollectResources runs its haul through GetResourcesWithBonuses before adding it to the
    /// vault — both online and offline — so converting here catches every collection path without
    /// the room ever having to hold caps itself.
    /// </summary>
    [HarmonyPatch(typeof(ProductionRoom), "GetResourcesWithBonuses")]
    internal static class CollectConversionPatch
    {
        private static void Postfix(ProductionRoom __instance, ref GameResources __result)
        {
            if (!Plugin.RoomRegistered || __instance.RoomType != Plugin.AdoptedType) return;
            if (__result == null) return;

            float carried = __result[Plugin.CarrierResource];
            if (carried <= 0f) return;

            __result[Plugin.CarrierResource] = 0f;
            __result[EResource.Nuka] = __result[EResource.Nuka] + carried;
        }
    }

    /// <summary>
    /// Makes the "ready to collect" bubble advertise caps rather than the carrier resource.
    ///
    /// The work cycle runs on an ordinary resource so the game's UI paths stay happy, but that is an
    /// implementation detail the player should not see — the bubble was showing the donor's
    /// lightning bolt because energy is literally what the room storage holds.
    ///
    /// Safe only in combination with PositiveResourcesPatch below; on its own it crashes the game.
    /// </summary>
    [HarmonyPatch(typeof(Room), "SetTappingMessageResources")]
    internal static class TappingMessagePatch
    {
        private static void Prefix(Room __instance, ref GameResources value)
        {
            if (!Plugin.RoomRegistered || __instance.RoomType != Plugin.AdoptedType) return;
            if (value == null) return;

            float carried = value[Plugin.CarrierResource];
            if (carried <= 0f) return;

            value = new GameResources(EResource.Nuka, carried);
        }
    }

    /// <summary>
    /// Stops a caps-only resource set from resolving to nothing.
    ///
    /// GameResources.GetPositiveResources returns **null**, not an empty list, when nothing
    /// qualifies — and it deliberately skips caps unless asked for them, because in the base game
    /// caps are only ever a bonus on top of a room's real output. SetAsResourceIcon feeds that
    /// result straight into GetResourceData, which dereferences it with .Count. So a room whose
    /// storage holds caps alone crashed the game the moment it finished a cycle.
    ///
    /// Returning a real list here fixes it: caps do have an icon (Icon_nukacapsGreen), they simply
    /// never reached the lookup. Only the null case is touched, so every vanilla call is unaffected
    /// — no ordinary room ever holds caps alone.
    /// </summary>
    [HarmonyPatch(typeof(GameResources), "GetPositiveResources")]
    internal static class PositiveResourcesPatch
    {
        private static void Postfix(GameResources __instance, bool bIncludeNuka, ref List<EResource> __result)
        {
            if (!Plugin.RoomRegistered || bIncludeNuka) return;
            if (__result != null && __result.Count > 0) return;
            if (__instance == null || __instance[EResource.Nuka] <= 0f) return;

            __result = new List<EResource> { EResource.Nuka };
        }
    }

    /// <summary>
    /// Hides the Storage row from the upgrade window.
    ///
    /// Production rooms normally advertise how much they add to the vault's capacity for what they
    /// make. This room deliberately adds none — that bonus is refused when it is built — so the row
    /// showed a storage figure that does not exist.
    ///
    /// Hidden the same way the game hides a row a level says it does not use: deactivate it and
    /// report false, so nothing downstream tries to lay it out.
    /// </summary>
    [HarmonyPatch(typeof(RoomUpgradeWindow), "UpdateLabel")]
    internal static class HideStorageRowPatch
    {
        /// <summary>Row index of the storage entry in the upgrade table.</summary>
        private const int StorageRow = 1;

        private static bool Prefix(RoomUpgradeWindow __instance, int i, Room room, ref bool __result)
        {
            if (!Plugin.RoomRegistered || room == null || room.RoomType != Plugin.AdoptedType) return true;
            if (i != StorageRow) return true;

            try
            {
                UpgradeLabelInfo[] labels = Traverse.Create(__instance)
                                                    .Field("m_Labels").GetValue<UpgradeLabelInfo[]>();
                if (labels != null && i < labels.Length && labels[i] != null && labels[i].m_Parent != null)
                {
                    labels[i].m_Parent.SetActive(false);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not hide the storage row: " + e.Message);
                return true;
            }

            __result = false;
            return false;
        }
    }

    /// <summary>
    /// Corrects the production figure shown in the upgrade window.
    ///
    /// ProductionLevel.GetUpgradeRoomGUILabel derives the number from m_resourcesReserve — the
    /// level's own storage figures, which for this room are still the donor's. The upgrade panel
    /// therefore advertised the power plant's energy output.
    ///
    /// Only cells that are purely a number are replaced, so titles and any other text the method
    /// returns are left exactly as the game wrote them.
    /// </summary>
    [HarmonyPatch(typeof(ProductionLevel), "GetUpgradeRoomGUILabel")]
    internal static class UpgradeLabelPatch
    {
        /// <summary>Row index of the production entry in the upgrade table.</summary>
        private const int ProductionRow = 0;

        private static void Postfix(ProductionLevel __instance, int LabelIndex, EUpgradeLabelType type,
                                    Room room, RoomLevel nextLevel, ref string __result)
        {
            if (!Plugin.RoomRegistered || room == null || room.RoomType != Plugin.AdoptedType) return;

            // Only the production row. An earlier version rewrote every numeric cell in the table,
            // which also overwrote the storage row with the production figure.
            if (LabelIndex != ProductionRow) return;

            // Label the row with its unit. Without this the panel shows a bare number next to an
            // icon, and a player has no way to tell it means caps per hour rather than per cycle —
            // the game's own rooms show per-cycle figures there.
            if (type == EUpgradeLabelType.Title)
            {
                __result = "Caps / hour";
                return;
            }

            // CURRENT reads this level; UPGRADED must read the next one, or both columns show the
            // same number and the upgrade looks pointless.
            RoomLevel source = (type == EUpgradeLabelType.Value) ? nextLevel : (RoomLevel)__instance;
            if (source == null) return;

            float perHour = Plugin.BatchFor(room) / Plugin.HoursFor(room, source.LevelNumber);
            __result = Mathf.RoundToInt(perHour).ToString();
        }
    }

    /// <summary>
    /// Keeps the room out of the vault production statistics.
    ///
    /// StatsWindow sums GetProducedResources() across every Production room to report output per
    /// minute. This room's cycle runs on a carrier resource, so it was being counted as a power
    /// plant — the stats screen showed energy the vault never actually receives, since the amount
    /// is converted to caps at collection.
    ///
    /// A flag is used rather than adjusting the total afterwards: GetProducedResources cannot tell
    /// who is asking, and reversing an already-summed figure would have to guess at units.
    /// </summary>
    [HarmonyPatch]
    internal static class StatsExclusionPatch
    {
        /// <summary>True only while the stats screen is totalling room output.</summary>
        internal static bool Summing;

        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("StatsWindow+VaultInstantData:UpdateRoomProduction");
        }

        private static void Prefix() { Summing = true; }

        private static void Finalizer() { Summing = false; }
    }

    /// <summary>
    /// Registers the room the moment the game's data is ready.
    ///
    /// The update-loop poll alone is a race: a vault can start loading before it fires, and a save
    /// containing our room then has no prefab to resolve. Hooking the data manager's own awake
    /// guarantees the room exists before anything can ask for it.
    /// </summary>
    [HarmonyPatch(typeof(ParameterDataMgr), "OnAwake")]
    internal static class EarlyRegistrationPatch
    {
        private static void Postfix()
        {
            if (Plugin.RoomRegistered || Plugin.Enabled == null || !Plugin.Enabled.Value) return;

            try { Plugin.EnsureRoomRegistered(); }
            catch (Exception e) { Plugin.Log.LogError("Early room registration failed: " + e); }
        }
    }

    /// <summary>
    /// Restores the room's identity after it is built.
    ///
    /// This is the reason nothing else worked. AddRoom fetches the room object from
    /// PoolMgr.GetPool(roomType + "Room"), and our pool redirect points that at "GeothermalRoom" —
    /// so the game hands back a Geothermal room object carrying the *donor's* RoomInfo. The built
    /// room therefore reported RoomType == Geothermal, and every patch keyed on our type (caps
    /// production, name, tint) silently skipped it. It really was a power generator.
    ///
    /// Swapping RoomInfo back to our clone right after construction restores the identity. The
    /// clone's level controllers are copies of the donor's, so the room's levels and merge
    /// behaviour are unchanged — only the type, stat, icon and name differ.
    ///
    /// This also covers rooms restored from a save, which arrive through the same call.
    /// </summary>
    [HarmonyPatch(typeof(BaseConstructionMgr<ConstructionMgr>), "AddRoom",
        new Type[] { typeof(ERoomType), typeof(int), typeof(int), typeof(int), typeof(int),
                     typeof(List<Room>), typeof(bool), typeof(bool) })]
    internal static class RoomIdentityPatch
    {
        /// <summary>
        /// True only while AddRoom is constructing one of our rooms.
        ///
        /// During construction the room still wears the donor's RoomInfo, so nothing downstream can
        /// recognise it as ours — this flag is the only way to tell.
        /// </summary>
        internal static bool Constructing;

        private static void Prefix(ERoomType roomType)
        {
            if (Plugin.RoomRegistered && roomType == Plugin.AdoptedType) Constructing = true;
        }

        private static void Finalizer()
        {
            Constructing = false;
        }

        private static void Postfix(ERoomType roomType, Room __result)
        {
            if (!Plugin.RoomRegistered || roomType != Plugin.AdoptedType || __result == null) return;

            try
            {
                ParameterDataMgr mgr = ParameterDataMgr.Instance;
                if (mgr == null) return;

                RoomInfo clone = mgr.GetRoomInfoForType(Plugin.AdoptedType);
                if (clone == null) return;

                __result.m_RoomInfo = clone;

                // Room.ChangeCurrentRoomLevel caches the display name into m_RoomName, and it runs
                // during construction — before this point — so a freshly built room kept showing the
                // donor's "Power Generator" until the vault was reloaded. Refresh the cache here.
                Traverse.Create(__result).Field("m_RoomName").SetValue(Plugin.RoomName.Value);

                LevelRebinder.Rebind(__result, clone);

                // Tinting is queued from here, not from OnStart: the room's OnStart runs while it
                // is still wearing the donor's RoomInfo, so a type check there always failed.
                Plugin.QueueTint(__result);

                if (Plugin.Verbose) Plugin.LogDetail("Restored " + Plugin.RoomName.Value + " identity on a built room " +
                                 "(was " + Plugin.DonorTypeName + " from the shared pool).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not restore the room's identity: " + e.Message);
            }
        }
    }

    /// <summary>
    /// Points the room at our clone's level data.
    ///
    /// A room resolves its current RoomLevel during construction, while it is still wearing the
    /// donor's RoomInfo — so a room restored from a save kept the donor's level object and reported
    /// its upgrade cost. Freshly built rooms happened to land on ours, which is why the cost looked
    /// right until the game was restarted.
    ///
    /// The fields are set directly rather than through ChangeCurrentRoomLevel, which would also
    /// rebuild the room's visuals.
    /// </summary>
    internal static class LevelRebinder
    {
        internal static void Rebind(Room room, RoomInfo clone)
        {
            try
            {
                LevelController[] controllers = clone.m_LevelControllers;
                if (controllers == null || controllers.Length == 0) return;

                // Restore the costs first: they are correct at registration but zeroed by the time
                // a saved room is rebuilt.
                Plugin.EnsureLevelData(clone);

                int mergeIndex = Mathf.Clamp(room.MergeLevel - 1, 0, controllers.Length - 1);
                LevelController controller = controllers[mergeIndex];
                if (controller == null || controller.m_roomLevels == null) return;

                int levelIndex = Mathf.Clamp(room.CurrentLevelNumber - 1, 0, controller.m_roomLevels.Length - 1);
                RoomLevel level = controller.m_roomLevels[levelIndex];
                if (level == null) return;

                Traverse t = Traverse.Create(room);
                t.Field("m_currentRoomLevelController").SetValue(controller);
                t.Field("m_currentRoomLevel").SetValue(level);

                float cost = level.m_upgradeCost == null ? -1f : level.m_upgradeCost[EResource.Nuka];
                if (Plugin.Verbose) Plugin.LogDetail("Rebound level data: merge " + room.MergeLevel + ", level " +
                                 room.CurrentLevelNumber + ", upgrade = " + cost.ToString("0") + " caps.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not rebind level data: " + e.Message);
            }
        }

    }

    /// <summary>
    /// Stops the room handing out the donor's free build reward.
    ///
    /// Rooms grant free resources when construction finishes, read from
    /// RoomInfo.buildingResources — for a power plant that is energy, which is what those icons
    /// flying to the energy counter were.
    ///
    /// Clearing the clone's copy was not enough: this runs while the room still wears the donor's
    /// RoomInfo, so it read the donor's bonus regardless. Skipping the whole method is timing-proof,
    /// and it is checked two ways — by the construction flag, and by room type for any later call.
    /// </summary>
    [HarmonyPatch]
    internal static class BuildRewardPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method("Room+RoomBuilding:GetFinishBuildingResources");
        }

        private static bool Prefix(object __instance)
        {
            if (!Plugin.RoomRegistered) return true;

            try
            {
                if (RoomIdentityPatch.Constructing) return false;

                Room room = Traverse.Create(__instance).Field("m_room").GetValue<Room>();
                if (room != null && room.RoomType == Plugin.AdoptedType) return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Build-reward check failed, letting the game proceed: " + e.Message);
            }

            return true;
        }
    }

    /// <summary>
    /// Stops the room from donating the visual donor's storage bonus to the vault.
    ///
    /// Production rooms register a StorageModifier that raises the vault's capacity for what they
    /// make. OnChangeRoomLevel does this during construction, while the room still wears the donor's
    /// identity, so a Caps Foundry was handing over the reactor's ENERGY capacity — complete with
    /// the flying-icon animation.
    ///
    /// Withdrawing it afterwards fixed the number but not the animation, which had already played.
    /// Refusing the registration outright fixes both. The suppression lasts only for the moment
    /// AddRoom spends building one of our rooms, so no other room is affected.
    /// </summary>
    [HarmonyPatch(typeof(VaultStorage), "AddModifier")]
    internal static class StorageModifierPatch
    {
        private static bool Prefix()
        {
            return !RoomIdentityPatch.Constructing;
        }
    }

    /// <summary>
    /// Queues a newly built room for tinting.
    ///
    /// Hooked on ProductionRoom.OnStart, not Room.OnStart: ProductionRoom overrides that method,
    /// so a patch on the base never ran — which is why nothing was ever tinted.
    ///
    /// The colouring itself is deferred to Plugin's update loop, because the room's meshes are
    /// pulled from an object pool and are not attached yet at this point.
    /// </summary>
    [HarmonyPatch(typeof(ProductionRoom), "OnStart")]
    internal static class RoomTintOnStartPatch
    {
        private static void Postfix(ProductionRoom __instance)
        {
            if (!Plugin.RoomRegistered || __instance.RoomType != Plugin.AdoptedType) return;
            Plugin.QueueTint(__instance);
        }
    }

    /// <summary>Same, for rooms restored from a save rather than newly built.</summary>
    [HarmonyPatch(typeof(ProductionRoom), "OnRoomLoaded")]
    internal static class RoomTintOnLoadPatch
    {
        private static void Postfix(ProductionRoom __instance)
        {
            if (!Plugin.RoomRegistered || __instance.RoomType != Plugin.AdoptedType) return;
            Plugin.QueueTint(__instance);
        }
    }

    /// <summary>
    /// Supplies the text for the clone's own localisation keys.
    ///
    /// Every room label — the build menu, the room panel, the tooltip — resolves through
    /// ScriptLocalization.Get, so answering here covers all of them at once instead of chasing
    /// each UI class. Only our two keys are handled; every other term falls through untouched.
    /// </summary>
    [HarmonyPatch(typeof(I2.Loc.ScriptLocalization), "Get")]
    internal static class ScriptLocalizationPatch
    {
        private static bool Prefix(string Term, ref string __result)
        {
            if (Term == Plugin.NameLocId) { __result = Plugin.RoomName.Value; return false; }
            if (Term == Plugin.DescriptionLocId)
            {
                __result = "Presses bottle caps from scrap. Luck decides how fast the press runs.";
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Names a built room. The clone inherits the donor's localisation ids, and inventing new ids would
    /// only produce missing-key text, so the resolved name is replaced instead.
    /// </summary>
    [HarmonyPatch(typeof(Room), "GetLevelRoomName")]
    internal static class GetLevelRoomNamePatch
    {
        private static void Postfix(Room __instance, ref string __result)
        {
            if (!Plugin.RoomRegistered || __instance.RoomType != Plugin.AdoptedType) return;
            __result = Plugin.RoomName.Value;
        }
    }
}
