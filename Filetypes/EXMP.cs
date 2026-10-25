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
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections;
using System.IO;
using System.Text;
using System.Xml;
using System.Diagnostics;

namespace DatGen.Types.TS2
{
    using DatGen.DBPF.IO;

    #region ExmpTypeID
    public enum ExmpTypeID
    {
        TypeUInt32,
        TypeInt32,
        TypeString,
        TypeBoolean,
        TypeSingle
    }
    #endregion

    #region ExmpPropertyCollection
    public class ExmpPropertyCollection : CollectionBase
    {
        public ExmpProperty this[int index]
        {
            get
            {
                //if(index>=List.Count)

                return ((ExmpProperty)(List[index]));
            }
            set
            {
                List[index] = value;
            }
        }

        public int Add(ExmpProperty value)
        {
            return List.Add(value);
        }

        public void Insert(int index, ExmpProperty value)
        {
            List.Insert(index, value);
        }

        public void Remove(ExmpProperty value)
        {
            List.Remove(value);
        }

        public bool Contains(ExmpProperty value)
        {
            return List.Contains(value);
        }

        public ExmpProperty GetByName(string name)
        {
            for(int i=0;i<Count;i++)
            {
                if(this[i].Name == name)
                    return this[i];

            }
            return new ExmpProperty();
        }

