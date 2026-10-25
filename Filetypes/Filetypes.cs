/***************************************************************************
 *   Copyright (C) 2004-2007 by Karol Rybak                                *
 *   http://phervers.ModTheSims.info                                       *
 *                                                                         *
 *   Additional programming:                                               *
 *   Copyright (C) 2010-2013 by Mootilda                                   *
 *   http://www.modthesims.info/member.php?u=589252                        *
 *                                                                         *
 *   This program is free software; you can redistribute it and/or modify  *
 *   it under the terms of the GNU General Public License as published by  *
 *   the Free Software Foundation; either version 2 of the License, or     *
 *   (at your option) any later version.                                   *
 *                                                                         *
 *   This program is distributed in the hope that it will be useful,       *
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of        *
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the         *
 *   GNU General Public License for more details.                          *
 *                                                                         *
 *   You should have received a copy of the GNU General Public License     *
 *   along with this program; if not, write to the                         *
 *   Free Software Foundation, Inc.,                                       *
 *   59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.             *
 ***************************************************************************/

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections;
using System.IO;
using System.Text;


namespace DatGen.Types.TS2
{
    /// <summary>
    /// Summary description for TXTRFileType.
    /// </summary>
    using DatGen.DBPF.IO;


    #region Temp
    public class Temp : Unknown, ILoadable, ISavable, IRawData, ICloneable, IDisposable
    {
        #region data
        // put private data storage here
        #endregion

        #region constructors
        /// <summary>
        /// Default constructor
        /// </summary>
        public Temp()
        {

        }
        #endregion

        #region ILoadable Members
        public override void Load(byte[] byteArray)
        {
            base.Load(byteArray);
            Decode();
        }
        public override void Load(string fileName)
        {

            base.Load(fileName);
            Decode();
        }
        public override void Load(Stream stream, uint size)
        {
            base.Load(stream, size);
            Decode();
        }
        #endregion

        #region ISavable Members

        public override byte[] Save()
        {
            return base.Save();
        }


        public override void Save(string fileName)
        {
            base.Save(fileName);
        }


        public override void Save(Stream stream)
        {
            base.Save(stream);
        }
        #endregion

        #region IRawData Members
        public override byte[] RawData
        {
            get
            {
                return base.RawData;
            }
            set
            {
                base.RawData = value;
            }
        }
        #endregion

        #region IFileTypeFilename Members
        //        public string Filename
        //        {
        //            get
        //            {
        //                return fFileName;
        //            }
        //            set
        //            {
        //                fFileName = value;
        //            }
        //        }
        #endregion

        #region ICloneable Members
        public new object Clone()
        {

            // add code to copy any variables from this class

            return base.Clone();
        }
        #endregion

        #region IDisposable Members
        public new void Dispose()
        {

            // Add any cleanup code

            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region private Methods

        /// <summary>
        /// Decode file structure fill data structures etc.
        /// </summary>
        /// <returns></returns>
        private bool Decode()
        {
            return true;
        }
        #endregion

        #region public Properties
        // public data access
        #endregion
    }
    #endregion


    #region TXTR



    #region TXTRMipMapImage
    public struct TXTRMipMapImage
    {
        internal Bitmap m_RGB;
        internal Bitmap m_Alpha;
        internal bool m_hasAlpha;

        public Bitmap RGB
        {
            get
            {
                return m_RGB;
            }
        }
        public Bitmap Alpha
        {
            get
            {
                return m_Alpha;
            }
        }
        public bool HasAlpha
        {
            get
            {
                return m_hasAlpha;
            }
        }
    }

    #endregion


    #region TXTRMipmapCollection
    public class TXTRMipMapCollection : CollectionBase
    {
        public TXTRMipmap this[int index]
        {
            get
            {
                //if(index>=List.Count)

                return ((TXTRMipmap)(List[index]));
            }
            set { List[index] = value; }
        }

        public int Add(TXTRMipmap value)
        {
            return List.Add(value);
        }

        public void Insert(int index, TXTRMipmap value)
        {
            List.Insert(index, value);
        }

        public void Remove(TXTRMipmap value)
        {
            List.Remove(value);
        }

        public bool Contains(TXTRMipmap value)
        {
            return List.Contains(value);
        }

    }
    #endregion

    #region TXTRMipmap
    public class TXTRMipmap
    {
        public TXTR txtr;
        private bool fReference;
        private  string fLifoName;
        private Bitmap fImage;

        internal int m_width, m_height;
        internal long m_offset;
        internal uint m_size;
        internal uint m_EncodingType;

        #region Public properties


        /// <summary>
        /// if (reference == true) mipmap is a lifo reference
        /// if (reference == false) mipmap is an actual image
        /// </summary>
        public bool Reference
        {
            get{return fReference;}
        }


        public byte[] RawData
        {
            get
            {
                byte[] byteArray = new byte[m_size];
                MemoryStream stream = new MemoryStream(txtr.RawData);
                BinaryReader reader = new BinaryReader(stream);
                reader.BaseStream.Seek(m_offset, SeekOrigin.Begin);
                byteArray = reader.ReadBytes((int)m_size);
                return byteArray;
            }

        }

