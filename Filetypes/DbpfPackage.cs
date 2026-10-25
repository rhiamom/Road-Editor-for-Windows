/***************************************************************************
 *   Vendored from SimPE                                                   *
 *   Copyright (C) 2004-2007 by Ambertation (Quaxi) <quaxi@ambertation.de> *
 *   .NET 8 / Avalonia port © 2026 GramzeSweatshop (rhiamom@mac.com)       *
 *                                                                         *
 *   GNU GPLv2 or later, see LICENSE.                                      *
 ***************************************************************************/
// =============================================================================
// Focused DBPF v1.1 (Sims 2 .package) reader / writer for the Clean Installer.
//
// Scope: just enough of SimPe.Packages to make the Remove Furniture install
// option work end-to-end on macOS. Supports the calls R_LOT and S2Sims2Pack.
// RemoveFurniture make: LoadFromStream, FindFile(type, subtype, group,
// instance), Read(pfd).UncompressedData, pfd.SetUserData(bytes, _), Build(),
// Close(). Decompresses QFS records on read; on write, modified records are
// stored UNcompressed and removed from the CLST so the game doesn't try to
// decompress them.
//
// Wire format references (line numbers from SimPE-Fixed):
//   Header  — SimPE.Packages/HeaderData.cs Load() (~308)
//   Index   — SimPE.Packages/PackedFileDescription.cs LoadFromStream (~749)
//             entry size = 24 bytes (ptLongFileIndex) or 20 (ptShortFileIndex)
//   CLST    — SimPE.Clst/ClstItem.cs Unserialize (~159)
//             entry size = 20 bytes (long) or 16 bytes (short)
//   QFS     — SimPE.Packages/PackedFile.cs Uncompress (~351)
//             magic 0xFB10 at bytes 4-5; uncompressed size big-endian 24-bit
//             at bytes 6-8.
//   Build() — SimPE.Packages/GeneratableFile.cs Build (~254)
//             order: header (placeholder) -> bodies -> CLST -> index ->
//             rewrite header with final index offset/size.
// =============================================================================

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace SimPe.Interfaces.Files
{
    public interface IPackedFileDescriptor
    {
        // Replaces user-supplied bytes for this entry on the next Read() and
        // for the next Build(). The bool was originally "stored compressed";
        // we always write user-provided bytes uncompressed and remove them
        // from the CLST.
        void SetUserData(byte[] data, bool compressed);

        // Hood Checker reads the resource Instance off descriptors returned by
        // FindFiles (e.g. to resolve a name STR# by instance).
        uint Instance { get; }
    }

    public interface IPackedFile
    {
        byte[] UncompressedData { get; }
    }

    public interface IPackageFile
    {
        IPackedFile Read(IPackedFileDescriptor pfd);

        // SimPE's real signature is FindFile(type, subtype, group, instance).
        // The earlier stub mis-named these as (typeId, groupId, instanceHi,
        // instanceLo); positional callers (R_LOT) still work, but the renames
        // make the contract honest.
        IPackedFileDescriptor? FindFile(uint type, uint subtype, uint group, uint instance);

        // Returns every resource of the given type. Hood Checker's resource
        // handlers enumerate by type (FAMI, SREL, FAMT, NGBH, IDNO, CTSS, …).
        IPackedFileDescriptor[] FindFiles(uint type);

        // Deletes a resource from the package. Hood Checker's fix path removes
        // invalid SREL/FAMT records; the entry is gone from the next Build().
        void Remove(IPackedFileDescriptor pfd);

        // Path the package was loaded from. Hood Checker derives the folder to
        // scan sibling subhood *.package files.
        string FileName { get; }
    }
}

namespace SimPe.Packages
{
    using SimPe.Interfaces.Files;

    internal sealed class PackedFileDescriptor : IPackedFileDescriptor
    {
        public uint Type;
        public uint Group;
        public uint Instance;
        public uint SubType;

        // Original Offset/Size are captured at Load time and never mutated;
        // Build() reads record bodies from _source using these. Offset/Size
        // are updated to the rewritten file's layout as bodies are emitted.
        public uint OriginalOffset;
        public int  OriginalSize;
        public uint Offset;
        public int  Size;

        // When non-null, Read() returns these bytes verbatim and Build()
        // writes them out (uncompressed) instead of the original record body.
        public byte[]? UserData;

        public void SetUserData(byte[] data, bool compressed)
        {
            UserData = data;
        }

        // Explicit impl so the public `Instance` field is still usable
        // internally while satisfying IPackedFileDescriptor.Instance.
        uint IPackedFileDescriptor.Instance => Instance;
    }

    internal sealed class PackedFile : IPackedFile
    {
        public byte[] UncompressedData { get; }
        public PackedFile(byte[] data) { UncompressedData = data; }
    }

