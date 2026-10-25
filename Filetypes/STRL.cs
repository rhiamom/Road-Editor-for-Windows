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
using System.Text;
using System.Collections;

using DatGen.DBPF;
using DatGen.DBPF.IO;

namespace DatGen.Types.TS2
{

    #region StrlItemCollection
    public class StrlItemCollection : CollectionBase
    {
        public StrlItem this[int index]
        {
            get
            {
                if(index >= List.Count)
                    return new StrlItem("","");

                return ((StrlItem)(List[index]));
            }
            set { List[index] = value; }
        }

        public int Add(StrlItem value)
        {
            return List.Add(value);
        }

        public void Insert(int index, StrlItem value)
        {
            List.Insert(index, value);
        }

        public void Remove(StrlItem value)
        {
            List.Remove(value);
        }

        public bool Contains(StrlItem value)
        {
            return List.Contains(value);
        }

    }
    #endregion

    #region StrlItem
    public struct StrlItem
    {
        public string Value, Description;
        public StrlItem(string value, string description)
        {
            Value = value;
            Description = description;
        }
    }
    #endregion

    #region StrlLanguageCollection
    public class StrlLanguageCollection : CollectionBase
    {
        public StrlLanguage this[int index]
        {
            get
            {
                //if(index>=List.Count)

                return ((StrlLanguage)(List[index]));
            }
            set { List[index] = value; }
        }

        public int Add(StrlLanguage value)
        {
            return List.Add(value);
        }

        public void Insert(int index, StrlLanguage value)
        {
            List.Insert(index, value);
        }

        public void Remove(StrlLanguage value)
        {
            List.Remove(value);
        }

        public bool Contains(StrlLanguage value)
        {
            return List.Contains(value);
        }
        /// <summary>
        /// Returns index of item with specified code
        /// </summary>
        /// <param name="code">code to find</param>
        /// <returns></returns>
        public int IndexOfCode(UInt16 code)
        {
            for(int i=0; i<List.Count ; i++)
            {
                if( ((StrlLanguage)List[i]).Code == code)
                {
                    return i;
                }
            }
            return -1;
        }

    }
    #endregion

    #region StrlLanguage
    /// <summary>
    /// Always use constructor: StrlLanguage(UInt16 code) !!
    /// </summary>
    public struct StrlLanguage
    {
        private StrlItemCollection m_items;
        private UInt16 m_languageCode;

        /// <summary>
        /// Always use this constructor !
        /// </summary>
        /// <param name="code"></param>
        public StrlLanguage(UInt16 code)
        {
            m_items = new StrlItemCollection();
            m_languageCode = code;
        }

        public StrlItemCollection Items
        {
            get
            {
                return m_items;
            }
            set
            {
                m_items = value;
            }
        }
        public UInt16 Code
        {
            get
            {
                return m_languageCode;
            }
        }

    }
    #endregion

    #region STRL
    public class STRL : Unknown, ILoadable, ISavable, IRawData, ICloneable, IDisposable, IFileTypeFilename
    {
        #region data
        private string m_FileName;
        private StrlLanguageCollection m_languages;
        private uint m_formatCode;
        #endregion

        #region constructors
        /// <summary>
        /// Default constructor
        /// </summary>
        public STRL()
        {
            m_languages = new StrlLanguageCollection();
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
        public string Filename
        {
            get
            {
                return m_FileName;
            }
            set
            {
                m_FileName = value;
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
        /// Decode file structure fill data structures etc.
        /// </summary>
        /// <returns></returns>
        private bool Decode()
        {
            try
            {
                MemoryStream stream = new MemoryStream(RawData);
                BinaryReader reader = new BinaryReader(stream);

                m_FileName = new string(reader.ReadChars(64));

                m_formatCode = reader.ReadUInt16();

                uint nItems = reader.ReadUInt16();

                for(int i=0 ; i<nItems ; i++)
                {
                    UInt16 tLangId        = (UInt16)reader.ReadSByte();

                    string tValue        = "";

                    char tChar = reader.ReadChar();
                    while(tChar != 0)
                    {
                        tValue += new string(tChar, 1);
                        tChar = reader.ReadChar();
                    }

                    string tDescription = "";
                    tChar = reader.ReadChar();
                    while(tChar != 0)
                    {
                        tDescription += new string(tChar, 1);
                        tChar = reader.ReadChar();
                    }


                    int t = m_languages.IndexOfCode(tLangId);
                    if(t==-1)
                    {
                        m_languages.Add(new StrlLanguage(tLangId));
                        t = m_languages.IndexOfCode(tLangId);
                    }
                    m_languages[t].Items.Add(new StrlItem(tValue, tDescription));
                }


                return true;
            }
            catch
            {
                return false;
            }
        }



        #endregion

        #region public Properties
        public StrlLanguageCollection Languages
        {
            get
            {
                return m_languages;
            }
            set
            {
                m_languages = value;
            }
        }
        #endregion
    }
    #endregion



}
