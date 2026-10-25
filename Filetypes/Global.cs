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
using DatGen.Types.TS2;
using DatGen.DBPF.IO;

namespace DatGen.Types.TS2
{
    #region Global
    public class Global : Unknown, ILoadable, ISavable, IRawData, ICloneable, IDisposable
    {
        #region data
        public string FileName;
        public string Data;
        // put private data storage here
        #endregion

        #region constructors
        /// <summary>
        /// Default constructor
        /// </summary>
        public Global()
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
            MemoryStream stream = new MemoryStream(RawData);
            BinaryReader reader = new BinaryReader(stream);

            FileName = new string(reader.ReadChars(64));

            try
            {
                Data = reader.ReadString();
            }
            catch
            {
            }

            return true;
        }
        #endregion

        #region public Properties
        // public data access
        #endregion
    }
    #endregion
}
