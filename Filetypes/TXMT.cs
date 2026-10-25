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
using System.Collections;
using System.Collections.Specialized;
using DatGen.DBPF;
using DatGen.DBPF.IO;

namespace DatGen.Types.TS2
{



    #region TxmtPropertyCollection
    public class TxmtPropertyCollection : CollectionBase
    {
        public TxmtProperty this[int index]
        {
            get
            {
                //if(index>=List.Count)

                return ((TxmtProperty)(List[index]));
            }
            set { List[index] = value; }
        }

        public int Add(TxmtProperty value)
        {
            return List.Add(value);
        }

        public void Insert(int index, TxmtProperty value)
        {
            List.Insert(index, value);
        }

        public void Remove(TxmtProperty value)
        {
            List.Remove(value);
        }

        public bool Contains(TxmtProperty value)
        {
            return List.Contains(value);
        }

        public TxmtProperty GetByName(string name)
        {
            for(int i=0;i<Count;i++)
            {
                if(this[i].Name == name)
                    return this[i];

            }
            return new TxmtProperty();
        }
        public bool HasProperty(string name)
        {
            for(int i=0;i<Count;i++)
            {
                if(this[i].Name == name)
                    return true;

            }
            return false;
        }

    }
    #endregion

    #region TxmtProperty
    public struct TxmtProperty
    {
        public string Name, Value;
        public TxmtProperty(string name, string value)
        {
            Name = name;
            Value = value;
        }


    }
    #endregion

    #region TXMT
    public class TXMT : Unknown, ILoadable, ISavable, IRawData, ICloneable, IDisposable, IFileTypeFilename
    {
        #region data
        // put private data storage here
        private string m_fileName, m_materialName, m_materialType;
        private StringCollection m_textures;
        private uint m_version, unknown1;
        private TxmtPropertyCollection m_properties;
        byte[] header, sgResource;
        #endregion

        #region constructors
        /// <summary>
        /// Default constructor
        /// </summary>
        public TXMT()
        {
            m_properties    = new TxmtPropertyCollection();
            m_textures        = new StringCollection();

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
                return m_fileName;
            }
            set
            {
                m_fileName = value;
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

        #region public Methods
        public void ReplaceAll(string oldString, string newString)
        {
            for(int i=0; i<m_properties.Count; i++)
            {
                if(m_properties[i].Value==oldString)
                {
                    TxmtProperty tProperty = m_properties[i];
                    tProperty.Value = newString;
                    m_properties[i] = tProperty;
                }
            }
            for(int i=0; i<m_textures.Count; i++)
            {
                if(m_textures[i] == oldString)
                    m_textures[i] = newString;
            }
        }

        #endregion

        #region private Methods

        /// <summary>
        /// Decode file structure fill data structures etc.
        /// </summary>
        /// <returns></returns>
        private bool Decode()
        {
            string s;
            uint i0,ver;


            MemoryStream stream = new MemoryStream(RawData);
            BinaryReader reader = new BinaryReader(stream);

            header = new byte[16];
            header = reader.ReadBytes(16);


            s = reader.ReadString();
            if (s!="cMaterialDefinition") return false;
            unknown1 = reader.ReadUInt32();
            if (unknown1 != 0x49596978) return false;

            ver= reader.ReadUInt32();
            if (ver!=8&&ver!=9&&ver!=10&&ver!=11)
                return false;

            m_version = ver;



            sgResource = new byte[20];
            sgResource = reader.ReadBytes(20);


            m_fileName = reader.ReadString();

            m_materialName = reader.ReadString();

            m_materialType = reader.ReadString();

            i0 = reader.ReadUInt32();

            for ( uint i=0;i<i0;i++)
            {
                string name        = reader.ReadString();
                string value    = reader.ReadString();
                m_properties.Add(new TxmtProperty(name, value));
            }
            if (ver!=8)
            {

                i0=reader.ReadUInt32();
                for (uint i=0;i<i0;i++)
                    m_textures.Add(reader.ReadString());
            }


            return true;
        }

        private void Encode()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(header);

            writer.Write("cMaterialDefinition");

            writer.Write((UInt32)unknown1);

            writer.Write((UInt32)m_version);

            writer.Write(sgResource);
            writer.Write(m_fileName);
            writer.Write(m_materialName);
            writer.Write(m_materialType);

            writer.Write((UInt32)m_properties.Count);
            for (int i=0 ; i<m_properties.Count ; i++)
            {
                writer.Write(m_properties[i].Name);
                writer.Write(m_properties[i].Value);
            }
            if (m_version!=8)
            {
                writer.Write((UInt32)m_textures.Count);
                for (int i=0 ; i<m_textures.Count ; i++)
                {
                    writer.Write( m_textures[i] );
                }
            }

            RawData = stream.ToArray();
        }
        #endregion

        #region public Properties
        public TxmtPropertyCollection Properties
        {
            get
            {
                return m_properties;
            }
        }

        public StringCollection Textures
        {
            get
            {
                return m_textures;
            }
        }
        public string MaterialName
        {
            get
            {
                return m_materialName;
            }
            set
            {
                m_materialName = value;
            }
        }
        #endregion

    }
    #endregion


}
