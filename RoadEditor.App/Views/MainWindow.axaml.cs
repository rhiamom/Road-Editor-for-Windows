/**************************************************************************
 *   Road Editor for Windows                                                  *
 *   © 2026 GramzeSweatshop (rhiamom@mac.com). Written with Claude        *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Main window: neighborhood list, tools, map, and the save checks      *
 *   (game closed, file not changed underneath us, no loose squares).     *
 *************************************************************************/

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HoodReplace;
using LotExpander;
using RoadEditor.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RoadEditor.App.Views;

public partial class MainWindow : Window
{
    private readonly MapView _map;
    private RoadDocument? _doc;
    private HoodEntry? _current;
    private bool _switching;
    private bool _closeConfirmed;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _map = this.Get<MapView>("Map");

        Opened += async (_, _) => await EnsureInstallPickedAsync();
        this.Get<ListBox>("Hoods").SelectionChanged += HoodSelected;
        this.Get<ToggleButton>("DrawTool").Click += (_, _) => SetTool(Tool.Draw);
        this.Get<ToggleButton>("EraseTool").Click += (_, _) => SetTool(Tool.Erase);
        this.Get<ToggleButton>("BridgeTool").Click += (_, _) => SetTool(Tool.Bridge);
        this.Get<Button>("UndoButton").Click += (_, _) => { if (_doc?.Undo() == true) { _map.Rebuild(); Refresh(); } };
        this.Get<Button>("ZoomIn").Click += (_, _) => _map.Scale += 1;
        this.Get<Button>("ZoomOut").Click += (_, _) => _map.Scale -= 1;
        this.Get<Button>("SaveButton").Click += SaveClick;
        this.Get<Button>("InstallButton").Click += async (_, _) => await SwitchInstallAsync();