    public sealed class GeneratableFile : IPackageFile, IDisposable
    {
        // === Header (96 bytes for v1.1) ===
        private int  _majorVersion;     // = 1
        private int  _minorVersion;     // = 1
        private readonly int[] _reserved00 = new int[3];
        private uint _created;
        private int  _modified;
        private int  _indexType;        // typically 7
        // index count / offset / size are derived from _entries on write
        private int  _holeCount;
        private uint _holeOffset;
        private int  _holeSize;
        private uint _indexKind;        // SimPe.Data.MetaData.IndexTypes: 1 = ptShortFileIndex (20-byte entry, no SubType), 2 = ptLongFileIndex (24-byte entry with SubType)
        private short _epicon;
        private short _showicon;
        private readonly int[] _reserved02 = new int[7];

        // === Index ===
        private readonly List<PackedFileDescriptor> _entries = new();
        // TGI tuples present in the original CLST (so we know which records
        // need QFS decompression on Read).
        private readonly HashSet<(uint Type, uint Group, uint Instance, uint SubType)> _clstSet = new();

        // Original file bytes — we slice unchanged record bodies out of this
        // on Build() rather than carrying them in RAM as byte[] copies.
        private byte[] _source = Array.Empty<byte>();

        // Path this package was loaded from (when opened via File.LoadFromFile).
        // Hood Checker uses it to find sibling subhood *.package files.
        public string FileName { get; internal set; } = string.Empty;

        internal GeneratableFile() { }

        // -----------------------------------------------------------------
        // Load
        // -----------------------------------------------------------------
        internal static GeneratableFile LoadFromStream(BinaryReader br)
        {
            var gf = new GeneratableFile();
            gf.Load(br);
            return gf;
        }

        private void Load(BinaryReader br)
        {
            // Snapshot full stream so we can slice out record bodies later
            // by absolute file offset without re-seeking the reader.
            long origPos = br.BaseStream.Position;
            br.BaseStream.Position = 0;
            _source = br.ReadBytes((int)br.BaseStream.Length);
            br.BaseStream.Position = 0;

            // Header (mirrors HeaderData.Load)
            for (int i = 0; i < 4; i++)
            {
                char c = br.ReadChar();
                if ((i == 0 && c != 'D') || (i == 1 && c != 'B') ||
                    (i == 2 && c != 'P') || (i == 3 && c != 'F'))
                {
                    throw new InvalidDataException("Not a DBPF file");
                }
            }

            _majorVersion = br.ReadInt32();
            _minorVersion = br.ReadInt32();
            for (int i = 0; i < 3; i++) _reserved00[i] = br.ReadInt32();
            _created  = br.ReadUInt32();
            _modified = br.ReadInt32();
            _indexType = br.ReadInt32();
            int  indexCount  = br.ReadInt32();
            uint indexOffset = br.ReadUInt32();
            int  indexSize   = br.ReadInt32();
            _holeCount  = br.ReadInt32();
            _holeOffset = br.ReadUInt32();
            _holeSize   = br.ReadInt32();

            bool isV0101 = _majorVersion > 1 || (_majorVersion == 1 && _minorVersion >= 1);
            _indexKind = isV0101 ? br.ReadUInt32() : 0;
            _epicon    = br.ReadInt16();
            _showicon  = br.ReadInt16();
            for (int i = 0; i < 7; i++) _reserved02[i] = br.ReadInt32();

            // Index
            br.BaseStream.Position = indexOffset;
            bool longIndex = _indexKind == 2;
            for (int i = 0; i < indexCount; i++)
            {
                var e = new PackedFileDescriptor
                {
                    Type     = br.ReadUInt32(),
                    Group    = br.ReadUInt32(),
                    Instance = br.ReadUInt32(),
                };
                e.SubType = longIndex ? br.ReadUInt32() : 0;
                e.Offset  = br.ReadUInt32();
                e.Size    = br.ReadInt32();
                e.OriginalOffset = e.Offset;
                e.OriginalSize   = e.Size;
                _entries.Add(e);
            }

            // CLST (0xE86B1EEF) — records whose bodies are QFS-compressed.
            PackedFileDescriptor? clst = null;
            foreach (var e in _entries) if (e.Type == 0xE86B1EEF) { clst = e; break; }
            if (clst != null)
            {
                int entrySize = longIndex ? 20 : 16;
                int count = clst.Size / entrySize;
                int p = (int)clst.Offset;
                for (int i = 0; i < count; i++)
                {
                    uint t = BitConverter.ToUInt32(_source, p); p += 4;
                    uint g = BitConverter.ToUInt32(_source, p); p += 4;
                    uint ix = BitConverter.ToUInt32(_source, p); p += 4;
                    uint s = longIndex ? BitConverter.ToUInt32(_source, p) : 0;
                    if (longIndex) p += 4;
                    p += 4; // skip stored uncompressedSize
                    _clstSet.Add((t, g, ix, s));
                }
            }
        }

