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

        // Priority ordering, most-recent-edition first. If exactly one of these
        // exists on disk, we use it without asking. If more than one exists,
        // the UI shows a picker.
        private static readonly (string Subpath, string DisplayName)[] Candidates = new[]
        {
            (@"EA Games\The Sims 2 Legacy Collection",   "The Sims 2 Legacy Collection"),
            (@"EA Games\The Sims 2 Super Collection",    "The Sims 2 Super Collection"),
            (@"EA Games\The Sims 2 Ultimate Collection", "The Sims 2 Ultimate Collection"),
            (@"EA Games\The Sims 2",                     "The Sims 2"),
        };

        /// <summary>
        /// Returns every Sims 2 install present on disk, in priority order.
        /// Empty list if nothing found.
        /// </summary>
        public static IReadOnlyList<SimsInstall> AllInstalls()
        {
            var found = new List<SimsInstall>();
            foreach (var (sub, display) in Candidates)
            {
                string user = Path.Combine(Documents, sub);
                if (!Directory.Exists(user)) continue;
                string nb = Path.Combine(user, "Neighborhoods");
                if (!Directory.Exists(nb)) continue;
                found.Add(new SimsInstall(display, user, nb));
            }
            return found;
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
