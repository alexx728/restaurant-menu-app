using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Restaurant.Desktop.Views;
public sealed class DialogService(Window owner)
{
    public Task<bool> ConfirmAsync(string text)
    {
        var window = new Window
        {
            Title = "Підтвердження",
            Width = 440,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var accept = new Button
        {
            Content = "Продовжити"
        };
        var cancel = new Button
        {
            Content = "Скасувати"
        };
        accept.Click += (_, _) => window.Close(true);
        cancel.Click += (_, _) => window.Close(false);
        window.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = text,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        accept,
                        cancel
                    }
                }
            }
        };
        return window.ShowDialog<bool>(owner);
    }
}
