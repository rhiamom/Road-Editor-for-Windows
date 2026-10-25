/***************************************************************************
 *   Hood Checker for Windows                                                  *
 *   Hood Checker © 2006 Andi8104, © 2007-2011 Mootilda                    *
 *   Windows port © 2026 GramzeSweatshop (rhiamom@mac.com)                   *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                         *
 *                                                                         *
 *   Enumerates installed neighborhoods and resolves each one's display    *
 *   name, mirroring PrimaryForm.NeighborhoodScreen (the hood name lives in *
 *   the main package's CTSS, a null-terminated string starting at byte 69).*
 ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using SimPe.Interfaces.Files;
using SimPe.Packages;

namespace LotExpander
{
    /// <summary>One installed neighborhood: folder code, display name, main package path.</summary>
    public sealed class NeighborhoodInfo
    {
        public string Code { get; set; } = "";              // e.g. "N001"
        public string Name { get; set; } = "";              // e.g. "Pleasantview"
        public string MainPackagePath { get; set; } = "";   // …/N001/N001_Neighborhood.package

        public override string ToString() =>
            string.IsNullOrEmpty(Name) ? Code : $"{Code}: {Name}";
    }

    public static class NeighborhoodCatalog
    {
        /// <summary>
        /// Lists neighborhoods under <paramref name="neighborhoodsFolder"/> (or the
        /// resolved default). Skips the Tutorial hood and folders without a main
        /// package. Sorted by folder code.
        /// </summary>
        public static List<NeighborhoodInfo> List(string? neighborhoodsFolder = null)
        {
            var result = new List<NeighborhoodInfo>();
            string? folder = neighborhoodsFolder ?? SimsPaths.NeighborhoodsFolder;
            if (folder == null || !Directory.Exists(folder))
                return result;

            string[] dirs = Directory.GetDirectories(folder);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);

            foreach (string dir in dirs)
            {
                string code = Path.GetFileName(dir);
                if (string.Equals(code, "Tutorial", StringComparison.OrdinalIgnoreCase))
                    continue;

                string main = Path.Combine(dir, code + "_Neighborhood.package");
                if (!System.IO.File.Exists(main))
                    continue;

                var info = new NeighborhoodInfo { Code = code, MainPackagePath = main };
                try { info.Name = ReadHoodName(main); }
                catch { /* leave name blank if the CTSS can't be read */ }
                result.Add(info);
            }
            return result;
        }

        // The neighborhood display name is a null-terminated string beginning at
        // byte 69 of the CTSS (0x43545353, instance 1) in the main package.
        private static string ReadHoodName(string mainPackagePath)
        {
            GeneratableFile pkg = SimPe.Packages.File.LoadFromFile(mainPackagePath);
            IPackedFileDescriptor? ctss = pkg.FindFile(0x43545353, 0, 0xFFFFFFFF, 1);
            if (ctss == null) return "";

            byte[] data = pkg.Read(ctss).UncompressedData;
            int z = 0;
            while (69 + z < data.Length && data[69 + z] != 0) z++;
            byte[] bd = new byte[z];
            Array.Copy(data, 69, bd, 0, z);
            return SimPe.Helper.ToString(bd).Replace(":", ";");
        }
    }
}
