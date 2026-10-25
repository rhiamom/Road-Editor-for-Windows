/**************************************************************************
 *   Road Editor for Windows                                                  *
 *   © 2026 GramzeSweatshop (rhiamom@mac.com). Written with Claude        *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Headless checks against real neighborhoods. Every write goes to a    *
 *   scratch copy in the temp folder; the game's files are only read.     *
 *     RoadEditor.SmokeTest [N001 N002 ...]   (default N001 N002 N003)    *
 *************************************************************************/

using LotExpander;
using RoadEditor.Engine;
using System;
using System.IO;
using System.Linq;

int failures = 0;
void Check(bool ok, string what)
{
    Console.WriteLine($"   {(ok ? "ok  " : "FAIL")} {what}");
    if (!ok) failures++;
}

string? nb = SimsPaths.NeighborhoodsFolder;
if (nb == null) { Console.WriteLine("No Neighborhoods folder found."); return 1; }
string scratch = Path.Combine(Path.GetTempPath(), "roadeditor-smoke");
Directory.CreateDirectory(scratch);

var codes = args.Length > 0 ? args : new[] { "N001", "N002", "N003" };
foreach (string code in codes)
{
    string real = Path.Combine(nb, code, code + "_Neighborhood.package");
    string work = Path.Combine(scratch, code + "_Neighborhood.package");
    File.Copy(real, work, true);
    Console.WriteLine($"\n{code}: working on {work}");

    var doc = new RoadDocument(work);
    Console.WriteLine($"   {doc.Width}x{doc.Height} terrain, {doc.RoadCount} road squares, {doc.Lots.Count} lots");
    byte[] before = RoadBytes(work);

    // 1. Open + save with no edits must not change a single road byte.
    doc.Save();
    Check(RoadBytes(work).SequenceEqual(before), "save without edits leaves the road list byte-identical");

    // 2. Centre height is the mean of the four corners (what Build writes for new squares).
    var byteTiles = Split(before);
    int meanOk = byteTiles.Count(t =>
    {
        float c = BitConverter.ToSingle(t, 9);
        float m = Enumerable.Range(0, 4).Select(k => BitConverter.ToSingle(t, 30 + k * 20 + 8)).Average();
        return Math.Abs(c - m) < 0.01f;
    });
    Console.WriteLine($"   info: centre height = corner mean on {meanOk}/{byteTiles.Count} Maxis squares");

    // 3. Erase a straight square that isn't a lot front, then draw it back:
    //    the neighbours must come back byte-identical, and the square itself
    //    identical apart from heights we recompute from the terrain.
    doc = new RoadDocument(work);
    var victim = doc.Roads.FirstOrDefault(r => (doc.MaskAt(r.x, r.y) == 0x5 || doc.MaskAt(r.x, r.y) == 0xA)
                                               && doc.WhyNotErase(r.x, r.y) == null && doc.WhyNotDraw(r.x, r.y) == null);
    if (victim != default)
    {
        var tiles = Split(before).ToDictionary(RoadTiles.SquareOf);
        doc.BeginStroke();
        Check(doc.Erase(victim.x, victim.y) == null, $"erase straight square at {victim.x},{victim.y}");
        var nbrs = RoadTiles.Neighbours.Select(n => (victim.x + n.dx, victim.y + n.dy)).Where(n => doc.IsRoad(n.Item1, n.Item2)).ToList();
        Check(nbrs.All(n => doc.MaskAt(n.Item1, n.Item2) != tiles[n][RoadTiles.MaskOffset]), "its neighbours lose the connection");
        Check(doc.Draw(victim.x, victim.y) == null, "draw it back");
        doc.Save();
        var after = Split(RoadBytes(work)).ToDictionary(RoadTiles.SquareOf);
        Check(nbrs.All(n => after[n].SequenceEqual(tiles[n])), "neighbours are byte-identical to the original");
        var diff = Enumerable.Range(0, RoadTiles.Size).Where(i => after[victim][i] != tiles[victim][i]).ToList();
        bool onlyHeights = diff.All(i => (i >= 9 && i < 13) || Enumerable.Range(0, 4).Any(k => i >= 30 + k * 20 + 8 && i < 30 + k * 20 + 12));
        Check(onlyHeights, $"redrawn square matches except heights ({diff.Count} bytes differ)");
    }
    else Console.WriteLine("   (no erasable straight square found)");

    // 3b. Bridges: untouched save keeps them; deck formula; erase + rebuild gives Maxis's pieces back.
    doc = new RoadDocument(work);
    Check(BridgeBytes(work).SequenceEqual(BridgeBytes(real)), $"bridge list unchanged by saving ({doc.BridgeCount} bridge(s))");
    foreach (var br in doc.BridgeList.ToList())
    {
        var (lo, hi) = br.EndRoads();
        var maxis = br.Pieces.OrderBy(p => BitConverter.ToSingle(p, br.AlongX ? 13 : 17)).ToList();
        int loMask = doc.MaskAt(lo.x, lo.y), hiMask = doc.MaskAt(hi.x, hi.y);
        doc.BeginStroke();
        doc.Erase(br.AlongX ? br.From : br.Across, br.AlongX ? br.Across : br.From);
        Check(doc.BridgeAt(br.AlongX ? br.From : br.Across, br.AlongX ? br.Across : br.From) == null, $"erasing a bridge square removes the bridge {br.From}..{br.To}");
        Check(doc.MaskAt(lo.x, lo.y) != loMask && doc.MaskAt(hi.x, hi.y) != hiMask, "its end roads lose the bridge connection");
        string? why = doc.AddBridge(lo, hi);
        if (why != null && why.Contains("differ in height"))
        {
            // Built before the end-height limit existed; the rules now refuse it, which is the point.
            Console.WriteLine($"   info: this bridge breaks today's rules ({why[..why.IndexOf('(')].Trim()}); skipping rebuild checks");
            doc.Undo();
            continue;
        }
        Check(why == null, "rebuild it between the same two roads" + (why == null ? "" : ": " + why));
        var rebuilt = doc.BridgeList.FirstOrDefault(b => b.From == br.From && b.To == br.To && b.Across == br.Across);
        if (rebuilt != null)
        {
            Check(Math.Abs(rebuilt.Deck - br.Deck) < 0.01f, $"deck height from the terrain matches Maxis ({rebuilt.Deck:F2} vs {br.Deck:F2})");
            int same = maxis.Zip(rebuilt.Pieces).Count(z => Enumerable.Range(0, 145).All(k => k == 119 || z.First[k] == z.Second[k]));
            Check(same == maxis.Count, $"rebuilt pieces match Maxis ({same}/{maxis.Count}, before the model-rotation bytes)");
        }
        Check(doc.MaskAt(lo.x, lo.y) == loMask && doc.MaskAt(hi.x, hi.y) == hiMask, "end roads get their bridge connection back");
        Check(doc.Erase(lo.x, lo.y) != null, "refuses to erase a road a bridge ends on");
    }

    // 4. The rules.
    doc = new RoadDocument(work);
    var front = doc.Lots.Select(l => doc.Roads.FirstOrDefault(r => l.IsFront(r.x, r.y) && l.Contains(r.x, r.y))).FirstOrDefault(r => r != default);
    if (front != default) Check(doc.Erase(front.x, front.y) != null, $"refuses to erase a lot's front road at {front.x},{front.y}");
    var lot = doc.Lots.FirstOrDefault(l => l.Width > 1 && l.Height > 1);
    if (lot != null)
    {
        int ix = lot.Orientation == 0 ? lot.Top + 1 : lot.Orientation == 2 ? lot.Top : lot.Top;
        int iy = lot.Orientation == 3 ? lot.Left + 1 : lot.Orientation == 1 ? lot.Left : lot.Left;
        Check(doc.Draw(ix, iy) != null, $"refuses to draw inside a lot at {ix},{iy}");
    }
    // Lot fronts obey the ground limits; roads already on bad fronts may be erased.
    var squares = Enumerable.Range(1, doc.Width - 3).SelectMany(x => Enumerable.Range(1, doc.Height - 3).Select(y => (x, y))).ToList();
    var badFront = squares.FirstOrDefault(q => doc.GroundAt(q.x, q.y) == Ground.LotFront && doc.GroundRefuses(q.x, q.y) && !doc.IsRoad(q.x, q.y));
    if (badFront != default) Check(doc.Draw(badFront.x, badFront.y) != null, $"refuses to draw on a steep/wet lot front at {badFront.x},{badFront.y}");
    var badRoad = doc.Roads.FirstOrDefault(q => doc.GroundAt(q.x, q.y) == Ground.LotFront && doc.GroundRefuses(q.x, q.y));
    if (badRoad != default) Check(doc.Erase(badRoad.x, badRoad.y) == null, $"allows erasing a lot-front road on steep/wet ground at {badRoad.x},{badRoad.y}");
    var counts = Enumerable.Range(1, doc.Width - 3).SelectMany(x => Enumerable.Range(1, doc.Height - 3).Select(y => doc.GroundAt(x, y)))
                           .GroupBy(g => g).ToDictionary(g => g.Key, g => g.Count());
    Console.WriteLine("   ground: " + string.Join(", ", counts.Select(kv => $"{kv.Key} {kv.Value}")));
    var roadsOnBlocked = doc.Roads.Where(r => doc.GroundAt(r.x, r.y) is Ground.Water or Ground.Steep).ToList();
    Console.WriteLine($"   info: {roadsOnBlocked.Count} existing Maxis road squares sit on ground we'd refuse (bridge ends / water edge)");
}

Console.WriteLine(failures == 0 ? "\nALL CHECKS PASSED" : $"\n{failures} CHECK(S) FAILED");
return failures == 0 ? 0 : 1;

static byte[] RoadBytes(string path)
{
    var pkg = SimPe.Packages.File.LoadFromFile(path);
    var r = new HoodReplace.R_NHTR(pkg, pkg.FindFile(RoadDocument.NHTR, 0, 0xFFFFFFFF, 0)).Roads;
    pkg.ForgetUpdate(); pkg.Close();
    return r;
}

static byte[] BridgeBytes(string path)
{
    var pkg = SimPe.Packages.File.LoadFromFile(path);
    var r = new HoodReplace.R_NHTR(pkg, pkg.FindFile(RoadDocument.NHTR, 0, 0xFFFFFFFF, 0)).Bridges;
    pkg.ForgetUpdate(); pkg.Close();
    return r;
}

static System.Collections.Generic.List<byte[]> Split(byte[] raw) =>
    Enumerable.Range(0, raw.Length / RoadTiles.Size).Select(i => raw.Skip(i * RoadTiles.Size).Take(RoadTiles.Size).ToArray()).ToList();