        public int IndexOfName(string name)
        {
            for(int i=0;i<Count;i++)
            {
                if(this[i].Name == name)
                    return i;

            }
            return -1;
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

    #region ExmpProperty
    public class ExmpProperty
    {
        public ExmpTypeID Type;
        public string Name;
        public string Description;

        public string StringValue;
        public UInt32 UInt32Value;
        public float  SingleValue;
        public bool   BooleanValue;
        public int    Int32Value;

    }
    #endregion



    /// <summary>
    /// Class for binary and XML Exemplars
    /// </summary>
    #region EXMP
    public class EXMP : Unknown, ILoadable, ISavable, IRawData, ICloneable, IDisposable
    {
        #region data
        // put private data storage here

        public ExmpPropertyCollection Properties;
        private uint unknown1;

        #endregion

        #region constructors
        /// <summary>
        /// Default constructor
        /// </summary>
        public EXMP()
        {
            Properties = new ExmpPropertyCollection();
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

        private string DecString(string text)
        {
            text = text.Replace(";amp;"," & ");
            return text;
        }
        private string EncString(string text)
        {
            text = text.Replace(" & ", ";amp;");
            return text;
        }

        private string mToString(byte[] val)
        {
            System.IO.BinaryReader br = new System.IO.BinaryReader(new System.IO.MemoryStream(val));
            try
            {
                string ret = "";
                while (br.PeekChar()!=-1) ret+=br.ReadChar();
                return ret;
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// Decode file structure fill data structures etc.
        /// </summary>
        /// <returns></returns>
        private void Decode()
        {
            MemoryStream stream = new MemoryStream(RawData);
            BinaryReader reader = new BinaryReader(stream);
//    0xCBE7505E
            uint typeid = reader.ReadUInt32();
            if(typeid == 0xcbe750e0)
            {
                // Decode binary
                unknown1 = reader.ReadUInt16();
                uint numProperties = reader.ReadUInt32();
                for(uint i=0; i<numProperties ; i++)
                {
                    ExmpProperty tProperty = new ExmpProperty();

                    uint dataType = reader.ReadUInt32();
                    uint length = reader.ReadUInt32();
                    tProperty.Name = new string(reader.ReadChars((int)length));
                    switch(dataType)
                    {
                        case 0x0b8bea18:
                            // string
                            int strlen = reader.ReadInt32();


                            // Fixed to work
                            byte[] val;
                            val = reader.ReadBytes(strlen);
                            tProperty.StringValue = mToString(val);

                            //tProperty.StringValue = new string(reader.ReadChars(strlen)); this doesn't work well :(
                            tProperty.Type = ExmpTypeID.TypeString;
                            break;
                        case 0xeb61e4f7:
                            //uint
                            tProperty.UInt32Value = reader.ReadUInt32();
                            tProperty.Type = ExmpTypeID.TypeUInt32;
                            break;
                        case 0xabc78708:
                            //real32() 4 bytes
                            tProperty.SingleValue = reader.ReadSingle();
                            tProperty.Type = ExmpTypeID.TypeSingle;
                            break;
                        case 0xCBA908E1:
                            //boolean = 1 byte,
                            tProperty.BooleanValue = reader.ReadBoolean();
                            tProperty.Type = ExmpTypeID.TypeBoolean;
                            break;
                        case 0x0C264712:
                            //int(0C264712) = 4 bytes
                            tProperty.Int32Value = reader.ReadInt32();
                            tProperty.Type = ExmpTypeID.TypeInt32;
                            break;
                    }
                    Properties.Add(tProperty);
                }
            }
            else
            {
                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                string xml = new string(reader.ReadChars((int)reader.BaseStream.Length));

                xml = EncString(xml);

                XmlTextReader parser = new XmlTextReader(xml, XmlNodeType.Document, null);
                while (parser.Read())
                {
                    if(parser.NodeType == XmlNodeType.Element)
                    {
                        if(parser.Name == "cGZPropertySetString")
                        {
                            // Do nothing
                        }
                        else
                        {
                            ExmpProperty tProperty = new ExmpProperty();

                            tProperty.Name = parser.GetAttribute("key");

                            string ValueType = parser.Name;
                            parser.Read();
                            string Value = parser.Value;

                            switch(ValueType)
                            {
                                case "AnyString":
                                    tProperty.StringValue        = DecString(Value);
                                    tProperty.Type = ExmpTypeID.TypeString;
                                    break;
                                case "AnyBoolean":
                                    tProperty.BooleanValue    = (Value=="true"?true:false);
                                    tProperty.Type = ExmpTypeID.TypeBoolean;
                                    break;
                                case "AnyUint32":
                                    tProperty.UInt32Value        = Convert.ToUInt32(Value);
                                    tProperty.Type = ExmpTypeID.TypeUInt32;
                                    break;
                                case "AnySint32":
                                    tProperty.Int32Value        = Convert.ToInt32(Value);
                                    tProperty.Type = ExmpTypeID.TypeInt32;
                                    break;
                                case "AnyFloat32":
                                    tProperty.SingleValue = Convert.ToSingle(Value.Replace(".",","));
                                    tProperty.Type = ExmpTypeID.TypeSingle;
                                    break;
                            }
                            Properties.Add(tProperty);
                        }
                    }
                }
            }
        }

        private void Encode()
        {

            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            //    0xCBE7505E

            writer.Write((UInt32)0xcbe750e0);
            writer.Write((UInt16)unknown1);

            writer.Write((UInt32)Properties.Count);

            for(uint i=0; i<Properties.Count ; i++)
            {
                ExmpProperty tProperty = Properties[(int)i];

                switch(tProperty.Type)
                {
                    case ExmpTypeID.TypeString:

                        // string
                        writer.Write((UInt32)0x0b8bea18);
                        writer.Write((UInt32)tProperty.Name.Length);
                        writer.Write(tProperty.Name.ToCharArray());

                        writer.Write((UInt32)tProperty.StringValue.Length);
                        writer.Write(tProperty.StringValue.ToCharArray());

                        break;
                    case ExmpTypeID.TypeUInt32:
                        //uint
                        writer.Write((UInt32)0xeb61e4f7);
                        writer.Write((UInt32)tProperty.Name.Length);
                        writer.Write(tProperty.Name.ToCharArray());

                        writer.Write((UInt32)tProperty.UInt32Value);

                        break;
                    case ExmpTypeID.TypeSingle:
                        //real32() 4 bytes
                        writer.Write((UInt32)0xabc78708);
                        writer.Write((UInt32)tProperty.Name.Length);
                        writer.Write(tProperty.Name.ToCharArray());

                        writer.Write((float)tProperty.SingleValue);

                        break;
                    case ExmpTypeID.TypeBoolean:
                        //boolean = 1 byte,
                        writer.Write((UInt32)0xCBA908E1);
                        writer.Write((UInt32)tProperty.Name.Length);
                        writer.Write(tProperty.Name.ToCharArray());

                        writer.Write((bool)tProperty.BooleanValue);

                        break;
                    case ExmpTypeID.TypeInt32:
                        //int(0C264712) = 4 bytes
                        writer.Write((UInt32)0x0C264712);
                        writer.Write((UInt32)tProperty.Name.Length);
                        writer.Write(tProperty.Name.ToCharArray());

                        writer.Write((Int32)tProperty.Int32Value);



                        break;
                }

            }

            RawData = stream.ToArray();

        }
        #endregion

        #region public Properties
        // public data access
        #endregion
    }
    #endregion


}
