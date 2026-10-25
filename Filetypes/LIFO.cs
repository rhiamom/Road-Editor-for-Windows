/***************************************************************************
 *   Copyright (C) 2004-2007 by Karol Rybak                                *
 *   http://phervers.ModTheSims.info                                       *
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
using System.IO;
using DatGen.DBPF.IO;



namespace DatGen.Types.TS2
{
    /// <summary>
    /// Summary description for LIFO.
    /// </summary>



    #region LIFO
    public class LIFO : Unknown, ILoadable, ISavable, IRawData, ICloneable, IDisposable, IFileTypeFilename
    {
        #region data
        // put private data storage here
        TXTRMipmap m_Mipmap;
        byte[] header, cSGResource;
        string fileType, m_filename, fTempFileName;
        UInt32 unknown1, modifier, typeIndicator;

        FileStream fTempFileStream;
        #endregion

        #region constructors
        /// <summary>
        /// Default constructor
        /// </summary>
        public LIFO()
        {
            fTempFileName = Path.GetTempFileName();
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
            Encode();
            return base.Save();
        }


        public override void Save(string fileName)
        {
            Encode();
            base.Save(fileName);
        }


        public override void Save(Stream stream)
        {
            Encode();
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
        public string Filename
        {
            get
            {
                return m_filename;
            }
            set
            {
                m_filename = value;
            }
        }
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
        /// Decode file structure fill data structures etc.
        /// </summary>
        /// <returns></returns>
        private bool Decode()
        {
            CreateTempFile();
            BinaryReader reader = new BinaryReader(fTempFileStream);

            m_Mipmap = new TXTRMipmap();


            header = new byte[16];
            header = reader.ReadBytes(16);

            fileType = reader.ReadString();
            if(fileType != "cLevelInfo")
                throw new Exception("Wrong filetype");

            unknown1 = reader.ReadUInt32();

            modifier = reader.ReadUInt32();

            cSGResource = new byte[20];
            cSGResource = reader.ReadBytes(20);

            m_filename = reader.ReadString();

            typeIndicator = reader.ReadUInt32();
            m_Mipmap.m_width = (int)reader.ReadUInt32();
            m_Mipmap.m_height = (int)reader.ReadUInt32();

            m_Mipmap.m_size = reader.ReadUInt32();
            m_Mipmap.m_offset = reader.BaseStream.Position;

            //TODO:fix this LIFO txtr :()
            //m_Mipmap.txtr = this;




            return true;
        }

        public void Encode()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(header);
            writer.Write(fileType);

            writer.Write((UInt32)unknown1);

            writer.Write((UInt32)modifier);

            writer.Write(cSGResource);

            writer.Write(m_filename);

            writer.Write((UInt32)typeIndicator);

            writer.Write((UInt32)m_Mipmap.m_width);
            writer.Write((UInt32)m_Mipmap.m_height);
            writer.Write((UInt32)m_Mipmap.m_size);

            writer.Write(m_Mipmap.RawData);

            RawData = stream.ToArray();

        }
        #endregion

        #region public Properties
        // public data access
        #endregion
    }
    #endregion

}
