using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace SaveBackup
{
    /// <summary>
    /// Development tool. Copies every vault save into a timestamped folder when the game starts,
    /// keeping the most recent few sets.
    ///
    /// This lives in tools/ and is deliberately NOT part of either mod. A released mod that
    /// duplicates the player's save folder on every launch is doing something the game never asked
    /// it to, and Caps Foundry 1.3.0 dropped that behaviour for exactly that reason. But while a
    /// mod is being changed — particularly anything touching rooms, pools or sections, which is
    /// what has crashed saves before — a copy taken before the game touches anything is the
    /// difference between an annoyance and lost progress.
    ///
    /// Install it while working. Take it out before shipping. It is never packaged into a release
    /// archive: the mods' build scripts only ever stage their own DLL.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ovolo.falloutshelter.savebackup";
        public const string PluginName = "Save Backup (dev)";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;

        private static ConfigEntry<int> KeepSets;
        private static ConfigEntry<string> BackupFolder;

        private void Awake()
        {
            Log = Logger;

            KeepSets = Config.Bind("Backup", "KeepSets", 15,
                "How many timestamped backup sets to keep. The oldest are deleted beyond this.");

            BackupFolder = Config.Bind("Backup", "BackupFolder", "",
                "Where to write backups. Empty means %LocalAppData%/FalloutShelter/ModBackups.");

            Backup();
        }

        private void Backup()
        {
            try
            {
                string saveDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FalloutShelter");
                if (!Directory.Exists(saveDir))
                {
                    Log.LogWarning("No save folder at " + saveDir + "; nothing backed up.");
                    return;
                }

                string[] saves = Directory.GetFiles(saveDir, "Vault*.sav");
                if (saves.Length == 0)
                {
                    Log.LogWarning("No Vault*.sav files in " + saveDir + "; nothing backed up.");
                    return;
                }

                string root = string.IsNullOrEmpty(BackupFolder.Value)
                    ? Path.Combine(saveDir, "ModBackups")
                    : BackupFolder.Value;
                Directory.CreateDirectory(root);

                string dest = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(dest);

                for (int i = 0; i < saves.Length; i++)
                {
                    File.Copy(saves[i], Path.Combine(dest, Path.GetFileName(saves[i])), true);
                }

                Log.LogInfo("Backed up " + saves.Length + " save(s) to " + dest);
                Prune(root);
            }
            catch (Exception e)
            {
                // A failed backup must never stop the game loading.
                Log.LogWarning("Save backup failed: " + e.Message);
            }
        }

        private static void Prune(string root)
        {
            try
            {
                string[] sets = Directory.GetDirectories(root);
                Array.Sort(sets, StringComparer.Ordinal);   // timestamped names sort chronologically

                int keep = KeepSets.Value < 1 ? 1 : KeepSets.Value;
                for (int i = 0; i < sets.Length - keep; i++)
                {
                    try { Directory.Delete(sets[i], true); } catch { }
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not prune old backups: " + e.Message);
            }
        }
    }
}