        // -----------------------------------------------------------------
        // IPackageFile
        // -----------------------------------------------------------------
        public IPackedFileDescriptor? FindFile(uint type, uint subtype, uint group, uint instance)
        {
            foreach (var e in _entries)
                if (e.Type == type && e.SubType == subtype &&
                    e.Group == group && e.Instance == instance)
                    return e;
            return null;
        }

        public IPackedFileDescriptor[] FindFiles(uint type)
        {
            var hits = new List<IPackedFileDescriptor>();
            foreach (var e in _entries)
                if (e.Type == type)
                    hits.Add(e);
            return hits.ToArray();
        }

        public void Remove(IPackedFileDescriptor pfd)
        {
            _entries.Remove((PackedFileDescriptor)pfd);
        }

        public IPackedFile Read(IPackedFileDescriptor pfd)
        {
            var e = (PackedFileDescriptor)pfd;
            if (e.UserData != null) return new PackedFile(e.UserData);

            byte[] body = new byte[e.OriginalSize];
            Array.Copy(_source, (int)e.OriginalOffset, body, 0, e.OriginalSize);

            bool wasInClst = _clstSet.Contains((e.Type, e.Group, e.Instance, e.SubType));
            if (wasInClst || IsQfsHeader(body))
                body = QfsDecompress(body);
            return new PackedFile(body);
        }

        // -----------------------------------------------------------------
        // Build — re-serialize the package
        // -----------------------------------------------------------------
        public MemoryStream Build()
        {
            // Records that had SetUserData called are written uncompressed AND
            // removed from the rewritten CLST. Untouched records keep their
            // original (possibly QFS-compressed) bytes verbatim — no decode
            // needed.
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bool longIndex = _indexKind == 2;

            // 1. Header placeholder — final offsets patched in at the end.
            WriteHeader(bw, indexCount: 0, indexOffset: 0, indexSize: 0);

            // 2. Bodies. Record each entry's new offset.
            //    Drop the original CLST entry; we'll rebuild it from scratch.
            var liveEntries = new List<PackedFileDescriptor>(_entries.Count);
            foreach (var e in _entries)
            {
                if (e.Type == 0xE86B1EEF) continue; // skip original CLST; rebuilt below
                e.Offset = (uint)bw.BaseStream.Position;
                if (e.UserData != null)
                {
                    bw.Write(e.UserData);
                    e.Size = e.UserData.Length;
                }
                else
                {
                    bw.Write(_source, (int)e.OriginalOffset, e.OriginalSize);
                }
                liveEntries.Add(e);
            }

            // 3. Rebuilt CLST: every record that was in the original CLST AND
            //    wasn't user-modified (modified ones are now stored uncompressed).
            var newClstItems = new List<(uint Type, uint Group, uint Instance, uint SubType, uint UncSize)>();
            foreach (var e in liveEntries)
            {
                if (e.UserData != null) continue; // now uncompressed; drop from CLST
                var key = (e.Type, e.Group, e.Instance, e.SubType);
                if (!_clstSet.Contains(key)) continue;
                // Body still original; extract uncompressed size from QFS header bytes 6-8.
                uint unc = DecodeQfsUncompressedSize(_source, (int)e.OriginalOffset);
                newClstItems.Add((e.Type, e.Group, e.Instance, e.SubType, unc));
            }

            PackedFileDescriptor? clstEntry = null;
            if (newClstItems.Count > 0)
            {
                clstEntry = new PackedFileDescriptor
                {
                    Type = 0xE86B1EEF, Group = 0xE86B1EEF,
                    Instance = 0x286B1F03, SubType = 0,
                    Offset = (uint)bw.BaseStream.Position,
                };
                int entrySize = longIndex ? 20 : 16;
                clstEntry.Size = newClstItems.Count * entrySize;
                foreach (var c in newClstItems)
                {
                    bw.Write(c.Type); bw.Write(c.Group); bw.Write(c.Instance);
                    if (longIndex) bw.Write(c.SubType);
                    bw.Write(c.UncSize);
                }
                liveEntries.Add(clstEntry);
            }

            // 4. Index
            uint indexOffset = (uint)bw.BaseStream.Position;
            foreach (var e in liveEntries)
            {
                bw.Write(e.Type); bw.Write(e.Group); bw.Write(e.Instance);
                if (longIndex) bw.Write(e.SubType);
                bw.Write(e.Offset); bw.Write(e.Size);
            }
            int indexSize = liveEntries.Count * (longIndex ? 24 : 20);

            // 5. Patch header with real index location
            bw.BaseStream.Position = 0;
            WriteHeader(bw, indexCount: liveEntries.Count,
                            indexOffset: indexOffset, indexSize: indexSize);

            ms.Position = 0;
            return ms;
        }

