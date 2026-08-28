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
    /// Development tool. Writes every texture a room draws with to disk as PNG, plus a manifest
    /// saying which renderer, material, shader and shader property each file came from.
    ///
    /// The point is an exact repaint template: the dimensions and UV layout the game actually uses.
    /// Guessing at either produces artwork that lands in the wrong place on the mesh.
    ///
    /// Not shipped with either mod. It reads the game and writes files; it changes nothing.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ovolo.falloutshelter.roomtexturedump";
        public const string PluginName = "Room Texture Dump";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;

        private static ConfigEntry<string> RoomTypes;
        private static ConfigEntry<string> OutputFolder;
        private static ConfigEntry<bool> DumpEveryLevel;

        private readonly HashSet<string> _done = new HashSet<string>();
        private readonly List<ERoomType> _wanted = new List<ERoomType>();
        private int _frames;
        private const int PollInterval = 60;   // frames, about once a second

        private void Awake()
        {
            Log = Logger;

            RoomTypes = Config.Bind("Dump", "RoomTypes", "Energy2",
                "Comma-separated ERoomType names to dump, for example 'Energy2, Geothermal, NukaCola'. " +
                "Energy2 is the Nuclear Reactor.");

            DumpEveryLevel = Config.Bind("Dump", "DumpEveryLevel", true,
                "Dump each level and merge width separately. Room levels use different meshes, so a " +
                "template taken from one level does not necessarily fit another.");

            OutputFolder = Config.Bind("Dump", "OutputFolder", "",
                "Where to write the files. Empty means %LocalAppData%/FalloutShelter/RoomTextures.");

            ParseWantedTypes();
            Log.LogInfo(PluginName + " " + PluginVersion + " ready. Watching for: " + RoomTypes.Value);
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
            if (_wanted.Count == 0) Log.LogWarning("No valid room types configured; nothing will be dumped.");
        }

        private void Update()
        {
            if (_wanted.Count == 0) return;
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

            for (int i = 0; i < rooms.Length; i++)
            {
                Room room = rooms[i];
                if (room == null || !_wanted.Contains(room.RoomType)) continue;

                string key = DumpEveryLevel.Value
                    ? room.RoomType + "_L" + room.CurrentLevelNumber + "_W" + room.MergeLevel
                    : room.RoomType.ToString();

                if (!_done.Add(key)) continue;
                Dump(room, key);
            }
        }

        private void Dump(Room room, string key)
        {
            try
            {
                List<Renderer> renderers = CollectRenderers(room);
                if (renderers.Count == 0)
                {
                    // Sections stream in from a pool after the room starts; try again next poll.
                    _done.Remove(key);
                    return;
                }

                string dir = Path.Combine(ResolveOutputFolder(), key);
                Directory.CreateDirectory(dir);

                StringBuilder manifest = new StringBuilder();
                manifest.AppendLine("Room:        " + room.RoomType);
                manifest.AppendLine("Level:       " + room.CurrentLevelNumber);
                manifest.AppendLine("Merge width: " + room.MergeLevel);
                manifest.AppendLine("Renderers:   " + renderers.Count);
                manifest.AppendLine();

                int written = 0;
                HashSet<string> seenTextures = new HashSet<string>();

                for (int r = 0; r < renderers.Count; r++)
                {
                    Material[] mats = renderers[r].sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] == null || mats[m].shader == null) continue;
                        Shader sh = mats[m].shader;

                        manifest.AppendLine("renderer " + renderers[r].name);
                        manifest.AppendLine("    material " + mats[m].name);
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

                            // The same atlas is shared by many renderers; write it once.
                            if (!seenTextures.Add(file)) continue;
                            if (WritePng(tex, Path.Combine(dir, file))) written++;
                        }
                        manifest.AppendLine();
                    }
                }

                File.WriteAllText(Path.Combine(dir, "manifest.txt"), manifest.ToString());
                Log.LogInfo("Dumped " + written + " texture(s) for " + key + " to " + dir);
            }
            catch (Exception e)
            {
                Log.LogWarning("Dump of " + key + " failed: " + e.Message);
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
