using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace RoomTextureDump
{
    /// <summary>
    /// Development tool. Writes the textures rooms are drawn with to disk as PNG, plus a manifest
    /// saying which renderer, material, shader and shader property each file came from.
    ///
    /// The point is an exact repaint template: the dimensions and UV layout the game actually uses.
    /// Guessing at either produces artwork that lands in the wrong place on the mesh.
    ///
    /// Two modes, because they have different blind spots:
    ///
    ///   Scene  — walks the rooms standing in the vault. Accurate about what a real room is made
    ///            of, but only covers the rooms, levels and widths that vault happens to contain.
    ///
    ///   Loaded — walks every renderer loaded in memory, including inactive ones and ones sitting
    ///            in object pools rather than in the scene. Room sections are pooled per type and
    ///            merge width, so this reaches levels and widths that are not built anywhere.
    ///
    /// Not shipped with either mod. It reads the game and writes files; it changes nothing.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ovolo.falloutshelter.roomtexturedump";
        public const string PluginName = "Room Texture Dump";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Log;

        private static ConfigEntry<string> RoomTypes;
        private static ConfigEntry<string> OutputFolder;
        private static ConfigEntry<bool> DumpEveryLevel;
        private static ConfigEntry<bool> DumpSceneRooms;
        private static ConfigEntry<bool> DumpLoadedAssets;
        private static ConfigEntry<string> LoadedNameFilter;
        private static ConfigEntry<int> MaxTextures;

        private readonly HashSet<string> _done = new HashSet<string>();
        private readonly List<ERoomType> _wanted = new List<ERoomType>();
        private bool _loadedDone;
        private int _written;
        private int _frames;
        private const int PollInterval = 60;   // frames, about once a second

        private void Awake()
        {
            Log = Logger;

            RoomTypes = Config.Bind("Scene", "RoomTypes", "Energy2",
                "Comma-separated ERoomType names to dump from the vault, for example " +
                "'Energy2, Geothermal, NukaCola'. Energy2 is the Nuclear Reactor.");

            DumpSceneRooms = Config.Bind("Scene", "DumpSceneRooms", true,
                "Dump the rooms actually standing in the vault.");

            DumpEveryLevel = Config.Bind("Scene", "DumpEveryLevel", true,
                "Dump each level and merge width separately. Room levels use different meshes, so a " +
                "template taken from one level does not necessarily fit another.");

            DumpLoadedAssets = Config.Bind("Loaded", "DumpLoadedAssets", true,
                "Also dump every renderer loaded in memory whose name matches LoadedNameFilter, " +
                "including inactive ones and ones held in object pools. This is what reaches room " +
                "levels and merge widths that are not built in your vault.");

            LoadedNameFilter = Config.Bind("Loaded", "LoadedNameFilter", "room",
                "Case-insensitive substring a renderer's name must contain to be dumped in Loaded " +
                "mode. The stock room meshes are named like 'MSH_Fusion_reactor_room_L3_R3_anim_a', " +
                "so 'room' catches them without dragging in dwellers and UI. Empty means everything, " +
                "which is a lot.");

            MaxTextures = Config.Bind("Loaded", "MaxTextures", 300,
                "Stop after this many distinct textures have been written. A safety net: an " +
                "unfiltered run can otherwise write a very large number of files.");

            OutputFolder = Config.Bind("Output", "OutputFolder", "",
                "Where to write the files. Empty means %LocalAppData%/FalloutShelter/RoomTextures.");

            ParseWantedTypes();
            Log.LogInfo(PluginName + " " + PluginVersion + " ready. Scene types: " + RoomTypes.Value +
                        "; loaded filter: '" + LoadedNameFilter.Value + "'.");
        }

        private void ParseWantedTypes()
        {
            string[] parts = RoomTypes.Value.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i].Trim();
                if (name.Length == 0) continue;
                try { _wanted.Add((ERoomType)Enum.Parse(typeof(ERoomType), name, true)); }
                catch { Log.LogWarning("'" + name + "' is not a room type; ignoring it."); }
            }
        }

        private void Update()
        {
            if (++_frames < PollInterval) return;
            _frames = 0;

            Room[] rooms;
            try
            {
                rooms = UnityEngine.Object.FindObjectsByType<Room>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not enumerate rooms: " + e.Message);
                enabled = false;
                return;
            }

            // A room in the scene means we are inside a vault and the room assets are loaded, which
            // is the only moment the Loaded pass is worth running.
            if (rooms.Length == 0) return;

            if (DumpSceneRooms.Value) DumpSceneRoomsPass(rooms);

            if (DumpLoadedAssets.Value && !_loadedDone)
            {
                _loadedDone = true;
                DumpLoadedPass();
            }
        }

        private void DumpSceneRoomsPass(Room[] rooms)
        {
            for (int i = 0; i < rooms.Length; i++)
            {
                Room room = rooms[i];
                if (room == null || !_wanted.Contains(room.RoomType)) continue;

                string key = DumpEveryLevel.Value
                    ? room.RoomType + "_L" + room.CurrentLevelNumber + "_W" + room.MergeLevel
                    : room.RoomType.ToString();

                if (!_done.Add(key)) continue;

                List<Renderer> renderers = CollectRenderers(room);
                if (renderers.Count == 0)
                {
                    // Sections stream in from a pool after the room starts; try again next poll.
                    _done.Remove(key);
                    continue;
                }

                StringBuilder header = new StringBuilder();
                header.AppendLine("Source:      room standing in the vault");
                header.AppendLine("Room:        " + room.RoomType);
                header.AppendLine("Level:       " + room.CurrentLevelNumber);
                header.AppendLine("Merge width: " + room.MergeLevel);

                Write(Path.Combine("scene", key), renderers, header);
            }
        }

        /// <summary>
        /// Walks everything loaded rather than everything placed.
        ///
        /// Resources.FindObjectsOfTypeAll returns objects that are inactive and objects that belong
        /// to no scene at all — which is exactly where pooled room sections live. That is how this
        /// reaches a level-1 one-wide reactor when the vault only contains a level-3 three-wide one.
        /// </summary>
        private void DumpLoadedPass()
        {
            try
            {
                Renderer[] all = Resources.FindObjectsOfTypeAll<Renderer>();
                string filter = LoadedNameFilter.Value;

                List<Renderer> matched = new List<Renderer>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null) continue;
                    if (filter.Length > 0 &&
                        all[i].name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    matched.Add(all[i]);
                }

                Log.LogInfo("Loaded pass: " + all.Length + " renderer(s) in memory, " +
                            matched.Count + " matching '" + filter + "'.");
                if (matched.Count == 0) return;

                StringBuilder header = new StringBuilder();
                header.AppendLine("Source:      every renderer loaded in memory, including pooled and inactive");
                header.AppendLine("Filter:      name contains '" + filter + "'");
                header.AppendLine("Matched:     " + matched.Count + " of " + all.Length + " renderer(s)");

                Write("loaded", matched, header);
            }
            catch (Exception e)
            {
                Log.LogWarning("Loaded pass failed: " + e.Message);
            }
        }

        private void Write(string subfolder, List<Renderer> renderers, StringBuilder header)
        {
            try
            {
                string dir = Path.Combine(ResolveOutputFolder(), subfolder);
                Directory.CreateDirectory(dir);

                StringBuilder manifest = header;
                manifest.AppendLine("Renderers:   " + renderers.Count);
                manifest.AppendLine();

                int wroteHere = 0;
                bool capped = false;
                HashSet<string> seenTextures = new HashSet<string>();
                HashSet<string> seenMaterials = new HashSet<string>();

                for (int r = 0; r < renderers.Count && !capped; r++)
                {
                    Material[] mats = renderers[r].sharedMaterials;
                    for (int m = 0; m < mats.Length && !capped; m++)
                    {
                        if (mats[m] == null || mats[m].shader == null) continue;
                        Shader sh = mats[m].shader;

                        // Many renderers share one material; describe each material once, but keep
                        // listing the renderers so the mesh-to-material mapping stays visible.
                        string matKey = mats[m].GetInstanceID().ToString();
                        bool firstTime = seenMaterials.Add(matKey);

                        manifest.AppendLine("renderer " + renderers[r].name);
                        manifest.AppendLine("    material " + mats[m].name + (firstTime ? "" : "   (already described)"));
                        if (!firstTime) { manifest.AppendLine(); continue; }
                        manifest.AppendLine("    shader   " + sh.name);

                        int count = sh.GetPropertyCount();
                        for (int p = 0; p < count; p++)
                        {
                            if (sh.GetPropertyType(p) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;

                            string prop = sh.GetPropertyName(p);
                            Texture tex = mats[m].GetTexture(prop);
                            if (tex == null) continue;

                            string file = Safe(tex.name) + ".png";
                            manifest.AppendLine("        " + prop.PadRight(24) + " -> " + file +
                                                "  (" + tex.width + "x" + tex.height + ", " +
                                                tex.GetType().Name + ")");

                            if (!seenTextures.Add(file)) continue;   // shared atlas, write it once

                            if (_written >= MaxTextures.Value)
                            {
                                manifest.AppendLine();
                                manifest.AppendLine("STOPPED: MaxTextures (" + MaxTextures.Value + ") reached.");
                                Log.LogWarning("Stopped at MaxTextures (" + MaxTextures.Value +
                                               "). Raise it or narrow LoadedNameFilter.");
                                capped = true;
                                break;
                            }

                            if (WritePng(tex, Path.Combine(dir, file))) { wroteHere++; _written++; }
                        }
                        manifest.AppendLine();
                    }
                }

                File.WriteAllText(Path.Combine(dir, "manifest.txt"), manifest.ToString());
                Log.LogInfo("Wrote " + wroteHere + " texture(s) to " + dir);
            }
            catch (Exception e)
            {
                Log.LogWarning("Writing " + subfolder + " failed: " + e.Message);
            }
        }

        /// <summary>
        /// The room's meshes are NOT children of the Room object — they live in its RoomSections,
        /// which is why searching the room's own hierarchy alone finds almost nothing.
        /// </summary>
        private static List<Renderer> CollectRenderers(Room room)
        {
            List<Renderer> found = new List<Renderer>();
            found.AddRange(room.GetComponentsInChildren<Renderer>(true));

            List<RoomSectionBase> sections = room.RoomSections;
            if (sections != null)
            {
                for (int i = 0; i < sections.Count; i++)
                {
                    if (sections[i] != null) found.AddRange(sections[i].GetComponentsInChildren<Renderer>(true));
                }
            }
            return found;
        }

        /// <summary>
        /// Textures shipped in asset bundles are almost always non-readable, so ReadPixels on them
        /// fails outright. Blitting through a temporary RenderTexture is the only way to reach the
        /// pixels without the original import settings.
        /// </summary>
        private static bool WritePng(Texture source, string path)
        {
            RenderTexture rt = null;
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                rt = RenderTexture.GetTemporary(source.width, source.height, 0,
                                                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;

                readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                readable.Apply();

                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(readable));
                return true;
            }
            catch (Exception e)
            {
                Log.LogWarning("  could not write " + Path.GetFileName(path) + ": " + e.Message);
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }
        }

        private static string ResolveOutputFolder()
        {
            if (!string.IsNullOrEmpty(OutputFolder.Value)) return OutputFolder.Value;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FalloutShelter", "RoomTextures");
        }

        private static string Safe(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