        /// <summary>
        /// Returns image of mipmap. Use only if Reference == false
        /// if set it will nullify LifoName
        /// </summary>
        public Bitmap MipImage
        {
            get
            {
                try
                {
                    MemoryStream stream = new MemoryStream(txtr.RawData);
                    stream.Seek(m_offset, SeekOrigin.Begin);
                    switch(m_EncodingType)
                    {
                        case 1:
                            return Util.ImageTools.DecodeRaw(stream, m_size, m_width, m_height, 32);
                        case 2:
                            return Util.ImageTools.DecodeRaw(stream, m_size, m_width, m_height, 24);
                        case 3:
                            return null;
                        case 4:
                            return Util.ImageTools.DecodeDXT(stream, m_size, m_width, m_height, 1);
                        case 5:
                            return Util.ImageTools.DecodeDXT(stream, m_size, m_width, m_height, 3);
                        case 6:
                            return Util.ImageTools.DecodeRaw(stream, m_size, m_width, m_height, 8);
                        case 7:
                            return null;
                        case 8:
                            return Util.ImageTools.DecodeDXT(stream, m_size, m_width, m_height, 5);



                        default:
                            return null;

                    }
                }
                catch
                {
                    return new Bitmap(1,1);
                }
            }
            set
            {
                if(value.Width != m_width || value.Height != m_height)
                {
                    throw new Exception("Bitmap width or height doesnt match");
                }
                MemoryStream stream = new MemoryStream(txtr.RawData);
                stream.Seek(m_offset, SeekOrigin.Begin);
                fImage = (Bitmap)value.Clone();
                fLifoName = "";
                fReference = false;
            }
        }



        /// <summary>
        /// Returns name of referenced lifo. Use only if Reference == true
        /// When set it will nullify image
        /// </summary>
        public string LifoName
        {
            get { return fLifoName; }
            set
            {
                fLifoName = value;
                fImage = null;
                fReference = true;
            }
        }


        /// <summary>
        /// Width of the mipmap
        /// </summary>
        public int Width
        {
            get { return m_width; }
        }



        /// <summary>
        /// Height of the mipmap
        /// </summary>
        public int Height
        {
            get { return m_height; }
        }

        /// <summary>
        /// TXTR encoding type: 4 = DXT1 (no alpha), 5 = DXT3, 8 = DXT5.
        /// Other values exist (raw/grayscale/LIFO) but only DXT1/3/5 are
        /// supported by the BGRA preview path.
        /// </summary>
        public uint EncodingType => m_EncodingType;

        /// <summary>
        /// True if this mipmap references an external LIFO record rather
        /// than carrying its own pixel data. LIFO-referenced mipmaps are
        /// not supported by the current preview pipeline.
        /// </summary>
        public bool IsLifoReference => fReference;

        /// <summary>
        /// Decode this mipmap to a BGRA8888 byte buffer (length = Width*Height*4).
        /// Returns null for unsupported encodings or LIFO references.
        /// Only DXT1 (type 4), DXT3 (type 5) and DXT5 (type 8) are handled.
        /// </summary>
        public byte[] DecodeBgra()
        {
            if (fReference) return null;
            int dxtFormat = m_EncodingType switch
            {
                4 => 1, // DXT1
                5 => 3, // DXT3
                8 => 5, // DXT5
                _ => 0,
            };
            if (dxtFormat == 0) return null;

            MemoryStream stream = new MemoryStream(txtr.RawData);
            stream.Seek(m_offset, SeekOrigin.Begin);
            try
            {
                return Util.ImageTools.DecodeDXTToBgra(stream, m_size, m_width, m_height, dxtFormat);
            }
            catch
            {
                return null;
            }
        }


        #endregion
    }
    #endregion


    #region TXTRImageCollection
    public class TXTRImageCollection : CollectionBase
    {
        public TXTRImage this[int index]
        {
            get
            {
                //if(index>=List.Count)

                return ((TXTRImage)(List[index]));
            }
            set { List[index] = value; }
        }

        public int Add(TXTRImage value)
        {
            return List.Add(value);
        }

        public void Insert(int index, TXTRImage value)
        {
            List.Insert(index, value);
        }

        public void Remove(TXTRImage value)
        {
            List.Remove(value);
        }

        public bool Contains(TXTRImage value)
        {
            return List.Contains(value);
        }

    }
    #endregion

    #region TXTRImage
    public struct TXTRImage
    {
        public TXTRMipMapCollection MipMaps;
        public uint Unknown1;
        public uint creatorID;
    }
    #endregion



    /// <summary>
    /// FileType class for TXTR files.
    /// Create an instance, and use load method.
    /// Does require temporary file.
    /// </summary>
    public class TXTR : Unknown, ILoadable, ISavable, IRawData, IFileTypeFilename, ICloneable, IDisposable
    {
        #region data
        private string fFileName, fileType;
        private string fTempFileName;
        private FileStream fTempFileStream;
        private byte[] header, cSGResource;


        private static string[] fEncodingTypeNameList = new string[8] {"Unknown",  "Raw + Alpha", "Raw", "Unknown", "DXT1 (No alpha)", "DXT3 (alpha)", "Raw grayscale", "Unknown"};
        private uint fEncodingType;
        private uint fWidth;
        private uint fHeight;
        private uint fNumMipMaps;
        private uint fModifier;
        private uint fOuterLoopCount, fInnerLoopCount;
        private uint const1, unknown1, unknown2;
        private byte unknownByte;

