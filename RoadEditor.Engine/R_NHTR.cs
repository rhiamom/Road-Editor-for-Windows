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
    public class R_NHTR
    {
        const int iTreeLength = 38;     // length of Tree entry
        const int iRoadLength = 124;    // length of Road entry
        const int iBridgeLength = 165;  // length of Bridge entry
        const int iDecoLength = 38;     // length of Decoration entry

        private IPackedFileDescriptor PFD;
        private byte[] bData;
        uint uVers;
        UInt16 u1, u2, u3, u4;
        int iTreeCount, iRoadCount, iBridgeCount, iDecoCount;
        byte[] baTreeArray, baRoadArray, baBridgeArray, baDecoArray;
        byte[] baRemainder;

        public R_NHTR(IPackageFile NBPack, IPackedFileDescriptor Descriptor)
        {
            PFD = Descriptor;
            IPackedFile PF = NBPack.Read(PFD);
            bData = PF.UncompressedData;
            BinaryReader BR = SimPe.Helper.GetBinaryReader(bData);

            uVers = BR.ReadUInt32();
            Debug.Assert((uVers == 0x6F) || (uVers == 0xCB) || (uVers == 0xCE));
            int iIndex = 4;

            u1 = BR.ReadUInt16();
            Debug.Assert(u1 == 8);
            iIndex += 2;

            iTreeCount = BR.ReadInt32();
            int iTreeSize = iTreeCount * iTreeLength;
            baTreeArray = BR.ReadBytes(iTreeSize);
            iIndex += 4 + iTreeSize;

            u2 = BR.ReadUInt16();
            Debug.Assert(u2 == 3);
            iIndex += 2;

            iRoadCount = BR.ReadInt32();
            int iRoadSize = iRoadCount * iRoadLength;
            baRoadArray = BR.ReadBytes(iRoadSize);
            iIndex += 4 + iRoadSize;

            u3 = BR.ReadUInt16();
            Debug.Assert(u3 == 3);
            iIndex += 2;

            iBridgeCount = BR.ReadInt32();
            int iBridgeSize = iBridgeCount * iBridgeLength;
            baBridgeArray = BR.ReadBytes(iBridgeSize);
            iIndex += 4 + iBridgeSize;

            u4 = BR.ReadUInt16();
            Debug.Assert(u4 == 8);
            iIndex += 2;

            iDecoCount = BR.ReadInt32();
            int iDecoSize = iDecoCount * iDecoLength;
            baDecoArray = BR.ReadBytes(iDecoSize);
            iIndex += 4 + iDecoSize;

            Debug.Assert(iIndex == bData.Length);
            if (iIndex < bData.Length)
            {
                int iSize = bData.Length - iIndex;
                baRemainder = BR.ReadBytes(iSize);
            }
        }

        public void Rewrite()
        {
            int iSize = 4;                      // uVers
            iSize += 6 + baTreeArray.Length;    // u1 + tree count & array
            iSize += 6 + baRoadArray.Length;    // u2 + road count & array
            iSize += 6 + baBridgeArray.Length;  // u3 + bridge count & array
            iSize += 6 + baDecoArray.Length;    // u4 + decoration count & array
            if (baRemainder != null)
                iSize += baRemainder.Length;

            byte[] bDataNew = new byte[iSize];
            BinaryWriter BW = new BinaryWriter(new MemoryStream(bDataNew));

            BW.Write(uVers);

            BW.Write(u1);
            BW.Write(iTreeCount);
            BW.Write(baTreeArray);

            BW.Write(u2);
            BW.Write(iRoadCount);
            BW.Write(baRoadArray);

            BW.Write(u3);
            BW.Write(iBridgeCount);
            BW.Write(baBridgeArray);

            BW.Write(u4);
            BW.Write(iDecoCount);
            BW.Write(baDecoArray);

            if (baRemainder != null)
                BW.Write(baRemainder);

            bData = bDataNew;
            PFD.SetUserData(bDataNew, true);
        }

        public byte[] Trees
        {
            get
            {
                return baTreeArray;
            }
            set
            {
                int i = value.Length / iTreeLength;
                if ((i * iTreeLength) != value.Length)
                    throw new InvalidDataException("Invalid tree array");
                iTreeCount = i;
                baTreeArray = value;
                // Rewrite();   // Caller should rewrite once everything is up-to-date
            }
        }

        public byte[] Roads
        {
            get
            {
                return baRoadArray;
            }
            set
            {
                int i = value.Length / iRoadLength;
                if ((i * iRoadLength) != value.Length)
                    throw new InvalidDataException("Invalid road array");
                iRoadCount = i;
                baRoadArray = value;
                // Rewrite();   // Caller should rewrite once everything is up-to-date
            }
        }

        public byte[] Bridges
        {
            get
            {
                return baBridgeArray;
            }
            set
            {
                int i = value.Length / iBridgeLength;
                if ((i * iBridgeLength) != value.Length)
                    throw new InvalidDataException("Invalid bridge array");
                iBridgeCount = i;
                baBridgeArray = value;
                // Rewrite();   // Caller should rewrite once everything is up-to-date
            }
        }

        public byte[] Deco
        {
            get
            {
                return this.baDecoArray;
            }
            set
            {
                int i = value.Length / iDecoLength;
                if ((i * iDecoLength) != value.Length)
                    throw new InvalidDataException("Invalid decoration array");
                iDecoCount = i;
                baDecoArray = value;
                // Rewrite();   // Caller should rewrite once everything is up-to-date
            }
        }

        struct TileOrientation
        {
            public float fX;
            public float fY;
            public float fZ;
            public UInt32 u1;
            public UInt32 u2;

            public TileOrientation(BinaryReader BR)
            {
                fX = BR.ReadSingle();
                Debug.Assert(fX <= iGridSize);
                fY = BR.ReadSingle();
                Debug.Assert(fY <= iGridSize);
                fZ = BR.ReadSingle();
                u1 = BR.ReadUInt32();
                u2 = BR.ReadUInt32();
            }
        }
        private const int iTileSize = 20;
        private const int iTileZIndex = 8;

        private bool FloatEqual(float f1, float f2)
        {
            const float fEpsilon = 5000;  // compare floats within epsilon
            return ((int)(fEpsilon * f1 + 0.5) - (int)(fEpsilon * f2 + 0.5)) == 0;
        }

        private void ReplaceFloat(byte[] bArray, float f, int iIndex)
        {
            byte[] BA = new byte[4];
            BinaryWriter BW = new BinaryWriter(new MemoryStream(BA));
            BW.Write(f);
            Array.Copy(BA, 0, bArray, iIndex, 4);
        }

        private const int iGridSize = 1280; // Obviously in lot-sized tiles, rather than neighborhood-sized tiles
        private const float iSpaceAboveTerrain = .4F;
        private const float TS2WaterLevel = 312.5F;

        public void FixTreeElevations(float[,] fTerrain)
        {
            int iIndex = 0;
            BinaryReader BR = SimPe.Helper.GetBinaryReader(baTreeArray);
            for (int i = 0; i < iTreeCount; i++)
            {
                byte b = BR.ReadByte();
                Debug.Assert(b == 2);
                iIndex++;

                float fX = BR.ReadSingle();
                Debug.Assert(fX <= iGridSize);
                iIndex += 4;
                float fY = BR.ReadSingle();
                Debug.Assert(fY <= iGridSize);
                iIndex += 4;
                float fZ = BR.ReadSingle();
                int X = (int)fX / 10;
                int Y = (int)fY / 10;
                // ToDo: Average of bounding box?
                float Z = fTerrain[Y, X] + iSpaceAboveTerrain;
                if (Z < TS2WaterLevel)
                    Z = TS2WaterLevel;
                ReplaceFloat(baTreeArray, Z, iIndex);
                iIndex += 4;

                float fXTL = BR.ReadSingle();
                Debug.Assert(fXTL <= iGridSize);
                iIndex += 4;
                float fYTR = BR.ReadSingle();
                Debug.Assert(fYTR <= iGridSize);
                iIndex += 4;

                float fXBL = BR.ReadSingle();
                Debug.Assert(fXBL <= iGridSize);
                iIndex += 4;
                float fYBR = BR.ReadSingle();
                Debug.Assert(fYBR <= iGridSize);
                iIndex += 4;

                b = BR.ReadByte();
                Debug.Assert(b == 8);
                iIndex++;

                UInt32 uRotation = BR.ReadUInt32();
                iIndex += 4;

                UInt32 uGUID = BR.ReadUInt32();
                iIndex += 4;

                Debug.Print("Tree: {0,3}  {1,4} {2,4} {3,8}  {4,4} {5,4}  {6,4} {7,4}",
                    i, fX, fY, fZ, fXTL, fYTR, fXBL, fYBR);
            }
            Debug.Assert(iIndex == baTreeArray.Length);
        }

        public void DeleteAllTrees()
        {
            iTreeCount = 0;
            baTreeArray = new byte[0];
        }

        public void FixRoadElevations(float[,] fTerrain)
        {
            // ToDo: Fix stepping problem:
            // Each segment of the road exists independently, with small disconnects between the segments.
            // Really need to ensure that roads are flat across:
            // ie: ((TL == TR) && (BL == BR)) || ((TL == BL) && (TR == BR))
            // This means that we need to determine which direction the road is travelling: T->B or L->R
            // Either this information is in the additional bytes at the end,
            // or we need to know what's happening in the roads nearby.
            // So, keep an array[128,128] of floats which a pointer to the corresponding structure.
            // Then, we can check whether there are other roads nearby and determine direction.
            int iIndex = 0;
            int iZIndex = 0;
            BinaryReader BR = SimPe.Helper.GetBinaryReader(baRoadArray);
            for (int i = 0; i < iRoadCount; i++)
            {
                byte b = BR.ReadByte();
                Debug.Assert(b == 2);
                iIndex++;

                float fX = BR.ReadSingle();
                Debug.Assert(fX <= iGridSize);
                iIndex += 4;
                float fY = BR.ReadSingle();
                Debug.Assert(fY <= iGridSize);
                iIndex += 4;
                float fZ = BR.ReadSingle();
                iZIndex = iIndex;
                iIndex += 4;

                float fXTL = BR.ReadSingle();
                Debug.Assert(fXTL <= iGridSize);
                iIndex += 4;
                float fYTR = BR.ReadSingle();
                Debug.Assert(fYTR <= iGridSize);
                iIndex += 4;

                float fXBL = BR.ReadSingle();
                Debug.Assert(fXBL <= iGridSize);
                iIndex += 4;
                float fYBR = BR.ReadSingle();
                Debug.Assert(fYBR <= iGridSize);
                iIndex += 4;

                Debug.Assert(FloatEqual(fX - 5, fXTL));
                Debug.Assert(FloatEqual(fXTL + 10, fXBL));
                Debug.Assert(FloatEqual(fY - 5, fYTR));
                Debug.Assert(FloatEqual(fYTR + 10, fYBR));

                b = BR.ReadByte();
                Debug.Assert(b == 3);
                iIndex++;

                TileOrientation tTL = new TileOrientation(BR);
                Debug.Assert(FloatEqual(tTL.fX, fXTL));
                Debug.Assert(FloatEqual(tTL.fY, fYTR));
                int X1 = (int)(tTL.fX) / 10;
                int Y1 = (int)(tTL.fY) / 10;
                float Z1 = fTerrain[Y1, X1] + iSpaceAboveTerrain;
                // Debug.Assert(FloatEqual(Z1, tTL.fZ));
                ReplaceFloat(baRoadArray, Z1, iIndex + 8);
                iIndex += iTileSize;

                TileOrientation tTR = new TileOrientation(BR);
                Debug.Assert(FloatEqual(tTR.fX, fXTL));
                Debug.Assert(FloatEqual(tTR.fY, fYBR));
                int X2 = (int)(tTR.fX) / 10;
                int Y2 = (int)(tTR.fY) / 10;
                float Z2 = fTerrain[Y2, X2] + iSpaceAboveTerrain;
                // Debug.Assert(FloatEqual(Z2, tTR.fZ));
                ReplaceFloat(baRoadArray, Z2, iIndex + 8);
                iIndex += iTileSize;

                TileOrientation tBL = new TileOrientation(BR);
                Debug.Assert(FloatEqual(tBL.fX, fXBL));
                Debug.Assert(FloatEqual(tBL.fY, fYBR));
                int X3 = (int)(tBL.fX) / 10;
                int Y3 = (int)(tBL.fY) / 10;
                float Z3 = fTerrain[Y3, X3] + iSpaceAboveTerrain;
                // Debug.Assert(FloatEqual(Z3, tBL.fZ));
                ReplaceFloat(baRoadArray, Z3, iIndex + 8);
                iIndex += iTileSize;

                TileOrientation tBR = new TileOrientation(BR);
                Debug.Assert(FloatEqual(tBR.fX, fXBL));
                Debug.Assert(FloatEqual(tBR.fY, fYTR));
                int X4 = (int)(tBR.fX) / 10;
                int Y4 = (int)(tBR.fY) / 10;
                float Z4 = fTerrain[Y4, X4];
                // Debug.Assert(FloatEqual(Z4, tBR.fZ));
                ReplaceFloat(baRoadArray, Z4, iIndex + 8);
                iIndex += iTileSize;

                // Debug.Assert(FloatEqual(fZ, ((tTL.fZ + tTR.fZ + tBL.fZ + tBR.fZ) / 4)));
                ReplaceFloat(baRoadArray, (Z1 + Z2 + Z3 + Z4) / 4, iZIndex);

                // Roads are (usually) flat along >= 2 edges:
                // Debug.Assert(FloatEqual(tTL.fZ, tTR.fZ) || FloatEqual(tTL.fZ, tBL.fZ));
                // Debug.Assert(FloatEqual(tTR.fZ, tTL.fZ) || FloatEqual(tTR.fZ, tBR.fZ));
                // Debug.Assert(FloatEqual(tBL.fZ, tTL.fZ) || FloatEqual(tBL.fZ, tBR.fZ));
                // Debug.Assert(FloatEqual(tBR.fZ, tTR.fZ) || FloatEqual(tBR.fZ, tTR.fZ));

                b = BR.ReadByte();
                Debug.Assert(b == 0);
                iIndex++;

                UInt16 uTexture = BR.ReadUInt16();
                Debug.Assert((0x03 == uTexture)     // end piece 
                          || (0x0F == uTexture)     // bend 
                          || (0x4B == uTexture)     // straight 
                          || (0x57 == uTexture)     // T-junction 
                          || (0x207 == uTexture)    // cross roads junction 
                );
                iIndex += 2;

                byte[] ba = BR.ReadBytes(11);
                iIndex += 11;
                Debug.Print("Road: {0,3}  {1,4} {2,4} {3,8}  {4,4} {5,4}  {6,4} {7,4}  {8,4} {9,4} {10,8}  {11,4} {12,4} {13,8}  {14,4} {15,4} {16,8}  {17,4} {18,4} {19,8}",
                    i, fX, fY, fZ, fXTL, fYTR, fXBL, fYBR, tTL.fX, tTL.fY, tTL.fZ, tTR.fX, tTR.fY, tTR.fZ, tBL.fX, tBL.fY, tBL.fZ, tBR.fX, tBR.fY, tBR.fZ);
                if ((Z1 < TS2WaterLevel) || (Z2 < TS2WaterLevel) || (Z3 < TS2WaterLevel) || (Z4 < TS2WaterLevel))
                    Debug.Print("Road should be bridge");
            }
            Debug.Assert(iIndex == baRoadArray.Length);
        }

        public void DeleteAllRoads()
        {
            iRoadCount = 0;
            baRoadArray = new byte[0];
        }

        public void FixBridgeElevations(float[,] fTerrain)
        {
            int iIndex = 0;
            int iZIndex = 0;
            int iZ1Index = 0;
            int iZ2Index = 0;
            int iZ3Index = 0;
            int iZ4Index = 0;
            bool bEndPiece = false;
            float fEndPiece = 0.0F;
            float fFirstX = 0.0F;
            float fFirstY = 0.0F;
            float fFirstZ = 0.0F;
            BinaryReader BR = SimPe.Helper.GetBinaryReader(baBridgeArray);
            for (int i = 0; i < iBridgeCount; i++)
            {
                byte b = BR.ReadByte();
                Debug.Assert(b == 2);
                iIndex++;

                float fX = BR.ReadSingle();
                Debug.Assert(fX <= iGridSize);
                iIndex += 4;
                float fY = BR.ReadSingle();
                Debug.Assert(fY <= iGridSize);
                iIndex += 4;
                float fZ = BR.ReadSingle();
                iZIndex = iIndex;
                ReplaceFloat(baBridgeArray, /* fZ */ fEndPiece, iIndex);
                iIndex += 4;

                if (0 == i)
                {
                    fFirstX = fX;
                    fFirstY = fY;
                    fFirstZ = fZ;
                    int X = (int)fX / 10;
                    int Y = (int)fY / 10;
                    fEndPiece = fTerrain[Y, X] + iSpaceAboveTerrain;
                }
                Debug.Assert(FloatEqual(fX, fFirstX) || FloatEqual(fY, fFirstY));
                Debug.Assert(FloatEqual(fZ, fFirstZ));

                float fXTL = BR.ReadSingle();
                Debug.Assert(fXTL <= iGridSize);
                iIndex += 4;
                float fYTR = BR.ReadSingle();
                Debug.Assert(fYTR <= iGridSize);
                iIndex += 4;

                float fXBL = BR.ReadSingle();
                Debug.Assert(fXBL <= iGridSize);
                iIndex += 4;
                float fYBR = BR.ReadSingle();
                Debug.Assert(fYBR <= iGridSize);
                iIndex += 4;

                b = BR.ReadByte();
                Debug.Assert(b == 3);
                iIndex++;

                TileOrientation tTL = new TileOrientation(BR);
                Debug.Assert(FloatEqual(tTL.fX, fXTL));
                Debug.Assert(FloatEqual(tTL.fY, fYTR));
                Debug.Assert(FloatEqual(tTL.fZ, fFirstZ));
                int X1 = (int)(tTL.fX) / 10;
                int Y1 = (int)(tTL.fY) / 10;
                float Z1 = fTerrain[Y1, X1] + iSpaceAboveTerrain;
                // Debug.Assert(FloatEqual(Z1, tTL.fZ));
                ReplaceFloat(baBridgeArray, /* Z1 */ fEndPiece, iIndex + 8);
                iZ1Index += iTileZIndex;
                iIndex += iTileSize;

                TileOrientation tTR = new TileOrientation(BR);
                Debug.Assert(FloatEqual(tTR.fX, fXTL));
                Debug.Assert(FloatEqual(tTR.fY, fYBR));
                Debug.Assert(FloatEqual(tTR.fZ, fFirstZ));
                int X2 = (int)(tTR.fX) / 10;
                int Y2 = (int)(tTR.fY) / 10;
                float Z2 = fTerrain[Y2, X2] + iSpaceAboveTerrain;
                // Debug.Assert(FloatEqual(Z2, tTR.fZ));
                ReplaceFloat(baBridgeArray, /* Z2 */ fEndPiece, iIndex + 8);
                iZ2Index += iTileZIndex;
                iIndex += iTileSize;

                TileOrientation tBL = new TileOrientation(BR);
                Debug.Assert(FloatEqual(tBL.fX, fXBL));
                Debug.Assert(FloatEqual(tBL.fY, fYBR));
                Debug.Assert(FloatEqual(tBL.fZ, fFirstZ));
                int X3 = (int)(tBL.fX) / 10;
                int Y3 = (int)(tBL.fY) / 10;
                float Z3 = fTerrain[Y3, X3] + iSpaceAboveTerrain;
                // Debug.Assert(FloatEqual(Z3, tBL.fZ));
                ReplaceFloat(baBridgeArray, /* Z3 */ fEndPiece, iIndex + 8);
                iZ3Index += iTileZIndex;
                iIndex += iTileSize;

                TileOrientation tBR = new TileOrientation(BR);
                Debug.Assert(FloatEqual(tBR.fX, fXBL));
                Debug.Assert(FloatEqual(tBR.fY, fYTR));
                Debug.Assert(FloatEqual(tBR.fZ, fFirstZ));
                int X4 = (int)(tBR.fX) / 10;
                int Y4 = (int)(tBR.fY) / 10;
                float Z4 = fTerrain[Y4, X4];
                // Debug.Assert(FloatEqual(Z4, tBR.fZ));
                ReplaceFloat(baBridgeArray, /* Z4 */ fEndPiece, iIndex + 8);
                iZ4Index += iTileZIndex;
                iIndex += iTileSize;

                UInt16 uUnk = BR.ReadUInt16();
                iIndex += 2;

                UInt16 uPiece = BR.ReadUInt16();
                Debug.Assert((0x0AC0 == uPiece) // End 
                          || (0x0AC1 == uPiece) // Straight piece (0) 
                          || (0x0AC2 == uPiece) // Arch part 2 
                          || (0x0AC3 == uPiece) // Arch part 1 
                          || (0x0AC4 == uPiece) // Pylon 
                          || (0x0AC5 == uPiece) // Arch part 3 
                          || (0x0AD0 == uPiece) // Tunnel entrance 
                );
                iIndex += 2;

/*                // Bridges are higher than land... How MUCH higher is the question...
                if (0x0AC0 != uPiece)
                {
                    // Debug.Assert(bEndPiece);    // Should be in between two end pieces
                    Z1 = Z2 = Z3 = Z4 = fEndPiece;
                }
                else if (bEndPiece)
                {
                    // This is the end piece
                    bEndPiece = false;
                    // ToDo: Very likely that Z2 or Z3 should be set independently, but which one?
                    Z1 = Z2 = Z3 = fEndPiece;
                }
                else
                {
                    // This is the start piece
                    bEndPiece = true;
                    fEndPiece = (Z1 + Z2 + Z3 + Z4) / 4;
                    // ToDo: Very likely that Z2 or Z3 should be set independently, but which one?
                    Z2 = Z3 = Z4 = fEndPiece;
                }
                ReplaceFloat(baBridgeArray, fEndPiece, iZIndex);
                ReplaceFloat(baBridgeArray, Z1, iZ1Index);
                ReplaceFloat(baBridgeArray, Z2, iZ2Index);
                ReplaceFloat(baBridgeArray, Z3, iZ3Index);
                ReplaceFloat(baBridgeArray, Z4, iZ4Index);
 */
                byte[] ba = BR.ReadBytes(51);
                iIndex += 51;

                Debug.Print("Bridge: {0,3} Type={1:X4}  {2,4} {3,4} {4,8}  {5,4} {6,4}  {7,4} {8,4}  {9,4} {10,4} {11,8}  {12,4} {13,4} {14,8}  {15,4} {16,4} {17,8}  {18,4} {19,4} {20,8}",
                    i, uPiece, fX, fY, fZ, fXTL, fYTR, fXBL, fYBR, tTL.fX, tTL.fY, tTL.fZ, tTR.fX, tTR.fY, tTR.fZ, tBL.fX, tBL.fY, tBL.fZ, tBR.fX, tBR.fY, tBR.fZ);
                if ((Z1 > TS2WaterLevel) && (Z2 > TS2WaterLevel) && (Z3 > TS2WaterLevel) && (Z4 > TS2WaterLevel))
                    Debug.Print("Bridge should be road");
            }
            Debug.Assert(!bEndPiece);   // Should not be in between two end pieces
            Debug.Assert(iIndex == baBridgeArray.Length);
        }

        public void DeleteAllBridges()
        {
            iBridgeCount = 0;
            baBridgeArray = new byte[0];
        }

        public void FixDecoElevations(float[,] fTerrain)
        {
            int iIndex = 0;
            BinaryReader BR = SimPe.Helper.GetBinaryReader(baDecoArray);
            for (int i = 0; i < iDecoCount; i++)
            {
                byte b = BR.ReadByte();
                Debug.Assert(b == 2);
                iIndex++;

                float fX = BR.ReadSingle();
                Debug.Assert(fX <= iGridSize);
                iIndex += 4;
                float fY = BR.ReadSingle();
                Debug.Assert(fY <= iGridSize);
                iIndex += 4;
                float fZ = BR.ReadSingle();
                int X = (int)fX / 10;
                int Y = (int)fY / 10;
                // ToDo: Average of bounding box?
                float Z = fTerrain[Y, X] + iSpaceAboveTerrain;
                if (Z < TS2WaterLevel)
                    Z = TS2WaterLevel;
                ReplaceFloat(baDecoArray, Z, iIndex);
                iIndex += 4;

                float fXTL = BR.ReadSingle();
                Debug.Assert(fXTL <= iGridSize);
                iIndex += 4;
                float fYTR = BR.ReadSingle();
                Debug.Assert(fYTR <= iGridSize);
                iIndex += 4;

                float fXBL = BR.ReadSingle();
                Debug.Assert(fXBL <= iGridSize);
                iIndex += 4;
                float fYBR = BR.ReadSingle();
                Debug.Assert(fYBR <= iGridSize);
                iIndex += 4;

                b = BR.ReadByte();
                Debug.Assert(b == 8);
                iIndex++;

                UInt32 uGUID = BR.ReadUInt32();
                iIndex += 4;

                UInt32 uRotation = BR.ReadUInt32();
                iIndex += 4;

                Debug.Print("Decoration: {0,3}  {1,4} {2,4} {3,8}  {4,4} {5,4}",
                    i, fX, fY, fZ, fXTL, fYTR);
            }
            Debug.Assert(iIndex == baDecoArray.Length);
        }

        public void DeleteAllDeco()
        {
            iDecoCount = 0;
            baDecoArray = new byte[0];
        }

        public void ResizeArrays(int iTop, int iLeft, int iHeight, int iWidth)
        {
            // ToDo: need to move elements in NHTR by iTop, iLeft
            //       and truncate elements at iHeight, iWidth
        }
    }
}