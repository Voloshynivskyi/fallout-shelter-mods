using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NukaColaQuantumProduction
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ovolo.falloutshelter.nukaquantum";
        public const string PluginName = "Nuka-Cola Quantum Production";
        public const string PluginVersion = "1.12.0";

        internal static ConfigEntry<float> HoursLevel1;
        internal static ConfigEntry<float> HoursLevel2;
        internal static ConfigEntry<float> HoursLevel3;
        internal static ConfigEntry<bool> SuppressCapsBonus;
        internal static ConfigEntry<string> QuantumIconOverride;

        // UnityEngine.Debug.Log does not reach BepInEx's LogOutput.log, so diagnostics go here.
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            // Stated as hours per bottle per room level rather than as multipliers: it is the
            // number you actually want to control, and it avoids awkward values (3 hours from a
            // 4-hour base would be a multiplier of 1.333...).
            HoursLevel1 = Config.Bind("Rate", "HoursLevel1", 4.0f,
                "Hours a LEVEL 1, one-segment Bottler takes per bottle of Quantum at full worker efficiency.");
            HoursLevel2 = Config.Bind("Rate", "HoursLevel2", 3.0f,
                "Hours a LEVEL 2, one-segment Bottler takes per bottle at full worker efficiency.");
            HoursLevel3 = Config.Bind("Rate", "HoursLevel3", 2.0f,
                "Hours a LEVEL 3, one-segment Bottler takes per bottle at full worker efficiency.");

            SuppressCapsBonus = Config.Bind(
                "Rate", "SuppressCapsBonus", false,
                "Suppress the Luck-based caps bonus that CollectResources awards on top of normal output, " +
                "so the Bottler yields Quantum only. Set to false to keep the vanilla caps bonus.");

            QuantumIconOverride = Config.Bind(
                "Icon", "QuantumIconOverride", "",
                "UI sprite name to use for the Bottler's icon. Leave empty for the built-in " +
                "Quantum icon (Icon_NukaQuantum).");

            ApplyPatches();
        }

        /// <summary>
        /// Applies each patch class separately instead of PatchAll().
        ///
        /// PatchAll aborts on the first failure — and it was called unguarded, so a single method
        /// renamed by a game update would throw out of Awake and take the whole mod down. Patching
        /// one class at a time means an update costs only the features that actually broke, and the
        /// log names them.
        /// </summary>
        private void ApplyPatches()
        {
            Harmony harmony = new Harmony(PluginGuid);
            int applied = 0;
            List<string> failures = new List<string>();

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
                Log.LogInfo(PluginName + " " + PluginVersion + " ready (" + applied + " patches). " +
                            "Hours per bottle at size 1: L1=" + HoursLevel1.Value +
                            " L2=" + HoursLevel2.Value + " L3=" + HoursLevel3.Value + ".");
                return;
            }

            Log.LogWarning("Applied " + applied + " patches; " + failures.Count + " failed (" +
                           string.Join(", ", failures.ToArray()) + "). The Bottler may misbehave — " +
                           "this usually means the game updated.");
        }

        /// <summary>
        /// Structural output per hour, ignoring worker efficiency. Used both as the
        /// production rate basis and as the room's Quantum storage capacity, so the
        /// capacity stays stable no matter how the room is staffed.
        /// </summary>
        internal static float PerHourUnstaffed(ProductionRoom room)
        {
            float hours;
            switch (room.CurrentLevelNumber)
            {
                case 2: hours = HoursLevel2.Value; break;
                case 3: hours = HoursLevel3.Value; break;
                default: hours = HoursLevel1.Value; break;
            }
            if (hours < 0.01f) hours = 0.01f;

            // MergeLevel is the room's width in segments (1..3); a wider room works proportionally
            // faster, exactly as vanilla production rooms do.
            int size = room.MergeLevel;
            if (size < 1) size = 1;

            return (1f / hours) * size;
        }

        /// <summary>
        /// The game's own UI sprite for Nuka-Cola Quantum.
        ///
        /// Hardcoded on purpose. Asking the game via ResourceParameters does NOT work for Quantum:
        /// icons are looked up by bit flag, its resource table has no entry for NukaColaQuantum,
        /// and a miss does not throw — it silently falls back to another entry, returning
        /// "Icon_WeaponGreen" (the crafted-weapon sprite). Lunchbox, MrHandy and PetCarrier are
        /// missing outright and throw KeyNotFoundException. This name was read out of the game's
        /// asset files; QuantumIconOverride can replace it without a rebuild.
        /// </summary>
        private const string DefaultQuantumIcon = "Icon_NukaQuantum";

        private static string _quantumIcon;
        private static bool _quantumIconResolved;

        /// <summary>
        /// The UI sprite name to draw for Quantum, resolved once. Uses the config override when
        /// set, otherwise <see cref="DefaultQuantumIcon"/>.
        /// </summary>
        internal static string QuantumIconName()
        {
            if (_quantumIconResolved) return _quantumIcon;
            _quantumIconResolved = true;

            if (!string.IsNullOrEmpty(QuantumIconOverride.Value))
            {
                _quantumIcon = QuantumIconOverride.Value;
                return _quantumIcon;
            }

            _quantumIcon = DefaultQuantumIcon;

            return _quantumIcon;
        }

        /// <summary>
        /// How much Quantum the room holds before a cycle completes. Kept a whole number because
        /// Storage.SetMaxResources runs FloorToInt on whatever it is handed.
        /// </summary>
        /// <summary>Bottles per hour for a given room level and width, at full efficiency.</summary>
        internal static float RatePerHour(int levelNumber, int size)
        {
            float hours;
            switch (levelNumber)
            {
                case 2: hours = HoursLevel2.Value; break;
                case 3: hours = HoursLevel3.Value; break;
                default: hours = HoursLevel1.Value; break;
            }
            if (hours < 0.01f) hours = 0.01f;
            if (size < 1) size = 1;

            return (1f / hours) * size;
        }

        internal static float QuantumCapacity(ProductionRoom room)
        {
            float capacity = Mathf.Round(PerHourUnstaffed(room));
            if (capacity < 1f) capacity = 1f;
            return capacity;
        }

        /// <summary>
        /// Gives the room its own storage cap holding Quantum only.
        ///
        /// Two traps this works around:
        ///  - Storage.SetMaxResources assigns the reference it is handed, and
        ///    ProductionRoom.OnChangeRoomLevel hands it the ProductionLevel's *shared*
        ///    m_resourcesReserve asset. Writing into MaxResources in place would therefore
        ///    corrupt that shared asset for every room using the level, so a fresh
        ///    GameResources is assigned instead.
        ///  - ProductionRoom.IsWorkCompleted() is RoomStorage.IsFilled(), which requires every
        ///    resource to reach its cap. Food/water caps come from the level reserve and are
        ///    non-zero, so once their production is removed the room can never finish a cycle
        ///    (no timer shown, output only via rushing). Capping everything but Quantum at zero
        ///    fixes that.
        /// </summary>
        internal static void EnsureQuantumOnlyCapacity(ProductionRoom room)
        {
            Storage storage = room.RoomStorage;
            if (storage == null) return;

            float capacity = QuantumCapacity(room);

            GameResources max = storage.MaxResources;
            if (max != null
                && max[EResource.NukaColaQuantum] == capacity
                && max[EResource.Food] == 0f
                && max[EResource.Water] == 0f
                && max[EResource.Energy] == 0f)
            {
                return;
            }

            storage.SetMaxResources(new GameResources(EResource.NukaColaQuantum, capacity));
        }

    }

    /// <summary>
    /// Replaces the Nuka-Cola Bottler's normal food/water output with Nuka-Cola Quantum.
    ///
    /// GetProducedResources() returns a PER-SECOND rate: the game builds it as
    /// (level's m_resourcesProduced * workerEfficiency) / 60, then accumulates it into
    /// the room's own Storage until full, at which point the player collects.
    /// </summary>
    [HarmonyPatch(typeof(ProductionRoom), "GetProducedResources")]
    internal static class GetProducedResourcesPatch
    {
        private static void Postfix(ProductionRoom __instance, ref GameResourcesBuilder __result)
        {
            if (__instance.RoomType != ERoomType.NukaCola) return;

            float perHourUnstaffed = Plugin.PerHourUnstaffed(__instance);

            Plugin.EnsureQuantumOnlyCapacity(__instance);

            float efficiency = __instance.GetWorkingEfficiency(__instance.GetRoomSpecialStat());
            float perSecond = (perHourUnstaffed * efficiency) / 3600f;

            __result.Clear();
            __result.Add(new GameResources(EResource.NukaColaQuantum, perSecond));
        }
    }

    /// <summary>
    /// OnChangeRoomLevel calls RoomStorage.SetMaxResources(level.m_resourcesReserve), which
    /// puts the food/water caps back. Re-apply the Quantum-only cap right after it so the room
    /// can still complete a cycle after loading a save, upgrading, or merging.
    /// </summary>
    [HarmonyPatch(typeof(ProductionRoom), "OnChangeRoomLevel")]
    internal static class OnChangeRoomLevelPatch
    {
        private static void Postfix(ProductionRoom __instance)
        {
            if (__instance.RoomType != ERoomType.NukaCola) return;
            Plugin.EnsureQuantumOnlyCapacity(__instance);
        }
    }

    /// <summary>
    /// Fixes the amount shown on the room's production readouts.
    ///
    /// The production timer label, the collect button and the room management panel all display
    /// GetCurrentReserve().MaxResourceValue(true), and GetCurrentReserve() returns the level's
    /// m_resourcesReserve — which still carries the vanilla food and water figures.
    ///
    /// All three call sites are UI-only, so overriding this does not affect production itself.
    /// </summary>
    [HarmonyPatch(typeof(ProductionRoom), "GetCurrentReserve")]
    internal static class GetCurrentReservePatch
    {
        // Reused rather than allocated per call: these readouts refresh continuously, and every
        // caller only reads the value straight back out.
        private static GameResources _reserve;

        private static void Postfix(ProductionRoom __instance, ref GameResources __result)
        {
            if (__instance.RoomType != ERoomType.NukaCola) return;

            if (_reserve == null) _reserve = new GameResources();
            _reserve[EResource.NukaColaQuantum] = Plugin.QuantumCapacity(__instance);
            __result = _reserve;
        }
    }

    /// <summary>
    /// Swaps the Bottler's room icon for the Quantum one.
    ///
    /// The production timer, collect button and room management panel all take their sprite from
    /// RoomInfo.MainIconPlain, which for the Bottler is the combined food + water icon baked into
    /// the room asset — it does not follow what the room produces. RoomInfo is a per-room-type
    /// asset carrying m_eRoomType, so this stays scoped to the Bottler.
    /// </summary>
    [HarmonyPatch(typeof(RoomInfo), "get_MainIconPlain")]
    internal static class MainIconPlainPatch
    {
        private static void Postfix(RoomInfo __instance, ref string __result)
        {
            if (__instance.m_eRoomType != ERoomType.NukaCola) return;

            string icon = Plugin.QuantumIconName();
            if (!string.IsNullOrEmpty(icon)) __result = icon;
        }
    }

    /// <summary>
    /// Fixes the icon on the "ready to collect" tapping message, which is a separate path from the
    /// room icon: UIRoomTappingMessage.SetAsResourceIcon feeds the collected resources into
    /// GetResourceData(List&lt;EResource&gt;) and uses the returned ResourceData's sprite fields.
    ///
    /// That lookup ORs the resources into a bit flag and indexes a dictionary which has no Quantum
    /// entry, so it fell through to the crafted-weapon entry — the pistol shown while collecting.
    ///
    /// m_tappingIconType is deliberately left as None: SetAsGenericIcon switches on that value and
    /// for several types overwrites the sprite it was just given, whereas None skips it entirely
    /// and leaves the Quantum sprite in place.
    /// </summary>
    [HarmonyPatch(typeof(ResourceParameters), "GetResourceData", new System.Type[] { typeof(List<EResource>) })]
    internal static class GetResourceDataListPatch
    {
        private static ResourceData _quantumData;

        private static void Postfix(List<EResource> __0, ref ResourceData __result)
        {
            if (__0 == null || __0.Count != 1 || __0[0] != EResource.NukaColaQuantum) return;

            if (_quantumData == null)
            {
                _quantumData = new ResourceData();
                _quantumData.m_resource = new List<EResource> { EResource.NukaColaQuantum };
                _quantumData.m_GUIicon = Plugin.QuantumIconName();
                _quantumData.m_GUIiconAux = "";
                _quantumData.m_tappingIconType = ETappingMessageIconType.None;

                // Keep whatever collect sound the fallback entry carried, so audio still plays.
                if (__result != null) _quantumData.m_audioPrefab = __result.m_audioPrefab;
            }

            __result = _quantumData;
        }
    }

    /// <summary>
    /// Same fallback problem, single-resource overload — used by the rush window among others.
    /// </summary>
    [HarmonyPatch(typeof(ResourceParameters), "GetIconName", new System.Type[] { typeof(EResource) })]
    internal static class GetIconNamePatch
    {
        private static void Postfix(EResource __0, ref string __result)
        {
            if (__0 != EResource.NukaColaQuantum) return;

            string icon = Plugin.QuantumIconName();
            if (!string.IsNullOrEmpty(icon)) __result = icon;
        }
    }

    /// <summary>
    /// Hides the Storage row from the upgrade window.
    ///
    /// Production rooms normally advertise how much they add to the vault's capacity for what they
    /// make. This mod withdraws the Bottler's food-and-water bonus, since it no longer makes either
    /// — so the row showed a storage figure that does not exist.
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
            if (room == null || room.RoomType != ERoomType.NukaCola) return true;
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
    /// ProductionLevel.GetUpgradeRoomGUILabel derives the number from m_resourcesReserve, which for
    /// the Bottler is still its vanilla food-and-water figure — so the upgrade panel advertised food
    /// and water for a room that makes Quantum.
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
            if (room == null || room.RoomType != ERoomType.NukaCola) return;

            // Only the production row. An earlier version rewrote every numeric cell in the table,
            // which also overwrote the storage row with the production figure.
            if (LabelIndex != ProductionRow) return;

            // Label the row with its unit. Without this the panel shows a bare number next to an
            // icon, and a player has no way to tell it means bottles per day rather than per cycle —
            // the game's own rooms show per-cycle figures there.
            if (type == EUpgradeLabelType.Title)
            {
                __result = "Quantum / day";
                return;
            }

            // CURRENT reads this level; UPGRADED must read the next one, or both columns show the
            // same number and the upgrade looks pointless.
            RoomLevel source = (type == EUpgradeLabelType.Value) ? nextLevel : __instance;
            if (source == null) return;

            float perDay = Plugin.RatePerHour(source.LevelNumber, room.MergeLevel) * 24f;
            __result = Mathf.RoundToInt(perDay).ToString();
        }
    }

    /// <summary>
    /// Stops the Bottler handing out free food and water when it is built.
    ///
    /// Rooms grant free resources on completing construction, read from RoomInfo.buildingResources.
    /// That made sense while the Bottler produced food and water; now that it makes Quantum, handing
    /// out a food-and-water starter batch — icons flying to those counters and all — does not.
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
            try
            {
                Room room = Traverse.Create(__instance).Field("m_room").GetValue<Room>();
                if (room != null && room.RoomType == ERoomType.NukaCola) return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Build-reward check failed, letting the game proceed: " + e.Message);
            }

            return true;
        }
    }

    /// <summary>
    /// Stops the Bottler raising the vault's food and water capacity.
    ///
    /// Production rooms register a StorageModifier for what they make, and the Bottler's is still
    /// the vanilla food-and-water one. A room producing Quantum should not be enlarging those
    /// stores, so the modifier is withdrawn right after the game installs it.
    /// </summary>
    [HarmonyPatch(typeof(ProductionRoom), "OnChangeRoomLevel")]
    internal static class StorageBonusPatch
    {
        private static void Postfix(ProductionRoom __instance)
        {
            if (__instance.RoomType != ERoomType.NukaCola) return;

            try
            {
                Traverse field = Traverse.Create(__instance).Field("m_vaultStorageModifier");
                StorageModifier modifier = field.GetValue<StorageModifier>();
                if (modifier == null) return;

                Vault vault = Vault.Instance;
                if (vault == null || vault.Storage == null) return;

                vault.Storage.RemoveModifier(modifier);
                field.SetValue(null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not remove the vault storage bonus: " + e.Message);
            }
        }
    }

    /// <summary>
    /// CollectResources awards a Luck-based caps bonus on a path completely separate from
    /// GetProducedResources: it calls GetLuckNukaProduced(), multiplies by NoRushNukaMultiplier,
    /// and adds the result straight into resource index 0 (EResource.Nuka). Zeroing it here makes
    /// the Bottler yield Quantum and nothing else.
    /// </summary>
    [HarmonyPatch(typeof(ProductionRoom), "GetLuckNukaProduced")]
    internal static class GetLuckNukaProducedPatch
    {
        private static void Postfix(ProductionRoom __instance, ref float __result)
        {
            if (!Plugin.SuppressCapsBonus.Value) return;
            if (__instance.RoomType != ERoomType.NukaCola) return;
            __result = 0f;
        }
    }
}