        private TXTRImageCollection fImages;

        #endregion

        #region constructors
        /// <summary>
        /// Default constructor
        /// </summary>
        public TXTR()
        {

            fFileName = "";
            //fTempFileName = Path.GetTempFileName();
            fImages = new TXTRImageCollection();
        }
        #endregion

        #region ILoadable Members
        public override void Load(byte[] byteArray)
        {
            base.Load(byteArray);
            DecodeTXTR();
        }
        public override void Load(string fileName)
        {
            base.Load(fileName);
            DecodeTXTR();
        }
        public override void Load(Stream stream, uint size)
        {
            base.Load(stream, size);
            DecodeTXTR();
        }
        #endregion

        #region ISavable Members
        public override byte[] Save()
        {
            EncodeTXTR();
            return new byte[1];
        }
        public override void Save(string fileName)
        {
            EncodeTXTR();
            base.Save(fileName);
        }
        public override void Save(Stream stream)
        {
            EncodeTXTR();
            base.Save(stream);
        }
        #endregion

        #region IRawData Members
        public override byte[] RawData
        {
            get
            {
                return base.RawData;
            }
            set
            {
                base.RawData = value;
                //DecodeTXTR();
            }
        }
        #endregion

        #region IFileTypeFilename Members
        public string Filename
        {
            get
            {
                return fFileName;
        }
            set
            {
                fFileName = value;
            }
        }
        #endregion

        #region ICloneable Members
        public new object Clone()
        {
            TXTR temp = (TXTR)base.Clone();
            // add code to copy any variables from this class

            return temp;
        }
        #endregion

        #region IDisposable Members
        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region private Methods


        /// <summary>
        /// Create temporary file, write contents, Seek to beginning and leave stream open
        /// </summary>
        private void CreateTempFile()
        {

            MemoryStream stream = new MemoryStream(RawData);
            BinaryReader reader = new BinaryReader(stream);

            fTempFileStream = new FileStream(fTempFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BinaryWriter writer = new BinaryWriter(fTempFileStream);

            //reader.BaseStream.Seek(16,SeekOrigin.Begin);

            writer.Write( reader.ReadBytes((int)stream.Length) );

            fTempFileStream.Seek(0, SeekOrigin.Begin);

            stream.Close();
        }


        /// <summary>
        /// Put all mipmaps back into TXTR
        /// changes RawData
        /// </summary>
        /// <returns></returns>
        private bool EncodeTXTR()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(header);
            writer.Write(fileType);
            writer.Write((UInt32)const1);
            writer.Write((UInt32)fModifier);

            writer.Write(cSGResource);

            writer.Write(fFileName);

            writer.Write((UInt32)fWidth);
            writer.Write((UInt32)fHeight);
            writer.Write((UInt32)fEncodingType);
            writer.Write((UInt32)fNumMipMaps);
            writer.Write((UInt32)unknown1);
            writer.Write((UInt32)fOuterLoopCount);
            writer.Write((UInt32)unknown2);
            writer.Write((byte)unknownByte);

            for(int i=0 ; i<fOuterLoopCount ; i++)
            {
                switch(fModifier)
                {
                    case 9:
                        writer.Write((UInt32)fInnerLoopCount);
                        break;
                }

                for(int j=0 ; j<fInnerLoopCount ; j++)
                {
                    TXTRMipmap tMipMap = fImages[i].MipMaps[j];
                    if(tMipMap.Reference)
                    {
                        writer.Write((byte)1);
                        writer.Write(tMipMap.LifoName);
                    }
                    else
                    {
                        writer.Write((byte)0);
                        writer.Write(tMipMap.m_size);
                        // Write mipmap contents
                        writer.Write(tMipMap.RawData);
                    }
                }
                switch(fModifier)
                {
                    case 7:
                        writer.Write((UInt32)fImages[i].Unknown1);
                        break;
                    case 8:
                    case 9:
                        writer.Write((UInt32)fImages[i].creatorID);
                        writer.Write((UInt32)fImages[i].Unknown1);
                        break;
                }
            }

            RawData = stream.ToArray();


            return true;
        }


        private void GetHeader(BinaryReader f)
        {
            bool newIndex;
            uint test = f.ReadUInt32();
            if (test == 0xffff0001)
            {
                newIndex = true;
            }
            else
            {
                newIndex = false;
                f.BaseStream.Seek(0,SeekOrigin.Begin);
            }
            uint count = f.ReadUInt32();
            TGIEntry[] links = new TGIEntry[count];
            for (uint i=0;i<count;i++)
            {
                links[i] = new TGIEntry();
                links[i].TypeID = f.ReadUInt32();
                links[i].GroupID = f.ReadUInt32();
                links[i].InstanceID = f.ReadUInt32();
                if (newIndex)
                    links[i].ResourceID = f.ReadUInt32();
            }
            count = f.ReadUInt32();
            uint[] blocks = new uint[count];
            for (uint i=0;i<count;i++)
            {
                blocks[i]=f.ReadUInt32();
            }
        }



