/**************************************************************************
 *   Road Editor for Windows                                                  *
 *   © 2026 GramzeSweatshop (rhiamom@mac.com). Written with Claude        *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   One neighborhood road square, as stored in the NHTR road list.       *
 *   Builds on Mootilda's reading of the format in R_NHTR.cs              *
 *   (FixRoadElevations). What she left open -- which way a piece faces   *
 *   -- turned out to be a 4-bit connection mask in the tail, and the     *
 *   piece type and texture rotation both follow from it. Rebuilding all  *
 *   1,120 road squares in Pleasantview, Strangetown and Veronaville from *
 *   position + mask gives byte-identical entries.                        *
 *                                                                        *
 *   Layout, 124 bytes:                                                   *
 *     [0]      2                                                         *
 *     [1..12]  centre X, Y, Z (X,Y = square*10 + 5)                      *
 *     [13..28] bounds X-5, Y-5, X+5, Y+5                                 *
 *     [29]     3                                                         *
 *     [30..109] four corners, each X, Y, Z, U, V (floats):               *
 *              (X-5,Y-5), (X-5,Y+5), (X+5,Y+5), (X+5,Y-5)               *
 *     [110]    0                                                         *
 *     [111]    u16 piece texture                                         *
 *     [113..123] 00 00 00 00 00 01 MM 00 00 00 00, MM = mask at [119]    *
 *   Mask bits: 1 = x-1, 2 = y+1, 4 = x+1, 8 = y-1.                       *
 *************************************************************************/

using System;
using System.IO;

namespace RoadEditor.Engine
{
    public static class RoadTiles
    {
        public const int Size = 124;
        public const int MaskOffset = 119;
        public const int TextureOffset = 111;
        public const int CornerOffset = 30;
        public const int CornerSize = 20;

        /// <summary>Height of the road surface above the terrain (Mootilda's iSpaceAboveTerrain).</summary>
        public const float SpaceAboveTerrain = 0.4f;

        public const int Left = 1, Down = 2, Right = 4, Up = 8;   // x-1, y+1, x+1, y-1

        /// <summary>The mask bit that points from a square to its neighbour at (dx, dy).</summary>
        public static int BitToward(int dx, int dy) =>
            dx == -1 ? Left : dx == 1 ? Right : dy == 1 ? Down : Up;

        public static readonly (int dx, int dy)[] Neighbours = { (-1, 0), (0, 1), (1, 0), (0, -1) };

        public static ushort TextureFor(int mask)
        {
            int sides = ((mask >> 0) & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1) + ((mask >> 3) & 1);
            switch (sides)
            {
                case 1: return 0x03;    // end piece
                case 3: return 0x57;    // T-junction
                case 4: return 0x207;   // crossroads
                default: return (mask == 0x5 || mask == 0xA) ? (ushort)0x4B : (ushort)0x0F;   // straight : bend
            }
        }

        // Texture rotation per mask, as found in every Maxis hood checked.
        private static int RotationFor(int mask) => mask switch
        {
            0x1 or 0x3 or 0x5 or 0x7 => 0,
            0x2 or 0x6 or 0xA or 0xE or 0xF => 1,
            0x8 or 0x9 or 0xB => 2,
            _ => 3,
        };

        private static readonly float[][] UV =
        {
            new float[] { 0,1, 1,1, 1,0, 0,0 },
            new float[] { 0,0, 0,1, 1,1, 1,0 },
            new float[] { 1,1, 1,0, 0,0, 0,1 },
            new float[] { 1,0, 0,0, 0,1, 1,1 },
        };

        public static (int x, int y) SquareOf(byte[] tile) =>
            ((int)(BitConverter.ToSingle(tile, 1) / 10), (int)(BitConverter.ToSingle(tile, 5) / 10));

        public static int MaskOf(byte[] tile) => tile[MaskOffset];

        /// <summary>A new square. <paramref name="height"/> gives terrain height at grid point (x, y).</summary>
        public static byte[] Build(int x, int y, int mask, Func<int, int, float> height)
        {
            float X = x * 10 + 5, Y = y * 10 + 5;
            var corners = new (float cx, float cy)[] { (X - 5, Y - 5), (X - 5, Y + 5), (X + 5, Y + 5), (X + 5, Y - 5) };
            var z = new float[4];
            for (int k = 0; k < 4; k++)
                z[k] = height((int)corners[k].cx / 10, (int)corners[k].cy / 10) + SpaceAboveTerrain;

            var ms = new MemoryStream(Size);
            var w = new BinaryWriter(ms);
            w.Write((byte)2);
            w.Write(X); w.Write(Y); w.Write((z[0] + z[1] + z[2] + z[3]) / 4);
            w.Write(X - 5); w.Write(Y - 5); w.Write(X + 5); w.Write(Y + 5);
            w.Write((byte)3);
            for (int k = 0; k < 4; k++)
            {
                w.Write(corners[k].cx); w.Write(corners[k].cy); w.Write(z[k]);
                w.Write(0f); w.Write(0f);   // U,V set by Retype
            }
            w.Write((byte)0);
            w.Write((ushort)0);
            w.Write(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0 });
            byte[] tile = ms.ToArray();
            Retype(tile, mask);
            return tile;
        }

        /// <summary>
        /// Changes an existing square's connections in place: mask, piece
        /// texture and texture rotation. Position and heights are left exactly
        /// as the game wrote them.
        /// </summary>
        public static void Retype(byte[] tile, int mask)
        {
            tile[MaskOffset] = (byte)mask;
            BitConverter.GetBytes(TextureFor(mask)).CopyTo(tile, TextureOffset);
            float[] uv = UV[RotationFor(mask)];
            for (int k = 0; k < 4; k++)
            {
                BitConverter.GetBytes(uv[2 * k]).CopyTo(tile, CornerOffset + k * CornerSize + 12);
                BitConverter.GetBytes(uv[2 * k + 1]).CopyTo(tile, CornerOffset + k * CornerSize + 16);
            }
        }
    }
}
