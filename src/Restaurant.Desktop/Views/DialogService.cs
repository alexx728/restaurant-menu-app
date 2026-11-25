using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Restaurant.Desktop.Views;

public sealed class DialogService
{
    private readonly Window _owner;

    public DialogService(Window owner) => _owner = owner;

    public Task<bool> ConfirmAsync(string text)
    {
        var dialog = new Window
        {
            Title = "Підтвердження дії",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var cancel = new Button { Content = "Скасувати", IsCancel = true };
        var accept = new Button { Content = "Продовжити" };
        accept.Classes.Add("primary");
        cancel.Click += (_, _) => dialog.Close(false);
        accept.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 15 },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancel, accept }
                }
            }
        };
        dialog.Opened += (_, _) => cancel.Focus();
        return dialog.ShowDialog<bool>(_owner);
    }
}