        /// <summary>
        /// Creates temp file, Decodes txtr format
        /// Writes mipmaps offsets, size, and w,h
        /// </summary>
        /// <returns></returns>
        private bool DecodeTXTR()
        {
            //CreateTempFile();
            MemoryStream stream = new MemoryStream(RawData);
            BinaryReader reader = new BinaryReader(stream);

//            header = new byte[16];
//            header = reader.ReadBytes(16);

            GetHeader(reader);

            fileType    = reader.ReadString();
            const1        = reader.ReadUInt32();
            fModifier    = reader.ReadUInt32();
            if( (fileType!="cImageData")||(const1!=0x1c4a276c) )  return false;
            if( (fModifier!=7) && (fModifier!=8) && (fModifier!=9) ) return false;

//          Reade cSGResource
            cSGResource = new byte[20];
            cSGResource = reader.ReadBytes(20);

            fFileName = reader.ReadString();

            fWidth            = reader.ReadUInt32();
            fHeight            = reader.ReadUInt32();
            fEncodingType    = reader.ReadUInt32();
            fNumMipMaps        = reader.ReadUInt32();
            unknown1        = reader.ReadUInt32();
            fOuterLoopCount    = reader.ReadUInt32();
            unknown2        = reader.ReadUInt32();
            //unknownByte        = reader.ReadByte();


            string fileNameShort = reader.ReadString();
            //fInnerLoopCount    = reader.ReadUInt32();

            for(uint i=0 ; i<fOuterLoopCount ; i++)
            {
                switch(fModifier)
                {
                    case 7:
                    case 8:
                        fInnerLoopCount = fNumMipMaps;
                        break;
                    case 9:
                        fInnerLoopCount = reader.ReadUInt32();
                        if(fInnerLoopCount != fNumMipMaps) return false;
                        break;
                }

                TXTRImage tImage = new TXTRImage();
                tImage.MipMaps = new TXTRMipMapCollection();

                for(uint j=0 ; j<fInnerLoopCount ; j++)
                {
                    TXTRMipmap tMipmap = new TXTRMipmap();
                    tMipmap.txtr = this;
                    byte MipType = reader.ReadByte();

                    switch(MipType)
                    {
                        case 0:
                            tMipmap.m_size        = reader.ReadUInt32();
                            tMipmap.m_offset    = reader.BaseStream.Position;
                            tMipmap.m_width        = (int)fWidth;
                            tMipmap.m_height    = (int)fHeight;

                            double m = 1;
                            int block = 1;
                            switch(fEncodingType)
                            {
                                case 0:
                                    return false;

                                case 1:
                                    m = 4;
                                    break;
                                case 2:
                                    m = 3;
                                    break;
                                case 3:
                                    return false;
                                case 4:
                                    m = 0.5;
                                    block = 4;
                                    break;
                                case 5:
                                    m = 1;
                                    block = 4;
                                    break;
                                case 6:
                                    m = 1;
                                    break;
                                case 7:
                                    return false;
                                case 8:
                                    return false;
                                case 9:
                                    m = 3;
                                    break;
                            }


                            tMipmap.m_height = (int)(Math.Ceiling((double)tMipmap.m_height / (double)block) * block);
                            tMipmap.m_width = (int)(Math.Ceiling((double)tMipmap.m_width /(double)block) * block);


                            while((tMipmap.m_size / m) != tMipmap.m_width * tMipmap.m_height )
                            {
                                if(tMipmap.m_width>block)
                                    tMipmap.m_width  /= 2;
                                if(tMipmap.m_height>block)
                                    tMipmap.m_height /= 2;
                                if ((tMipmap.m_width <= block) && (tMipmap.m_height <= block))
                                    break;
                            }


                            reader.BaseStream.Seek(tMipmap.m_size, SeekOrigin.Current);
                            break;
                        case 1:
                            tMipmap.LifoName    = reader.ReadString();
                            break;
                    }
                    //tMipmap.m_TempFileStream = fTempFileStream;
                    tMipmap.m_EncodingType = fEncodingType;
                    tImage.MipMaps.Add(tMipmap);

                }
                switch(fModifier)
                {
                    case 7:
                        tImage.Unknown1        = reader.ReadUInt32();
                        break;
                    case 8:
                    case 9:
                        tImage.creatorID    = reader.ReadUInt32();
                        tImage.Unknown1        = reader.ReadUInt32();
                        break;
                }
                fImages.Add(tImage);
            }


            return true;

        }



        Image DecodeDXT(byte[] data, int width, int height, int type)
        {
            Image image = Image.FromFile("");
            return image;
        }

        Image DecodeRAW(byte[] data, int width, int height, int bitsPerPixel)
        {
            Image image = Image.FromFile("");
            return image;
        }

        #endregion

        #region public Methods
        public void Close()
        {
            if(fTempFileStream != null)
                fTempFileStream.Close();
            File.Delete(fTempFileName);
        }
        #endregion

        #region public Properties


        /// <summary>
        /// Returns number of mipmaps
        /// </summary>
        public TXTRImageCollection Images
        {
            get
            {
                return fImages;
            }
            set
            {
            }
        }


