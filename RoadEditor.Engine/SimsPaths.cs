/***************************************************************************
 *   Road Editor for Windows                                               *
 *   Windows port © 2026 GramzeSweatshop (rhiamom@mac.com)                 *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                         *
 ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;

namespace LotExpander
{
    /// <summary>One Sims 2 install discovered under My Documents\EA Games\.</summary>
    public sealed record SimsInstall(string DisplayName, string UserFolder, string NeighborhoodsFolder);

    /// <summary>
    /// Resolves the Sims 2 user folder on Windows. The user folder lives under
    /// My Documents\EA Games\, with the final subfolder depending on which
    /// edition the player installed: classic retail, Ultimate Collection,
    /// Legacy Collection, Super Collection, etc.
    ///
    /// This class is registry-free on purpose. EA's installer wrote keys under
    /// HKLM\SOFTWARE\WOW6432Node\EA GAMES\The Sims 2\, but modern repacks
    /// (Legacy / Ultimate Collection bundles / Magipack) frequently skip
    /// registry writes, and the Install Dir there points at the GAME path
    /// (Program Files), not the USER path (Documents) that Road Editor needs.
    /// Scanning the Documents\EA Games\ tree is the more reliable signal and
    /// matches what SimPE's own GameRootScanner has used for years.
    /// </summary>
    public static class SimsPaths
    {
        private static string Documents =>
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        /// <summary>
        /// Returns every Sims 2 install present on disk. Auto-detection scans
        /// Documents\EA Games\ for any subfolder that contains a Neighborhoods
        /// subdirectory — that's inherently a Sims 2 install, regardless of
        /// edition name or trademark glyphs ("The Sims™ 2", "The Sims™ 2
        /// Ultimate Collection", "The Sims 2 Legacy Collection", etc.).
        /// User-added custom paths from AppConfig follow. Empty list if
        /// nothing qualifies.
        /// </summary>
        public static IReadOnlyList<SimsInstall> AllInstalls()
        {
            var found = new List<SimsInstall>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string eaGames = Path.Combine(Documents, "EA Games");
            if (Directory.Exists(eaGames))
            {
                string[] subdirs;
                try { subdirs = Directory.GetDirectories(eaGames); }
                catch { subdirs = Array.Empty<string>(); }

                // Alphabetical for a stable order in the picker.
                Array.Sort(subdirs, StringComparer.OrdinalIgnoreCase);

                foreach (string user in subdirs)
                {
                    string nb = Path.Combine(user, "Neighborhoods");
                    if (!Directory.Exists(nb)) continue;
                    if (!seen.Add(user)) continue;
                    string name = Path.GetFileName(user.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (string.IsNullOrEmpty(name)) name = user;
                    found.Add(new SimsInstall(name, user, nb));
                }
            }

            foreach (string custom in AppConfig.Load().CustomInstalls)
            {
                if (string.IsNullOrWhiteSpace(custom) || !Directory.Exists(custom)) continue;
                string nb = Path.Combine(custom, "Neighborhoods");
                if (!Directory.Exists(nb)) continue;
                if (!seen.Add(custom)) continue;
                string name = Path.GetFileName(custom.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(name)) name = custom;
                found.Add(new SimsInstall(name + " (custom)", custom, nb));
            }

            return found;
        }

        /// <summary>
        /// Validates that <paramref name="userFolder"/> looks like a Sims 2
        /// user folder (exists and has a Neighborhoods subdirectory), adds
        /// it to the persisted custom-installs list if so, and returns the
        /// resulting SimsInstall. Throws DirectoryNotFoundException if the
        /// path doesn't qualify. Duplicate paths are a no-op.
        /// </summary>
        public static SimsInstall AddCustomInstall(string userFolder)
        {
            if (string.IsNullOrWhiteSpace(userFolder) || !Directory.Exists(userFolder))
                throw new DirectoryNotFoundException("The chosen folder does not exist.");

            string nb = Path.Combine(userFolder, "Neighborhoods");
            if (!Directory.Exists(nb))
                throw new DirectoryNotFoundException("The chosen folder has no \"Neighborhoods\" subdirectory. Pick the Sims 2 user folder (the one that contains Neighborhoods, Downloads, etc.), not the folder above or below it.");

            var cfg = AppConfig.Load();
            bool already = false;
            foreach (string existing in cfg.CustomInstalls)
                if (string.Equals(existing, userFolder, StringComparison.OrdinalIgnoreCase))
                    { already = true; break; }
            if (!already)
            {
                cfg.CustomInstalls.Add(userFolder);
                cfg.Save();
            }

            string name = Path.GetFileName(userFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(name)) name = userFolder;
            return new SimsInstall(name + " (custom)", userFolder, nb);
        }

        /// <summary>
        /// The selected Sims 2 user folder, honoring the persisted choice when
        /// one is set. Returns null if nothing is found or if multiple installs
        /// exist and the user hasn't yet picked one.
        /// </summary>
        public static string? UserFolder
        {
            get
            {
                var installs = AllInstalls();
                if (installs.Count == 0) return null;

                string? saved = AppConfig.Load().SelectedUserFolder;
                if (!string.IsNullOrEmpty(saved))
                {
                    foreach (var i in installs)
                        if (string.Equals(i.UserFolder, saved, StringComparison.OrdinalIgnoreCase))
                            return i.UserFolder;
                    // Persisted choice no longer exists — fall through to auto-pick.
                }

                if (installs.Count == 1) return installs[0].UserFolder;

                // Multiple present, no valid saved choice: caller must prompt.
                return null;
            }
        }

        /// <summary>
        /// The selected Neighborhoods folder, or null if ambiguous / none found.
        /// </summary>
        public static string? NeighborhoodsFolder
        {
            get
            {
                string? user = UserFolder;
                if (user == null) return null;
                string nb = Path.Combine(user, "Neighborhoods");
                return Directory.Exists(nb) ? nb : null;
            }
        }

        /// <summary>
        /// Records the user's install choice and returns the Neighborhoods
        /// folder for it.
        /// </summary>
        public static string Select(SimsInstall install)
        {
            if (install == null) throw new ArgumentNullException(nameof(install));
            var cfg = AppConfig.Load();
            cfg.SelectedUserFolder = install.UserFolder;
            cfg.Save();
            return install.NeighborhoodsFolder;
        }
    }
}
