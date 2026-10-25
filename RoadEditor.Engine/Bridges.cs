/**************************************************************************
 *   Road Editor for Windows                                                  *
 *   © 2026 GramzeSweatshop (rhiamom@mac.com). Written with Claude        *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Neighborhood bridges, as stored in the NHTR bridge list. Builds on   *
 *   Mootilda's notes in R_NHTR.cs (FixBridgeElevations), which named the *
 *   piece types. Worked out from the only three Maxis bridges shipped    *
 *   (Pleasantview, Riverblossom Hills, Nightlife's Downtown):            *
 *                                                                        *
 *   - A bridge is a straight run of pieces, each two road squares long   *
 *     and one wide, between two road squares that connect to it (their  *
 *     masks include the bridge side).                                    *
 *   - The whole deck is flat, 0.4 above the higher of the two ground     *
 *     points where it meets its roads. The End piece ramps down to the   *
 *     lower road by itself (Riverblossom Hills drops 26 at one end).     *
 *   - Pieces: End, then spans of Arch1 Arch2 Straight.. Arch2 Arch1      *
 *     separated by Pylons, then End. With M pieces between the ends      *
 *     there are floor((M+1)/6) spans -- true of all three bridges.       *
 *   - Rising half of a span uses one texture rotation, falling half the  *
 *     mirror. Straights and pylons are symmetric; Maxis uses either.     *
 *                                                                        *
 *   Piece layout, 165 bytes: the 124-byte road layout (texture field     *
 *   zero, piece u16 at [112], tail zero -- Maxis leaves junk at [119]),  *
 *   then [124] = 3, 20 zero bytes, the piece model's rotation at [145]  *
 *   as two floats (cos, sin of half the angle), and 12 bytes Maxis       *
 *   never cleared (stale text shows up there) -- written as zero.        *
 *************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RoadEditor.Engine
{
    public sealed class Bridge
    {
        /// <summary>True if the bridge runs along X (varying x, fixed y).</summary>
        public bool AlongX;
        /// <summary>First and last squares covered by the bridge, inclusive, along its axis.</summary>
        public int From, To;
        /// <summary>The fixed coordinate across the bridge.</summary>
        public int Across;
        public float Deck;
        /// <summary>The pieces' bytes: the game's own for a loaded bridge, ours for a new one.</summary>
        public List<byte[]> Pieces = new();

        public int Length => To - From + 1;

        public IEnumerable<(int x, int y)> Squares() =>
            Enumerable.Range(From, Length).Select(k => AlongX ? (k, Across) : (Across, k));

        public bool Covers(int x, int y) => AlongX ? (y == Across && x >= From && x <= To)
                                                   : (x == Across && y >= From && y <= To);

        /// <summary>The road square just beyond each end.</summary>
        public ((int x, int y) low, (int x, int y) high) EndRoads() =>
            AlongX ? ((From - 1, Across), (To + 1, Across)) : ((Across, From - 1), (Across, To + 1));
    }

    public static class Bridges
    {
        public const int Size = 165;
        public const int PieceOffset = 112;
        public const ushort End = 0x0AC0, Straight = 0x0AC1, Arch2 = 0x0AC2, Arch1 = 0x0AC3, Pylon = 0x0AC4;

        /// <summary>Shortest bridge Maxis ever builds: End, one full span, End = 7 pieces.</summary>
        public const int MinSquares = 14;

        public static string Name(ushort piece) => piece switch
        {
            End => "End", Straight => "Straight", Arch2 => "Arch2", Arch1 => "Arch1", Pylon => "Pylon",
            0x0AC5 => "Arch3", 0x0AD0 => "Tunnel", _ => $"0x{piece:X}",
        };

        /// <summary>The piece sequence for a bridge of <paramref name="pieces"/> pieces, with its rising/falling half.</summary>
        public static List<(ushort piece, bool falling)> Sequence(int pieces)
        {
            int m = pieces - 2;
            int spans = Math.Max(1, (m + 1) / 6);
            int straights = m - 5 * spans + 1;                  // shared out over the spans, middle ones first
            var perSpan = Enumerable.Repeat(straights / spans, spans).ToArray();
            int extra = straights - perSpan.Sum();
            foreach (int s in Enumerable.Range(0, spans).OrderBy(s => Math.Abs(s - (spans - 1) / 2.0)).Take(extra))
                perSpan[s]++;   // Downtown: 1,2,2,1

            var seq = new List<(ushort, bool)> { (End, false) };
            for (int s = 0; s < spans; s++)
            {
                if (s > 0) seq.Add((Pylon, false));
                seq.Add((Arch1, false)); seq.Add((Arch2, false));
                for (int k = 0; k < perSpan[s]; k++) seq.Add((Straight, false));
                seq.Add((Arch2, true)); seq.Add((Arch1, true));
            }
            seq.Add((End, true));
            return seq;
        }

        // Rising half / falling half texture rotations, by axis (corners TL,TR,BL,BR as U,V).
        private static readonly float[] AlongXRising = { 0,1, 1,1, 1,0, 0,0 };   // R0
        private static readonly float[] AlongXFalling = { 1,0, 0,0, 0,1, 1,1 };  // R3
        private static readonly float[] AlongYRising = { 1,1, 1,0, 0,0, 0,1 };   // R2
        private static readonly float[] AlongYFalling = { 0,0, 0,1, 1,1, 1,0 };  // R1

        /// <summary>Why a bridge of this length can't be built, or null.</summary>
        public static string? WhyNotLength(int squares)
        {
            if (squares < MinSquares) return $"A bridge needs at least {MinSquares} squares between its two roads ({squares} here).";
            if (squares % 2 != 0) return $"Bridge pieces are two squares long, so the span must be an even number of squares ({squares} here) — move one end by a square.";
            return null;
        }

        public static Bridge Build(bool alongX, int from, int to, int across, float deck)
        {
            var b = new Bridge { AlongX = alongX, From = from, To = to, Across = across, Deck = deck };
            var seq = Sequence(b.Length / 2);
            for (int i = 0; i < seq.Count; i++)
            {
                int a = from + 2 * i;   // first square of this piece along the axis
                float x0 = alongX ? a * 10 : across * 10, x1 = alongX ? (a + 2) * 10 : (across + 1) * 10;
                float y0 = alongX ? across * 10 : a * 10, y1 = alongX ? (across + 1) * 10 : (a + 2) * 10;
                float[] uv = alongX ? (seq[i].falling ? AlongXFalling : AlongXRising)
                                    : (seq[i].falling ? AlongYFalling : AlongYRising);
                var ms = new MemoryStream(Size);
                var w = new BinaryWriter(ms);
                w.Write((byte)2);
                w.Write((x0 + x1) / 2); w.Write((y0 + y1) / 2); w.Write(deck);
                w.Write(x0); w.Write(y0); w.Write(x1); w.Write(y1);
                w.Write((byte)3);
                var corners = new (float x, float y)[] { (x0, y0), (x0, y1), (x1, y1), (x1, y0) };
                for (int k = 0; k < 4; k++)
                {
                    w.Write(corners[k].x); w.Write(corners[k].y); w.Write(deck);
                    w.Write(uv[2 * k]); w.Write(uv[2 * k + 1]);
                }
                w.Write((ushort)0);
                w.Write(seq[i].piece);
                w.Write(new byte[10]);
                w.Write((byte)3);
                w.Write(new byte[20]);
                // Model rotation as (cos, sin) of half the angle: along X +90° rising / -90° falling,
                // along Y 0° rising / 180° falling. Bits copied from Maxis, including the -0 rounding.
                uint[] rot = alongX ? (seq[i].falling ? new uint[] { 0x3F3504F4, 0xBF3504F4 } : new uint[] { 0x3F3504F4, 0x3F3504F4 })
                                    : (seq[i].falling ? new uint[] { 0x00000000, 0x3F800000 } : new uint[] { 0x3F800000, 0xB33BBD2E });
                w.Write(rot[0]); w.Write(rot[1]);
                w.Write(new byte[12]);   // Maxis leaves stale memory here
                b.Pieces.Add(ms.ToArray());
            }
            return b;
        }

        /// <summary>Groups the game's bridge list into bridges (touching, collinear pieces).</summary>
        public static List<Bridge> Parse(byte[] raw)
        {
            var pieces = Enumerable.Range(0, raw.Length / Size).Select(i => raw.Skip(i * Size).Take(Size).ToArray()).ToList();
            var boxes = pieces.Select(p => (p,
                x0: (int)Math.Round(BitConverter.ToSingle(p, 13) / 10), y0: (int)Math.Round(BitConverter.ToSingle(p, 17) / 10),
                x1: (int)Math.Round(BitConverter.ToSingle(p, 21) / 10), y1: (int)Math.Round(BitConverter.ToSingle(p, 25) / 10))).ToList();
            var result = new List<Bridge>();
            var left = boxes.ToList();
            while (left.Count > 0)
            {
                var seed = left[0];
                bool alongX = seed.x1 - seed.x0 > seed.y1 - seed.y0;
                int across = alongX ? seed.y0 : seed.x0;
                var group = new List<(byte[] p, int x0, int y0, int x1, int y1)> { seed };
                left.RemoveAt(0);
                bool grew = true;
                while (grew)
                {
                    grew = false;
                    int lo = group.Min(g => alongX ? g.x0 : g.y0), hi = group.Max(g => alongX ? g.x1 : g.y1);
                    foreach (var c in left.ToList())
                    {
                        if ((alongX ? c.y0 : c.x0) != across) continue;
                        int c0 = alongX ? c.x0 : c.y0, c1 = alongX ? c.x1 : c.y1;
                        if (c1 == lo || c0 == hi) { group.Add(c); left.Remove(c); grew = true; }
                    }
                }
                var sorted = group.OrderBy(g => alongX ? g.x0 : g.y0).ToList();
                result.Add(new Bridge
                {
                    AlongX = alongX, Across = across,
                    From = alongX ? sorted[0].x0 : sorted[0].y0,
                    To = (alongX ? sorted[^1].x1 : sorted[^1].y1) - 1,
                    Deck = BitConverter.ToSingle(sorted[0].p, 9),
                    Pieces = group.Select(g => g.p).ToList(),   // the game's order, untouched
                });
            }
            return result;
        }
    }
}