        /// <summary>
        /// Image encoding used in TXTR.
        /// Encoding types
        ///        0 = 'Unknown'
        ///        1 = 'Raw + Alpha'
        ///        2 = 'Raw'
        ///        3 = 'Unknown'
        ///        4 = 'DXT1 (No alpha)'
        ///        5 = 'DXT3 (alpha)'
        ///        6 = 'Raw grayscale'
        ///        7 = 'Unknown'
        /// </summary>
        public uint ImageEncodingType
        {
            get
            {
                return fEncodingType;
            }
            set
            {
            }
        }


        /// <summary>
        /// Returns a string containing meaningful name of TXTR encoding
        /// </summary>
        public string ImageEncodingTypeName
        {
            get
            {
                return fEncodingTypeNameList[ fEncodingType ];
            }
        }


        /// <summary>
        /// Returns width of largest mipmap
        /// </summary>
        public uint Width
        {
            get
            {
                return fWidth;
            }
        }


        /// <summary>
        /// Returns height of largest mipmap
        /// </summary>
        public uint Height
        {
            get
            {
                return fHeight;
            }
        }
        #endregion

    }
    #endregion

}

namespace Util
{

    public struct DecodedDXT
    {
        Bitmap RGB;
        Bitmap Alpha;
    }


    public class ImageTools
    {
        /// <summary>
        /// Decode DXT into a bitmap you can use in Image class
        /// </summary>
        /// <param name="data">DXT data in a byte array</param>
        /// <param name="width">Width of the image, you need to know that before</param>
        /// <param name="height">Height of the image, you need to know that before</param>
        /// <param name="DXTType">Type of DXT currently supported 1, 3</param>
        /// <returns>Decoded bitmap</returns>
        public static Bitmap DecodeDXT(Stream stream, uint size ,int width, int height, int format)
        {
            BinaryReader reader = new BinaryReader(stream);
            Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            Bitmap alphaBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            BitmapData bmData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            BitmapData alphaBmData = alphaBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            //
            unsafe
            {
                byte * p = (byte *)(void *)bmData.Scan0;
                int[] Alpha = new int[16]; // FH: f�r Alpha reicht hier [4 * 4] !!!!
                for (int y = 0; y < bitmap.Height; y += 4) // DXT encodes 4x4 blocks of pixel
                {
                    for (int x = 0; x < bitmap.Width; x += 4)
                    {
                        // decode the alpha data (DXT3)
                        if (format == 3)
                        {
                            long abits = reader.ReadInt64();
                            // 16 alpha values are here, one for each pixel, each 4 bits long
                            for (int i = 0; i < 16; i++)
                            {
                                Alpha[i] = (int) ((abits & 0xf) * 0x11);    // je 4 bit herausschieben
                                abits >>= 4;
                            } // for by
                        }
                        else if (format == 5) // DXT5
                        {
                            int alpha1 = reader.ReadByte();
                            int alpha2 = reader.ReadByte();
                            long abits = (long)reader.ReadUInt32() | ((long)reader.ReadUInt16() << 32);
                            int[] alphas = new int[8]; // holds the calculated alpha values
                            alphas[0] = alpha1;
                            alphas[1] = alpha2;
                            if (alpha1 > alpha2)
                            {
                                alphas[2] = (6 * alpha1 + alpha2) / 7;
                                alphas[3] = (5 * alpha1 + 2 * alpha2) / 7;
                                alphas[4] = (4 * alpha1 + 3 * alpha2) / 7;
                                alphas[5] = (3 * alpha1 + 4 * alpha2) / 7;
                                alphas[6] = (2 * alpha1 + 5 * alpha2) / 7;
                                alphas[7] = (alpha1 + 6 * alpha2) / 7;
                            }
                            else
                            {
                                alphas[2] = (4 * alpha1 + alpha2) / 5;
                                alphas[3] = (3 * alpha1 + 2 * alpha2) / 5;
                                alphas[4] = (2 * alpha1 + 3 * alpha2) / 5;
                                alphas[5] = (1 * alpha1 + 4 * alpha2) / 5;
                                alphas[6] = 0;
                                alphas[7] = 0xff;
                            }
                            for (int i = 0; i < 16; i++)
                            {
                                Alpha[i] = alphas[abits & 7];    // je 3 bit als Code herausschieben
                                abits >>= 3;
                            }
                        } // if format..

                        // two 16 bit encoded colors (red 5 bits, green 6 bits, blue 5 bits)
                        uint c1packed16 = ((uint)reader.ReadByte()) | ((uint)reader.ReadByte() << 8);
                        uint c2packed16 = ((uint)reader.ReadByte()) | ((uint)reader.ReadByte() << 8);

                        // separate the R,G,B values
                        uint color1r = (c1packed16 >> 8) & 0xF8;
                        uint color1g = (c1packed16 >> 3) & 0xFC;
                        uint color1b = (c1packed16 << 3) & 0xF8;

                        uint color2r = (c2packed16 >> 8) & 0xF8;
                        uint color2g = (c2packed16 >> 3) & 0xFC;
                        uint color2b = (c2packed16 << 3) & 0xF8;

                        uint[] colors = new uint[8]; // color table for all possible codes
                        // colors 0 and 1 point to the two 16 bit colors we read in
                        colors[0] = (color1r << 16) | (color1g << 8) | color1b ;
                        colors[1] = (color2r << 16) | (color2g << 8) | color2b  ;

                        // 2/3 Color1, 1/3 color2
                        uint colorr = (((color1r<<1) + color2r) / 3) & 0xFF;
                        uint colorg = (((color1g<<1) + color2g) / 3) & 0xFF;
                        uint colorb = (((color1b<<1) + color2b) / 3) & 0xFF;
                        colors[2] = (colorr << 16) | (colorg << 8) | colorb  ;

                        // 2/3 Color2, 1/3 color1
                        colorr = (((color2r<<1) + color1r) / 3) & 0xFF;
                        colorg = (((color2g<<1) + color1g) / 3) & 0xFF;
                        colorb = (((color2b<<1) + color1b) / 3) & 0xFF;
                        colors[3] = (colorr << 16) | (colorg << 8) | colorb  ;

                        // read in the color code bits, 16 values, each 2 bits long
                        // then look up the color in the color table we built
                        int bits1 = reader.ReadByte() + (reader.ReadByte() << 8) + (reader.ReadByte() << 16) + (reader.ReadByte() << 24);

                        for (int by = 0; by < 4; by++)
                        {
                            for (int bx = 0; bx < 4; bx++)
                            {
                                try
                                {
                                    if (((x + bx) < width) && ((y + by) < height))
                                    {
                                        int code = (bits1 >> (((by<<2)+bx)<<1))&0x3;
                                        p[(y + by)*bmData.Stride + (x + bx)*3]        = (byte)colors[code] ;
                                        p[(y + by)*bmData.Stride + (x + bx)*3 + 1]    = (byte)(colors[code] >> 8) ;
                                        p[(y + by)*bmData.Stride + (x + bx)*3 + 2]    = (byte)(colors[code] >> 16) ;

                                        if ((format == 3) || (format == 5))
                                        {
                                            // Alpha
                                            //bitmap.SetPixel(x + bx, y + by, Color.FromArgb(Alpha[(by << 2) + bx], colors[code]));
                                        }
                                        else
                                        {
                                            //bitmap.SetPixel(x + bx, y + by, colors[code]);
                                        }
                                    }
                                }
                                catch
                                {

                                }
                            }
                        } // for by
                    } // for x
                } // for y
            }
            bitmap.UnlockBits(bmData);
            return bitmap;

            #region oldcode
//            if( DXTType!=1 && DXTType!=3) return null;
//            switch(DXTType)
//            {
//                case 1:
//                    if( (width*height) != size*2 )
//                        throw new Exception("Mipmap size is invalid");
//                    break;
//                case 3:
//                    if( (width*height) != size )
//                        throw new Exception("Mipmap size is invalid");
//                    break;
//
//            }
//
//            // Add size check
//
//            BinaryReader reader = new BinaryReader(stream);
//
//            Bitmap bitmap = new Bitmap(width, height);
//
//            Bitmap alphaBitmap = new Bitmap(width, height);
//
//            BitmapData bmData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
//            BitmapData alphaBmData = alphaBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
//
//
//            int stride = bmData.Stride;
//            System.IntPtr Scan0 = bmData.Scan0;
//
//
//            unsafe
//            {
//                byte * p = (byte *)(void *)Scan0;
//                byte * pa = (byte *)(void *)alphaBmData.Scan0;
//                //int nOffset = stride - bitmap.Width;
//                for (int y=0;y<height;y+=4) // DXT encodes 4x4 blocks of pixels
//                {
//                    for (int x=0;x<width;x+=4)
//                    {
//                        // decode the alpha data
//                        if (DXTType == 3) // DXT3 has 64 bits of alpha data, then 64 bits of DXT1 RGB data
//                        {
//                            // DXT3 Alpha
//                            // 16 alpha values are here, one for each pixel, each is 4 bits long
//                            uint abits1 = (uint)reader.ReadByte() | ((uint)reader.ReadByte() << 8) | ((uint)reader.ReadByte() << 16) | ((uint)reader.ReadByte() << 24);
//                            uint abits2 = (uint)reader.ReadByte() | ((uint)reader.ReadByte() << 8) | ((uint)reader.ReadByte() << 16) | ((uint)reader.ReadByte() << 24);
//                            for (int by=0;by<4;++by)
//                            {
//                                for (int bx=0;bx<4;++bx)
//                                {
//                                    uint bits;
//                                    if (by < 2)
//                                        bits = ((abits1 >> (((by<<2)+bx)<<2))&0xF)<<4;
//                                    else
//                                        bits = ((abits2 >> ((((by-2)<<2)+bx)<<2))&0xF)<<4;
//
//                                    //p[(y + by)*stride + (x + bx)*3]        = (byte)colors[code] ;
//                                    // Read alpha here and append to image
//                                    //imageSourceAlpha[(y+by)*width+x+bx] = (int)(0xFF000000 | (bits << 16) | (bits << 8) | bits);
//                                }
//                            }
//                        }
//
//                        // decode the DXT1 RGB data
//
//                        // two 16 bit encoded colors (red 5 bits, green 6 bits, blue 5 bits)
//                        uint c1packed16 = ((uint)reader.ReadByte()) | ((uint)reader.ReadByte() << 8);
//                        uint c2packed16 = ((uint)reader.ReadByte()) | ((uint)reader.ReadByte() << 8);
//
//                        // separate the R,G,B values
//                        uint color1r = (c1packed16 >> 8) & 0xF8;
//                        uint color1g = (c1packed16 >> 3) & 0xFC;
//                        uint color1b = (c1packed16 << 3) & 0xF8;
//
//                        uint color2r = (c2packed16 >> 8) & 0xF8;
//                        uint color2g = (c2packed16 >> 3) & 0xFC;
//                        uint color2b = (c2packed16 << 3) & 0xF8;
//
//                        uint[] colors = new uint[8]; // color table for all possible codes
//                        // colors 0 and 1 point to the two 16 bit colors we read in
//                        colors[0] = (color1r << 16) | (color1g << 8) | color1b ;
//                        colors[1] = (color2r << 16) | (color2g << 8) | color2b  ;
//
//                        // 2/3 Color1, 1/3 color2
//                        uint colorr = (((color1r<<1) + color2r) / 3) & 0xFF;
//                        uint colorg = (((color1g<<1) + color2g) / 3) & 0xFF;
//                        uint colorb = (((color1b<<1) + color2b) / 3) & 0xFF;
//                        colors[2] = (colorr << 16) | (colorg << 8) | colorb  ;
//
//                        // 2/3 Color2, 1/3 color1
//                        colorr = (((color2r<<1) + color1r) / 3) & 0xFF;
//                        colorg = (((color2g<<1) + color1g) / 3) & 0xFF;
//                        colorb = (((color2b<<1) + color1b) / 3) & 0xFF;
//                        colors[3] = (colorr << 16) | (colorg << 8) | colorb  ;
//
//                        // read in the color code bits, 16 values, each 2 bits long
//                        // then look up the color in the color table we built
//                        int bits1 = reader.ReadByte() + (reader.ReadByte() << 8) + (reader.ReadByte() << 16) + (reader.ReadByte() << 24);
//
//                        for (int by=0;by<4;++by)
//                        {
//                            for (int bx=0;bx<4;++bx)
//                            {
//                                int code = (bits1 >> (((by<<2)+bx)<<1))&0x3;
//
//
//                                p[(y + by)*stride + (x + bx)*3]        = (byte)colors[code] ;
//                                p[(y + by)*stride + (x + bx)*3 + 1]    = (byte)(colors[code] >> 8) ;
//                                p[(y + by)*stride + (x + bx)*3 + 2]    = (byte)(colors[code] >> 16) ;
//
//
//                                //if (colors[0] > colors[1])   // DXT1 Alpha stuff, ignore this
//                                //imageSourceRGB[(y+by)*width+x+bx] = colors[code];
//                                //else  // use DXT1 Alpha codes (ignore this)
//                                //    imageSourceRGB[(y+j)*width+x+i] = colors[code+4];
//
//                            }
//                        }
//                    }
//                    //p += nOffset;
//                }
//
//            }
//
//
//            bitmap.UnlockBits(bmData);
//            return bitmap;
            #endregion
        }


