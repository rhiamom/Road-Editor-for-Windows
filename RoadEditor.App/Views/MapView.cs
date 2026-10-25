/**************************************************************************
 *   Road Editor for Windows                                                  *
 *   © 2026 GramzeSweatshop (rhiamom@mac.com). Written with Claude        *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Top-down map of one neighborhood, one block per road square, and the *
 *   painting surface: press and drag to draw or erase. A drag that skips *
 *   squares is filled in with a 4-connected line, so a fast stroke still *
 *   makes a road with no gaps. Screen columns are the Y axis and rows    *
 *   the X axis, the same way round as the game's lot records.            *
 *************************************************************************/

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RoadEditor.Engine;
using System;
using System.Runtime.InteropServices;

namespace RoadEditor.App.Views;

public enum Tool { Draw, Erase, Bridge }

public sealed class MapView : Control
{
    private RoadDocument? _doc;
    private WriteableBitmap? _bitmap;
    private int _scale = 5;
    private (int x, int y)? _hover;
    private (int x, int y)? _last;
    private bool _stroking;
    private (int x, int y)? _bridgeStart, _bridgeEnd;

    public Tool Tool { get; set; } = Tool.Draw;

    /// <summary>Hovered square changed (null when the pointer leaves the map).</summary>
    public event Action<(int x, int y)?>? HoverChanged;
    /// <summary>A square was refused; the text says why.</summary>
    public event Action<string>? Refused;
    /// <summary>Progress text while dragging out a bridge.</summary>
    public event Action<string>? BridgeHint;
    /// <summary>Roads changed.</summary>
    public event Action? Edited;

    public RoadDocument? Document
    {
        get => _doc;
        set { _doc = value; _hover = null; Rebuild(); }
    }

    public int Scale
    {
        get => _scale;
        set { _scale = Math.Clamp(value, 2, 16); InvalidateMeasure(); InvalidateVisual(); }
    }

    public int Columns => _doc == null ? 0 : _doc.Height - 1;   // y
    public int Rows => _doc == null ? 0 : _doc.Width - 1;       // x

    protected override Size MeasureOverride(Size availableSize) => new(Columns * _scale, Rows * _scale);

    // ---- colours -------------------------------------------------------

    private static uint Bgra(byte r, byte g, byte b) => 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;

    private uint ColourAt(int x, int y, float lo, float hi)
    {
        var d = _doc!;
        if (d.BridgeAt(x, y) != null) return Bgra(150, 128, 100);
        if (d.IsRoad(x, y))
        {
            if (d.MaskAt(x, y) == 0) return Bgra(230, 90, 30);                   // connects to nothing
            return d.GroundAt(x, y) == Ground.LotFront ? Bgra(90, 72, 40) : Bgra(58, 58, 62);
        }
        switch (d.GroundAt(x, y))
        {
            case Ground.Lot: return Bgra(196, 64, 58);
            case Ground.LotFront: return d.GroundRefuses(x, y) ? Bgra(150, 120, 30)  // missing road, can't have one
                                                               : Bgra(240, 200, 60); // missing road
            case Ground.Water: return Bgra(52, 104, 186);
            case Ground.Edge: return Bgra(150, 150, 150);
        }
        float f = hi > lo ? (d.Terrain[y, x] - lo) / (hi - lo) : 0.5f;
        byte r = (byte)(96 + 140 * f), g = (byte)(152 + 88 * f), b = (byte)(72 + 160 * f);
        if (d.GroundAt(x, y) == Ground.Steep) { r = (byte)(r * 0.55); g = (byte)(g * 0.55); b = (byte)(b * 0.55); }
        return Bgra(r, g, b);
    }

