/**************************************************************************
 *   Road Editor for Windows                                                  *
 *   © 2026 GramzeSweatshop (rhiamom@mac.com). Written with Claude        *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   One neighborhood opened for road editing: its terrain, its lots and  *
 *   its road squares. Reads and writes through Mootilda's handlers       *
 *   (R_NHTG, R_NHTR, R_DESC), unchanged.                                 *
 *                                                                        *
 *   Squares the game already wrote keep their bytes; a square only gets  *
 *   rebuilt when it is new, and a neighbour of an edited square only has *
 *   its connections retyped. So a road that meets a bridge keeps the     *
 *   connection bit it had toward that bridge.                            *
 *                                                                        *
 *   Lots: a lot's rectangle includes the strip of road along its front,  *
 *   and its orientation says which edge that is (0 = row x=Top, 2 = row  *
 *   x=Top+Width-1, 3 = column y=Left, 1 = column y=Left+Height-1). The   *
 *   game only lets a lot sit where that strip is road, so a front road   *
 *   can't be erased (unless it is on ground no road should be on), and   *
 *   the rest of the lot takes no road at all. Fronts obey the water and  *
 *   steepness limits like any other square.                              *
 *************************************************************************/

using HoodReplace;
using SimPe.Interfaces.Files;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RoadEditor.Engine
{
    public enum Ground { Open, Water, Steep, Edge, Lot, LotFront, Bridge }

    public sealed class LotInfo
    {
        public string Name = "";
        public int Top, Left, Width, Height, Orientation;
        public bool IsFront(int x, int y) => Orientation switch
        {
            0 => x == Top,
            2 => x == Top + Width - 1,
            3 => y == Left,
            _ => y == Left + Height - 1,
        };
        public bool Contains(int x, int y) =>
            x >= Top && x < Top + Width && y >= Left && y < Left + Height;
    }

    public sealed class RoadDocument
    {
        public const uint NHTG = 0xABCB5DA4, NHTR = 0xABD0DC63, LotDescription = 0x0BF999E7;
        public const float WaterLevel = 312.5f;

        /// <summary>
        /// Largest rise, corner to corner, allowed across one road square. The
        /// steepest square under a Maxis road (Veronaville) rises 9.19.
        /// </summary>
        public const float MaxRise = 10f;

        /// <summary>
        /// Largest height difference between a bridge's two ends. The deck is
        /// level with the higher bank and the End piece ramps down to the other;
        /// a 45 drop made a tall wedge in-game. Maxis's bridges drop 0.4, 5 and 26.
        /// </summary>
        public const float MaxBridgeDrop = 10f;

        /// <summary>Tree entry size in the NHTR tree list (Mootilda's iTreeLength).</summary>
        public const int TreeSize = 38;

        public string Path { get; }
        /// <summary>Terrain grid size in points; squares run 0..Width-2 by 0..Height-2.</summary>
        public int Width { get; }
        public int Height { get; }
        public float[,] Terrain { get; }          // [y, x]
        public List<LotInfo> Lots { get; } = new();

        private readonly List<(int x, int y)> _order = new();
        private Dictionary<(int x, int y), byte[]> _roads = new();
        private List<Bridge> _bridges = new();
        private byte[] _originalBridgeBytes = Array.Empty<byte>();
        private bool _bridgesChanged;
        private List<byte[]> _trees = new();
        private byte[] _originalTreeBytes = Array.Empty<byte>();
        private bool _treesChanged;
        private readonly Stack<(Dictionary<(int, int), byte[]> roads, List<Bridge> bridges, bool changed, List<byte[]> trees, bool treesChanged)> _undo = new();

        /// <summary>Trees removed since the last save.</summary>
        public int TreesRemoved => _originalTreeBytes.Length / TreeSize - _trees.Count;
        private DateTime _loadedWriteTime;

        public bool Dirty { get; private set; }
        public int RoadCount => _roads.Count;
        public int BridgeCount => _bridges.Count;
        public IReadOnlyList<Bridge> BridgeList => _bridges;
        public bool CanUndo => _undo.Count > 0;

        public RoadDocument(string path)
        {
            Path = path;
            _loadedWriteTime = File.GetLastWriteTimeUtc(path);
            var pkg = SimPe.Packages.File.LoadFromFile(path);
            try
            {
                var tg = pkg.FindFile(NHTG, 0, 0xFFFFFFFF, 0)
                         ?? throw new InvalidDataException("This package has no neighborhood terrain.");
                var nhtg = new R_NHTG(pkg, tg);
                Width = nhtg.Width;
                Height = nhtg.Height;
                Terrain = nhtg.GetTerrain(0, 0, Height, Width);

                var tr = pkg.FindFile(NHTR, 0, 0xFFFFFFFF, 0)
                         ?? throw new InvalidDataException("This package has no road list.");
                var nhtr = new R_NHTR(pkg, tr);
                byte[] raw = nhtr.Roads;
                for (int i = 0; i < raw.Length / RoadTiles.Size; i++)
                {
                    byte[] tile = new byte[RoadTiles.Size];
                    Array.Copy(raw, i * RoadTiles.Size, tile, 0, RoadTiles.Size);
                    var sq = RoadTiles.SquareOf(tile);
                    if (_roads.ContainsKey(sq)) continue;   // never seen, but don't double up
                    _roads[sq] = tile;
                    _order.Add(sq);
                }
                _originalBridgeBytes = nhtr.Bridges;
                _originalTreeBytes = nhtr.Trees;
                for (int i = 0; i < _originalTreeBytes.Length / TreeSize; i++)
                    _trees.Add(_originalTreeBytes.Skip(i * TreeSize).Take(TreeSize).ToArray());
                _bridges = Bridges.Parse(_originalBridgeBytes);

                foreach (IPackedFileDescriptor pfd in pkg.FindFiles(LotDescription))
                {
                    var d = new R_DESC(pkg, pfd, true);
                    if (d.LotType == 9) continue;   // apartment sublots share the base lot's ground
                    var lot = new LotInfo
                    {
                        Name = d.LotName ?? "", Top = d.Top, Left = d.Left,
                        Width = d.Width, Height = d.Height, Orientation = d.Orientation,
                    };
                    // Hidden lots (vacation, hobby, witch) sit outside the map; skip anything off-grid.
                    if (lot.Top < 0 || lot.Left < 0 || lot.Top + lot.Width > Width - 1 || lot.Left + lot.Height > Height - 1)
                        continue;
                    Lots.Add(lot);
                }
            }
            finally
            {
                pkg.ForgetUpdate();
                pkg.Close();
            }
        }

        // ---- reading -------------------------------------------------------

        public bool IsRoad(int x, int y) => _roads.ContainsKey((x, y));
        public int MaskAt(int x, int y) => _roads.TryGetValue((x, y), out var t) ? RoadTiles.MaskOf(t) : 0;
        public IEnumerable<(int x, int y)> Roads => _roads.Keys;

        public bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < Width - 1 && y < Height - 1;

        public float Rise(int x, int y)
        {
            float a = Terrain[y, x], b = Terrain[y + 1, x], c = Terrain[y, x + 1], d = Terrain[y + 1, x + 1];
            return Math.Max(Math.Max(a, b), Math.Max(c, d)) - Math.Min(Math.Min(a, b), Math.Min(c, d));
        }

        public float LowestCorner(int x, int y) =>
            Math.Min(Math.Min(Terrain[y, x], Terrain[y + 1, x]), Math.Min(Terrain[y, x + 1], Terrain[y + 1, x + 1]));

        public LotInfo? LotAt(int x, int y) => Lots.FirstOrDefault(l => l.Contains(x, y));

        public Ground GroundAt(int x, int y)
        {
            if (x < 1 || y < 1 || x >= Width - 2 || y >= Height - 2) return Ground.Edge;
            if (BridgeAt(x, y) != null) return Ground.Bridge;
            var lot = LotAt(x, y);
            if (lot != null) return lot.IsFront(x, y) ? Ground.LotFront : Ground.Lot;
            return Terrain_(x, y);
        }

        // What the ground itself is like, ignoring lots.
        private Ground Terrain_(int x, int y)
        {
            if (LowestCorner(x, y) < WaterLevel) return Ground.Water;
            if (Rise(x, y) > MaxRise) return Ground.Steep;
            return Ground.Open;
        }

        /// <summary>True if the ground itself (lot or not) would refuse a road.</summary>
        public bool GroundRefuses(int x, int y) => Terrain_(x, y) != Ground.Open;

        /// <summary>Why a road can't go here, or null if it can.</summary>
        public string? WhyNotDraw(int x, int y)
        {
            var ground = GroundAt(x, y);
            if (ground == Ground.Edge) return "Too close to the edge of the neighborhood.";
            if (ground == Ground.Bridge) return "A bridge is here.";
            if (ground == Ground.Lot) return $"Inside the lot \"{LotAt(x, y)!.Name}\".";
            // A lot's front gets no exemption: a road on a cliff looks broken whether or not a lot uses it.
            string where = ground == Ground.LotFront ? $" (front of the lot \"{LotAt(x, y)!.Name}\")" : "";
            switch (Terrain_(x, y))
            {
                case Ground.Water: return "Water — roads over water need a bridge, which this version can't build." + where;
                case Ground.Steep: return $"Too steep for a road (rises {Rise(x, y):F1}; the limit is {MaxRise:F0})." + where;
                default: return null;
            }
        }

        /// <summary>Why this road square can't be removed, or null if it can.</summary>
        public string? WhyNotErase(int x, int y)
        {
            if (BridgesEndingAt(x, y).Any())
                return "A bridge ends on this road; erase the bridge first.";
            var lot = LotAt(x, y);
            // A lot's front road is protected -- unless it sits on ground no road should be on.
            if (lot != null && lot.IsFront(x, y) && !GroundRefuses(x, y))
                return $"This road is the front of the lot \"{lot.Name}\"; the lot needs it.";
            return null;
        }

        /// <summary>Road squares with no connection at all. The game has no piece for these.</summary>
        public List<(int x, int y)> Isolated() =>
            _roads.Where(kv => RoadTiles.MaskOf(kv.Value) == 0).Select(kv => kv.Key).ToList();

        // ---- editing -------------------------------------------------------

        /// <summary>Call once before each stroke so Undo takes back the whole stroke.</summary>
        public void BeginStroke() =>
            _undo.Push((_roads.ToDictionary(kv => kv.Key, kv => (byte[])kv.Value.Clone()), _bridges.ToList(), _bridgesChanged, _trees.ToList(), _treesChanged));

        /// <summary>Drops the snapshot BeginStroke took, when the stroke ended up changing nothing.</summary>
        public void DiscardStroke() { if (_undo.Count > 0) _undo.Pop(); }

        public bool Undo()
        {
            if (_undo.Count == 0) return false;
            var (roads, bridges, changed, trees, treesChanged) = _undo.Pop();
            _roads = roads.ToDictionary(kv => ((int, int))kv.Key, kv => kv.Value);
            _bridges = bridges;
            _bridgesChanged = changed;
            _trees = trees;
            _treesChanged = treesChanged;
            Dirty = true;
            return true;
        }

        /// <summary>Adds a road square. Returns null on success, or the reason it was refused.</summary>
        public string? Draw(int x, int y)
        {
            if (!InGrid(x, y)) return "Outside the neighborhood.";
            if (IsRoad(x, y)) return null;
            string? why = WhyNotDraw(x, y);
            if (why != null) return why;

            int mask = 0;
            foreach (var (dx, dy) in RoadTiles.Neighbours)
                if (IsRoad(x + dx, y + dy)) mask |= RoadTiles.BitToward(dx, dy);
            foreach (var (_, dx, dy) in BridgesEndingAt(x, y))
                mask |= RoadTiles.BitToward(dx, dy);
            _roads[(x, y)] = RoadTiles.Build(x, y, mask, (gx, gy) => Terrain[gy, gx]);
            ClearTrees(x, y, x, y);
            if (!_order.Contains((x, y))) _order.Add((x, y));

            foreach (var (dx, dy) in RoadTiles.Neighbours)
                if (_roads.TryGetValue((x + dx, y + dy), out var n))
                    RoadTiles.Retype(n, RoadTiles.MaskOf(n) | RoadTiles.BitToward(-dx, -dy));
            Dirty = true;
            return null;
        }

        /// <summary>Removes a road square. Returns null on success, or the reason it was refused.</summary>
        public string? Erase(int x, int y)
        {
            var bridge = BridgeAt(x, y);
            if (bridge != null) { RemoveBridge(bridge); return null; }
            if (!IsRoad(x, y)) return null;
            string? why = WhyNotErase(x, y);
            if (why != null) return why;

            _roads.Remove((x, y));
            foreach (var (dx, dy) in RoadTiles.Neighbours)
                if (_roads.TryGetValue((x + dx, y + dy), out var n))
                    RoadTiles.Retype(n, RoadTiles.MaskOf(n) & ~RoadTiles.BitToward(-dx, -dy));
            Dirty = true;
            return null;
        }

        // ---- bridges -------------------------------------------------------

        public Bridge? BridgeAt(int x, int y) => _bridges.FirstOrDefault(b => b.Covers(x, y));

        /// <summary>Bridges whose end meets the road square (x, y), with the direction toward each.</summary>
        public IEnumerable<(Bridge bridge, int dx, int dy)> BridgesEndingAt(int x, int y)
        {
            foreach (var b in _bridges)
            {
                var (low, high) = b.EndRoads();
                if (low == (x, y)) yield return (b, b.AlongX ? 1 : 0, b.AlongX ? 0 : 1);
                if (high == (x, y)) yield return (b, b.AlongX ? -1 : 0, b.AlongX ? 0 : -1);
            }
        }

        /// <summary>
        /// Builds a bridge between two road squares (a and b), which must be in
        /// the same row or column. The bridge covers the squares between them;
        /// either end square gets a road if it hasn't one. Null on success, or
        /// the reason it was refused (nothing is changed then).
        /// </summary>
        public string? AddBridge((int x, int y) a, (int x, int y) b)
        {
            if (a.x != b.x && a.y != b.y) return "A bridge must run straight: pick two ends in the same row or column.";
            bool alongX = a.y == b.y;
            int lo = alongX ? Math.Min(a.x, b.x) : Math.Min(a.y, b.y), hi = alongX ? Math.Max(a.x, b.x) : Math.Max(a.y, b.y);
            int across = alongX ? a.y : a.x;
            int from = lo + 1, to = hi - 1;
            string? why = Bridges.WhyNotLength(to - from + 1);
            if (why != null) return why;

            for (int k = from; k <= to; k++)
            {
                var (x, y) = alongX ? (k, across) : (across, k);
                if (!InGrid(x, y) || GroundAt(x, y) == Ground.Edge) return "The bridge would run off the edge of the neighborhood.";
                if (IsRoad(x, y)) return $"There's already a road under the bridge's path at {x}, {y}.";
                if (BridgeAt(x, y) != null) return "The bridge would cross another bridge.";
                if (LotAt(x, y) is { } lot) return $"The bridge would cross the lot \"{lot.Name}\".";
            }
            foreach (var end in new[] { a, b })
                if (!IsRoad(end.x, end.y) && WhyNotDraw(end.x, end.y) is { } no)
                    return $"The bridge can't end at {end.x}, {end.y}: {no}";

            // Deck: 0.4 above the higher of the two lines where the bridge meets its roads.
            float H(int px, int py) => Terrain[py, px];
            float deck = alongX
                ? new[] { H(from, across), H(from, across + 1), H(to + 1, across), H(to + 1, across + 1) }.Max()
                : new[] { H(across, from), H(across + 1, from), H(across, to + 1), H(across + 1, to + 1) }.Max();
            float lowLine = alongX ? Math.Max(H(from, across), H(from, across + 1)) : Math.Max(H(across, from), H(across + 1, from));
            float highLine = alongX ? Math.Max(H(to + 1, across), H(to + 1, across + 1)) : Math.Max(H(across, to + 1), H(across + 1, to + 1));
            if (Math.Abs(lowLine - highLine) > MaxBridgeDrop)
                return $"The two banks differ in height by {Math.Abs(lowLine - highLine):F0} (the limit is {MaxBridgeDrop:F0}). " +
                       "The deck is level with the higher bank, so the other end would be a tall ramp — pick banks of similar height.";
            deck += RoadTiles.SpaceAboveTerrain;
            for (int k = from + 1; k <= to; k++)
                for (int j = 0; j <= 1; j++)
                    if ((alongX ? H(k, across + j) : H(across + j, k)) > deck)
                        return "The ground rises above the bridge deck along the way — bridges are level, so pick a lower crossing.";

            var bridge = Bridges.Build(alongX, from, to, across, deck);
            _bridges.Add(bridge);
            if (alongX) ClearTrees(from, across, to, across); else ClearTrees(across, from, across, to);
            _bridgesChanged = true;
            foreach (var end in new[] { a, b })
            {
                if (!IsRoad(end.x, end.y)) Draw(end.x, end.y);    // picks up the bridge bit itself
                else
                {
                    var tile = _roads[end];
                    int bit = 0;
                    foreach (var (_, dx, dy) in BridgesEndingAt(end.x, end.y).Where(e => e.bridge == bridge))
                        bit |= RoadTiles.BitToward(dx, dy);
                    RoadTiles.Retype(tile, RoadTiles.MaskOf(tile) | bit);
                }
            }
            Dirty = true;
            return null;
        }

        /// <summary>
        /// Removes trees whose footprint overlaps squares x0..x1 by y0..y1.
        /// Maxis roads and bridges never have trees on them; the game doesn't
        /// clear them itself. Decorations are left alone -- players place those.
        /// </summary>
        private void ClearTrees(int x0, int y0, int x1, int y1)
        {
            float ax = x0 * 10, ay = y0 * 10, bx = (x1 + 1) * 10, by = (y1 + 1) * 10;
            int before = _trees.Count;
            _trees.RemoveAll(t =>
            {
                float tx0 = BitConverter.ToSingle(t, 13), ty0 = BitConverter.ToSingle(t, 17);
                float tx1 = BitConverter.ToSingle(t, 21), ty1 = BitConverter.ToSingle(t, 25);
                return tx0 < bx && tx1 > ax && ty0 < by && ty1 > ay;
            });
            if (_trees.Count != before) _treesChanged = true;
        }

        public void RemoveBridge(Bridge bridge)
        {
            var ends = BridgesEndingAt(bridge.EndRoads().low.x, bridge.EndRoads().low.y)
                .Concat(BridgesEndingAt(bridge.EndRoads().high.x, bridge.EndRoads().high.y))
                .Where(e => e.bridge == bridge).ToList();
            var (low, high) = bridge.EndRoads();
            _bridges.Remove(bridge);
            _bridgesChanged = true;
            foreach (var (sq, e) in new[] { low, high }.Zip(ends))
                if (_roads.TryGetValue(sq, out var tile))
                    RoadTiles.Retype(tile, RoadTiles.MaskOf(tile) & ~RoadTiles.BitToward(e.dx, e.dy));
            Dirty = true;
        }

        // ---- saving --------------------------------------------------------

        /// <summary>True if the game appears to be running; saving then could be overwritten by it.</summary>
        public static bool GameIsRunning() =>
            Process.GetProcesses().Any(p =>
            {
                try { return p.ProcessName.Contains("Sims 2", StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            });

        public bool ChangedOnDisk() => File.GetLastWriteTimeUtc(Path) != _loadedWriteTime;

        /// <summary>
        /// Writes the roads back, after copying the package to .bkp (the same
        /// backup HoodReplacer makes). Returns the backup path.
        /// </summary>
        public string Save()
        {
            var isolated = Isolated();
            if (isolated.Count > 0)
                throw new InvalidOperationException(
                    $"{isolated.Count} road square(s) connect to nothing, e.g. at {isolated[0].x},{isolated[0].y}. " +
                    "Extend them to another square or erase them first.");

            // Original order first (the game's), then squares added in this session.
            var order = _order.Where(_roads.ContainsKey).Distinct().ToList();
            var bytes = new List<byte>(order.Count * RoadTiles.Size);
            foreach (var sq in order) bytes.AddRange(_roads[sq]);

            var pkg = SimPe.Packages.File.LoadFromFile(Path);
            var nhtr = new R_NHTR(pkg, pkg.FindFile(NHTR, 0, 0xFFFFFFFF, 0));
            nhtr.Roads = bytes.ToArray();
            nhtr.Bridges = _bridgesChanged ? _bridges.SelectMany(b => b.Pieces).SelectMany(p => p).ToArray()
                                           : _originalBridgeBytes;
            nhtr.Trees = _treesChanged ? _trees.SelectMany(t => t).ToArray() : _originalTreeBytes;
            nhtr.Rewrite();

            string backup = System.IO.Path.ChangeExtension(Path, ".bkp");
            File.Copy(Path, backup, true);
            var ms = pkg.Build();
            pkg.Close();
            File.WriteAllBytes(Path, ms.ToArray());

            _loadedWriteTime = File.GetLastWriteTimeUtc(Path);
            _originalBridgeBytes = nhtr.Bridges;
            _bridgesChanged = false;
            _originalTreeBytes = nhtr.Trees;
            _treesChanged = false;
            _undo.Clear();
            Dirty = false;
            return backup;
        }
    }
}
