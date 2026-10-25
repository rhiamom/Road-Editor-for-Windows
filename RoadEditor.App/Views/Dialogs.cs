/**************************************************************************
 *   Road Editor for Windows                                                  *
 *   © 2026 GramzeSweatshop (rhiamom@mac.com). Written with Claude        *
 *   GPL v2 or later. See Licences/GPL-LICENSE.txt                        *
 *                                                                        *
 *   Small modal helpers (copied from HoodReplacer for Mac).              *
 *************************************************************************/

using Avalonia.Controls;
using Avalonia.Layout;
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
}
