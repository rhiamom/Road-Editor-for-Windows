/**************************************************************************
 *   Road Editor for Windows                                                  *
 *   © 2026 GramzeSweatshop (rhiamom@mac.com). Written with Claude        *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Small modal helpers (copied from HoodReplacer for Mac).              *
 *************************************************************************/

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using LotExpander;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RoadEditor.App.Views;

internal static class Dialogs
{
    private static Window Build(string title, string message, bool withCancel, TaskCompletionSource<bool> tcs)
    {
        var text = new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 420 };
        var ok = new Button { Content = withCancel ? "Continue" : "OK", IsDefault = true, MinWidth = 90 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        var win = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };
        if (withCancel)
        {
            var cancel = new Button { Content = "Cancel", MinWidth = 90 };
            cancel.Click += (_, _) => { tcs.TrySetResult(false); win.Close(); };
            buttons.Children.Add(cancel);
        }
        ok.Click += (_, _) => { tcs.TrySetResult(true); win.Close(); };
        buttons.Children.Add(ok);
        win.Content = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 16, Children = { text, buttons } };
        win.Closed += (_, _) => tcs.TrySetResult(false);
        return win;
    }

    public static async Task Message(Window owner, string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();
        await Build(title, message, false, tcs).ShowDialog(owner);
    }

    public static async Task<bool> Confirm(Window owner, string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();
        await Build(title, message, true, tcs).ShowDialog(owner);
        return await tcs.Task;
    }

    /// <summary>
    /// The engine calls back from a worker thread, so this hops to the UI
    /// thread and waits. Used only for the terrain-size confirmation.
    /// </summary>
    public static bool ConfirmBlocking(Window owner, string title, string message) =>
        Avalonia.Threading.Dispatcher.UIThread
            .InvokeAsync(() => Confirm(owner, title, message))
            .GetAwaiter().GetResult();

    /// <summary>
    /// Shows a modal that lets the user pick which Sims 2 install Road Editor
    /// should work against. Returns the chosen install, or null if the user
    /// cancelled. Cancel is treated as "don't change anything" by the caller.
    /// </summary>
    public static async Task<SimsInstall?> PickInstall(Window owner, IReadOnlyList<SimsInstall> installs, SimsInstall? current)
    {
        var tcs = new TaskCompletionSource<SimsInstall?>();

        var list = new ListBox
        {
            ItemsSource = installs,
            SelectedItem = current ?? (installs.Count > 0 ? installs[0] : null),
            MinWidth = 420,
            MaxHeight = 220,
        };
        list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SimsInstall>((inst, _) =>
            new StackPanel
            {
                Margin = new Avalonia.Thickness(4),
                Children =
                {
                    new TextBlock { Text = inst!.DisplayName, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                    new TextBlock { Text = inst.UserFolder, Opacity = 0.65, FontSize = 11 },
                },
            });

        var ok = new Button { Content = "Use this install", IsDefault = true, MinWidth = 140 };
        var browse = new Button { Content = "Browse…", MinWidth = 90 };
        var cancel = new Button { Content = "Cancel", MinWidth = 90 };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { browse, cancel, ok },
        };

        var win = new Window
        {
            Title = "Which Sims 2 install?",
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Pick which Sims 2 user folder Road Editor should work against. Click Browse… to add an install from a non-standard location (the user folder, the one that contains Neighborhoods).",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        MaxWidth = 420,
                    },
                    list,
                    buttons,
                },
            },
        };

        ok.Click += (_, _) => { tcs.TrySetResult(list.SelectedItem as SimsInstall); win.Close(); };
        cancel.Click += (_, _) => { tcs.TrySetResult(null); win.Close(); };
        browse.Click += async (_, _) =>
        {
            var folders = await win.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Pick a Sims 2 user folder (contains Neighborhoods)",
                AllowMultiple = false,
            });
            if (folders == null || folders.Count == 0) return;
            string? path = folders[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            SimsInstall added;
            try { added = SimsPaths.AddCustomInstall(path); }
            catch (System.IO.DirectoryNotFoundException ex)
            {
                await Message(win, "Not a Sims 2 user folder", ex.Message);
                return;
            }

            // Rebuild the list including the new one, and select it.
            var updated = new List<SimsInstall>(SimsPaths.AllInstalls());
            list.ItemsSource = updated;
            foreach (var i in updated)
                if (string.Equals(i.UserFolder, added.UserFolder, System.StringComparison.OrdinalIgnoreCase))
                    { list.SelectedItem = i; break; }
        };
        win.Closed += (_, _) => tcs.TrySetResult(null);

        await win.ShowDialog(owner);
        return await tcs.Task;
    }
}
