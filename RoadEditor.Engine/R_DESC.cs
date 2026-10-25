/***************************************************************************
 *   Copyright (C) 2006 by Andi8104                                        *
 *   Andi8104@arcor.de                                                     *
 *                                                                         *
 *   Additional programming:                                               *
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
    public class R_DESC
    {
        private bool Test_PrintDebugInfo = false;   // Enable (T) or disable (F) printing of debug information

        const int iMaxLotSize = 6;
        const bool bErrorChecking = true;

        private byte[] Data;
        private IPackedFileDescriptor PFD;
        private ushort uVersion1 = 0;
        private ushort uVersion2 = 0;
        private int iWidthIndex = 4;
        private int iWidth = 0;
        private int iHeightIndex = 8;
        private int iHeight = 0;
        private byte bType = 0xFF;
        private int iU10Index = 13;
        private byte bU10 = 0xFF;
        private byte bU11 = 0xFF;
        private int iLotNameLen = 0;
        private string sLotName = null;
        private int iDescIndex;
        private int iLotDescLen = 0;
        private string sLotDesc = null;
        private const int iMaxGrid = 128;
        private int iTerrainIndex;
        private float[] fTerrain = null;
        private int iTopIndex = 0;
        private int iTop = 0;
        private int iLeftIndex = 0;
        private int iLeft = 0;
        private int iElevIndex = 0;
        private float fElevation = 0;
        private int iLotNumberIndex = 0;
        private int iLotNumber = 0;
        private int iExtraIndex = 0;
        private float fExtra = 0;   // Another copy of the elevation?
        private int iLotClassValueIndex = 0;
        private uint iLotClassValue = 0;
        private int iClassOverrideIndex = 0;
        private byte bClassOverride = 0;
        private byte bOrientation = 0xFF;
        private int iSublotCount = 0;

        public R_DESC(IPackageFile NBPackage, IPackedFileDescriptor LotDescriptor, bool bTestRun)
        {
            PFD = LotDescriptor;
            IPackedFile PF = NBPackage.Read(PFD);
            Data = PF.UncompressedData;
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
            int iIndex = 0;

            uVersion1 = BR.ReadUInt16();
            Debug.Assert( (uVersion1 == 13)
                       || (uVersion1 == 14)                         // Open for Business
                       || (uVersion1 == 18)                         // Apartment Life
            );   // ToDo: Determine whether other versions are known and handled correctly
            iIndex += 2;

            uVersion2 = BR.ReadUInt16();
            Debug.Assert((uVersion2 == 6)
                      || (uVersion2 == 7)                           // Bon Voyage
                      || (uVersion2 == 8)                           // Free Time
                      || (uVersion2 == 11)                          // Apartment Life
            );   // ToDo: Determine whether other versions are known and handled correctly
            iIndex += 2;

            Debug.Assert(iWidthIndex == iIndex);
            iWidthIndex = iIndex;
            iWidth = BR.ReadInt32();
            iIndex += 4;
            if ((Width < 1) || (Width > iMaxLotSize))
                if (bErrorChecking)
                    throw new InvalidDataException("Invalid DESC: Width");

            Debug.Assert(iHeightIndex == iIndex);
            iHeightIndex = iIndex;
            iHeight = BR.ReadInt32();
            iIndex += 4;
            if ((Height < 1) || (Height > iMaxLotSize))
                if (bErrorChecking)
                    throw new InvalidDataException("Invalid DESC: Height");

            bType = BR.ReadByte();
            Debug.Assert((bType == 0)   // Residential
                      || (bType == 1)   // Community
                      || (bType == 2)   // University: Dorm
                      || (bType == 3)   // University: Greek House
                      || (bType == 4)   // University: Secret Society?
                      || (bType == 5)   // Bon Voyage: Hotel
                      || (bType == 6)   // Bon Voyage: Hidden Vacation Lot
                      || (bType == 7)   // FreeTime: Hidden Hobby Lot
                      || (bType == 8)   // Apartment Life: Apartment Base
                      || (bType == 9)   // Apartment Life: Apartment Sublot
                      || (bType == 10)  // Apartment Life: Hidden Lot (Witches)
            );
            iIndex++;

            Debug.Assert(iU10Index == iIndex);
            iU10Index = iIndex;
            bU10 = BR.ReadByte();
            Debug.Assert(bU10 < 0x10);
            iIndex++;

            bU11 = BR.ReadByte();
            Debug.Assert(bU11 < 4);
            iIndex++;

            int iDummy = BR.ReadInt32();
            iIndex += 4;
            // Start unknown:
            // The above could be an int, or two shorts, but it looks like 4 bytes:
            // first two bytes look like bitfields, last two are zero
            // Many lots within a neighborhood have the same 4 bytes
            /*
            byte bTop = 0xF0, bBot = 0x0F;
            byte b = BR.ReadByte(), b1 = (byte)(b & bBot), b2 = (byte)((b & bTop) >> 4);
            Debug.Assert((1 == b1) 
                      || (2 == b1)
                      || (3 == b1)  // = 2 + 1     Pleasantview (base game)
                      || (9 == b1)  // = 8 + 1     Pleasantview (base game): "30 Middle Lane"
                      || (11 == b1) // = 8 + 2 + 1 Veronaville  (base game): "Old Silo Farm"
            );
            Debug.Assert((4 & b1) == 0);
            Debug.Assert((0 == b2)  //             Pleasantview (base game)
                      || (2 == b2)
                      || (3 == b2)
            );
            Debug.Assert((12 & b2) == 0);
            iIndex++;

            b = BR.ReadByte(); b1 = (byte)(b & bBot); b2 = (byte)((b & bTop) >> 4);
            Debug.Assert((0 == b1)  //             Pleasantview (base game)
                      || (1 == b1)
            );
            Debug.Assert((0 == b2)  //             Pleasantview (base game)
                      || (1 == b2)
                      || (2 == b2)
                      || (4 == b2)
            );
            Debug.Assert((8 & b2) == 0);
            iIndex++;

            ushort u = BR.ReadUInt16();
            Debug.Assert(0 == u);
            iIndex += 2;
             */
            // End unknown

            iLotNameLen = BR.ReadInt32();
            sLotName = SimPe.Helper.ToString(BR.ReadBytes(iLotNameLen));
            iIndex += 4 + iLotNameLen;
            if (Test_PrintDebugInfo)
                Debug.Print("Lot Name: {0}", sLotName);

            iDescIndex = iIndex;
            iLotDescLen = BR.ReadInt32();
            sLotDesc = SimPe.Helper.ToString(BR.ReadBytes(iLotDescLen));
            iIndex += 4 + iLotDescLen;
            if (Test_PrintDebugInfo)
                Debug.Print("Lot Desc: {0}", sLotDesc);

            iTerrainIndex = iIndex;
            int iArrayLen = BR.ReadInt32();
            fTerrain = new float[iArrayLen];
            // Is this the relative elevations from NHTG for this lot?
            // Debug.Assert(iArrayLen == ((iHeight + 1) * (iWidth + 1)));
            for (int i = 0; i < iArrayLen; i++)
            {
                fTerrain[i] = BR.ReadSingle();
                // Debug.Assert(0 == fTerrain[i]);
            }
            iIndex += 4 + iArrayLen * 4;

            if ((uVersion2 >= 7))
            {
                // LETools.PrintDataTypes(Data, iIndex);
                iExtraIndex = iIndex;
                fExtra = BR.ReadSingle();
                iIndex += 4;
                if (uVersion2 >= 8)
                {
                    // LETools.PrintDataTypes(Data, iIndex);
                    iDummy = BR.ReadInt32();
                    iIndex += 4;
                    if (uVersion2 == 11)    // Apartment Life
                    {
                        byte bNumberOfApts = BR.ReadByte();
                        iIndex++;

                        int iPrice1 = BR.ReadInt32();
                        iIndex += 4;
                        int iPrice2 = BR.ReadInt32();
                        iIndex += 4;

                        // Note: may be unsigned value, instead of signed.
                        iLotClassValueIndex = iIndex;
                        iLotClassValue = BR.ReadUInt32();
                        iIndex += 4;

                        iClassOverrideIndex = iIndex;
                        bClassOverride = BR.ReadByte();
                        Debug.Assert((0 == bClassOverride) || (1 == bClassOverride));
                        iIndex++;

                        if (bNumberOfApts > 0)
                        {
                            Debug.Assert(iPrice2 <= iPrice1);
                            if (bTestRun)
                                Debug.Print("Lot{0} Apartment price range: {1} - {2} Class {3}({4})", PFD.Instance, iPrice2, iPrice1, bClassOverride, iLotClassValue);
                        }
                        else
                        {
                            Debug.Assert((-1 == iPrice1) || (0 == iPrice1));
                            Debug.Assert((0 == iPrice2) || (32000 == iPrice2));
                            if (bTestRun)
                                Debug.Print("Lot{0} Price range: {1} - {2} Class {3}({4})", PFD.Instance, iPrice2, iPrice1, bClassOverride, iLotClassValue);
                        }
                    }
                }
            }

            iTopIndex = iIndex;
            iTop = BR.ReadInt32();
            iIndex += 4;
            if ((iTop < 0) || (iTop > iMaxGrid))
                if (bErrorChecking)
                    throw new InvalidDataException("Invalid DESC: Top");

            iLeftIndex = iIndex;
            iLeft = BR.ReadInt32();
            iIndex += 4;
            if ((iLeft < 0) || (iLeft > iMaxGrid))
                if (bErrorChecking)
                    throw new InvalidDataException("Invalid DESC: Left");

            iElevIndex = iIndex;
            fElevation = BR.ReadSingle();
            iIndex += 4;

            iLotNumberIndex = iIndex;
            iLotNumber = BR.ReadInt32();
            Debug.Assert(iLotNumber == PFD.Instance);
            iIndex += 4;

            bOrientation = BR.ReadByte();
            Debug.Assert(bOrientation < 4);
            iIndex += 1;

            int iTextureLen = BR.ReadInt32();
            string sTexture = SimPe.Helper.ToString(BR.ReadBytes(iTextureLen));
            iIndex += 4 + sTexture.Length;

            byte b = BR.ReadByte();
            iIndex++;
            // Debug.Assert(0 == b);

            if (14 <= uVersion1)    // Open for Business
            {
                uint uOwner = BR.ReadUInt32();  // Sim info instance number
                iIndex += 4;

                if (bTestRun && (0 != uOwner))
                    Debug.Print("Business owned by {0:X8}", uOwner);
            }
            if (11 == uVersion2)    // Apartment Life
            {
                if (bType == 8)
                {
                    if (Test_PrintDebugInfo)
                        Debug.Print("Lot{0} Apartment Base: {1}", PFD.Instance, LotName);
                }
                else if (bType == 9)
                {
                    if (Test_PrintDebugInfo)
                        Debug.Print("Lot{0} Apartment Sublot: {1}", PFD.Instance, LotName);
                }

                uint uAptBase = BR.ReadUInt32();
                iIndex += 4;
                // Only an apartment sublot should have an associated apartment base:
                if (bType == 9)
                {
                    Debug.Assert(uAptBase != 0);
                    if (Test_PrintDebugInfo)
                        Debug.Print("Base lot: {0}", uAptBase);
                }
                else
                    Debug.Assert(uAptBase == 0);

                for (int i = 0; i < 9; i++)
                {
                    b = BR.ReadByte();
                    iIndex++;
                    Debug.Assert(0 == b);
                }

                iSublotCount = BR.ReadInt32();
                iIndex += 4;
                Debug.Assert(iSublotCount < 5);
                for (int i = 0; i < iSublotCount; i++)
                {
                    uint uAptSublot = BR.ReadUInt32();
                    iIndex += 4;

                    uint uFamily = BR.ReadUInt32();  // Family info instance number
                    iIndex += 4;

                    uint u2 = BR.ReadUInt32();
                    iIndex += 4;

                    uint u3 = BR.ReadUInt32();
                    iIndex += 4;
                    Debug.Assert(u3 == 0);

                    if (Test_PrintDebugInfo)
                        Debug.Print("Occupied lot: {0} {1} {2} {3}", uAptSublot, uFamily, u2, u3);
                }

#if DEBUG
                int iCount = BR.ReadInt32();
                iIndex += 4;
                for (int i = 0; i < iCount; i++)
                {
                    uint uDummy = BR.ReadUInt32();
                    iIndex += 4;
                    // Debug.Assert(0 == uDummy);
                }
                Debug.Assert(Data.Length == iIndex);
#endif
            }

            Debug.Assert(Data.Length == iIndex);
        }

        private void ReplaceUInt(uint iOld, uint iNew, int iIndex)
        {
            byte[] BA = new byte[4];

            Array.Copy(Data, iIndex, BA, 0, 4);
            BinaryReader BR = SimPe.Helper.GetBinaryReader(BA);
            uint iInt = BR.ReadUInt32();
            Debug.Assert(iInt == iOld);

            BinaryWriter BW = new BinaryWriter(new MemoryStream(BA));
            BW.Write(iNew);
            Array.Copy(BA, 0, Data, iIndex, 4);
            PFD.SetUserData(Data, true);
        }

        private void ReplaceInt(int iOld, int iNew, int iIndex)
        {
            byte[] BA = new byte[4];

            Array.Copy(Data, iIndex, BA, 0, 4);
            BinaryReader BR = SimPe.Helper.GetBinaryReader(BA);
            int iInt = BR.ReadInt32();
            Debug.Assert(iInt == iOld);

            BinaryWriter BW = new BinaryWriter(new MemoryStream(BA));
            BW.Write(iNew);
            Array.Copy(BA, 0, Data, iIndex, 4);
            PFD.SetUserData(Data, true);
        }

        public int Width
        {
            get
            {
                return iWidth;
            }
            set
            {
                if (iWidth != value)
                {
                    ReplaceInt(iWidth, value, iWidthIndex);
                    iWidth = value;
                }
            }
        }

        public int Height
        {
            get
            {
                return iHeight;
            }
            set
            {
                if (iHeight != value)
                {
                    ReplaceInt(iHeight, value, iHeightIndex);
                    iHeight = value;
                }
            }
        }

        private void ReplaceFloat(float fOld, float fNew, int iIndex)
        {
            byte[] BA = new byte[4];

            Array.Copy(Data, iIndex, BA, 0, 4);
            BinaryReader BR = SimPe.Helper.GetBinaryReader(BA);
            float fFloat = BR.ReadSingle();
            Debug.Assert(fFloat == fOld);

            BinaryWriter BW = new BinaryWriter(new MemoryStream(BA));
            BW.Write(fNew);
            Array.Copy(BA, 0, Data, iIndex, 4);
            PFD.SetUserData(Data, true);
        }

        public float Elevation
        {
            get
            {
                return fElevation;
            }
            set
            {
                if (fElevation != value)
                {
                    if (fElevation == fExtra)
                    {
                        ReplaceFloat(fExtra, value, iExtraIndex);
                        fExtra = value;
                    }
                    ReplaceFloat(fElevation, value, iElevIndex);
                    fElevation = value;
                }
            }
        }

        public byte LotType
        {
            get
            {
                return bType;
            }
        }

        public bool Occupied
        {
            get
            {
                return (0 != iSublotCount);
            }
        }

        public bool HasClassValue
        {
            get
            {
                return (11 == uVersion2);
            }
        }

        public uint LotClassValue
        {
            get
            {
                // If Apartment Life, this should be set, otherwise 0
                return (iLotClassValue);
            }
            set
            {
                // If not Apartment Life, this should never be called:
                Debug.Assert(0 != iClassOverrideIndex);
                Debug.Assert(0 != iLotClassValueIndex);

                // Only called to override value
                Debug.Assert(bClassOverride == Data[iClassOverrideIndex]);
                Data[iClassOverrideIndex] = bClassOverride = 1;
                ReplaceUInt(iLotClassValue, value, iLotClassValueIndex);
            }
        }

        public int LotClassValueOverride
        {
            get
            {
                // If Apartment Life, this should be 0 or 1, otherwise 0
                return (bClassOverride);
            }
        }

        public void ClearLotClassValue(uint value)
        {
            // If not Apartment Life, this should never be called:
            Debug.Assert(0 != iClassOverrideIndex);
            Debug.Assert(0 != iLotClassValueIndex);

            // Only called to clear value
            Debug.Assert(bClassOverride == Data[iClassOverrideIndex]);
            Data[iClassOverrideIndex] = bClassOverride = 0;
            ReplaceUInt(iLotClassValue, value, iLotClassValueIndex);
        }

        public byte U10
        {
            get
            {
                return bU10;
            }
            set
            {
                Debug.Assert(bU10 == Data[iU10Index]);
                Data[iU10Index] = bU10 = value;
                PFD.SetUserData(Data, true);
            }
        }

        public byte U11
        {
            get
            {
                return bU11;
            }
        }

        public string LotName
        {
            get
            {
                return sLotName;
            }
        }

        // Unused
        public string LotDesc
        {
            get
            {
                return sLotDesc;
            }
            set
            {
                // Only called by the LotCorrupter at the very end of processing
                // Will destroy the indexes, so better not process any more.
                int iLenNew = value.Length;

                // Unfortunately, BW.Write will write out the length as part of a 7BitStr
                // So, we must convert from string to byte[]
                // Looks like SimPE will do this for us:
                byte[] b = SimPe.Helper.ToBytes(value);

                byte[] DataNew = new byte[Data.Length - iLotDescLen + iLenNew];
                BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);
                BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
                BW.Write(BR.ReadBytes(iDescIndex));

                int iLenOld = BR.ReadInt32();
                BW.Write(iLenNew);
                Debug.Assert(iLenOld == iLotDescLen);

                string s = SimPe.Helper.ToString(BR.ReadBytes(iLotDescLen));
                Debug.Print("Replace DESC \"{0}\"", s);
                BW.Write(b);
                BW.Write(BR.ReadBytes(Data.Length - iDescIndex - 4 - iLotDescLen));

                Data = DataNew;
                PFD.SetUserData(Data, true);
            }
        }

        public int Top
        {
            get
            {
                return iTop;
            }
            set
            {
                ReplaceInt(iTop, value, iTopIndex);
                iTop = value;
            }
        }

        public int Left
        {
            get
            {
                return iLeft;
            }
            set
            {
                ReplaceInt(iLeft, value, iLeftIndex);
                iLeft = value;
            }
        }

        public byte Orientation
        {
            get
            {
                return bOrientation;
            }
        }

        private void Swap(ref int X, ref int Y)
        {
            int iTemp = X;
            X = Y;
            Y = iTemp;
        }

        public void CheckTerrain(float[,] fHoodTerrain)
        {
            int iCount = 0;
            int iH = iHeight;
            int iW = iWidth;
            if (0 == (U11 % 2))         // if U11_Left or U11_Right
                Swap(ref iW, ref iH);   //     Meanings of height and width are swapped
            if (1 == (Orientation % 2)) // ir Orientation_Left or Orientation_Right
                Swap(ref iW, ref iH);   //     Meanings of height and width are swapped

            if (Test_PrintDebugInfo)
            {
                for (int i = 0; i <= iW; i++)
                {
                    for (int j = 0; j <= iH; j++)
                        Debug.Print("Hood[{0},{1}] = {2}", i, j, fHoodTerrain[i, j]);
                }
                for (int i = 0; i <= iW; i++)
                {
                    for (int j = 0; j <= iH; j++)
                        Debug.Print("DESC[{0},{1}] = {2}", i, j, fTerrain[i * (iH + 1) + j]);
                }
            }
            for (int i = 0; i <= iW; i++)
            {
                for (int j = 0; j <= iH; j++)
                {
                    if (fTerrain[i * (iH + 1) + j] == fHoodTerrain[i, j] - fElevation)
                        iCount++;
                }
            }
            Debug.Assert(iCount == fTerrain.Length);

            // ToDo: Should also compare major terrain vertices from lot package
        }

        public void FixTerrain(float[,] fHoodTerrain)
        {
            Debug.Fail("R_DESC::FixTerrain should never be called; not yet implemented!");

            int iLengthNew = (iWidth + 1) * (iHeight + 1);
            byte[] DataNew = new byte[Data.Length + (iLengthNew * 4) - (fTerrain.Length * 4)];
            BinaryWriter BW = new BinaryWriter(new MemoryStream(DataNew));
            BinaryReader BR = SimPe.Helper.GetBinaryReader(Data);

            // Copy up to the terrain array
            BW.Write(BR.ReadBytes(iTerrainIndex));

            // Read old length, replace with new length
            int iLengthOld = BR.ReadInt32();
            Debug.Assert(iLengthOld == fTerrain.Length);
            BW.Write(iLengthNew);

            int iW = iWidth;
            int iH = iHeight;
            if (0 == (U11 % 2))         // if U11_Left or U11_Right
                Swap(ref iW, ref iH);   //     Meanings of height and width are swapped
            if (1 == (Orientation % 2)) // ir Orientation_Left or Orientation_Right
                Swap(ref iW, ref iH);   //     Meanings of height and width are swapped

            // Read old array, replace with new array
            for (int i = 0; i < iLengthOld; i++)
            {
                float f = BR.ReadSingle();
            }
            for (int i = 0; i <= iW; i++)
            {
                for (int j = 0; j <= iH; j++)
                {
                    BW.Write(fHoodTerrain[i, j] - fElevation);
                }
            }

            // Copy the rest of the record
            BW.Write(BR.ReadBytes(Data.Length - iTerrainIndex - 4 - iLengthOld * 4));

            Data = DataNew;
            PFD.SetUserData(Data, true);
        }

    }
}