        /// <summary>
        /// DXT1/3/5 -> BGRA8888 byte buffer (length = width*height*4).
        /// Mirrors DecodeDXT's block logic but writes B,G,R,A per pixel so
        /// Avalonia WriteableBitmap can consume it without System.Drawing.
        /// Alpha: DXT1 = always 0xFF; DXT3 = 4-bit per pixel; DXT5 = interpolated.
        /// </summary>
        public static byte[] DecodeDXTToBgra(Stream stream, uint size, int width, int height, int format)
        {
            BinaryReader reader = new BinaryReader(stream);
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            int[] Alpha = new int[16];

            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    // DXT3: 16 explicit 4-bit alpha values, scaled to 0..255
                    if (format == 3)
                    {
                        long abits = reader.ReadInt64();
                        for (int i = 0; i < 16; i++)
                        {
                            Alpha[i] = (int)((abits & 0xf) * 0x11);
                            abits >>= 4;
                        }
                    }
                    // DXT5: two endpoint alphas + 3-bit-per-pixel lookup table
                    else if (format == 5)
                    {
                        int alpha1 = reader.ReadByte();
                        int alpha2 = reader.ReadByte();
                        long abits = (long)reader.ReadUInt32() | ((long)reader.ReadUInt16() << 32);
                        int[] alphas = new int[8];
                        alphas[0] = alpha1;
                        alphas[1] = alpha2;
                        if (alpha1 > alpha2)
                        {
                            alphas[2] = (6 * alpha1 + alpha2) / 7;
                            alphas[3] = (5 * alpha1 + 2 * alpha2) / 7;
                            alphas[4] = (4 * alpha1 + 3 * alpha2) / 7;
                            alphas[5] = (3 * alpha1 + 4 * alpha2) / 7;
                            alphas[6] = (2 * alpha1 + 5 * alpha2) / 7;
                            alphas[7] = (alpha1 + 6 * alpha2) / 7;
                        }
                        else
                        {
                            alphas[2] = (4 * alpha1 + alpha2) / 5;
                            alphas[3] = (3 * alpha1 + 2 * alpha2) / 5;
                            alphas[4] = (2 * alpha1 + 3 * alpha2) / 5;
                            alphas[5] = (1 * alpha1 + 4 * alpha2) / 5;
                            alphas[6] = 0;
                            alphas[7] = 0xff;
                        }
                        for (int i = 0; i < 16; i++)
                        {
                            Alpha[i] = alphas[abits & 7];
                            abits >>= 3;
                        }
                    }

                    uint c1packed16 = ((uint)reader.ReadByte()) | ((uint)reader.ReadByte() << 8);
                    uint c2packed16 = ((uint)reader.ReadByte()) | ((uint)reader.ReadByte() << 8);

                    uint color1r = (c1packed16 >> 8) & 0xF8;
                    uint color1g = (c1packed16 >> 3) & 0xFC;
                    uint color1b = (c1packed16 << 3) & 0xF8;

                    uint color2r = (c2packed16 >> 8) & 0xF8;
                    uint color2g = (c2packed16 >> 3) & 0xFC;
                    uint color2b = (c2packed16 << 3) & 0xF8;

                    uint[] colors = new uint[4];
                    colors[0] = (color1r << 16) | (color1g << 8) | color1b;
                    colors[1] = (color2r << 16) | (color2g << 8) | color2b;

                    // For DXT1 with c1<=c2, the third slot is the midpoint and
                    // the fourth slot is transparent black. For DXT3/5 the alpha
                    // channel carries opacity, so the regular DXT1 transparency
                    // rule is ignored; we always use the 4-color mode.
                    if (format == 1 && c1packed16 <= c2packed16)
                    {
                        uint mr = (color1r + color2r) / 2;
                        uint mg = (color1g + color2g) / 2;
                        uint mb = (color1b + color2b) / 2;
                        colors[2] = (mr << 16) | (mg << 8) | mb;
                        colors[3] = 0; // transparent black
                    }
                    else
                    {
                        uint colorr = (((color1r << 1) + color2r) / 3) & 0xFF;
                        uint colorg = (((color1g << 1) + color2g) / 3) & 0xFF;
                        uint colorb = (((color1b << 1) + color2b) / 3) & 0xFF;
                        colors[2] = (colorr << 16) | (colorg << 8) | colorb;

                        colorr = (((color2r << 1) + color1r) / 3) & 0xFF;
                        colorg = (((color2g << 1) + color1g) / 3) & 0xFF;
                        colorb = (((color2b << 1) + color1b) / 3) & 0xFF;
                        colors[3] = (colorr << 16) | (colorg << 8) | colorb;
                    }

                    int bits1 = reader.ReadByte() + (reader.ReadByte() << 8) + (reader.ReadByte() << 16) + (reader.ReadByte() << 24);

                    for (int by = 0; by < 4; by++)
                    {
                        for (int bx = 0; bx < 4; bx++)
                        {
                            int px = x + bx;
                            int py = y + by;
                            if (px >= width || py >= height) continue;

                            int code = (bits1 >> (((by << 2) + bx) << 1)) & 0x3;
                            uint c = colors[code];
                            int dst = py * stride + px * 4;
                            pixels[dst]     = (byte)c;          // B
                            pixels[dst + 1] = (byte)(c >> 8);   // G
                            pixels[dst + 2] = (byte)(c >> 16);  // R

                            byte a;
                            if (format == 3 || format == 5)
                                a = (byte)Alpha[(by << 2) + bx];
                            else if (format == 1 && c1packed16 <= c2packed16 && code == 3)
                                a = 0; // DXT1 punch-through transparent
                            else
                                a = 0xFF;
                            pixels[dst + 3] = a;
                        }
                    }
                }
            }

