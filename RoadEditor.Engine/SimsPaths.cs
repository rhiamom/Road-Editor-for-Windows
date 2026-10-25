/***************************************************************************
 *   Road Editor for Windows                                               *
 *   Windows port © 2026 GramzeSweatshop (rhiamom@mac.com)                 *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                         *
 ***************************************************************************/

using System;
using System.IO;

namespace LotExpander
{
    /// <summary>
    /// Resolves the Sims 2 user folder on Windows. The user folder is always
    /// under My Documents\EA Games\, but the final subfolder name depends on
    /// which edition the player installed: classic retail, Ultimate Collection,
    /// Legacy Collection, Super Collection, etc. Candidates are checked in a
    /// recency-biased order so modern installs win when both are present.
    /// </summary>
    public static class SimsPaths
    {
        private static string Documents =>
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        private static readonly string[] Candidates =
        {
            @"EA Games\The Sims 2 Legacy Collection",
            @"EA Games\The Sims 2 Super Collection",
            @"EA Games\The Sims 2 Ultimate Collection",
            @"EA Games\The Sims 2",
        };

        /// <summary>The Sims 2 user folder, or null if none found.</summary>
        public static string? UserFolder
        {
            get
            {
                foreach (string c in Candidates)
                {
                    string full = Path.Combine(Documents, c);
                    if (Directory.Exists(full))
                        return full;
                }
                return null;
            }
        }

        /// <summary>The Neighborhoods folder, or null if the user folder isn't found.</summary>
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
    }
}