    /// <summary>Redraws the whole map bitmap. Cheap: 127×127 pixels.</summary>
    public void Rebuild()
    {
        InvalidateMeasure();
        if (_doc == null) { _bitmap = null; InvalidateVisual(); return; }
        int w = Columns, h = Rows;
        float lo = RoadDocument.WaterLevel, hi = lo + 1;
        foreach (float v in _doc.Terrain) hi = Math.Max(hi, v);

        var px = new int[w * h];
        for (int x = 0; x < h; x++)
            for (int y = 0; y < w; y++)
                px[x * w + y] = unchecked((int)ColourAt(x, y, lo, hi));

        _bitmap ??= null;
        if (_bitmap == null || _bitmap.PixelSize.Width != w || _bitmap.PixelSize.Height != h)
            _bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var fb = _bitmap.Lock())
            for (int row = 0; row < h; row++)
                Marshal.Copy(px, row * w, fb.Address + row * fb.RowBytes, w);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_bitmap == null) return;
        var dest = new Rect(0, 0, Columns * _scale, Rows * _scale);
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            context.DrawImage(_bitmap, new Rect(0, 0, Columns, Rows), dest);

        // A faint line every 10 squares, to help find your place.
        var grid = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 1);
        for (int k = 10; k < Columns; k += 10) context.DrawLine(grid, new Point(k * _scale, 0), new Point(k * _scale, dest.Height));
        for (int k = 10; k < Rows; k += 10) context.DrawLine(grid, new Point(0, k * _scale), new Point(dest.Width, k * _scale));

        if (_bridgeStart is { } bs && _bridgeEnd is { } be)
        {
            var pen = new Pen(Brushes.White, 2, new DashStyle(new double[] { 2, 2 }, 0));
            int x0 = Math.Min(bs.x, be.x), x1 = Math.Max(bs.x, be.x), y0 = Math.Min(bs.y, be.y), y1 = Math.Max(bs.y, be.y);
            context.DrawRectangle(null, pen, new Rect(y0 * _scale, x0 * _scale, (y1 - y0 + 1) * _scale, (x1 - x0 + 1) * _scale).Inflate(1));
        }

        if (_hover is { } hv)
        {
            var colour = Tool == Tool.Erase ? Color.FromRgb(255, 90, 90) : Colors.White;
            context.DrawRectangle(null, new Pen(new SolidColorBrush(colour), 2),
                new Rect(hv.y * _scale, hv.x * _scale, _scale, _scale).Inflate(1));
        }
    }

    // ---- painting ------------------------------------------------------

    private (int x, int y)? SquareAt(Point p)
    {
        if (_doc == null) return null;
        int y = (int)Math.Floor(p.X / _scale), x = (int)Math.Floor(p.Y / _scale);
        return x >= 0 && y >= 0 && x < Rows && y < Columns ? (x, y) : null;
    }

    private void Apply((int x, int y) sq)
    {
        string? why = Tool == Tool.Draw ? _doc!.Draw(sq.x, sq.y) : _doc!.Erase(sq.x, sq.y);
        if (why != null) Refused?.Invoke(why);
    }

    // Walk from a to b one square at a time, never diagonally, so the road stays connected.
    private void Line((int x, int y) a, (int x, int y) b)
    {
        int x = a.x, y = a.y;
        while ((x, y) != b)
        {
            int dx = Math.Sign(b.x - x), dy = Math.Sign(b.y - y);
            if (dx != 0 && (dy == 0 || Math.Abs(b.x - x) >= Math.Abs(b.y - y))) x += dx; else y += dy;
            Apply((x, y));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_doc == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var sq = SquareAt(e.GetPosition(this));
        if (sq == null) return;
        if (Tool == Tool.Bridge)
        {
            _bridgeStart = _bridgeEnd = sq;
            e.Pointer.Capture(this);
            BridgeHint?.Invoke("Drag across to the road on the other side, then let go.");
            InvalidateVisual();
            return;
        }
        _doc.BeginStroke();
        _stroking = true;
        _last = sq;
        e.Pointer.Capture(this);
        Apply(sq.Value);
        Rebuild();
        Edited?.Invoke();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var sq = SquareAt(e.GetPosition(this));
        if (sq != _hover) { _hover = sq; HoverChanged?.Invoke(sq); InvalidateVisual(); }
        if (_bridgeStart is { } start && sq is { } now)
        {
            // Snap to the row or column, whichever the pointer is further along.
            _bridgeEnd = Math.Abs(now.x - start.x) >= Math.Abs(now.y - start.y) ? (now.x, start.y) : (start.x, now.y);
            int span = Math.Abs(_bridgeEnd.Value.x - start.x) + Math.Abs(_bridgeEnd.Value.y - start.y) - 1;
            BridgeHint?.Invoke(span < 1 ? "Drag across to the road on the other side, then let go."
                : $"Bridge of {span} squares" + (Bridges.WhyNotLength(span) is { } w ? " — " + w : " — let go to build it."));
            InvalidateVisual();
            return;
        }
        if (!_stroking || sq == null || sq == _last) return;
        Line(_last!.Value, sq.Value);
        _last = sq;
        Rebuild();
        Edited?.Invoke();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_bridgeStart is { } a && _bridgeEnd is { } b && _doc != null)
        {
            _bridgeStart = _bridgeEnd = null;
            e.Pointer.Capture(null);
            if (a != b)
            {
                _doc.BeginStroke();
                string? why = _doc.AddBridge(a, b);
                if (why != null) { _doc.DiscardStroke(); Refused?.Invoke(why); }
                else { Rebuild(); Edited?.Invoke(); }
            }
            InvalidateVisual();
            return;
        }
        _stroking = false;
        _last = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_stroking) return;
        _hover = null;
        HoverChanged?.Invoke(null);
        InvalidateVisual();
    }
}
