/**************************************************************************
 *   Road Editor for Windows (copied from HoodReplacer for Mac)               *
 *   List logic from HoodReplace © 2008-2010 Mootilda                     *
 *   © 2026 GramzeSweatshop (rhiamom@mac.com). Written with Claude        *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Mirrors the list Mootilda builds in                                  *
 *   HoodReplace.NeighborhoodScreen(): for each neighborhood folder, the  *
 *   main neighborhood first (always shown), then every other .package in *
 *   that folder — the sub-hoods (Downtown, Suburb, University, and so    *
 *   on). A sub-hood with no lot descriptors is "empty" and is hidden     *
 *   unless the user asks for it. Her comment says why: it keeps the      *
 *   game's hidden neighborhoods out of the list — Pets, Weather          *
 *   (Seasons) and Exotic Destinations (Bon Voyage) are real folders but  *
 *   not playable hoods. The main neighborhood is never filtered; she     *
 *   passes bSkipEmpty:false for it.                                      *
 *************************************************************************/

using SimPe.Interfaces.Files;
using SimPe.Packages;
using System;
using System.Collections.Generic;
using System.IO;

namespace HoodReplace
{
    public sealed class HoodEntry
    {
        /// <summary>Folder code, e.g. "N006".</summary>
        public string FolderCode { get; set; } = "";
        /// <summary>Name read from the package, e.g. "Testing".</summary>
        public string Name { get; set; } = "";
        public string PackagePath { get; set; } = "";
        /// <summary>True for &lt;code&gt;_Neighborhood.package.</summary>
        public bool IsMain { get; set; }

        public override string ToString() =>
            IsMain ? $"{Name}  ({FolderCode})" : $"      {Name}";
    }

    public static class HoodList
    {
        private const uint LotDescription = 0x0BF999E7;
        private const uint CTSS = 0x43545353;

        public static List<HoodEntry> Build(string neighborhoodsFolder, bool showEmpty)
        {
            var result = new List<HoodEntry>();
            if (string.IsNullOrEmpty(neighborhoodsFolder) || !Directory.Exists(neighborhoodsFolder))
                return result;

            string[] dirs = Directory.GetDirectories(neighborhoodsFolder);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);

            foreach (string dir in dirs)
            {
                string code = Path.GetFileName(dir);
                if (string.Equals(code, "Tutorial", StringComparison.OrdinalIgnoreCase))
                    continue;   // she skips Tutorial by name

                string main = Path.Combine(dir, code + "_Neighborhood.package");
                bool hasMain = System.IO.File.Exists(main);

                if (hasMain)
                {
                    string? name = TryReadName(main, skipEmpty: false);
                    if (name != null)
                        result.Add(new HoodEntry { FolderCode = code, Name = name, PackagePath = main, IsMain = true });
                }

                string[] all = Directory.GetFiles(dir, "*.package");
                Array.Sort(all, StringComparer.OrdinalIgnoreCase);
                foreach (string pkg in all)
                {
                    if (hasMain && string.Equals(pkg, main, StringComparison.OrdinalIgnoreCase))
                        continue;   // already listed
                    string? name = TryReadName(pkg, skipEmpty: !showEmpty);
                    if (name != null)
                        result.Add(new HoodEntry { FolderCode = code, Name = name, PackagePath = pkg, IsMain = false });
                }
            }
            return result;
        }

        /// <summary>
        /// Her LoadHoodName: null means "leave it out" — either it would not
        /// open, or it is empty and we were told to skip empties.
        /// </summary>
        private static string? TryReadName(string path, bool skipEmpty)
        {
            try
            {
                GeneratableFile pkg = SimPe.Packages.File.LoadFromFile(path);
                try
                {
                    if (skipEmpty && pkg.FindFiles(LotDescription).Length == 0)
                        return null;

                    IPackedFileDescriptor d = pkg.FindFile(CTSS, 0, 0xFFFFFFFF, 1);
                    if (d == null) return Path.GetFileNameWithoutExtension(path);
                    IPackedFile pf = pkg.Read(d);
                    byte[] data = pf.UncompressedData;
                    int z = 0;
                    while (69 + z < data.Length && data[69 + z] != 0) z++;
                    byte[] bd = new byte[z];
                    Array.Copy(data, 69, bd, 0, z);
                    string name = SimPe.Helper.ToString(bd);
                    return string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(path) : name;
                }
                finally
                {
                    pkg.ForgetUpdate();
                    pkg.Close();
                }
            }
            catch
            {
                return null;   // she shows a retry/ignore dialog; we simply leave it out
            }
        }
    }
}
