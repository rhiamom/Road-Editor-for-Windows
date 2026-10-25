/***************************************************************************
 *   Copyright (C) 2008 by Mootilda                                        *
 *   http://Mootilda.ModTheSims2.com                                       *
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using SimPe.Interfaces.Files;
using SimPe.Packages;

namespace HoodReplace
{
    public class R_NHTG : R_Terrain
    {
        private IPackedFileDescriptor PFD;
        private byte[] Data;
        private const int iMaxGrid = 128;
        private const int iCSGrid = 256;
        private int iGridWidth;
        private int iGridHeight;
        private int iHeaderSize = 61;

        public R_NHTG(IPackageFile NBPack, IPackedFileDescriptor Descriptor)
        {
            PFD = Descriptor;
            IPackedFile PF = NBPack.Read(PFD);
            Data = PF.UncompressedData;

            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            uint uBlockID = BR.ReadUInt32();
            if (uBlockID != 0xABCB5DA4)
                throw new InvalidDataException("Invalid NHTG: Block ID");
            int iIndex = 4;

            uint uBlockVersion = BR.ReadUInt32();
            Debug.Assert(uBlockVersion == 4);   // ToDo: Determine whether other versions are known and handled correctly
            iIndex += 4;

            // Width and Height may be swapped,
            // but it doesn't matter, since they are constant and equal
            iGridWidth = BR.ReadInt32();
            Debug.Assert((iGridWidth == iMaxGrid) || (iGridWidth == iCSGrid));
            iIndex += 4;

            iGridHeight = BR.ReadInt32();
            Debug.Assert((iGridHeight == iMaxGrid) || (iGridWidth == iCSGrid));
            iIndex += 4;

            float uWaterTableHeight = BR.ReadSingle();
            Debug.Assert(uWaterTableHeight == 312.5);
            iIndex += 4;

            int iStrLen = BR.ReadInt32();
            byte[] bString = BR.ReadBytes(iStrLen);
            string sTerrainType = SimPe.Helper.ToString(bString);
            Debug.Assert((sTerrainType == "Concrete")
                      || (sTerrainType == "Desert")
                      || (sTerrainType == "Dirt")
                      || (sTerrainType == "Temperate")
                      );
            iIndex += 4 + iStrLen;

            uBlockID = BR.ReadUInt32();
            if (uBlockID != 0x6B943B43)
                throw new InvalidDataException("Invalid NHTG: 2DArray Block ID");
            iIndex += 4;

            uBlockVersion = BR.ReadUInt32();
            Debug.Assert(uBlockVersion == 1);   // ToDo: Determine whether other versions are known and handled correctly
            iIndex += 4;

            iStrLen = BR.ReadInt32();
            bString = BR.ReadBytes(iStrLen);
            string sSectionType = SimPe.Helper.ToString(bString);
            if (sSectionType != "c2DArray")
                throw new InvalidDataException("Invalid NHTG: 2DArray Block Name");
            iIndex += 4 + iStrLen;

            // Width and Height may be swapped,
            // but it doesn't matter, since they are constant and equal
            int iWidth = BR.ReadInt32();
            Debug.Assert(iWidth == iGridWidth + 1);
            iIndex += 4;

            int iHeight = BR.ReadInt32();
            Debug.Assert(iHeight == iGridHeight + 1);
            iIndex += 4;

            iHeaderSize = iIndex;   // Size varies, depending upon terrain type
        }

        public override int Width
        {
            get
            {
                return this.iGridWidth;
            }
        }

        public override int Height
        {
            get
            {
                return this.iGridHeight;
            }
        }

        public override byte[] Raw
        {
            get
            {
                return this.Data;
            }
            set
            {
                PFD.SetUserData(value, true);
            }
        }

        public override float[,] GetTerrain(int iTop, int iLeft, int iHeight, int iWidth)
        {
            if ((iTop + iHeight) > iGridHeight)
                throw new IndexOutOfRangeException("Invalid Terrain Height");
            if ((iLeft + iWidth) > iGridWidth)
                throw new IndexOutOfRangeException("Invalid Terrain Width");

            // We are working with vertices, rather than tiles, so convert:
            iHeight++;
            iWidth++;

            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            BR.ReadBytes(iHeaderSize);

            float[,] fTerrainData = new float[iGridWidth + 1, iGridHeight + 1];
            for (int i = 0; i < (iGridWidth + 1); i++)
            {
                for (int j = 0; j < (iGridHeight + 1); j++)
                {
                    fTerrainData[i,j] = BR.ReadSingle();
                }
            }

            float[,] fTerrain = new float[iWidth, iHeight];
            for (int iFromX = iLeft, iToX = 0; iToX < iWidth; iFromX++, iToX++)
            {
                for (int iFromY = iTop, iToY = 0; iToY < iHeight; iFromY++, iToY++)
                {
                    fTerrain[iToX, iToY] = fTerrainData[iFromX, iFromY];
                }
            }
            return fTerrain;
        }

        public override void ReplaceTerrain(int iTop, int iLeft, int iHeight, int iWidth, float[,] fTerrain)
        {
            if ((iTop + iHeight) > iGridHeight)
                throw new IndexOutOfRangeException("Invalid Terrain Height");
            if ((iLeft + iWidth) > iGridWidth)
                throw new IndexOutOfRangeException("Invalid Terrain Width");

            // We are working with vertices, rather than tiles, so convert:
            iHeight++;
            iWidth++;

            byte[] bDataNew = new byte[Data.Length];
            Array.Copy(Data, bDataNew, Data.Length);

            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            BinaryWriter BW = new BinaryWriter(new MemoryStream(bDataNew));
            BW.Write(BR.ReadBytes(iHeaderSize));

            float[,] fTerrainData = new float[iGridWidth + 1, iGridHeight + 1];
            for (int i = 0; i < (iGridWidth + 1); i++)
            {
                for (int j = 0; j < (iGridHeight + 1); j++)
                {
                    fTerrainData[i, j] = BR.ReadSingle();
                }
            }

            for (int iFromX = 0, iToX = iLeft; iFromX < iWidth; iFromX++, iToX++)
            {
                for (int iFromY = 0, iToY = iTop; iFromY < iHeight; iFromY++, iToY++)
                {
                    fTerrainData[iToX, iToY] = fTerrain[iFromX, iFromY];
                }
            }

            for (int i = 0; i < (iGridWidth + 1); i++)
            {
                for (int j = 0; j < (iGridHeight + 1); j++)
                {
                    BW.Write(fTerrainData[i, j]);
                }
            }
            PFD.SetUserData(bDataNew, true);
        }
    }
}