        _map.Edited += Refresh;
        _map.Refused += why => Say(why);
        _map.BridgeHint += Say;
        _map.HoverChanged += ShowHover;
        Closing += OnClosing;
    }

    private void Say(string message) => this.Get<TextBlock>("Status").Text = message;

    private void SetTool(Tool tool)
    {
        _map.Tool = tool;
        this.Get<ToggleButton>("DrawTool").IsChecked = tool == Tool.Draw;
        this.Get<ToggleButton>("EraseTool").IsChecked = tool == Tool.Erase;
        this.Get<ToggleButton>("BridgeTool").IsChecked = tool == Tool.Bridge;
        if (tool == Tool.Bridge) Say("Bridge: drag from a road on one side straight across to a road on the other.");
    }

    private void Refresh()
    {
        bool dirty = _doc?.Dirty == true;
        this.Get<Button>("SaveButton").IsEnabled = dirty;
        this.Get<Button>("UndoButton").IsEnabled = _doc?.CanUndo == true;
        Title = _current == null ? "Road Editor for Windows" : $"Road Editor for Windows — {_current.Name}{(dirty ? " •" : "")}";
        if (_doc != null && dirty)
            Say($"{_doc.RoadCount} road squares, {_doc.BridgeCount} bridge(s)" +
                (_doc.TreesRemoved > 0 ? $", {_doc.TreesRemoved} tree(s) cleared from under them" : "") + " — not saved yet.");
    }

    private void ShowHover((int x, int y)? sq)
    {
        var text = this.Get<TextBlock>("Hover");
        if (_doc == null || sq == null) { text.Text = ""; return; }
        var (x, y) = sq.Value;
        string what = _doc.GroundAt(x, y) switch
        {
            Ground.Lot or Ground.LotFront => $"lot \"{_doc.LotAt(x, y)!.Name}\"",
            Ground.Steep => "too steep",
            Ground.Water => "water",
            Ground.Edge => "map edge",
            Ground.Bridge => "bridge",
            _ => "open ground",
        };
        if (_doc.IsRoad(x, y)) what = "road · " + what;
        if (_doc.BridgeAt(x, y) is { } br) what = $"bridge, deck {br.Deck:F1}";
        text.Text = $"{x}, {y} · height {_doc.Terrain[y, x]:F1} · rise {_doc.Rise(x, y):F1} · {what}";
    }

    // ---- installs ------------------------------------------------------

    /// <summary>
    /// On launch: if there's a single install (or a persisted choice), load
    /// its neighborhoods immediately. If multiple installs are present and
    /// no choice is saved, prompt. If none, show empty state.
    /// </summary>
    private async System.Threading.Tasks.Task EnsureInstallPickedAsync()
    {
        // Install… stays enabled even with zero or one install present: the
        // picker's Browse… button lets the player add a custom location, so
        // "no installs" and "one install" are both valid starting points
        // from which to add another.
        this.Get<Button>("InstallButton").IsEnabled = true;

        var installs = SimsPaths.AllInstalls();
        if (installs.Count == 0)
        {
            Say("No Sims 2 install found. Click \"Install…\" to add one from a custom location.");
            return;
        }

        if (SimsPaths.NeighborhoodsFolder == null)
        {
            // Multiple installs, no saved pick — ask.
            var chosen = await Dialogs.PickInstall(this, installs, null);
            if (chosen == null)
            {
                Say("No install chosen. Click \"Install…\" to pick one.");
                return;
            }
            SimsPaths.Select(chosen);
        }

        LoadNeighborhoods();
    }

    private async System.Threading.Tasks.Task SwitchInstallAsync()
    {
        if (_doc?.Dirty == true)
        {
            bool ok = await Dialogs.Confirm(this, "Discard unsaved changes?",
                "The current neighborhood has unsaved road changes. Switching installs will lose them.");
            if (!ok) return;
        }

        // Pass the current list even when it's empty — the picker's Browse…
        // button is the entire point of opening it in that case.
        var installs = SimsPaths.AllInstalls();

        // Figure out which install the current folder belongs to, so the picker
        // starts highlighted on it.
        string? currentFolder = SimsPaths.UserFolder;
        SimsInstall? currentInstall = null;
        foreach (var i in installs)
            if (string.Equals(i.UserFolder, currentFolder, StringComparison.OrdinalIgnoreCase))
                { currentInstall = i; break; }

        var chosen = await Dialogs.PickInstall(this, installs, currentInstall);
        if (chosen == null || chosen.UserFolder == currentFolder) return;

        SimsPaths.Select(chosen);
        _doc = null;
        _current = null;
        _map.Document = null;
        LoadNeighborhoods();
        Refresh();
    }

    // ---- neighborhoods -------------------------------------------------

    private void LoadNeighborhoods()
    {
        string? folder = SimsPaths.NeighborhoodsFolder;
        if (folder == null) { Say("No Sims 2 neighborhoods folder found."); return; }
        try
        {
            var hoods = HoodList.Build(folder, false);
            this.Get<ListBox>("Hoods").ItemsSource = hoods;
            Say("Pick a neighborhood.");

            // "--open N006" selects a neighborhood at launch (handy for testing).
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--open");
            if (i >= 0 && i + 1 < args.Length)
                this.Get<ListBox>("Hoods").SelectedItem =
                    hoods.FirstOrDefault(h => h.IsMain && string.Equals(h.FolderCode, args[i + 1], StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Say("Windows blocked access to the neighborhoods folder.");
            _ = Dialogs.Message(this, "Can't read your neighborhoods",
                "Windows refused access to the Sims 2 neighborhoods folder:\n\n" + folder +
                "\n\nCheck the folder's permissions, or if OneDrive manages your Documents folder, " +
                "mark EA Games as \"Always keep on this device.\" Controlled Folder Access " +
                "(Windows Security → Virus & threat protection) can also block it — add Road " +
                "Editor to the allowed apps list if that's enabled." +
                "\n\n(" + ex.Message + ")");
        }
    }

    private async void HoodSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_switching) return;
        var list = this.Get<ListBox>("Hoods");
        if (list.SelectedItem is not HoodEntry hood || hood == _current) return;

        if (_doc?.Dirty == true &&
            !await Dialogs.Confirm(this, "Unsaved roads", $"Your changes to {_current!.Name} aren't saved. Discard them?"))
        {
            _switching = true;
            list.SelectedItem = _current;
            _switching = false;
            return;
        }

        try
        {
            _doc = new RoadDocument(hood.PackagePath);
            _current = hood;
            _map.Document = _doc;
            FitZoom();
            Say($"{hood.Name}: {_doc.RoadCount} road squares, {_doc.Lots.Count} lots." +
                (_doc.BridgeCount > 0 ? $" {_doc.BridgeCount} bridge(s)." : ""));
        }
        catch (Exception ex)
        {
            _doc = null;
            _current = null;
            _map.Document = null;
            Say("Couldn't open that neighborhood.");
            await Dialogs.Message(this, "Can't open neighborhood", $"{hood.Name}\n\n{ex.Message}");
        }
        Refresh();
    }

    private void FitZoom()
    {
        if (_doc == null) return;
        var viewer = this.Get<ScrollViewer>("Viewer");
        double w = viewer.Bounds.Width - 4, h = viewer.Bounds.Height - 4;
        if (w <= 0 || h <= 0) { _map.Scale = 5; return; }
        _map.Scale = (int)Math.Max(2, Math.Floor(Math.Min(w / _map.Columns, h / _map.Rows)));
    }

    // ---- saving --------------------------------------------------------

    private async System.Threading.Tasks.Task<bool> Save()
    {
        if (_doc == null || !_doc.Dirty) return true;

        if (RoadDocument.GameIsRunning())
        {
            await Dialogs.Message(this, "Close the game first",
                "The Sims 2 is running. Quit the game before saving — otherwise it can overwrite these roads when it saves the neighborhood.");
            return false;
        }
        if (_doc.ChangedOnDisk() &&
            !await Dialogs.Confirm(this, "Neighborhood changed",
                $"{_current!.Name} was changed on disk after you opened it (probably by the game). Saving writes your roads over that version's road list. Continue?"))
            return false;

        var loose = _doc.Isolated();
        if (loose.Count > 0)
        {
            await Dialogs.Message(this, "Loose road squares",
                $"{loose.Count} road square(s), shown in orange, connect to nothing — the game has no piece for a lone square. " +
                $"Extend each one to another road square, or erase it. The first is at {loose[0].x}, {loose[0].y}.");
            return false;
        }

        try
        {
            string backup = _doc.Save();
            Refresh();
            Say($"Saved {_current!.Name} — the previous version is in {Path.GetFileName(backup)}.");
            return true;
        }
        catch (Exception ex)
        {
            await Dialogs.Message(this, "Save failed", ex.Message);
            return false;
        }
    }

    private async void SaveClick(object? sender, RoutedEventArgs e) => await Save();

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || _doc?.Dirty != true) return;
        e.Cancel = true;
        bool discard = await Dialogs.Confirm(this, "Unsaved roads",
            $"Your changes to {_current!.Name} aren't saved. Quit without saving them?");
        if (!discard) return;
        _closeConfirmed = true;
        Close();
    }
}
