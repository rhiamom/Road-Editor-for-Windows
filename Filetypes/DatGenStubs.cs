/***************************************************************************
 *   .NET 8 / Avalonia port stubs                                          *
 *   Copyright (C) 2026 by GramzeSweatshop                                 *
 *   rhiamom@mac.com                                                       *
 *   GNU GPLv2 or later, see LICENSE.                                      *
 *                                                                         *
 *   Replaces the legacy DatGen.DBPF.dll surface area that the Filetypes   *
 *   project relied on. We keep the original namespaces ("DatGen.DBPF.IO"  *
 *   and "DatGen.Types.TS2") so the rest of the installer code links       *
 *   without churn — the name is just an identifier.                       *
 ***************************************************************************/

using System;
using System.IO;

namespace DatGen.DBPF.IO
{
    public interface ILoadable
    {
        void Load(byte[] byteArray);
        void Load(string fileName);
        void Load(Stream stream, uint size);
    }

    public interface ISavable
    {
        byte[] Save();
        void Save(string fileName);
        void Save(Stream stream);
    }

    public interface IRawData
    {
        byte[] RawData { get; set; }
    }

    public interface IFileTypeFilename
    {
        string Filename { get; set; }
    }

    /// <summary>
    /// Minimal base class matching the original DatGen.DBPF.IO.Unknown shape.
    /// Subclasses override Load/Save/RawData and call the base to handle the
    /// raw byte storage; specialised decoding is layered on top in the
    /// Decode() method each subclass defines.
    /// </summary>
    public class Unknown : IDisposable
    {
        private byte[] _rawData = Array.Empty<byte>();

        public virtual void Load(byte[] byteArray)
        {
            _rawData = byteArray ?? Array.Empty<byte>();
        }

        public virtual void Load(string fileName)
        {
            _rawData = File.ReadAllBytes(fileName);
        }

        public virtual void Load(Stream stream, uint size)
        {
            var buf = new byte[size];
            int read = 0;
            while (read < buf.Length)
            {
                int n = stream.Read(buf, read, buf.Length - read);
                if (n <= 0) break;
                read += n;
            }
            _rawData = read == buf.Length ? buf : buf.AsSpan(0, read).ToArray();
        }

        public virtual byte[] Save() => _rawData;

        public virtual void Save(string fileName)
        {
            File.WriteAllBytes(fileName, _rawData);
        }

        public virtual void Save(Stream stream)
        {
            stream.Write(_rawData, 0, _rawData.Length);
        }

        public virtual byte[] RawData
        {
            get => _rawData;
            set => _rawData = value ?? Array.Empty<byte>();
        }

        public virtual object Clone() => MemberwiseClone();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }
    }
}

namespace DatGen.Types.TS2
{
    /// <summary>
    /// Simple TGI(R) record used by TXTR header parsing in Filetypes.cs.
    /// </summary>
    public class TGIEntry
    {
        public uint TypeID;
        public uint GroupID;
        public uint InstanceID;
        public uint ResourceID;
    }

    /// <summary>
    /// DBPF TypeID constants for the Sims 2 file types referenced by the
    /// installer. Values sourced from SimPE's tgi.xml (TXTR/GMDC/etc.) and
    /// SimPE.Filehandlers (JPG variants).
    /// </summary>
    public enum Types : uint
    {
        Texture       = 0x1C4A276C, // TXTR - Texture Image
        GeometricData = 0xAC4F8687, // GMDC - Geometric Data Container
        Image         = 0x856DDBAC, // IMG  - jpg/png/tga image
        JPG           = 0x0C7E9A76, // JFIF - jpeg variant
        Globals       = 0x474C4F42, // GLOB - Global Data
        OBJD          = 0x4F424A44, // OBJD - Object Data
        CatDesc       = 0x43545353, // CTSS - Catalog Description (STRL)
        MatOverride   = 0x4C697E5A, // MMAT - Material Override
        FloorXML      = 0x4DCADB7E, // XFLR - Floor XML
        ObjectXML     = 0xCCA8E925, // XOBJ - Object XML
        TextLists     = 0x53545223, // STR# - Text Lists
        VersionInfo   = 0xEBFEE342, // VERS - Version Information
        PropertySet   = 0xEBCF3E27, // GZPS - Property Set
    }
}

namespace DatGen
{
    /// <summary>
    /// Shim for the legacy DatGen.CRCHash static API. Delegates to SimPE's
    /// canonical TS2 hash routines so we don't carry a second implementation.
    /// The original installer only used the two methods below.
    /// </summary>
    public static class CRCHash
    {
        public static uint GenerateCRC24_TS2(byte[] data)
        {
            byte[] result = SimPe.Hashes.Crc24.ComputeHash(data);
            return (uint)SimPe.Hashes.ToLong(result);
        }

        public static uint GenerateCRC32_TS2(byte[] data)
        {
            byte[] result = SimPe.Hashes.Crc32.ComputeHash(data);
            return (uint)SimPe.Hashes.ToLong(result);
        }
    }
}