            return pixels;
        }


        public static Bitmap DecodeRaw(Stream stream, uint size ,int width, int height, int bitsPerPixel)
        {

            if(size != ( (width*height*bitsPerPixel)/8) )
                throw new Exception("wrong image data size !!");

            //PixelFormat format = PixelFormat.Format24bppRgb;



            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

            BinaryReader reader = new BinaryReader(stream);

            unsafe
            {
                BitmapData bmData = bitmap.LockBits(new Rectangle(0,0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                byte * p = (byte *)(void *)bmData.Scan0;

                for(int y=0; y<height ; y++)
                {
                    for(int x=0; x<width; x++)
                    {
                        byte    a = 0,
                                r = 0,
                                g = 0,
                                b = 0;
                        switch(bitsPerPixel)
                        {
                            case 8:
                                //a = 0xFF;
                                r = reader.ReadByte();
                                g = r;
                                b = r;
                                break;
                            case 24:
                                a = 0xFF;
                                r = reader.ReadByte();
                                g = reader.ReadByte();
                                b = reader.ReadByte();
                                break;
                            case 32:
                                a = reader.ReadByte();
                                r = reader.ReadByte();
                                g = reader.ReadByte();
                                b = reader.ReadByte();
                                break;

                        }

                        int i = 0;
                        //p[(y*bmData.Stride)+x + i++] =  a;
                        p[(y*bmData.Stride)+x*3 + i++] =  r;
                        p[(y*bmData.Stride)+x*3 + i++] =  g;
                        p[(y*bmData.Stride)+x*3 + i++] =  b;

                    }
                }

                bitmap.UnlockBits(bmData);
            }

            return bitmap;

        }



    }
}