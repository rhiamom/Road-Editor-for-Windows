/***************************************************************************
 *   Road Editor for Windows                                               *
 *   © 2026 GramzeSweatshop (rhiamom@mac.com). Written with Claude         *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                         *
 ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LotExpander
{
    /// <summary>
    /// Minimal per-user settings for Road Editor. Lives at
    /// %APPDATA%\RoadEditor\config.json. Currently holds just the chosen
    /// Sims 2 install so a player with multiple editions doesn't get asked
    /// every launch.
    /// </summary>
    public sealed class AppConfig
    {
        public string? SelectedUserFolder { get; set; }

        /// <summary>
        /// User-added install locations (full paths to the user folder, the
        /// one that contains a Neighborhoods subdirectory). Lets a player
        /// point Road Editor at a Sims 2 install that isn't in the standard
        /// Documents\EA Games\&lt;edition&gt; layout.
        /// </summary>
        public List<string> CustomInstalls { get; set; } = new();

        private static string Folder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RoadEditor");

        private static string ConfigPath => Path.Combine(Folder, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return new AppConfig();
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                // A corrupt config isn't worth crashing over — just start fresh.
                return new AppConfig();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Non-fatal: if we can't persist the choice, the user just gets
                // asked again next launch. Beats crashing on a read-only disk.
            }
        }
    }
}