        private void WriteHeader(BinaryWriter bw, int indexCount, uint indexOffset, int indexSize)
        {
            bw.Write('D'); bw.Write('B'); bw.Write('P'); bw.Write('F');
            bw.Write(_majorVersion); bw.Write(_minorVersion);
            for (int i = 0; i < 3; i++) bw.Write(_reserved00[i]);
            bw.Write(_created); bw.Write(_modified);
            bw.Write(_indexType);
            bw.Write(indexCount); bw.Write(indexOffset); bw.Write(indexSize);
            bw.Write(0); bw.Write((uint)0); bw.Write(0);  // hole index cleared
            if (_majorVersion > 1 || (_majorVersion == 1 && _minorVersion >= 1))
                bw.Write(_indexKind);
            bw.Write(_epicon); bw.Write(_showicon);
            for (int i = 0; i < 7; i++) bw.Write(_reserved02[i]);
        }

        public void Close() { _source = Array.Empty<byte>(); }

        // SimPE API the ported Hood Checker calls to discard pending edits.
        // We never mutate packages we only read, so this is a no-op here.
        public void ForgetUpdate() { }
        public void Dispose() => Close();

        // -----------------------------------------------------------------
        // QFS
        // -----------------------------------------------------------------
        /// <summary>
        /// Public access to the QFS detection / decoder so other consumers
        /// (e.g. the legacy myDBPF-based texture preview path) can decompress
        /// record bodies without going through a full GeneratableFile load.
        /// </summary>
        public static bool LooksQfsCompressed(byte[] body) => IsQfsHeader(body);
        public static byte[] DecompressQfs(byte[] body) => QfsDecompress(body);

        private static bool IsQfsHeader(byte[] body)
        {
            return body.Length >= 9
                && body[4] == 0x10 && body[5] == 0xFB;
        }

        private static uint DecodeQfsUncompressedSize(byte[] body, int offset)
        {
            // Big-endian 24-bit at offset+6..offset+8
            return (uint)((body[offset + 6] << 16) | (body[offset + 7] << 8) | body[offset + 8]);
        }

        // Reference: PackedFile.cs Uncompress() ~lines 351-432.
        private static byte[] QfsDecompress(byte[] data)
        {
            int outSize = (int)DecodeQfsUncompressedSize(data, 0);
            byte[] outBuf = new byte[outSize];
            int outPos = 0;
            int i = 9; // skip QFS header

            while (i < data.Length && data[i] < 0xFC)
            {
                byte cc = data[i++];
                int plain, copyCount, copyOffset;

                if ((cc & 0x80) == 0)
                {
                    byte cc1 = data[i++];
                    plain      = cc & 0x03;
                    copyCount  = ((cc & 0x1C) >> 2) + 3;
                    copyOffset = ((cc & 0x60) << 3) + cc1 + 1;
                }
                else if ((cc & 0x40) == 0)
                {
                    byte cc1 = data[i++];
                    byte cc2 = data[i++];
                    plain      = (cc1 & 0xC0) >> 6;
                    copyCount  = (cc & 0x3F) + 4;
                    copyOffset = ((cc1 & 0x3F) << 8) + cc2 + 1;
                }
                else if ((cc & 0x20) == 0)
                {
                    byte cc1 = data[i++];
                    byte cc2 = data[i++];
                    byte cc3 = data[i++];
                    plain      = cc & 0x03;
                    copyCount  = ((cc & 0x0C) << 6) + cc3 + 5;
                    copyOffset = ((cc & 0x10) << 12) + (cc1 << 8) + cc2 + 1;
                }
                else
                {
                    plain      = (cc - 0xDF) << 2;
                    copyCount  = 0;
                    copyOffset = 0;
                }

                for (int k = 0; k < plain; k++) outBuf[outPos++] = data[i++];
                int src = outPos - copyOffset;
                for (int k = 0; k < copyCount; k++) outBuf[outPos++] = outBuf[src++];
            }

            if (i < data.Length)
            {
                int trailing = data[i++] & 0x03;
                for (int k = 0; k < trailing; k++) outBuf[outPos++] = data[i++];
            }

            return outBuf;
        }
    }

    public static class File
    {
        public static GeneratableFile LoadFromStream(BinaryReader br) =>
            GeneratableFile.LoadFromStream(br);

        // Hood Checker opens neighborhood packages by path (R_NGBH loads
        // subhoods this way). Mirrors SimPe.Packages.File.LoadFromFile.
        public static GeneratableFile LoadFromFile(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            var gf = GeneratableFile.LoadFromStream(br);
            gf.FileName = path;
            return gf;
        }
    }
}
